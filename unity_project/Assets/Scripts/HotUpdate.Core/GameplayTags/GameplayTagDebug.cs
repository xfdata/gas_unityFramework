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
    private static Dictionary<string, GameplayTag> tagByPath;
    private static bool initialized;

    public static void ClearCache()
    {
        pathByKey = null;
        pathByValueMask = null;
        tagByPath = null;
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
                ulong vm = GameplayTagEncoding.MakeValueMaskKey(tag.Value, tag.Mask);
                if (pathByValueMask != null && pathByValueMask.TryGetValue(vm, out var legacyPath))
                    return "Legacy/" + legacyPath;
                return FormatRaw("Legacy", tag);
            }

            return "None";
        }

        Ensure();

        ulong key = GameplayTagEncoding.MakeDomainValueKey(tag.Domain, tag.Value);
        if (pathByKey != null && pathByKey.TryGetValue(key, out var path))
            return path;

        return FormatRaw("Unknown", tag);
    }

    public static bool TryGetPath(GameplayTag tag, out string path)
    {
        path = null;
        if (!tag.IsValid)
            return false;

        Ensure();
        ulong key = GameplayTagEncoding.MakeDomainValueKey(tag.Domain, tag.Value);
        return pathByKey != null && pathByKey.TryGetValue(key, out path);
    }

    public static bool TryFindByPath(GameplayTagDomain domain, string path, out GameplayTag tag)
    {
        tag = GameplayTag.None;
        if (string.IsNullOrEmpty(path) || domain == GameplayTagDomain.None)
            return false;

        Ensure();
        string key = MakePathKey(domain, path);
        return tagByPath != null && tagByPath.TryGetValue(key, out tag);
    }

    private static void Ensure()
    {
        if (initialized)
            return;

        initialized = true;
        var all = GameplayTagCatalog.All;
        int cap = all.Length;
        pathByKey = new Dictionary<ulong, string>(cap);
        pathByValueMask = new Dictionary<ulong, string>(cap);
        tagByPath = new Dictionary<string, GameplayTag>(cap);

        for (int i = 0; i < all.Length; i++)
        {
            ref readonly var e = ref all[i];
            string display = e.Domain + "/" + e.Path;

            ulong dv = GameplayTagEncoding.MakeDomainValueKey(e.Domain, e.Value);
            if (!pathByKey.ContainsKey(dv))
                pathByKey.Add(dv, display);

            ulong vm = GameplayTagEncoding.MakeValueMaskKey(e.Value, e.Mask);
            if (!pathByValueMask.ContainsKey(vm))
                pathByValueMask.Add(vm, e.Path);

            string pk = MakePathKey(e.Domain, e.Path);
            if (!tagByPath.ContainsKey(pk))
                tagByPath.Add(pk, e.ToTag());
        }
    }

    private static string MakePathKey(GameplayTagDomain domain, string path)
    {
        return $"{(byte)domain}:{path}";
    }

    private static string FormatRaw(string prefix, GameplayTag tag)
    {
        return $"{prefix}/{tag.Domain}:0x{tag.Value:X8}/0x{tag.Mask:X8}";
    }
}
