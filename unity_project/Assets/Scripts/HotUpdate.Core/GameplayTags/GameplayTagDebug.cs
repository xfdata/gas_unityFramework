using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Human-readable GameplayTag paths. Backed by generated <see cref="GameplayTagCatalog"/>.
/// Safe in player builds when the catalog is present.
/// </summary>
public static class GameplayTagDebug
{
    private static Dictionary<ulong, string> pathByKey;
    private static Dictionary<ulong, string> pathByValueMask;
    private static bool initialized;

    public static void ClearCache()
    {
        pathByKey = null;
        pathByValueMask = null;
        initialized = false;
    }

    /// <summary>
    /// Returns Domain/Path when known, otherwise a hex fallback.
    /// </summary>
    public static string GetPath(GameplayTag tag)
    {
        if (!tag.IsValid)
        {
            if (tag.IsLegacyMissingDomain)
            {
                Ensure();
                ulong vm = MakeValueMaskKey(tag.Value, tag.Mask);
                if (pathByValueMask != null && pathByValueMask.TryGetValue(vm, out var legacyPath))
                    return "Legacy/" + legacyPath;
                return tag.ToString();
            }

            return "None";
        }

        Ensure();

        ulong key = MakeDomainValueKey(tag.Domain, tag.Value);
        if (pathByKey != null && pathByKey.TryGetValue(key, out var path))
            return path;

        return tag.ToString();
    }

    public static bool TryGetPath(GameplayTag tag, out string path)
    {
        path = null;
        if (!tag.IsValid)
            return false;

        Ensure();
        ulong key = MakeDomainValueKey(tag.Domain, tag.Value);
        return pathByKey != null && pathByKey.TryGetValue(key, out path);
    }

    public static bool TryFindByPath(GameplayTagDomain domain, string path, out GameplayTag tag)
    {
        tag = GameplayTag.None;
        if (string.IsNullOrEmpty(path) || domain == GameplayTagDomain.None)
            return false;

        var all = GameplayTagCatalog.All;
        for (int i = 0; i < all.Length; i++)
        {
            ref readonly var e = ref all[i];
            if (e.Domain == domain && string.Equals(e.Path, path, System.StringComparison.Ordinal))
            {
                tag = e.ToTag();
                return true;
            }
        }

        return false;
    }

    private static void Ensure()
    {
        if (initialized)
            return;

        initialized = true;
        pathByKey = new Dictionary<ulong, string>(256);
        pathByValueMask = new Dictionary<ulong, string>(256);

        var all = GameplayTagCatalog.All;
        for (int i = 0; i < all.Length; i++)
        {
            ref readonly var e = ref all[i];
            string display = e.Domain + "/" + e.Path;

            ulong dv = MakeDomainValueKey(e.Domain, e.Value);
            if (!pathByKey.ContainsKey(dv))
                pathByKey.Add(dv, display);

            ulong vm = MakeValueMaskKey(e.Value, e.Mask);
            if (!pathByValueMask.ContainsKey(vm))
                pathByValueMask.Add(vm, e.Path);
        }
    }

    private static ulong MakeDomainValueKey(GameplayTagDomain domain, uint value)
    {
        return ((ulong)(byte)domain << 32) | value;
    }

    private static ulong MakeValueMaskKey(uint value, uint mask)
    {
        return ((ulong)value << 32) | mask;
    }
}
