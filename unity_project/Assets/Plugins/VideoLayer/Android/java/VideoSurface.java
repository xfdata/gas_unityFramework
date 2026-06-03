package com.videolayer;

import android.graphics.SurfaceTexture;
import android.os.Build;
import android.os.Handler;
import android.os.HandlerThread;
import android.os.Looper;
import android.view.Surface;

import java.lang.reflect.Constructor;
import java.lang.reflect.Field;

public class VideoSurface implements SurfaceTexture.OnFrameAvailableListener {
    private static HandlerThread handlerThread;
    private static int HandlerThreadCount = 0;
    private static final Object handlerLock = new Object();

    private static synchronized void StartHandlerThread() {
        HandlerThreadCount++;
        if (handlerThread == null) {
            handlerThread = new HandlerThread("VideoSurface");
            handlerThread.start();
        }
    }

    private int preTex = 0;
    private Surface outputSurface;
    private SurfaceTexture surfaceTexture;
    private final Object frameSyncObject = new Object();
    private boolean frameAvailable = false;
    private boolean released = false;
    private int retainCount = 1;

    public static VideoSurface Make() {
        VideoSurface videoSurface = new VideoSurface();
        synchronized (handlerLock) {
            StartHandlerThread();
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                videoSurface.surfaceTexture = new SurfaceTexture(false);
            } else {
                videoSurface.surfaceTexture = new SurfaceTexture(0);
                videoSurface.surfaceTexture.detachFromGLContext();
            }
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
                videoSurface.surfaceTexture.setOnFrameAvailableListener(videoSurface, new Handler(handlerThread.getLooper()));
            } else {
                videoSurface.surfaceTexture.setOnFrameAvailableListener(videoSurface);
                videoSurface.reflectLooper();
            }
        }
        videoSurface.outputSurface = new Surface(videoSurface.surfaceTexture);
        return videoSurface;
    }

    private void reflectLooper() {
        Class<?>[] innerClassArray = SurfaceTexture.class.getDeclaredClasses();
        Class eventHandlerClass = null;
        for (Class innerC : innerClassArray) {
            if (innerC.getName().toLowerCase().contains("handler")) {
                eventHandlerClass = innerC;
                break;
            }
        }

        if (eventHandlerClass == null) {
            return;
        }

        Class[] paramTypes = {SurfaceTexture.class, Looper.class};
        try {
            @SuppressWarnings("unchecked")
            Constructor eventHandlerConstructor = eventHandlerClass.getConstructor(paramTypes);
            Object eventHandlerObj = eventHandlerConstructor.newInstance(surfaceTexture, handlerThread.getLooper());
            Class<?> classType = surfaceTexture.getClass();
            Field field = classType.getDeclaredField("mEventHandler");
            field.setAccessible(true);
            field.set(surfaceTexture, eventHandlerObj);
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    public void onFrameAvailable(SurfaceTexture st) {
        frameAvailable = true;
    }

    public Surface getOutputSurface() {
        return outputSurface;
    }

    private boolean updateTexImage() {
        try {
            if(frameAvailable){
                frameAvailable = false;
                surfaceTexture.updateTexImage();
            }
        } catch (Exception e) {
            e.printStackTrace();
            return false;
        }
        return true;
    }

    private boolean attachToGLContext(int texName) {
        try {
            surfaceTexture.attachToGLContext(texName);
        } catch (Exception e) {
            e.printStackTrace();
            return false;
        }
        return true;
    }

    public void retain() {
        retainCount++;
    }

    public void release() {
        retainCount--;
        if (released || retainCount > 0) {
            return;
        }
        released = true;
        synchronized (handlerLock) {
            HandlerThreadCount--;
            if (HandlerThreadCount == 0) {
                handlerThread.quit();
                handlerThread = null;
            }
        }
        
        if (outputSurface != null) {
            outputSurface.release();
            outputSurface = null;
        }

        if (surfaceTexture != null) {
            surfaceTexture.release();
            surfaceTexture = null;
        }
    }
}
