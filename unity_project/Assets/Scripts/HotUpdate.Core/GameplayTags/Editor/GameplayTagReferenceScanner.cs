#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Scans project assets for serialized GameplayTag values that would be affected by recycling a sibling id.
/// </summary>
public static class GameplayTagReferenceScanner
{
    public readonly struct Hit
    {
        public readonly string AssetPath;
        public readonly string ObjectName;
        public readonly string PropertyPath;
        public readonly GameplayTag Tag;

        public Hit(string assetPath, string objectName, string propertyPath, GameplayTag tag)
        {
            AssetPath = assetPath;
            ObjectName = objectName;
            PropertyPath = propertyPath;
            Tag = tag;
        }

        public override string ToString()
        {
            return $"{AssetPath} :: {ObjectName}.{PropertyPath} ({GameplayTagDebug.GetPath(Tag)})";
        }
    }

    /// <summary>
    /// Builds the exact GameplayTag identity that a retired sibling slot used to encode
    /// (Domain + value/mask for that path segment chain).
    /// </summary>
    public static bool TryBuildTagForRetiredSlot(
        GameplayTagDatabase db,
        string parentPath,
        int siblingId,
        string lastPath,
        out GameplayTag tag,
        out string error)
    {
        tag = GameplayTag.None;
        error = null;

        if (db == null)
        {
            error = "Database is null";
            return false;
        }

        db.EnsureMigrated();
        parentPath = parentPath ?? string.Empty;

        string path = lastPath;
        if (string.IsNullOrEmpty(path) || path == "(hole)")
        {
            path = string.IsNullOrEmpty(parentPath)
                ? $"_Retired{siblingId}"
                : parentPath + "._Retired" + siblingId;
            // Hole: encode using parent chain + siblingId as next depth only.
            if (!TryEncodeWithOverride(db, parentPath, siblingId, out uint value, out uint mask, out error))
                return false;

            tag = new GameplayTag(db.Domain, value, mask);
            return true;
        }

        if (!TryEncodePath(db, path, siblingIdOverrideLeaf: siblingId, out uint v, out uint m, out error))
            return false;

        tag = new GameplayTag(db.Domain, v, m);
        return true;
    }

