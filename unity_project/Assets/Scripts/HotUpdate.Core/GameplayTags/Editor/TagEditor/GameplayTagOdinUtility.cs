#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public struct GameplayTagOdinItem
{
    public GameplayTagDomain Domain;
    public string LibraryName;
    public string FieldName;
    public string DisplayName;
    public GameplayTag Tag;
}

/// <summary>
/// Editor dropdown cache. Prefer generated <see cref="GameplayTagCatalog"/> (no full assembly scan).
/// </summary>
public static class GameplayTagOdinUtility
{
    private static List<GameplayTagOdinItem> _items;
    private static Dictionary<ulong, string> _nameByKey;
    private static Dictionary<int, List<ValueDropdownItem<GameplayTag>>> _dropdownByFilter;

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
        _dropdownByFilter = null;
    }

    public static string GetDisplayName(GameplayTag tag)
    {
        if (!tag.IsValid)
        {
            if (tag.IsLegacyMissingDomain)
                return GameplayTagDebug.GetPath(tag);
            return "None";
        }

        EnsureCache();
        ulong key = MakeKey(tag);
        if (_nameByKey.TryGetValue(key, out var name))
            return name;

        return GameplayTagDebug.GetPath(tag);
    }

    public static bool IsBrokenOrLegacy(GameplayTag tag, out string reason)
    {
        reason = null;

        if (!tag.IsValid)
        {
            if (tag.IsLegacyMissingDomain)
            {
                reason = "Legacy: Domain missing. Run Tools/GAS/GameplayTags/Fix Legacy Tags, or click Fix.";
                return true;
            }

            return false;
        }

        EnsureCache();
        if (!_nameByKey.ContainsKey(MakeKey(tag)))
        {
            reason = "Unknown tag (not in GameplayTagCatalog). Generate Databases.";
            return true;
        }

        return false;
    }

    public static List<ValueDropdownItem<GameplayTag>> GetDropdownItems(
        GameplayTagDomain? domainFilter = null)
    {
        EnsureCache();

        int filterKey = domainFilter.HasValue ? (int)domainFilter.Value : -1;
        _dropdownByFilter ??= new Dictionary<int, List<ValueDropdownItem<GameplayTag>>>();

        if (_dropdownByFilter.TryGetValue(filterKey, out var cached))
            return new List<ValueDropdownItem<GameplayTag>>(cached);

        var result = new List<ValueDropdownItem<GameplayTag>>
        {
            new ValueDropdownItem<GameplayTag>("None", GameplayTag.None)
        };

        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (domainFilter.HasValue &&
                domainFilter.Value != GameplayTagDomain.None &&
                item.Domain != domainFilter.Value)
            {
                continue;
            }

            result.Add(new ValueDropdownItem<GameplayTag>(item.DisplayName, item.Tag));
        }

        _dropdownByFilter[filterKey] = result;
        // Return a shallow copy so callers can Insert without corrupting cache.
        return new List<ValueDropdownItem<GameplayTag>>(result);
    }

    public static ulong MakeKey(GameplayTag tag)
    {
        return ((ulong)(byte)tag.Domain << 32) | tag.Value;
    }

    private static void EnsureCache()
    {
        if (_items != null && _nameByKey != null)
            return;

        _items = new List<GameplayTagOdinItem>();
        _nameByKey = new Dictionary<ulong, string>();

        var all = GameplayTagCatalog.All;
        for (int i = 0; i < all.Length; i++)
        {
            ref readonly var e = ref all[i];
            if (e.Domain == GameplayTagDomain.None || e.Mask == 0)
                continue;

            var tag = e.ToTag();
            if (!tag.IsValid)
                continue;

            var item = new GameplayTagOdinItem
            {
                Domain = e.Domain,
                LibraryName = e.Library,
                FieldName = e.FieldName,
                DisplayName = e.DisplayName,
                Tag = tag,
            };

            _items.Add(item);

            ulong key = MakeKey(tag);
            if (!_nameByKey.ContainsKey(key))
                _nameByKey.Add(key, item.DisplayName);
        }

        _items.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
    }
}

#endif
