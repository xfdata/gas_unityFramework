package com.unity.androidplugin;

import android.app.Activity;
import android.content.res.AssetFileDescriptor;
import android.graphics.PixelFormat;
import android.media.AudioManager;
import android.media.MediaPlayer;
import android.net.Uri;
import android.util.Log;
import android.view.Gravity;
import android.view.SurfaceHolder;
import android.view.SurfaceView;
import android.widget.FrameLayout;
import android.widget.MediaController.MediaPlayerControl;

public class NativeVideoView extends SurfaceView implements MediaPlayerControl {

    class ViewRect
    {
        public int left;
        public int top;
        public int width;
        public int height;

        public void copy(ViewRect other) {
            this.left = other.left;
            this.top = other.top;
            this.width = other.width;
            this.height = other.height;
        }
    }

    private Activity _activity = null;
    private int _videoWidth = 0;
    private int _videoHeight = 0;
    private boolean _isAsset = false;
    private boolean _looping = false;
    private int _scaleMode = ScaleToFit;

    private MediaPlayer _mediaPlayer = null;
    private Uri _uri = null;
    private String _assetPath;
    private String TAG = "Unity_NativeVideoView";
    private ViewRect _drawRect = new ViewRect();
    private ViewRect _viewRect = new ViewRect();
    private int _bufferPercentage;
    private int _seek;
    private int _duration;
    private int _currentState = STATE_IDLE;
    private boolean _isPlaying;
    private boolean _useVideoSize = false;
    private boolean _retainLastFrame = true;
    

    public static final int StretchToFill= 0;
    public static final int ScaleAndCrop = 1;
    public static final int ScaleToFit = 2;
    // all possible internal states
    private static final int STATE_ERROR              = -1;
    private static final int STATE_IDLE               = 0;
    private static final int STATE_PREPARING          = 1;
    private static final int STATE_PREPARED           = 2;
    private static final int STATE_PLAYING            = 3;
    private static final int STATE_PAUSED             = 4;
    private static final int STATE_PLAYBACK_COMPLETED = 5;

    private static final String EVENT_ERROR = "Error";
    private static final String EVENT_COMPLETE = "Complete";
    private static final String EVENT_FINISH = "Finish";
    private static final String EVENT_PREPARE = "Prepare";
    
    public interface OnVideoEventListener
    {
        void onVideoEvent(String event);
    }

    private OnVideoEventListener _onVideoEventListener;


    public void setVideoListener(OnVideoEventListener l)
    {
        _onVideoEventListener = l;
    }


    public NativeVideoView(Activity activity) {
        super(activity);
        _activity = activity;
        initVideoView();
    }

    private void initVideoView() {
        _videoWidth = 0;
        _videoHeight = 0;
        _currentState = STATE_IDLE;

        Log.d(TAG, "initVideoView: " + _activity);

        getHolder().addCallback(new SurfaceHolder.Callback() {
            @Override
            public void surfaceCreated(SurfaceHolder holder)
            {
                Log.d(TAG,"surfaceCreated "+ holder);
                
                if(_mediaPlayer == null && _isPlaying)
                {
                    openVideo();
                }
                if (_mediaPlayer != null) {
                    _mediaPlayer.setDisplay(holder);
                }
            }

            @Override
            public void surfaceChanged(SurfaceHolder holder, int format, int w, int h)
            {
                Log.d(TAG,"surfaceChanged: " + w + " : "+ h);
                if (_mediaPlayer != null) {
                    _mediaPlayer.setDisplay(holder);
                }
            }

            @Override
            public void surfaceDestroyed(SurfaceHolder holder)
            {
                Log.d(TAG,"surfaceDestroyed ");
                _seek = getCurrentPosition();
                release();
            }
        });

        setFocusable(true);
        setFocusableInTouchMode(true);
    }

    public void openVideoURL(String url) {
        _isAsset = false;
        _uri = Uri.parse(url);
        openVideo();
    }

    public void openVideoAsset(String assetPath)
    {
        _isAsset = true;
        _assetPath = assetPath;
        openVideo();
    }

    public void setLooping(boolean looping) {
        _looping = looping;
    }

    public void useVideoSize(boolean use)
    {
        _useVideoSize = use;
    }

