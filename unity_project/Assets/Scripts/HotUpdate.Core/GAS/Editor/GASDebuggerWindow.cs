using UnityEditor;
using UnityEngine;

public class GASDebuggerWindow : EditorWindow
{
    [MenuItem("Tools/GAS 调试器")]
    public static void Open()
    {
        var window = GetWindow<GASDebuggerWindow>("GAS调试器");
        window.minSize = new Vector2(360f, 240f);
        window.Show();

        var debugger = GetOrCreateDebugger();
        debugger.showWindow = true;
        Selection.activeGameObject = debugger.gameObject;
    }

    private GAS.GASDebugger debugger;

    private void OnEnable()
    {
        debugger = FindDebugger();
    }

    private void OnInspectorUpdate()
    {
        if (debugger == null)
            debugger = FindDebugger();

        Repaint();
    }

    private void OnGUI()
    {
        debugger = FindDebugger();

        EditorGUILayout.LabelField("Game View Overlay", EditorStyles.boldLabel);

        if (debugger == null)
        {
            EditorGUILayout.HelpBox("场景中还没有 GAS 调试器。", MessageType.Info);
            if (GUILayout.Button("创建并显示"))
            {
                debugger = GetOrCreateDebugger();
                debugger.showWindow = true;
                Selection.activeGameObject = debugger.gameObject;
            }
            return;
        }

        EditorGUILayout.ObjectField("调试器", debugger, typeof(GAS.GASDebugger), true);
        EditorGUILayout.LabelField("显示状态", debugger.showWindow ? "已显示" : "已隐藏");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(debugger.showWindow ? "隐藏 Overlay" : "显示 Overlay"))
        {
            debugger.showWindow = !debugger.showWindow;
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("选中Debugger对象"))
        {
            Selection.activeGameObject = debugger.gameObject;
        }

        if (GUILayout.Button("重置窗口"))
        {
            debugger.windowX = 10f;
            debugger.windowY = 10f;
            debugger.windowWidth = 540;
            debugger.windowHeight = 640;
            debugger.showWindow = true;
            SceneView.RepaintAll();
        }
        GUILayout.EndHorizontal();

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Debug Info", EditorStyles.boldLabel);
        debugger.DrawDebuggerPanel();
    }

    private static GAS.GASDebugger GetOrCreateDebugger()
    {
        var debugger = FindDebugger();
        if (debugger != null)
            return debugger;

        var go = new GameObject("[GAS 调试器]");
        go.hideFlags = HideFlags.DontSave;
        debugger = go.AddComponent<GAS.GASDebugger>();
        debugger.showWindow = true;
        return debugger;
    }

    private static GAS.GASDebugger FindDebugger()
    {
        var debuggers = Resources.FindObjectsOfTypeAll<GAS.GASDebugger>();
        for (int i = 0; i < debuggers.Length; i++)
        {
            var item = debuggers[i];
            if (item != null && item.gameObject != null)
                return item;
        }

        return null;
    }
}
