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

    private GameplayTagDatabase DB => (GameplayTagDatabase)target;

    private void OnEnable()
    {
        treeState ??= new TreeViewState();

        treeView = new GameplayTagTreeView(treeState, DB);

        searchField = new SearchField();
        searchField.downOrUpArrowKeyPressed += treeView.SetFocusAndEnsureSelectedItem;
    }

    public override void OnInspectorGUI()
    {
        if (DB == null)
            return;

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

            treeView.searchString = searchField.OnToolbarGUI(treeView.searchString);
        }
    }

    private void DrawBottomBar()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"Tags: {DB.Tags.Count}");

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Restore from Code", GUILayout.Width(140), GUILayout.Height(24)))
            {
                GameplayTagCodeGenerator.RestoreTags(DB);
                treeView.DirtyReload();
            }

            if (GUILayout.Button("Generate Code", GUILayout.Width(140), GUILayout.Height(24)))
            {
                GenerateCode();
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

        if (DB.AddTag(tag))
        {
            addTag = string.Empty;
            treeView.DirtyReload();
            AssetDatabase.SaveAssetIfDirty(DB);
        }
    }

    private void GenerateCode()
    {
        try
        {
            GameplayTagCodeGenerator.BuildGameplayTags(DB);
            AssetDatabase.SaveAssetIfDirty(DB);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }
}
#endif