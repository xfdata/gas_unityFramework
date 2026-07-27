using UnityEditor;
using UnityEngine;

public class GameplayAddTagWindow : EditorWindow
{
    private string parentPath;
    private GameplayTagDatabase database;
    private System.Action onConfirm;
    private string tagName = "";

    private const string InputTagNameControlName = "InputTagNameField";
    private bool hasFocused;

    public static void Show(string parentPath, GameplayTagDatabase db, System.Action onConfirm)
    {
        var window = CreateInstance<GameplayAddTagWindow>();
        window.parentPath = parentPath ?? string.Empty;
        window.database = db;
        window.onConfirm = onConfirm;
        window.titleContent = new GUIContent("Add Tag");
        window.minSize = new Vector2(340, 160);
        window.maxSize = new Vector2(340, 160);
        window.ShowUtility();
        window.Focus();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);

        string parentLabel = string.IsNullOrEmpty(parentPath) ? "<root>" : parentPath;
        GUILayout.Label($"Parent: {parentLabel}", EditorStyles.boldLabel);

        if (database != null)
        {
            int usage = database.GetSiblingUsage(parentPath);
            int retired = database.GetRetiredCount(parentPath);
            int free = database.GetRecycledPoolCount(parentPath);
            EditorGUILayout.LabelField(
                $"Slots: {usage}/{GameplayTagDatabase.MaxSiblingId}  |  Retired: {retired}  |  Free pool: {free}",
                EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Tag Name:", GUILayout.Width(70));

        GUI.SetNextControlName(InputTagNameControlName);
        string input = EditorGUILayout.TextField(tagName);
        if (input != tagName)
        {
            tagName = ValidateAndFilterInput(input);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Confirm", GUILayout.Width(80)))
        {
            ConfirmAddTag();
        }

        if (GUILayout.Button("Cancel", GUILayout.Width(80)))
        {
            Close();
        }

        EditorGUILayout.EndHorizontal();
        if (!hasFocused)
        {
            GUI.FocusControl(InputTagNameControlName);
            hasFocused = true;
        }

        HandleInputEvent();
    }

    private void HandleInputEvent()
    {
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
        {
            ConfirmAddTag();
            Event.current.Use();
        }
        else if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            Close();
            Event.current.Use();
        }
    }

    private void ConfirmAddTag()
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            EditorUtility.DisplayDialog("Error", "Tag name cannot be empty!", "OK");
            return;
        }

        if (database == null)
        {
            EditorUtility.DisplayDialog("Error", "GameplayTagDatabase is missing.", "OK");
            return;
        }

        string fullPath = string.IsNullOrEmpty(parentPath)
            ? tagName
            : $"{parentPath}.{tagName}";

        Undo.RecordObject(database, "Add Gameplay Tag");

        if (!database.AddTag(fullPath, out var error))
        {
            bool looksFull = !string.IsNullOrEmpty(error) &&
                             (error.IndexOf("已满", System.StringComparison.Ordinal) >= 0
                              || error.IndexOf("回收", System.StringComparison.Ordinal) >= 0);

            if (looksFull)
            {
                bool open = EditorUtility.DisplayDialog(
                    "Add Gameplay Tag Failed",
                    (error ?? "Unknown error.") + "\n\n是否打开「Recycle Sibling IDs」？",
                    "Open Recycle Window",
                    "Cancel");

                if (open)
                {
                    GameplayTagRecycleWindow.Show(database, onConfirm, parentFilter: parentPath);
                }
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Add Gameplay Tag Failed",
                    string.IsNullOrEmpty(error) ? "Unknown error." : error,
                    "OK");
            }

            return;
        }

        AssetDatabase.SaveAssetIfDirty(database);
        onConfirm?.Invoke();
        Close();
    }

    private string ValidateAndFilterInput(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var sb = new System.Text.StringBuilder(input.Length);
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c < 128 && (char.IsLetterOrDigit(c) || c == '_'))
                sb.Append(c);
        }

        return sb.ToString();
    }
}
