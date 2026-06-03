package com.videolayer;

import android.app.Activity;
import android.opengl.GLES11Ext;
import android.opengl.GLES20;
import android.opengl.GLSurfaceView;
import android.view.ViewGroup;

import com.videolayer.VideoViewController;

import javax.microedition.khronos.egl.EGL10;
import javax.microedition.khronos.egl.EGLConfig;
import javax.microedition.khronos.opengles.GL10;

/** 每个 Renderer 持有自己的 native handle */
public class VideoViewRenderer implements GLSurfaceView.Renderer {
    private final long _handle;
    private static long fpsInterval = 33333333;//ns
    private long lastTime;
    private volatile boolean destroyed = false;
    private boolean isNativeDestroy = false;

    private static native void nativeInitGL(long hangle);
    private static native void nativeSizeChange(long handle,int w, int h);
    private static native void nativeDraw(long hangle);
    private static native void nativeDestroy(long hangle);
    public GLSurfaceView view;

    public VideoViewRenderer(long handle) {
        _handle = handle;
    }

    @Override public void onSurfaceCreated(GL10 gl, EGLConfig config) {
        nativeInitGL(_handle);
    }

    @Override public void onSurfaceChanged(GL10 gl, int w, int h) {
        GLES20.glViewport(0, 0, w, h);
        nativeSizeChange(_handle, w, h);
    }

    @Override public void onDrawFrame(GL10 gl) {
        if (destroyed) {
            if(!isNativeDestroy)
            {
                isNativeDestroy = true;
                doDestroy();
            }
            return;
        }

        final long now = System.nanoTime();
        final long interval = now - lastTime;

        if (interval < fpsInterval) {
            try {
                Thread.sleep((fpsInterval - interval) / 1000000);
            } catch (final Exception e) {
            }
        }

        lastTime = System.nanoTime();
        nativeDraw(_handle);
    }

    public void destroy() {
        destroyed = true;
    }

    private void doDestroy()
    {
        nativeDestroy(_handle);
        Activity act = com.unity3d.player.UnityPlayer.currentActivity;
        if (act == null || view == null) return;
        act.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                ViewGroup parent = (ViewGroup) view.getParent();
                if (parent != null) {
                    parent.removeView(view);
                }
            }
        });
    }

}