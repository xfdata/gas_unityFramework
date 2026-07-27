#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[CustomEditor(typeof(GameplayTagDatabase))]
public sealed class GameplayTagDatabaseEditor : UnityEditor.Editor
{
    private const float TreeHeight = 420f;
    private const string AddFieldControlName = "GameplayTagAddField";

    private GameplayTagTreeView treeView;
    private TreeViewState treeState;
    private SearchField searchField;
    private string addTag = "";
    private string domainValidationError;

    private GameplayTagDatabase DB => (GameplayTagDatabase)target;

    private void OnEnable()
    {
        if (DB != null)
            DB.EnsureMigrated();

        treeState ??= new TreeViewState();

        treeView = new GameplayTagTreeView(treeState, DB);

        searchField = new SearchField();
        searchField.downOrUpArrowKeyPressed += treeView.SetFocusAndEnsureSelectedItem;

        RefreshDomainValidation();
    }

    public override void OnInspectorGUI()
    {
        if (DB == null)
            return;

        DB.EnsureMigrated();

        DrawGeneratedCodePath();
        GUILayout.Space(4);

        DrawToolbar();

        GUILayout.Space(2);

        Rect rect = GUILayoutUtility.GetRect(
            0,
            10000,
            TreeHeight,
            TreeHeight,
            GUILayout.ExpandWidth(true));

        treeView.OnGUI(rect);

        GUILayout.Space(4);

        DrawBottomBar();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUI.SetNextControlName(AddFieldControlName);

            addTag = GUILayout.TextField(
                addTag ?? string.Empty,
                EditorStyles.toolbarTextField,
                GUILayout.MinWidth(180));

            bool canAdd = !string.IsNullOrWhiteSpace(addTag);

            using (new EditorGUI.DisabledScope(!canAdd))
            {
                if (GUILayout.Button("+ Add Tag", EditorStyles.toolbarButton, GUILayout.Width(90)))
                {
                    AddTagFromToolbar();
                }
            }

            HandleEnterToAdd(canAdd);

            GUILayout.FlexibleSpace();

            int retired = DB.GetRetiredCount();
            string recycleLabel = retired > 0 ? $"Recycle IDs ({retired})" : "Recycle IDs";
            if (GUILayout.Button(recycleLabel, EditorStyles.toolbarButton, GUILayout.Width(120)))
            {
                GameplayTagRecycleWindow.Show(DB, () => treeView.DirtyReload());
            }

            treeView.searchString = searchField.OnToolbarGUI(treeView.searchString);
        }
    }

    private void DrawGeneratedCodePath()
    {
        EditorGUI.BeginChangeCheck();
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("domain"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("generatedCodePath"));
        serializedObject.ApplyModifiedProperties();
        if (EditorGUI.EndChangeCheck())
            RefreshDomainValidation();

        int retired = DB.GetRetiredCount();
        int free = DB.GetRecycledPoolCount();

        EditorGUILayout.HelpBox(
            "Sibling Id 稳定分配：删除后进入弃用列表，默认不复用。" +
            "满 255 后可打开 Recycle IDs 手动回收。" +
            $"\n当前弃用: {retired}，可复用池: {free}",
            retired > 0 ? MessageType.Warning : MessageType.Info);

        if (!string.IsNullOrEmpty(domainValidationError))
        {
            EditorGUILayout.HelpBox(domainValidationError, MessageType.Error);
        }
    }

    private void DrawBottomBar()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(
                $"Tags: {DB.Entries.Count}    Retired: {DB.GetRetiredCount()}    Free: {DB.GetRecycledPoolCount()}");

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Recycle IDs", GUILayout.Width(100), GUILayout.Height(24)))
            {
                GameplayTagRecycleWindow.Show(DB, () => treeView.DirtyReload());
            }

            if (GUILayout.Button("Restore from Code", GUILayout.Width(130), GUILayout.Height(24)))
            {
                GameplayTagCodeGenerator.RestoreTags(DB);
                treeView.DirtyReload();
            }
        }

        GUILayout.Space(2);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Scan Legacy", GUILayout.Width(100), GUILayout.Height(24)))
            {
                var report = GameplayTagLegacyFixup.RunFixup(dryRun: true);
                EditorUtility.DisplayDialog("Scan Legacy GameplayTags", report, "OK");
            }

            if (GUILayout.Button("Fix Legacy Tags…", GUILayout.Height(24)))
            {
                if (EditorUtility.DisplayDialog(
                        "Fix Legacy GameplayTags",
                        "扫描项目中 Domain=None 的序列化 Tag，并在可唯一解析时写回 Domain。\n\n" +
                        "建议先 Scan Legacy 预览。继续修复？",
                        "Fix",
                        "Cancel"))
                {
                    var report = GameplayTagLegacyFixup.RunFixup(dryRun: false);
                    EditorUtility.DisplayDialog("Fix Legacy GameplayTags", report, "OK");
                }
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Generate Code", GUILayout.Width(120), GUILayout.Height(24)))
            {
                GenerateCode(force: false);
            }

            if (GUILayout.Button("Force Generate", GUILayout.Width(120), GUILayout.Height(24)))
            {
                if (EditorUtility.DisplayDialog(
                        "Force Generate",
                        "Force Generate 会跳过 value/mask 漂移保护。\n" +
                        "若已有 path 的编码变化，项目中序列化的 Tag 可能全部失效。\n\n确定继续？",
                        "Force Generate",
                        "Cancel"))
                {
                    GenerateCode(force: true);
                }
            }
        }
    }

    private void HandleEnterToAdd(bool canAdd)
    {
        if (!canAdd)
            return;

        Event e = Event.current;
        if (e == null || e.type != EventType.KeyDown)
            return;

        if (GUI.GetNameOfFocusedControl() != AddFieldControlName)
            return;

        if (e.keyCode != KeyCode.Return && e.keyCode != KeyCode.KeypadEnter)
            return;

        AddTagFromToolbar();
        e.Use();
    }

    private void AddTagFromToolbar()
    {
        string tag = addTag?.Trim();

        if (string.IsNullOrEmpty(tag))
            return;

        Undo.RecordObject(DB, "Add Gameplay Tag");

        if (DB.AddTag(tag, out var error))
        {
            addTag = string.Empty;
            treeView.DirtyReload();
            AssetDatabase.SaveAssetIfDirty(DB);
            return;
        }

        HandleAddFailure(error, GameplayTagDatabase.GetParentPath(tag));
    }

    private void HandleAddFailure(string error, string parentPath)
    {
        if (string.IsNullOrEmpty(error))
            return;

        bool looksFull = error.IndexOf("已满", StringComparison.Ordinal) >= 0
                         || error.IndexOf("回收", StringComparison.Ordinal) >= 0;

        if (looksFull && DB.GetRetiredCount(parentPath) + DB.GetRetiredCount() > 0)
        {
            bool open = EditorUtility.DisplayDialog(
                "Add Gameplay Tag Failed",
                error + "\n\n是否打开「Recycle Sibling IDs」回收弃用 Id？",
                "Open Recycle Window",
                "Cancel");

            if (open)
            {
                GameplayTagRecycleWindow.Show(
                    DB,
                    () => treeView.DirtyReload(),
                    parentFilter: parentPath);
            }

            return;
        }

        EditorUtility.DisplayDialog("Add Gameplay Tag Failed", error, "OK");
    }

    private void GenerateCode(bool force)
    {
        try
        {
            RefreshDomainValidation();
            if (!string.IsNullOrEmpty(domainValidationError) && !force)
            {
                EditorUtility.DisplayDialog("Generate Gameplay Tags Failed", domainValidationError, "OK");
                return;
            }

            GameplayTagCodeGenerator.BuildGameplayTags(DB, force);
            AssetDatabase.SaveAssetIfDirty(DB);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Generate Gameplay Tags Failed", e.Message, "OK");
        }
    }

    private void RefreshDomainValidation()
    {
        domainValidationError = null;
        if (DB == null)
            return;

        if (!GameplayTagDomainValidator.TryValidateUniqueDomains(out var error, DB))
            domainValidationError = error;
    }
}
#endif
