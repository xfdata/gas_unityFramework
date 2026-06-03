package com.videolayer;

import android.media.MediaCodec;
import android.media.MediaFormat;
import android.os.Build;
import android.os.Handler;
import android.os.HandlerThread;
import android.os.Looper;
import android.os.SystemClock;
import android.util.Log;
import java.nio.ByteBuffer;
import java.util.concurrent.atomic.AtomicInteger;


public class GPUDecoder {

    private static final int SUCCESS = 0;
    private static final int TRY_AGAIN_LATER = -1;
    private static final int ERROR = -2;
    private static final int END_OF_STREAM = -3;

    private VideoSurface videoSurface = null;
    private MediaCodec decoder;
    private MediaCodec.BufferInfo bufferInfo = new MediaCodec.BufferInfo();
    private static final int TIMEOUT_US = 2000;
    private int _inputBufferIndex = -1;

    // decoder.start();
    // decoder.flush();
    // HUAWEI Mate 40 Pro，在连续或者相近的时间执行上面代码会解码失败，
    // 报 `VIDEO-[pps_sps_check_tmp_id]:[5994]pps is null ppsid = 0 havn't decode`
    private boolean disableFlush = true;

    private static final int INIT_DECODER_TIMEOUT_MS = 2000;
    private static final int DECODER_THREAD_MAX_COUNT = 10;
    private static final AtomicInteger decoderThreadCount = new AtomicInteger();

    private static GPUDecoder Create(final VideoSurface surface, final MediaFormat mediaFormat) {
        if (decoderThreadCount.get() >= DECODER_THREAD_MAX_COUNT) {
            return null;
        }
        decoderThreadCount.getAndIncrement();
        HandlerThread initHandlerThread = new HandlerThread("GPUDecoder_init_decoder");
        initHandlerThread.start();
        SynchronizeHandler initHandler = new SynchronizeHandler(initHandlerThread.getLooper());
        final MediaCodec[] initDecoder = {null};

        boolean res = initHandler.runSync(new SynchronizeHandler.TimeoutRunnable() {
            private MediaCodec decoder;
            private long startTime;

            @Override
            public void run() {
                startTime = SystemClock.uptimeMillis();
                try {
                    decoder = MediaCodec.createDecoderByType(mediaFormat.getString(MediaFormat.KEY_MIME));
                    decoder.configure(mediaFormat, surface.getOutputSurface(), null, 0);                        
                    decoder.start();
                    //Log.d("GPUDecoder", "mediaFormat-->" + mediaFormat);
                    // if(Build.VERSION.SDK_INT >= 21) {
                    //     MediaCodecInfo codecInfo = decoder.getCodecInfo();
                    //     MediaCodecInfo.CodecCapabilities capabilities = codecInfo.getCapabilitiesForType("video/avc");
                    //     MediaCodecInfo.VideoCapabilities videoCapabilities = capabilities.getVideoCapabilities();
                    //     Range<Integer> heights = videoCapabilities.getSupportedHeights();
                    //     Range<Integer> widths = videoCapabilities.getSupportedWidths();
                    //     boolean isSupport = videoCapabilities.areSizeAndRateSupported(width,height,fps);
                    // }
                } catch (Exception e) {
                    e.printStackTrace();
                    if (decoder != null) {
                        decoder.release();
                        decoder = null;
                        surface.release();
                    }
                }
            }

            @Override
            public void afterRun(boolean isTimeout) {
                if (isTimeout && decoder != null) {
                    long costTime = SystemClock.uptimeMillis() - startTime;
                    try {
                        decoder.stop();
                    } catch (Exception ignored) {
                    }
                    try {
                        decoder.release();
                    } catch (Exception ignored) {
                    }
                    decoder = null;
                    surface.release();
                    String errorMessage = "init decoder timeout. cost: " + costTime + "ms";
                    new RuntimeException(errorMessage).printStackTrace();
                }
                if (!isTimeout) {
                    initDecoder[0] = decoder;
                }
                decoderThreadCount.getAndDecrement();
            }
        }, INIT_DECODER_TIMEOUT_MS);
        initHandlerThread.quitSafely();
        if (res && initDecoder[0] != null ) {
            GPUDecoder gpuDecoder = new GPUDecoder();
            gpuDecoder.videoSurface = surface;
            gpuDecoder.decoder = initDecoder[0];
            Log.d("GPUDecoder", "GPUDecoder Create true");
            return gpuDecoder;
        }
        Log.d("GPUDecoder", " GPUDecoder Create false");
        return null;
    }

