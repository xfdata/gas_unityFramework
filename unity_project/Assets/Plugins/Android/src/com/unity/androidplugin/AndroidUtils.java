package com.unity.androidplugin;

import android.Manifest;
import android.app.ActivityManager;
import android.app.AlarmManager;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.content.res.AssetManager;
import android.os.Environment;
import android.os.StatFs;
import android.util.Log;

import android.content.pm.PackageManager;

import com.unity3d.player.UnityPlayer;
import com.unity3d.player.UnityPlayerActivity;

import java.io.File;
import java.io.InputStream;

import android.os.Build;
import android.net.Uri;
import android.util.Log;
import android.content.Context;
import android.telephony.TelephonyManager;

// import androidx.core.content.ContextCompat;

public class AndroidUtils{
    private static  ActivityManager _activityManager;
    public static ActivityManager activityManager() {
        if (_activityManager == null) {
            _activityManager = (ActivityManager) UnityPlayer.currentActivity.getSystemService(Context.ACTIVITY_SERVICE);
        }
        return  _activityManager;
    }
    private  static AssetManager _assetManager;
    public  static  AssetManager assetManager() {
        if (_assetManager == null) {
            _assetManager = UnityPlayer.currentActivity.getAssets();
        }
        return  _assetManager;
    }
    public static int GetFileSize(String fileName)
    {
        AssetManager assetManager = assetManager();
        try {
            InputStream inputStream = assetManager.open(fileName);
            inputStream.reset();

            int size = inputStream.available();
            Log.e("Unity", "size=" + size);
            inputStream.close();

            return size;
        }
        catch (Exception e) {
            Log.e("Unity", "failed to GetFileSize " + fileName + ": " + e.getMessage() );
            return -1;
        }
    }
    public static byte[] ReadAllBytes(String fileName, byte[] buffer, int offset, int count) {
        AssetManager assetManager = assetManager();
        try {
            InputStream inputStream = assetManager.open(fileName);
            inputStream.reset();

            inputStream.skip(offset);

            int readed = inputStream.read(buffer, 0, count);

            inputStream.close();

            if (readed != count) {
                Log.e("Unity", "ReadAllBytes faield: readed=" + readed + ", count=" + count);
                return null;
            }

            return buffer;
        }
        catch (Exception e) {
            Log.e("Unity", "failed to ReadAllBytes " + fileName + ": " + e.getMessage() );
            return null;
        }
    }
    public static boolean IsFileExists(String fileName) {
        AssetManager assetManager = assetManager();
        try {
            InputStream inputStream = assetManager.open(fileName);
            
            inputStream.close();

            return true;
        }
        catch (Exception e) {
            return false;
        }
    }
	public static long GetFreeDiskSpace() {
        try {
            File file = Environment.getDataDirectory();
            StatFs sf = new StatFs(file.getPath());
            return sf.getAvailableBytes();
        } catch (Throwable e) {
            Log.e("Unity", "GetFreeDiskSpace: " + e.getLocalizedMessage());
			return -1;
        }
    }
    public static String getCarrier(Context context) {
        /* try {
            // 检查权限
            if (ContextCompat.checkSelfPermission(context, Manifest.permission.READ_PHONE_STATE)
                    != PackageManager.PERMISSION_GRANTED) {
                return "[permission denied]";
            }

            TelephonyManager telephonyManager = (TelephonyManager)
                    context.getSystemService(Context.TELEPHONY_SERVICE);

            String operatorString = telephonyManager.getSimOperator();
            return (operatorString == null || operatorString.isEmpty())
                    ? "[no Carrier]"
                    : operatorString;
        } catch (Exception e) {
            return "[error: " + e.getMessage() + "]";
        } */
        return "Unknown";
    }
}