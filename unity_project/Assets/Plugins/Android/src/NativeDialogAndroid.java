package com.unity3d.player;

import android.app.AlertDialog;
import android.content.DialogInterface;
import android.os.Build;
import android.util.Log;
import android.view.ContextThemeWrapper;

public class NativeDialogAndroid
{
    static String s_GameObjName;
    static String s_MethodName;

    public static void SetCallbackInfo(String gameObjName, String methodName)
    {
        s_GameObjName = gameObjName;
        s_MethodName = methodName;
    }
    public static void Show(String text, String caption, String okButton)
    {
        Log.v("Unity", "show-1");
        AlertDialog.Builder dialog = new AlertDialog.Builder(new ContextThemeWrapper(UnityPlayer.currentActivity, GetTheme()));
        if (caption != null)
        {
            dialog.setTitle(caption);
        }
        if (text != null)
        {
            dialog.setMessage(text);
        }
        dialog.setPositiveButton(okButton, new DialogInterface.OnClickListener() {
            @Override
            public void onClick(DialogInterface dialogInterface, int i) {
                Log.v("Unity", "show-1.1");
                UnityPlayer.UnitySendMessage(s_GameObjName, s_MethodName, "0");
                Log.v("Unity", "show-1.2");
            }
        });
        dialog.setCancelable(false);
        dialog.show();
        Log.v("Unity", "show-2");
    }
    public static void confirm(String text, String caption, String button1, String button2, String button3)
    {
        Log.v("Unity","confirm-1");
        AlertDialog.Builder dialog = new AlertDialog.Builder(new ContextThemeWrapper(UnityPlayer.currentActivity, GetTheme()));
        if (caption != null)
        {
            dialog.setTitle(caption);
        }
        if (text != null)
        {
            dialog.setMessage(text);
        }
        dialog.setPositiveButton(button1, new DialogInterface.OnClickListener() {
            @Override
            public void onClick(DialogInterface dialogInterface, int i) {
                UnityPlayer.UnitySendMessage(s_GameObjName, s_MethodName, "0");
            }
        });
        Log.v("Unity","confirm-2");
        if (button2 != null)
        {
            dialog.setNegativeButton(button2, new DialogInterface.OnClickListener() {
                @Override
                public void onClick(DialogInterface dialogInterface, int i) {
                    UnityPlayer.UnitySendMessage(s_GameObjName, s_MethodName, "1");
                }
            });
        }
        Log.v("Unity","confirm-3");
        if (button3 != null)
        {
            dialog.setNeutralButton(button3, new DialogInterface.OnClickListener() {
                @Override
                public void onClick(DialogInterface dialogInterface, int i) {
                    UnityPlayer.UnitySendMessage(s_GameObjName, s_MethodName, "2");
                }
            });
        }
        Log.v("Unity","confirm-4");
        dialog.setCancelable(false);
        dialog.show();
        Log.v("Unity","confirm-5");
    }
    static int GetTheme()
    {
        int theme = 0;
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP)
        {
            theme = android.R.style.Theme_Material_Light_Dialog;
        }
        else
        {
            theme = android.R.style.Theme_Holo_Dialog;
        }
        return  theme;
    }
}