    public static List<Hit> FindReferences(
        GameplayTagDomain domain,
        uint value,
        uint mask,
        bool matchExactMask = false,
        int maxHits = 64)
    {
        var hits = new List<Hit>();
        var paths = CollectAssetPaths();

        try
        {
            for (int i = 0; i < paths.Count; i++)
            {
                if (hits.Count >= maxHits)
                    break;

                string path = paths[i];
                if (EditorUtility.DisplayCancelableProgressBar(
                        "Scan GameplayTag References",
                        path,
                        (float)i / Mathf.Max(1, paths.Count)))
                {
                    break;
                }

                UnityEngine.Object[] assets;
                try
                {
                    assets = AssetDatabase.LoadAllAssetsAtPath(path);
                }
                catch
                {
                    continue;
                }

                if (assets == null)
                    continue;

                for (int a = 0; a < assets.Length; a++)
                {
                    var obj = assets[a];
                    if (obj == null)
                        continue;
                    if (!(obj is ScriptableObject) && !(obj is Component))
                        continue;

                    var so = new SerializedObject(obj);
                    var it = so.GetIterator();
                    bool enter = true;
                    while (it.NextVisible(enter))
                    {
                        enter = true;
                        if (it.propertyType != SerializedPropertyType.Generic || it.type != "GameplayTag")
                            continue;

                        enter = false;
                        var domainProp = it.FindPropertyRelative("domain");
                        var valueProp = it.FindPropertyRelative("value");
                        var maskProp = it.FindPropertyRelative("mask");
                        if (domainProp == null || valueProp == null || maskProp == null)
                            continue;

                        var d = (GameplayTagDomain)domainProp.intValue;
                        uint v = unchecked((uint)valueProp.intValue);
                        uint m = unchecked((uint)maskProp.intValue);

                        if (d != domain)
                            continue;
                        if (v != value)
                            continue;
                        if (matchExactMask && m != mask)
                            continue;

                        // Hierarchy-safe: any serialized tag whose exact value equals recycled encoding.
                        hits.Add(new Hit(path, obj.name, it.propertyPath, new GameplayTag(d, v, m)));
                        if (hits.Count >= maxHits)
                            break;
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        return hits;
    }

    public static List<Hit> FindReferencesForRetiredSlots(
        GameplayTagDatabase db,
        IReadOnlyList<(string parentPath, int siblingId, string lastPath)> slots,
        int maxHitsPerSlot = 32)
    {
        var all = new List<Hit>();
        if (db == null || slots == null)
            return all;

        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (!TryBuildTagForRetiredSlot(db, s.parentPath, s.siblingId, s.lastPath, out var tag, out _))
                continue;

            var hits = FindReferences(tag.Domain, tag.Value, tag.Mask, matchExactMask: false, maxHits: maxHitsPerSlot);
            all.AddRange(hits);
        }

        return all;
    }

    public static string FormatHits(IReadOnlyList<Hit> hits, int maxLines = 20)
    {
        if (hits == null || hits.Count == 0)
            return "（无引用）";

        var sb = new StringBuilder();
        int n = Math.Min(hits.Count, maxLines);
        for (int i = 0; i < n; i++)
            sb.AppendLine(hits[i].ToString());
        if (hits.Count > maxLines)
            sb.AppendLine($"... 另有 {hits.Count - maxLines} 处");
        return sb.ToString();
    }

    private static bool TryEncodePath(
        GameplayTagDatabase db,
        string path,
        int siblingIdOverrideLeaf,
        out uint value,
        out uint mask,
        out string error)
    {
        value = 0;
        mask = 0;
        error = null;

        var parts = path.Split('.');
        if (parts.Length < 1 || parts.Length > GameplayTagDatabase.MaxDepth)
        {
            error = "非法路径深度";
            return false;
        }

        int shift = 24;
        string full = "";
        for (int i = 0; i < parts.Length; i++)
        {
            full = i == 0 ? parts[i] : full + "." + parts[i];
            int id;
            if (i == parts.Length - 1 && siblingIdOverrideLeaf > 0)
            {
                id = siblingIdOverrideLeaf;
            }
            else if (!db.TryGetSiblingId(full, out id))
            {
                // Ancestor missing: cannot encode reliably.
                if (i == parts.Length - 1)
                {
                    id = siblingIdOverrideLeaf;
                }
                else
                {
                    error = $"缺少祖先 siblingId: {full}";
                    return false;
                }
            }

            if (id < 1 || id > GameplayTagDatabase.MaxSiblingId)
            {
                error = $"非法 siblingId: {full}={id}";
                return false;
            }

            value |= ((uint)id & 0xFFu) << shift;
            mask |= 0xFFu << shift;
            shift -= 8;
        }

        return true;
    }

    private static bool TryEncodeWithOverride(
        GameplayTagDatabase db,
        string parentPath,
        int siblingId,
        out uint value,
        out uint mask,
        out string error)
    {
        value = 0;
        mask = 0;
        error = null;

        if (siblingId < 1 || siblingId > GameplayTagDatabase.MaxSiblingId)
        {
            error = "非法 siblingId";
            return false;
        }

        int shift = 24;
        if (!string.IsNullOrEmpty(parentPath))
        {
            var parts = parentPath.Split('.');
            if (parts.Length >= GameplayTagDatabase.MaxDepth)
            {
                error = "父路径已达最大深度，无法再挂 sibling";
                return false;
            }

            string full = "";
            for (int i = 0; i < parts.Length; i++)
            {
                full = i == 0 ? parts[i] : full + "." + parts[i];
                if (!db.TryGetSiblingId(full, out int id))
                {
                    error = $"缺少父节点 siblingId: {full}";
                    return false;
                }

                value |= ((uint)id & 0xFFu) << shift;
                mask |= 0xFFu << shift;
                shift -= 8;
            }
        }

        value |= ((uint)siblingId & 0xFFu) << shift;
        mask |= 0xFFu << shift;
        return true;
    }

    private static List<string> CollectAssetPaths()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string filter)
        {
            string[] guids = AssetDatabase.FindAssets(filter);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    continue;
                set.Add(path);
            }
        }

        Add("t:ScriptableObject");
        Add("t:Prefab");

        var list = new List<string>(set);
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }
}
#endif