    private ByteBuffer getInputBuffer(int inputBufferIndex) {
        try {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
                return decoder.getInputBuffer(inputBufferIndex);
            }
            return decoder.getInputBuffers()[inputBufferIndex];
        } catch (Exception | Error e) {
            e.printStackTrace();
            return null;
        }
    }

    private int queueInputBuffer(int size, long presentationTimeUs, int flag) {
        try {
            if(_inputBufferIndex >= 0){
                decoder.queueInputBuffer(_inputBufferIndex, 0, size, presentationTimeUs, flag);
                _inputBufferIndex = -1;
                disableFlush = false;
                return SUCCESS;
            }
        } catch (Exception | Error e) {
            e.printStackTrace();
            return ERROR;
        }
        return TRY_AGAIN_LATER;
    }

    private int onEndOfStream() {
        int ret = queueInputBuffer(0, 0,MediaCodec.BUFFER_FLAG_END_OF_STREAM);
        return ret;
    }

    private int canInputBuffer()
    {
        try {
            if(_inputBufferIndex == -1){
                _inputBufferIndex = decoder.dequeueInputBuffer(TIMEOUT_US);
            }
        } catch (Exception e) {
            e.printStackTrace();
            return ERROR;
        }
        return _inputBufferIndex != -1 ? SUCCESS : TRY_AGAIN_LATER;
    }

    private int onDecodeFrame(ByteBuffer bytes, long pts) {
        if(_inputBufferIndex >= 0)
        {
            ByteBuffer inputBuffer = getInputBuffer(_inputBufferIndex);
            if (inputBuffer == null) {
                Log.d("GPUDecoder", "onDecodeFrame getInputBuffer error "+ pts + " _inputBufferIndex " + _inputBufferIndex);
                //try again
                _inputBufferIndex = decoder.dequeueInputBuffer(TIMEOUT_US); 
                if(_inputBufferIndex >= 0) inputBuffer = getInputBuffer(_inputBufferIndex); 
                if(inputBuffer == null) return ERROR;
            }
            // Log.d("GPUDecoder", "onDecodeFrame  "+ _inputBufferIndex + " pts = " + pts + " size = "+bytes.limit());
            inputBuffer.clear();
            bytes.position(0);
            inputBuffer.put(bytes);
            return queueInputBuffer(bytes.limit(), pts, 0);
        }
        return ERROR;
    }

    private long outputDecode(){
        int outputBufferIndex = -1;
        try {
            outputBufferIndex = decoder.dequeueOutputBuffer(bufferInfo, TIMEOUT_US);
            if (outputBufferIndex >= 0) {
                // Log.d("GPUDecoder", "outputDecode  "+ outputBufferIndex + " pts = "+bufferInfo.presentationTimeUs+" flag = "+bufferInfo.flags);
                if ((bufferInfo.flags & MediaCodec.BUFFER_FLAG_END_OF_STREAM) != 0) {
                    decoder.releaseOutputBuffer(outputBufferIndex, false);
                    return END_OF_STREAM;
                }else{
                    decoder.releaseOutputBuffer(outputBufferIndex, true);
                    if(bufferInfo.presentationTimeUs >= 0) return bufferInfo.presentationTimeUs;
                    return ERROR;
                }
            }
        } catch (Exception e) {
            e.printStackTrace();
            return ERROR;
        }
        return TRY_AGAIN_LATER;
    }

    private void onFlush() {
        if (disableFlush) {
            return;
        }

        try {
            decoder.flush();
            _inputBufferIndex = -1;
            bufferInfo.presentationTimeUs = 0;
            bufferInfo.flags = 0;
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    private boolean released = false;

    private void onRelease() {
        if (released) {
            return;
        }
        released = true;
        releaseDecoder();
    }

    private void releaseDecoder() {
        if (decoder == null) {
            return;
        }
        releaseAsync(new Runnable() {
            @Override
            public void run() {
                try {
                    decoder.stop();
                } catch (Exception e) {
                    e.printStackTrace();
                }

                try {
                    decoder.release();
                } catch (Exception e) {
                    e.printStackTrace();
                }
                decoder = null;
                videoSurface.release();
            }
        });
    }

    private void releaseAsync(final Runnable runnable) {
        if (runnable == null) {
            return;
        }
        decoderThreadCount.getAndIncrement();
        final HandlerThread releaseHandlerThread = new HandlerThread("GPUDecoder_release_decoder");
        releaseHandlerThread.start();
        Handler releaseHandler = new Handler(releaseHandlerThread.getLooper());
        releaseHandler.post(new Runnable() {
            @Override
            public void run() {
                runnable.run();
                decoderThreadCount.getAndDecrement();
                new Handler(Looper.getMainLooper()).post(new Runnable() {
                    @Override
                    public void run() {
                        releaseHandlerThread.quitSafely();
                    }
                });
            }
        });
    }
}