using UnityEditor;
using UnityEngine;

public class GameplayAddTagWindow : EditorWindow
{
    private string parentPath;
    private GameplayTagDatabase database;
    private System.Action onConfirm;
    private string tagName = "";
    
    private const string InputTagNameControlName = "InputTagNameField";
    private bool hasFocused = false;
    public static void Show(string parentPath, GameplayTagDatabase db, System.Action onConfirm)
    {
        var window = CreateInstance<GameplayAddTagWindow>();
        window.parentPath = parentPath;
        window.database = db;
        window.onConfirm = onConfirm;
        window.titleContent = new GUIContent("Add Tag");
        window.minSize = new Vector2(300, 120);
        window.maxSize = new Vector2(300, 120);
        window.ShowUtility();
        window.Focus();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);

        GUILayout.Label($"Parent: {parentPath}", EditorStyles.boldLabel);

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
        if (!string.IsNullOrWhiteSpace(tagName))
        {
            string fullPath = $"{parentPath}.{tagName}";
            database.AddTag(fullPath);
            onConfirm?.Invoke();
            Close();
        }
        else
        {
            EditorUtility.DisplayDialog("Error", "Tag name cannot be empty!", "OK");
        }
    }

    private string ValidateAndFilterInput(string input)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (char c in input)
        {
            if (char.IsLetterOrDigit(c) && (c < 128))
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}