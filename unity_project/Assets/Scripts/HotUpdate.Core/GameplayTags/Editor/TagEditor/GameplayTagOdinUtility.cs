#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Reflection;
using Sirenix.OdinInspector;
using UnityEngine;

public struct GameplayTagOdinItem
{
    public string LibraryName;
    public string FieldName;
    public string DisplayName;
    public GameplayTag Tag;
}

public static class GameplayTagOdinUtility
{
    private static List<GameplayTagOdinItem> _items;
    private static Dictionary<ulong, string> _nameByKey;

    public static IReadOnlyList<GameplayTagOdinItem> Items
    {
        get
        {
            EnsureCache();
            return _items;
        }
    }

    public static void ClearCache()
    {
        _items = null;
        _nameByKey = null;
    }

    public static string GetDisplayName(GameplayTag tag)
    {
        EnsureCache();

        if (!tag.IsValid)
            return "None";

        ulong key = MakeKey(tag);

        if (_nameByKey.TryGetValue(key, out var name))
            return name;

        return tag.ToString();
    }

    public static List<ValueDropdownItem<GameplayTag>> GetDropdownItems()
    {
        EnsureCache();

        var result = new List<ValueDropdownItem<GameplayTag>>
        {
            new ValueDropdownItem<GameplayTag>("None", GameplayTag.None)
        };

        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            result.Add(new ValueDropdownItem<GameplayTag>(item.DisplayName, item.Tag));
        }

        return result;
    }

    public static ulong MakeKey(GameplayTag tag)
    {
        return ((ulong)tag.Value << 32) | tag.Mask;
    }

    private static void EnsureCache()
    {
        if (_items != null && _nameByKey != null)
            return;

        _items = new List<GameplayTagOdinItem>();
        _nameByKey = new Dictionary<ulong, string>();

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
        {
            Type[] types;

            try
            {
                types = assemblies[assemblyIndex].GetTypes();
            }
            catch
            {
                continue;
            }

            for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
            {
                var type = types[typeIndex];

                if (!IsTagLibraryType(type))
                    continue;

                var fields = type.GetFields(
                    BindingFlags.Public |
                    BindingFlags.Static |
                    BindingFlags.FlattenHierarchy);

                for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
                {
                    var field = fields[fieldIndex];

                    if (field.FieldType != typeof(GameplayTag))
                        continue;

                    GameplayTag tag;

                    try
                    {
                        tag = (GameplayTag)field.GetValue(null);
                    }
                    catch
                    {
                        continue;
                    }

                    if (!tag.IsValid)
                        continue;

                    var item = new GameplayTagOdinItem
                    {
                        LibraryName = type.Name,
                        FieldName = field.Name,
                        DisplayName = $"{type.Name}/{field.Name}",
                        Tag = tag,
                    };

                    _items.Add(item);

                    ulong key = MakeKey(tag);

                    if (!_nameByKey.ContainsKey(key))
                        _nameByKey.Add(key, item.DisplayName);
                }
            }
        }

        _items.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
    }

    private static bool IsTagLibraryType(Type type)
    {
        if (type == null)
            return false;

        if (!type.IsAbstract || !type.IsSealed)
            return false;

        if (!type.Name.EndsWith("Tags", StringComparison.Ordinal))
            return false;

        return HasGameplayTagField(type);
    }

    private static bool HasGameplayTagField(Type type)
    {
        var fields = type.GetFields(
            BindingFlags.Public |
            BindingFlags.Static |
            BindingFlags.FlattenHierarchy);

        for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i].FieldType == typeof(GameplayTag))
                return true;
        }

        return false;
    }
}

#endif