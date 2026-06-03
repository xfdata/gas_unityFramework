package com.unity.androidplugin;

import android.app.Activity;
import android.content.Context;
import android.graphics.Color;
import android.graphics.PixelFormat;
import android.graphics.drawable.ColorDrawable;
import android.util.Log;
import android.view.SurfaceView;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.view.Gravity;
import android.widget.FrameLayout;
import com.unity3d.player.UnityPlayer;


public class NativeVideo{
    private SurfaceView unitySurfaceView = null;
    private NativeVideoView videoView;
    private Activity _activity;
    private boolean _onTop = false;
    private String TAG = "Unity_NativeVideo";

    public void Init(Activity activity)
    {
        _activity = activity;
        videoView = new NativeVideoView(_activity);
        FindSurfaceView(_activity.getWindow().getDecorView());
        _activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                if (unitySurfaceView != null) {
                    FrameLayout parentView = (FrameLayout) unitySurfaceView.getParent();
                    videoView.setZOrderOnTop(true);
                    videoView.setZOrderMediaOverlay(false);
                    unitySurfaceView.setZOrderMediaOverlay(true);
                    unitySurfaceView.getHolder().setFormat(PixelFormat.TRANSLUCENT);
                    if(parentView != null)
                    {
                        parentView.addView(videoView);
                    }
                }
            }
        });
    }

    public void SetEventListener(String gameObj,String method)
    {
        videoView.setVideoListener(new NativeVideoView.OnVideoEventListener() {
            @Override
            public void onVideoEvent(String event) {
                Log.d(TAG,"event " + event);
                UnityPlayer.UnitySendMessage(gameObj,method,event);
            }
        });
    }

    public void SetViewRect(int left,int top,int width,int height,int scaleMode)
    {
        Log.d(TAG,"SetViewRect " + left + ":" + top + " : " + width + " : " + height + " scaleMode " + scaleMode);
        _activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                videoView.setViewRect(left,top,width,height,scaleMode);
            }
        });
    }

    //StreamingAssets path
    public void OpenVideoAsset(String videoPath) {
        OpenVideo(videoPath,true);
    }

    public void OpenVideoUri(String videoUri)
    {
        OpenVideo(videoUri,false);
    }

    private void OpenVideo(String videoUri,boolean isAsset) {
        Log.d(TAG,"videoUri = "+videoUri + "isAsset " + isAsset);
        _activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                if(videoView != null){
                    if(isAsset)
                    {
                        videoView.openVideoAsset(videoUri);
                    }
                    else
                    {
                        videoView.openVideoURL(videoUri);
                    }
                }
            }
        });
    }

    public void UseVideoSize(boolean use)
    {
        videoView.useVideoSize(use);
    }

    public void SetLoop(boolean isLoop)
    {
        videoView.setLooping(isLoop);
    }

    public void Play(boolean isLoop)
    {
        videoView.setLooping(isLoop);
        videoView.start();
    }

    public void Pause()
    {
        videoView.pause();
    }

    public void Resume()
    {
        videoView.resume();
    }

    public void Stop()
    {
        videoView.stop();
    }

    public void SeekTo(int ms)
    {
        videoView.seekTo(ms);
    }
    
    public void RetainLastFrame(boolean retain)
    {
        videoView.retainLastFrame(retain);
    }
    
    public void Dispose()
    {
        Log.d(TAG,"Dispose");
        _activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                if(videoView != null)
                {
                    if(unitySurfaceView != null)
                    {
                        FrameLayout parentView = (FrameLayout) unitySurfaceView.getParent();
                        if(parentView != null)
                        {
                            parentView.removeView(videoView);
                        }
                        unitySurfaceView.getHolder().setFormat(PixelFormat.OPAQUE);
                        unitySurfaceView = null;
                    }
                    videoView = null;
                }
            }
        });
    }

    public void FindSurfaceView(View rootView) {
        if(rootView == null) return;
        if(rootView instanceof SurfaceView){
            unitySurfaceView = (SurfaceView)rootView;
            return;
        }

        // 如果是ViewGroup，则递归遍历其子View
        if (rootView instanceof ViewGroup) {
            ViewGroup viewGroup = (ViewGroup) rootView;
            for (int i = 0; i < viewGroup.getChildCount(); i++) {
                View childView = viewGroup.getChildAt(i);
                FindSurfaceView(childView); // 递归遍历子View
            }
        }
    }

    public void SetOnTop(boolean onTop)
    {
        _onTop = onTop;
        _activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                if(videoView != null)
                {
                    videoView.setZOrderOnTop(onTop);
                }
            }
        });
    }
}