#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
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

    public static bool TryBuildTagForRetiredSlot(
        GameplayTagDatabase db,
        GameplayTagSiblingSlotInfo slot,
        out GameplayTag tag,
        out string error)
    {
        tag = GameplayTag.None;
        error = null;

        if (slot.HasEncodedTag)
        {
            tag = new GameplayTag(db.Domain, slot.EncodedValue, slot.EncodedMask);
            return tag.IsValid;
        }

        return TryBuildTagForRetiredSlot(
            db,
            slot.ParentPath,
            slot.SiblingId,
            slot.LastPath,
            out tag,
            out error);
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


                if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    ScanSceneReferences(path, domain, value, mask, matchExactMask, maxHits, hits);
                    continue;
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
                        if (matchExactMask ? v != value : (v & mask) != value)
                            continue;
                        if (matchExactMask && m != mask)
                            continue;

                        // Reusing a parent id also changes the meaning of every serialized descendant.
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

    public static bool TryFindReferencesForRetiredSlots(
        GameplayTagDatabase db,
        IReadOnlyList<GameplayTagSiblingSlotInfo> slots,
        out List<Hit> hits,
        out string error,
        int maxHitsPerSlot = 32)
    {
        hits = new List<Hit>();
        error = null;

        if (db == null)
        {
            error = "GameplayTagDatabase is null.";
            return false;
        }

        if (slots == null)
            return true;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (!TryBuildTagForRetiredSlot(db, slot, out var tag, out var buildError))
            {
                error = $"Cannot safely validate recycled slot {slot.DisplayParent}/{slot.SiblingId}: {buildError}";
                return false;
            }

            var slotHits = FindReferences(
                tag.Domain,
                tag.Value,
                tag.Mask,
                matchExactMask: false,
                maxHits: maxHitsPerSlot);

            hits.AddRange(slotHits);
        }

        return true;
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

            GameplayTagEncoding.EncodeSibling(ref value, ref mask, id, i + 1);
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

        int depth = 1;
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

                GameplayTagEncoding.EncodeSibling(ref value, ref mask, id, depth);
                depth++;
            }
        }

        GameplayTagEncoding.EncodeSibling(ref value, ref mask, siblingId, depth);
        return true;
    }


    private static void ScanSerializedObject(UnityEngine.Object obj, string path, GameplayTagDomain domain, uint value, uint mask, bool matchExactMask, int maxHits, List<Hit> hits)
    {
        var so = new SerializedObject(obj);
        var it = so.GetIterator();
        bool enter = true;

        while (it.NextVisible(enter) && hits.Count < maxHits)
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

            var serializedDomain = (GameplayTagDomain)domainProp.intValue;
            uint serializedValue = unchecked((uint)valueProp.intValue);
            uint serializedMask = unchecked((uint)maskProp.intValue);

            if (serializedDomain != domain)
                continue;
            if (matchExactMask ? serializedValue != value : (serializedValue & mask) != value)
                continue;
            if (matchExactMask && serializedMask != mask)
                continue;

            hits.Add(new Hit(path, obj.name, it.propertyPath, new GameplayTag(serializedDomain, serializedValue, serializedMask)));
        }
    }

    private static void ScanSceneReferences(string path, GameplayTagDomain domain, uint value, uint mask, bool matchExactMask, int maxHits, List<Hit> hits)
    {
        var scene = SceneManager.GetSceneByPath(path);
        bool closeScene = false;

        try
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                closeScene = true;
            }

            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var components = roots[i].GetComponentsInChildren<Component>(true);
                for (int c = 0; c < components.Length; c++)
                {
                    ScanSerializedObject(components[c], path, domain, value, mask, matchExactMask, maxHits, hits);
                    if (hits.Count >= maxHits)
                        return;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to scan GameplayTag references in scene '{path}': {e.Message}");
        }
        finally
        {
            if (closeScene && scene.IsValid())
                EditorSceneManager.CloseScene(scene, true);
        }
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
        Add("t:Scene");

        var list = new List<string>(set);
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }
}
#endif