    private void openVideo() {
        Log.d(TAG,"openVideo " + " _uri " + _uri + " _assetPath "+ _assetPath );
        if (_uri == null && _assetPath == null) {
            // not ready for playback just yet, will try again later
            return;
        }

        release();

        try {
            _mediaPlayer = new MediaPlayer();
            _mediaPlayer.setOnPreparedListener(_preparedListener);
            _mediaPlayer.setOnVideoSizeChangedListener(_sizeChangedListener);
            _mediaPlayer.setOnCompletionListener(_completionListener);
            _mediaPlayer.setOnErrorListener(_errorListener);
            _mediaPlayer.setOnBufferingUpdateListener(_bufferingUpdateListener);
            _mediaPlayer.setAudioStreamType(AudioManager.STREAM_MUSIC);
            _mediaPlayer.setScreenOnWhilePlaying(true);
            _mediaPlayer.setLooping(_looping);
            _duration = 0;
            _bufferPercentage = 0;

            if (_isAsset) {
                AssetFileDescriptor afd = _activity.getAssets().openFd(_assetPath);
                _mediaPlayer.setDataSource(afd.getFileDescriptor(),afd.getStartOffset(),afd.getLength());
            } else {
                _mediaPlayer.setDataSource(_activity, _uri);
            }

            _mediaPlayer.prepareAsync();
            _currentState = STATE_PREPARING;
            requestLayout();
            invalidate();
        } catch (Exception ex) {
            _currentState = STATE_ERROR;
            Log.w(TAG, "Unable to open content: " + _uri, ex);
            return;
        }
    }

    protected
    MediaPlayer.OnVideoSizeChangedListener _sizeChangedListener =
            new MediaPlayer.OnVideoSizeChangedListener() {
                public void onVideoSizeChanged(MediaPlayer mp, int width, int height) {
                    _videoWidth = mp.getVideoWidth();
                    _videoHeight = mp.getVideoHeight();
                    Log.d(TAG,"onVideoSizeChanged");
                    adjustSize();
                }
            };


    MediaPlayer.OnPreparedListener _preparedListener = new MediaPlayer.OnPreparedListener() {
        public void onPrepared(MediaPlayer mp) {
            _videoWidth = mp.getVideoWidth();
            _videoHeight = mp.getVideoHeight();
            _duration = mp.getDuration();
            _currentState = STATE_PREPARED;

            Log.d(TAG, "onPrepared: "+_isPlaying + " size " + _videoWidth + ":" + _videoHeight + " duration "+_duration + " seek" + _seek );

            adjustSize();
            if(_isPlaying)
            {
                seekTo(_seek);
                start();
            }
            
            requestLayout();
            invalidate();
            if (_onVideoEventListener != null) {
                _onVideoEventListener.onVideoEvent(EVENT_PREPARE);
            }
        }
    };

    private MediaPlayer.OnCompletionListener _completionListener =new MediaPlayer.OnCompletionListener() {
        public void onCompletion(MediaPlayer mp) {
            _currentState = STATE_PLAYBACK_COMPLETED;

            // Do not release the player if we are looping as we still need the
            // the player resources to exist
            if (!_looping) {
                if (_onVideoEventListener != null) {
                    _onVideoEventListener.onVideoEvent(EVENT_FINISH);
                }
                release();
                if(!_retainLastFrame)
                {
                    getHolder().setFixedSize(0,0);
                }
                
                _isPlaying = false;
            }

            if (_onVideoEventListener != null) {
                _onVideoEventListener.onVideoEvent(EVENT_COMPLETE);
            }
        }
    };

    private MediaPlayer.OnErrorListener _errorListener = new MediaPlayer.OnErrorListener() {
        public boolean onError(MediaPlayer mp, int framework_err, int impl_err) {
            _currentState = STATE_ERROR;
            if (_onVideoEventListener != null) {
                _onVideoEventListener.onVideoEvent(EVENT_ERROR);
            }
            return  true;
        }
    };

    private MediaPlayer.OnBufferingUpdateListener _bufferingUpdateListener =
            new MediaPlayer.OnBufferingUpdateListener() {
                public void onBufferingUpdate(MediaPlayer mp, int percent) {
                    _bufferPercentage = percent;
                    Log.d(TAG, "onBufferingUpdate: "+_bufferPercentage);
                }
            };

//    @Override
//    protected void onMeasure(int widthMeasureSpec, int heightMeasureSpec) {
//        Log.d(TAG, "onMeasure: " + _drawRect.width + " : " + _drawRect.height);
//        setMeasuredDimension(_drawRect.width, _drawRect.height);
//    }

    public void setViewRect(int left, int top, int maxWidth, int maxHeight,int scaleMode) {
        _viewRect.left = left;
        _viewRect.top = top;
        _viewRect.width = maxWidth;
        _viewRect.height = maxHeight;
        _scaleMode = scaleMode;
        adjustSize();
    }

