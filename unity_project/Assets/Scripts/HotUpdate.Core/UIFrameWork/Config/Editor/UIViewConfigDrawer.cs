#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

[CustomPropertyDrawer(typeof(UIViewConfig))]
public sealed class UIViewConfigDrawer : PropertyDrawer
{
    private static List<Type> _cachedTypes;
    private static string[] _cachedTypeNames;
    private static string[] _cachedTypeFullNames;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;

        var y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        var lineHeight = EditorGUIUtility.singleLineHeight;
        var spacing = EditorGUIUtility.standardVerticalSpacing;

        var viewTypeNameProp = property.FindPropertyRelative("ViewTypeName");
        y = DrawViewTypeDropdown(position, viewTypeNameProp, y, lineHeight, spacing);

        var prefabRefProp = property.FindPropertyRelative("PrefabReference");
        var prefabRect = new Rect(position.x, y, position.width, lineHeight);
        EditorGUI.PropertyField(prefabRect, prefabRefProp, new GUIContent("Prefab Reference"));
        y += lineHeight + spacing;

        DrawField(property, "Layer", ref y, position, lineHeight, spacing);
        DrawField(property, "CacheMode", ref y, position, lineHeight, spacing);

        DrawField(property, "FullScreen", ref y, position, lineHeight, spacing);
        DrawField(property, "EnterPopupStack", ref y, position, lineHeight, spacing);
        DrawField(property, "PauseLowerView", ref y, position, lineHeight, spacing);
        DrawField(property, "HideLowerView", ref y, position, lineHeight, spacing);

        DrawField(property, "BlurMode", ref y, position, lineHeight, spacing);
        DrawField(property, "MaskMode", ref y, position, lineHeight, spacing);

        DrawField(property, "CloseByEsc", ref y, position, lineHeight, spacing);
        DrawField(property, "CloseByMask", ref y, position, lineHeight, spacing);

        DrawField(property, "SafeAreaMode", ref y, position, lineHeight, spacing);

        DrawField(property, "SortOffset", ref y, position, lineHeight, spacing);

        EditorGUI.indentLevel--;

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return EditorGUIUtility.singleLineHeight;

        var lines = 14;
        return EditorGUIUtility.singleLineHeight + (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * lines;
    }

    private float DrawViewTypeDropdown(Rect position, SerializedProperty viewTypeNameProp, float y, float lineHeight, float spacing)
    {
        RefreshTypeCache();

        var rect = new Rect(position.x, y, position.width, lineHeight);

        var currentIndex = Array.IndexOf(_cachedTypeFullNames, viewTypeNameProp.stringValue);
        if (currentIndex < 0)
            currentIndex = 0;

        var newIndex = EditorGUI.Popup(rect, "View Type", currentIndex, _cachedTypeNames);
        if (newIndex != currentIndex)
        {
            viewTypeNameProp.stringValue = _cachedTypeFullNames[newIndex];
        }

        return y + lineHeight + spacing;
    }

    private static void DrawField(SerializedProperty property, string fieldName, ref float y, Rect position, float lineHeight, float spacing)
    {
        var prop = property.FindPropertyRelative(fieldName);
        if (prop == null)
            return;

        var rect = new Rect(position.x, y, position.width, lineHeight);
        EditorGUI.PropertyField(rect, prop);
        y += lineHeight + spacing;
    }

    private static void RefreshTypeCache()
    {
        if (_cachedTypes != null)
            return;

        var allTypes = TypeCache.GetTypesDerivedFrom<ViewBase>()
            .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition)
            .OrderBy(t => t.FullName)
            .ToList();

        _cachedTypes = allTypes;
        _cachedTypeNames = new string[allTypes.Count + 1];
        _cachedTypeFullNames = new string[allTypes.Count + 1];

        _cachedTypeNames[0] = "(None)";
        _cachedTypeFullNames[0] = string.Empty;

        for (var i = 0; i < allTypes.Count; i++)
        {
            var type = allTypes[i];
            _cachedTypeNames[i + 1] = type.FullName;
            _cachedTypeFullNames[i + 1] = type.AssemblyQualifiedName;
        }
    }
}
#endif