package com.videolayer;

import android.app.Activity;
import android.content.Context;
import android.content.res.AssetManager;
import android.graphics.PixelFormat;
import android.opengl.GLSurfaceView;
import android.view.ViewGroup;
import android.util.LongSparseArray;
import android.view.SurfaceView;
import android.view.View;
import android.util.Log;

public class VideoViewController {
    static {
        System.loadLibrary("unity_video_layer");
    }

    private static native void JniHelper_SetAssetManager(Context ctx, AssetManager am);

    // 保存每个 handle 对应的 GLSurfaceView，方便销毁
    private static final LongSparseArray<VideoViewRenderer> sRenders = new LongSparseArray<>();

    public static void CreateFromNative(final long handle) {
        Activity act = com.unity3d.player.UnityPlayer.currentActivity; 
        if (act == null) return;

        JniHelper_SetAssetManager(act, act.getAssets());

        if (sRenders.get(handle) != null) return;
        VideoViewRenderer renderer = new VideoViewRenderer(handle);
        sRenders.put(handle, renderer);

        act.runOnUiThread(new Runnable() {
            @Override
            public void run() {
            try{
                GLSurfaceView view = new GLSurfaceView(act);
                renderer.view = view;
                view.setEGLContextClientVersion(3);
                view.setEGLConfigChooser(8,8,8,8,0,0);
                view.setZOrderOnTop(true);
                view.setZOrderMediaOverlay(true);
                view.setRenderer(renderer);
                view.setRenderMode(GLSurfaceView.RENDERMODE_CONTINUOUSLY);
                view.getHolder().setFormat(PixelFormat.TRANSLUCENT);
                ViewGroup parentView = (ViewGroup)act.getWindow().getDecorView();
                SurfaceView unitySurface = findSurfaceView(parentView,0);
                if(unitySurface != null && (ViewGroup)unitySurface.getParent() != null)
                {
                    parentView = (ViewGroup)unitySurface.getParent();
                }
                parentView.addView(view,
                        new ViewGroup.LayoutParams(
                                ViewGroup.LayoutParams.MATCH_PARENT,
                                ViewGroup.LayoutParams.MATCH_PARENT));
            }catch (Exception e) {
                    Log.e("VideoViewController", "CreateFromNative Error", e);
            }
            }
        });
    }

    private static SurfaceView findSurfaceView(View view, int depth) {
        if (view == null) return null;

        // 找到 SurfaceView
        if (view instanceof SurfaceView) {
            return (SurfaceView)view;
        }
        // 递归遍历子 View
        if (view instanceof ViewGroup) {
            ViewGroup group = (ViewGroup) view;
            for (int i = 0; i < group.getChildCount(); i++) {
                SurfaceView result = findSurfaceView(group.getChildAt(i),depth + 1);
                if (result != null) {
                    return result;
                }
            }
        }
        return null;
    }

    //Unity线程通知render线程进行destroy
    public static void DestroyFromNative(final long handle) {
        Activity act = com.unity3d.player.UnityPlayer.currentActivity; 
        if (act == null) return;
        VideoViewRenderer render = sRenders.get(handle);
        if (render == null) return;
        render.destroy();
        sRenders.remove(handle);
    }
}