    public void calDrawRect(int scaleMode,ViewRect rect)
    {
        if (_videoWidth == 0 || _videoHeight == 0 || rect.width == 0 || rect.height == 0 || scaleMode == StretchToFill) {
            _drawRect.copy(rect);
        }
        else{
            float scaleX = 1;
            float scaleY = 1;
            if(_scaleMode == ScaleToFit) {
                scaleX = Math.min((float) rect.width / _videoWidth, (float) rect.height / _videoHeight);
                scaleY = scaleX;
            }
            else if(_scaleMode == ScaleAndCrop) {
                scaleX = Math.max((float) rect.width / _videoWidth, (float) rect.height / _videoHeight);
                scaleY = scaleX;
            }
            else if(_scaleMode == StretchToFill)
            {
                scaleX = (float) rect.width / _videoWidth;
                scaleY = (float) rect.height / _videoHeight;
            }
            _drawRect.width = (int)(_videoWidth * scaleX);
            _drawRect.height = (int)(_videoHeight * scaleY);
            _drawRect.left = rect.left + (rect.width - _drawRect.width) / 2;
            _drawRect.top = rect.top + (rect.height - _drawRect.height) / 2;
        }
    }

    public void adjustSize() {
        if(_useVideoSize)
        {
            _viewRect.width = _videoWidth;
            _viewRect.height = _videoHeight;
        }
        Log.d(TAG, "adjustSize _viewRect: " + _viewRect.left + " : " + _viewRect.top + " : " + _viewRect.width + " : " + _viewRect.height);

        calDrawRect(_scaleMode,_viewRect);
        getHolder().setFixedSize(_drawRect.width, _drawRect.height);

        FrameLayout.LayoutParams lParams = new FrameLayout.LayoutParams(FrameLayout.LayoutParams.WRAP_CONTENT, FrameLayout.LayoutParams.WRAP_CONTENT);
        lParams.leftMargin = _drawRect.left;
        lParams.topMargin = _drawRect.top;
        lParams.gravity = Gravity.TOP | Gravity.LEFT;
        Log.d(TAG, "adjustSize result: " + _drawRect.left + " : " + _drawRect.top + " : " + _drawRect.width + " : " + _drawRect.height);
        setLayoutParams(lParams);
    }

    private void release() {
        if (_mediaPlayer != null) {
            _mediaPlayer.setDisplay(null);
            _mediaPlayer.reset();
            _mediaPlayer.release();
            _mediaPlayer = null;
            _currentState = STATE_IDLE;
            Log.d(TAG, "release mediaPlayer");
        }
    }

    public void start() {
        Log.d(TAG, "start");
        if (isInPlaybackState()) {
            Log.d(TAG, "mediaPlayer start");
            _mediaPlayer.start();
            _currentState = STATE_PLAYING;
        }
        _isPlaying = true;
    }

    public void pause() {
        Log.d(TAG, "pause");
        if (isInPlaybackState()) {
            if (_mediaPlayer.isPlaying()) {
                _mediaPlayer.pause();
                _currentState = STATE_PAUSED;
            }
        }
        _isPlaying = false;
    }

    public void stop() {
        Log.d(TAG, "stop");
        if (isInPlaybackState()) {
            if (_mediaPlayer.isPlaying()) {
                release();
            }
        }
        _isPlaying = false;
    }

    public void resume() {
        if (isInPlaybackState()) {
            if (_currentState == STATE_PAUSED) {
                _mediaPlayer.start();
                _currentState = STATE_PLAYING;
            }
        }
        _isPlaying = true;
    }

    public void restart() {
        if (isInPlaybackState()) {
            _seek = 0;
            _mediaPlayer.seekTo(_seek);
            _mediaPlayer.start();
            _currentState = STATE_PLAYING;
        }
        _isPlaying = true;
    }
    // cache duration as mDuration for faster access
    public int getDuration() {
        return _duration;
    }

    public int getCurrentPosition() {
        if (isInPlaybackState()) {
            return _mediaPlayer.getCurrentPosition();
        }
        return 0;
    }

    public void seekTo(int msec) {
        if (isInPlaybackState()) {
            _mediaPlayer.seekTo(msec);
            _seek = 0;
        } else {
            _seek = msec;
        }
    }

    public boolean isPlaying() {
        return isInPlaybackState() && _mediaPlayer.isPlaying();
    }
    
    public void retainLastFrame(boolean retain)
    {
        _retainLastFrame = retain;
    }

    @Override
    public int getBufferPercentage() {
        return _bufferPercentage;
    }

    public boolean isInPlaybackState() {
        return (_mediaPlayer != null &&
                _currentState != STATE_ERROR &&
                _currentState != STATE_IDLE &&
                _currentState != STATE_PREPARING);
    }

    @Override
    public void setVisibility(int visibility) {
        Log.d(TAG, "setVisibility: " + visibility);
        super.setVisibility(visibility);
    }

    @Override
    public boolean canPause() {
        return false;
    }

    @Override
    public boolean canSeekBackward() {
        return false;
    }

    @Override
    public boolean canSeekForward() {
        return false;
    }

    @Override
    public int getAudioSessionId() {
        return 0;
    }
}
