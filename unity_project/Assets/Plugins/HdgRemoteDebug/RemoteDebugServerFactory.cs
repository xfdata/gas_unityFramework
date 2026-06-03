using UnityEngine;
using System;
using System.Reflection;

public class RemoteDebugServerFactory : MonoBehaviour
{
    private const string EnableRemoteServerInIOSReleaseKey = "HdgRemoteDebug.EnableRemoteServerInIOSRelease";
    public static bool EnableRemoteServerInIOSRelease
    {
        get { return PlayerPrefs.GetInt(EnableRemoteServerInIOSReleaseKey, 0) == 1; }
        set
        {
            PlayerPrefs.SetInt(EnableRemoteServerInIOSReleaseKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
    public int ServerPort = 12000;
    public int BroadcastPort = 12000;

    public void Awake()
    {
        StartServerIfNeeded();
    }

    public void StartServerIfNeeded()
    {
        #if UNITY_IOS && !UNITY_EDITOR && IS_RELEASE
        if (!Debug.isDebugBuild && !EnableRemoteServerInIOSRelease) return;
        #endif
        // Load the server via reflection, in case the server DLL was never loaded
        // (e.g. if it was disabled, a reference to the type would be a compile error).
#if UNITY_WSA
        var serverType = typeof(Hdg.RemoteDebugServer);
#else
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        Type serverType = null;
        Type settingsType = null;

        for (var i = 0; i < assemblies.Length; i++)
        {
            var asm = assemblies[i];
            if (serverType == null)
                serverType = asm.GetType("Hdg.RemoteDebugServer");
            if (settingsType == null)
                settingsType = asm.GetType("Hdg.Settings");
            if (serverType != null && settingsType != null)
                break;
        }

        if (serverType == null)
            return;
#endif

        var server = FindObjectOfType(serverType);
        if (server == null)
        {
            // If there is no server in the scene, then create one.
            if (serverType != null)
            {
#if UNITY_WSA
                Hdg.Settings.DEFAULT_SERVER_PORT = ServerPort == 0 ? 12000 : ServerPort;
                Hdg.Settings.DEFAULT_BROADCAST_PORT = BroadcastPort == 0 ? 12000 : BroadcastPort;
#else
                // Update the default port.
                if (settingsType != null)
                {
                    if (ServerPort == 0)
                        ServerPort = 12000;
                    var fieldInfo = settingsType.GetField("DEFAULT_SERVER_PORT");
                    if (fieldInfo != null)
                        fieldInfo.SetValue(null, ServerPort);
                    if (BroadcastPort == 0)
                        BroadcastPort = 12000;
                    fieldInfo = settingsType.GetField("DEFAULT_BROADCAST_PORT");
                    if (fieldInfo != null)
                        fieldInfo.SetValue(null, BroadcastPort);
#if !UNITY_EDITOR
                    // 禁用广播，让广播线程不等待直接退出
                    fieldInfo = settingsType.GetField("BROADCAST_TIME");
                    if (fieldInfo != null)
                        fieldInfo.SetValue(null, 0);
#endif
                }
#endif
                var comp = gameObject.AddComponent(serverType);

#if !UNITY_EDITOR
                // 禁用广播
                var fi = serverType.GetField("m_broadcaster", BindingFlags.NonPublic | BindingFlags.Instance);
                if (fi != null)
                {
                    var broadcaster = fi.GetValue(comp);
                    if (broadcaster != null)
                    {
                        var methodInfo = broadcaster.GetType().GetMethod("Stop", BindingFlags.Public | BindingFlags.Instance);
                        if (methodInfo != null)
                            methodInfo.Invoke(broadcaster, null);
                    }
                }
#endif
            }
        }
        else
        {
            // Otherwise destroy ourselves because we aren't needed.
            Destroy(gameObject);
        }
    }
}

