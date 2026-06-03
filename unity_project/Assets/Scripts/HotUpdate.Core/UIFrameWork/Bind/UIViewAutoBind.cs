using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[AttributeUsage(AttributeTargets.Field)]
public class UIViewBindAttribute : Attribute
{
    public string Path { get; }
    public bool Optional { get; set; }

    public UIViewBindAttribute()
    {
    }

    public UIViewBindAttribute(string path)
    {
        Path = path;
    }
}

[AttributeUsage(AttributeTargets.Field)]
public sealed class UIAttribute : UIViewBindAttribute
{
    public UIAttribute()
    {
    }

    public UIAttribute(string path) : base(path)
    {
    }
}

public static class UIViewAutoBind
{
    private const BindingFlags FieldFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly Dictionary<Type, FieldInfo[]> FieldCache = new();

    public static void Bind(object target, Transform root)
    {
        if (target == null || root == null)
            return;

        var fields = GetBindableFields(target.GetType());
        if (fields.Length == 0)
            return;

        foreach (var field in fields)
            BindField(target, root, field);
    }

    private static FieldInfo[] GetBindableFields(Type type)
    {
        if (FieldCache.TryGetValue(type, out var cached))
            return cached;

        var fields = new List<FieldInfo>();
        var cursor = type;

        while (cursor != null && cursor != typeof(object))
        {
            foreach (var field in cursor.GetFields(FieldFlags))
            {
                if (field.IsStatic || field.IsInitOnly)
                    continue;

                if (GetBindAttribute(field) == null)
                    continue;

                fields.Add(field);
            }

            cursor = cursor.BaseType;
        }

        cached = fields.ToArray();
        FieldCache[type] = cached;
        return cached;
    }

    private static void BindField(object target, Transform root, FieldInfo field)
    {
        var attribute = GetBindAttribute(field);
        var path = string.IsNullOrWhiteSpace(attribute.Path)
            ? GuessObjectName(field.Name, field.FieldType)
            : attribute.Path.Trim();

        var transform = FindTransform(root, path);
        if (transform == null)
        {
            if (!attribute.Optional)
                throw new Exception($"[UIViewAutoBind] Cannot find UI node. view={target.GetType().Name}, field={field.Name}, path={path}");

            return;
        }

        var value = ResolveValue(transform, field.FieldType);
        if (value == null)
        {
            if (!attribute.Optional)
            {
                throw new Exception(
                    $"[UIViewAutoBind] Cannot bind field type. view={target.GetType().Name}, field={field.Name}, path={path}, type={field.FieldType.Name}");
            }

            return;
        }

        field.SetValue(target, value);
    }

    private static UIViewBindAttribute GetBindAttribute(FieldInfo field)
    {
        foreach (var attribute in field.GetCustomAttributes(false))
        {
            if (attribute is UIViewBindAttribute bindAttribute)
                return bindAttribute;
        }

        return null;
    }

    private static object ResolveValue(Transform transform, Type fieldType)
    {
        if (fieldType == typeof(GameObject))
            return transform.gameObject;
        if (fieldType == typeof(Transform))
            return transform;
        if (fieldType == typeof(RectTransform))
            return transform as RectTransform;

        if (typeof(Component).IsAssignableFrom(fieldType))
            return transform.GetComponent(fieldType);

        return null;
    }

    private static Transform FindTransform(Transform root, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (path == "." || path == root.name)
            return root;

        var direct = root.Find(path);
        if (direct != null)
            return direct;

        var normalized = path.Replace("\\", "/");
        if (normalized != path)
        {
            direct = root.Find(normalized);
            if (direct != null)
                return direct;
        }

        return FindByName(root, path);
    }

    private static Transform FindByName(Transform root, string name)
    {
        var stack = new Stack<Transform>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current.name == name)
                return current;

            for (var i = current.childCount - 1; i >= 0; i--)
                stack.Push(current.GetChild(i));
        }

        return null;
    }

    private static string GuessObjectName(string fieldName, Type fieldType)
    {
        var name = fieldName.TrimStart('_');

        name = StripSuffix(name, fieldType);

        if (name.StartsWith("m_", StringComparison.Ordinal))
            name = name.Substring(2);

        return name;
    }

    private static string StripSuffix(string name, Type fieldType)
    {
        if (fieldType == typeof(TextMeshProUGUI))
            return StripKnownSuffix(name, "TMPText", "Text", "Label");
        if (fieldType == typeof(TMP_InputField))
            return StripKnownSuffix(name, "TMPInput", "InputField", "Input");
        if (fieldType == typeof(Button))
            return StripKnownSuffix(name, "Button", "Btn");
        if (fieldType == typeof(Image))
            return StripKnownSuffix(name, "Image", "Img");
        if (fieldType == typeof(RawImage))
            return StripKnownSuffix(name, "RawImage");
        if (fieldType == typeof(Toggle))
            return StripKnownSuffix(name, "Toggle");
        if (fieldType == typeof(Slider))
            return StripKnownSuffix(name, "Slider");
        if (fieldType == typeof(ScrollRect))
            return StripKnownSuffix(name, "ScrollRect", "Scroll");
        if (fieldType == typeof(CanvasGroup))
            return StripKnownSuffix(name, "CanvasGroup");
        if (fieldType == typeof(RectTransform))
            return StripKnownSuffix(name, "Rect", "RectTransform");
        if (fieldType == typeof(GameObject))
            return StripKnownSuffix(name, "GameObject", "Go");

        return name;
    }

    private static string StripKnownSuffix(string name, params string[] suffixes)
    {
        foreach (var suffix in suffixes)
        {
            if (name.EndsWith(suffix, StringComparison.Ordinal) && name.Length > suffix.Length)
                return name.Substring(0, name.Length - suffix.Length);
        }

        return name;
    }
}
