#if UNITY_EDITOR

using System;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

public class GameplayTagOdinDrawer : OdinValueDrawer<GameplayTag>
{
    protected override void DrawPropertyLayout(GUIContent label)
    {
        var value = ValueEntry.SmartValue;
        var items = GameplayTagOdinUtility.GetDropdownItems();

        int currentIndex = 0;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Value.Equals(value))
            {
                currentIndex = i;
                break;
            }
        }

        string[] names = new string[items.Count];

        for (int i = 0; i < items.Count; i++)
        {
            names[i] = items[i].Text;
        }

        var rect = EditorGUILayout.GetControlRect();

        if (label != null)
        {
            rect = EditorGUI.PrefixLabel(rect, label);
        }

        EditorGUI.BeginChangeCheck();

        int newIndex = EditorGUI.Popup(rect, currentIndex, names);

        if (EditorGUI.EndChangeCheck())
        {
            newIndex = Mathf.Clamp(newIndex, 0, items.Count - 1);
            ValueEntry.SmartValue = items[newIndex].Value;
        }
    }
}

#endif