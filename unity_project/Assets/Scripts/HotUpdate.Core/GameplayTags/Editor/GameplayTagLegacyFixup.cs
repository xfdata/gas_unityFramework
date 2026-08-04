#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Repairs serialized GameplayTags that have value/mask but Domain=None (pre-Domain assets).
/// </summary>
public static class GameplayTagLegacyFixup
{
    private struct LookupEntry
    {
        public GameplayTag Tag;
        public string DisplayName;
    }

    private static Dictionary<ulong, List<LookupEntry>> _cachedLookup;
    private static int _cachedLibraryTagCount;

    [MenuItem("Tools/GAS/GameplayTags/Fix Legacy Tags (Missing Domain)")]
    public static void FixLegacyTagsMenu()
    {
        if (!EditorUtility.DisplayDialog(
                "Fix Legacy GameplayTags",
                "扫描项目中 Domain=None 的序列化 Tag，并在可唯一解析时写回 Domain。\n继续？",
                "Fix",
                "Cancel"))
        {
            return;
        }

        var report = RunFixup(dryRun: false);
        EditorUtility.DisplayDialog("Fix Legacy GameplayTags", report, "OK");
    }

    [MenuItem("Tools/GAS/GameplayTags/Scan Legacy Tags (Dry Run)")]
    public static void ScanLegacyTagsMenu()
    {
        var report = RunFixup(dryRun: true);
        EditorUtility.DisplayDialog("Scan Legacy GameplayTags", report, "OK");
    }

    public static void ClearLookupCache()
    {
        _cachedLookup = null;
        _cachedLibraryTagCount = 0;
    }

    public static string RunFixup(bool dryRun)
    {
        ClearLookupCache();
        var lookup = GetValueMaskLookup(out int libraryTagCount);
        if (lookup.Count == 0)
        {
            return "未找到任何有效 GameplayTag 库字段，请先 Generate Code。";
        }

        var assetPaths = CollectAssetPaths();
        int scannedObjects = 0;
        int legacyFound = 0;
        int fixedCount = 0;
        int ambiguousCount = 0;
        int unresolvedCount = 0;

        var ambiguousSamples = new List<string>(8);
        var unresolvedSamples = new List<string>(8);
        var fixedSamples = new List<string>(12);

        try
        {
            for (int i = 0; i < assetPaths.Count; i++)
            {
                string path = assetPaths[i];

                if (EditorUtility.DisplayCancelableProgressBar(
                        dryRun ? "Scan Legacy GameplayTags" : "Fix Legacy GameplayTags",
                        path,
                        (float)i / Mathf.Max(1, assetPaths.Count)))
                {
                    break;
                }

                if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    var sceneStats = ProcessScene(path, dryRun, lookup);
                    scannedObjects += sceneStats.ScannedObjects;
                    legacyFound += sceneStats.LegacyFound;
                    fixedCount += sceneStats.FixedCount;
                    ambiguousCount += sceneStats.AmbiguousCount;
                    unresolvedCount += sceneStats.UnresolvedCount;
                    MergeSamples(fixedSamples, sceneStats.FixedSamples, 12);
                    MergeSamples(ambiguousSamples, sceneStats.AmbiguousSamples, 8);
                    MergeSamples(unresolvedSamples, sceneStats.UnresolvedSamples, 8);
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

                if (assets == null || assets.Length == 0)
                    continue;

                for (int a = 0; a < assets.Length; a++)
                {
                    var obj = assets[a];
                    if (obj == null)
                        continue;

                    // Serialize ScriptableObjects and Components on prefabs.
                    if (!(obj is ScriptableObject) && !(obj is Component))
                        continue;

                    scannedObjects++;
                    var so = new SerializedObject(obj);
                    var iterator = so.GetIterator();
                    bool enterChildren = true;
                    bool assetTouched = false;

                    while (iterator.NextVisible(enterChildren))
                    {
                        enterChildren = true;

                        if (iterator.propertyType != SerializedPropertyType.Generic)
                            continue;

                        if (iterator.type != "GameplayTag")
                            continue;

                        // Do not enter into GameplayTag fields themselves.
                        enterChildren = false;

                        var domainProp = iterator.FindPropertyRelative("domain");
                        var valueProp = iterator.FindPropertyRelative("value");
                        var maskProp = iterator.FindPropertyRelative("mask");
                        if (domainProp == null || valueProp == null || maskProp == null)
                            continue;

                        int domain = domainProp.intValue;
                        int value = valueProp.intValue;
                        int mask = maskProp.intValue;

                        if (domain != (int)GameplayTagDomain.None || mask == 0)
                            continue;

                        legacyFound++;
                        uint uValue = unchecked((uint)value);
                        uint uMask = unchecked((uint)mask);
                        ulong key = MakeValueMaskKey(uValue, uMask);

                        if (!lookup.TryGetValue(key, out var candidates) || candidates.Count == 0)
                        {
                            unresolvedCount++;
                            if (unresolvedSamples.Count < 8)
                            {
                                unresolvedSamples.Add(
                                    $"{path} :: {obj.name}.{iterator.propertyPath}  value=0x{uValue:X8} mask=0x{uMask:X8}");
                            }

                            continue;
                        }

                        if (candidates.Count > 1)
                        {
                            ambiguousCount++;
                            if (ambiguousSamples.Count < 8)
                            {
                                var names = new StringBuilder();
                                for (int c = 0; c < candidates.Count; c++)
                                {
                                    if (c > 0) names.Append(" | ");
                                    names.Append(candidates[c].DisplayName);
                                }

                                ambiguousSamples.Add(
                                    $"{path} :: {obj.name}.{iterator.propertyPath} -> {names}");
                            }

                            continue;
                        }

                        var resolved = candidates[0].Tag;
                        if (!dryRun)
                        {
                            domainProp.intValue = (int)resolved.Domain;
                            // Keep value/mask; only domain was missing.
                            assetTouched = true;
                        }

                        fixedCount++;
                        if (fixedSamples.Count < 12)
                        {
                            fixedSamples.Add(
                                $"{path} :: {obj.name}.{iterator.propertyPath} -> {candidates[0].DisplayName}");
                        }
                    }

                    if (assetTouched)
                    {
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(obj);
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (!dryRun && fixedCount > 0)
            AssetDatabase.SaveAssets();

        GameplayTagOdinUtility.ClearCache();

        var sb = new StringBuilder(1024);
        sb.AppendLine(dryRun ? "【Dry Run】仅扫描，未写入。" : "【Fixup】已写入可自动修复的 Tag。");
        sb.AppendLine($"Tag 库条目: {libraryTagCount}");
        sb.AppendLine($"扫描路径: {assetPaths.Count}");
        sb.AppendLine($"扫描对象: {scannedObjects}");
        sb.AppendLine($"发现 Legacy (Domain=None, Mask!=0): {legacyFound}");
        sb.AppendLine($"可自动修复: {fixedCount}");
        sb.AppendLine($"歧义 (同 value/mask 跨 Domain): {ambiguousCount}");
        sb.AppendLine($"无法解析: {unresolvedCount}");

        if (fixedSamples.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(dryRun ? "将修复示例:" : "已修复示例:");
            for (int i = 0; i < fixedSamples.Count; i++)
                sb.AppendLine("  " + fixedSamples[i]);
        }

        if (ambiguousSamples.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("歧义示例（需手动重选）:");
            for (int i = 0; i < ambiguousSamples.Count; i++)
                sb.AppendLine("  " + ambiguousSamples[i]);
        }

        if (unresolvedSamples.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("无法解析示例:");
            for (int i = 0; i < unresolvedSamples.Count; i++)
                sb.AppendLine("  " + unresolvedSamples[i]);
        }

        Debug.Log(sb.ToString());
        return sb.ToString();
    }

    /// <summary>
    /// Resolve a legacy (domain=None) tag payload to a library tag, if unique.
    /// </summary>
    public static bool TryResolveLegacy(
        uint value,
        uint mask,
        out GameplayTag tag,
        out string displayName,
        out bool ambiguous)
    {
        tag = GameplayTag.None;
        displayName = null;
        ambiguous = false;

        var lookup = GetValueMaskLookup(out _);
        ulong key = MakeValueMaskKey(value, mask);
        if (!lookup.TryGetValue(key, out var list) || list.Count == 0)
            return false;

        if (list.Count > 1)
        {
            ambiguous = true;
            displayName = list[0].DisplayName + " (+" + (list.Count - 1) + " more)";
            return false;
        }

        tag = list[0].Tag;
        displayName = list[0].DisplayName;
        return true;
    }

    private sealed class SceneFixupStats
    {
        public int ScannedObjects;
        public int LegacyFound;
        public int FixedCount;
        public int AmbiguousCount;
        public int UnresolvedCount;
        public readonly List<string> FixedSamples = new List<string>(12);
        public readonly List<string> AmbiguousSamples = new List<string>(8);
        public readonly List<string> UnresolvedSamples = new List<string>(8);
    }

    private static SceneFixupStats ProcessScene(
        string path,
        bool dryRun,
        Dictionary<ulong, List<LookupEntry>> lookup)
    {
        var stats = new SceneFixupStats();
        var scene = SceneManager.GetSceneByPath(path);
        bool openedByFixup = false;
        bool sceneTouched = false;

        try
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                openedByFixup = true;
            }

            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var components = roots[i].GetComponentsInChildren<Component>(true);
                for (int c = 0; c < components.Length; c++)
                {
                    var component = components[c];
                    if (component == null)
                        continue;

                    if (ProcessSceneComponent(component, path, dryRun, lookup, stats))
                        sceneTouched = true;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to scan legacy GameplayTags in scene '{path}': {e.Message}");
        }
        finally
        {
            if (!dryRun && sceneTouched && scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (openedByFixup)
                {
                    if (!EditorSceneManager.SaveScene(scene))
                        Debug.LogError($"Failed to save scene after legacy GameplayTag fixup: {path}");
                }
                else
                {
                    Debug.LogWarning(
                        $"Legacy GameplayTags were modified in loaded scene '{path}'. Save the scene to persist the fix.");
                }
            }

            if (openedByFixup && scene.IsValid())
                EditorSceneManager.CloseScene(scene, true);
        }

        return stats;
    }

    private static bool ProcessSceneComponent(
        Component component,
        string path,
        bool dryRun,
        Dictionary<ulong, List<LookupEntry>> lookup,
        SceneFixupStats stats)
    {
        stats.ScannedObjects++;
        var so = new SerializedObject(component);
        var iterator = so.GetIterator();
        bool enterChildren = true;
        bool touched = false;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = true;
            if (iterator.propertyType != SerializedPropertyType.Generic || iterator.type != "GameplayTag")
                continue;

            enterChildren = false;
            var domainProp = iterator.FindPropertyRelative("domain");
            var valueProp = iterator.FindPropertyRelative("value");
            var maskProp = iterator.FindPropertyRelative("mask");
            if (domainProp == null || valueProp == null || maskProp == null)
                continue;

            int domain = domainProp.intValue;
            int value = valueProp.intValue;
            int mask = maskProp.intValue;
            if (domain != (int)GameplayTagDomain.None || mask == 0)
                continue;

            stats.LegacyFound++;
            uint encodedValue = unchecked((uint)value);
            uint encodedMask = unchecked((uint)mask);
            ulong key = MakeValueMaskKey(encodedValue, encodedMask);

            if (!lookup.TryGetValue(key, out var candidates) || candidates.Count == 0)
            {
                stats.UnresolvedCount++;
                if (stats.UnresolvedSamples.Count < 8)
                {
                    stats.UnresolvedSamples.Add(
                        $"{path} :: {component.name}.{iterator.propertyPath}  value=0x{encodedValue:X8} mask=0x{encodedMask:X8}");
                }

                continue;
            }

            if (candidates.Count > 1)
            {
                stats.AmbiguousCount++;
                if (stats.AmbiguousSamples.Count < 8)
                {
                    var names = new StringBuilder();
                    for (int i = 0; i < candidates.Count; i++)
                    {
                        if (i > 0)
                            names.Append(" | ");
                        names.Append(candidates[i].DisplayName);
                    }

                    stats.AmbiguousSamples.Add(
                        $"{path} :: {component.name}.{iterator.propertyPath} -> {names}");
                }

                continue;
            }

            var resolved = candidates[0].Tag;
            if (!dryRun)
            {
                domainProp.intValue = (int)resolved.Domain;
                touched = true;
            }

            stats.FixedCount++;
            if (stats.FixedSamples.Count < 12)
            {
                stats.FixedSamples.Add(
                    $"{path} :: {component.name}.{iterator.propertyPath} -> {candidates[0].DisplayName}");
            }
        }

        if (touched)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
        }

        return touched;
    }

    private static void MergeSamples(List<string> destination, List<string> source, int maxCount)
    {
        for (int i = 0; i < source.Count && destination.Count < maxCount; i++)
            destination.Add(source[i]);
    }
    private static List<string> CollectAssetPaths()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddGuids(string filter)
        {
            string[] guids = AssetDatabase.FindAssets(filter);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path))
                    continue;

                if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Skip tag database assets themselves (no serialized GameplayTag fields of interest).
                if (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) &&
                    path.IndexOf("GameplayTag", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // still scan — GrantedTags live on ability assets, not only non-editor paths
                }

                set.Add(path);
            }
        }

        AddGuids("t:ScriptableObject");
        AddGuids("t:Prefab");
        AddGuids("t:Scene");

        var list = new List<string>(set);
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }

    private static Dictionary<ulong, List<LookupEntry>> GetValueMaskLookup(out int tagCount)
    {
        if (_cachedLookup != null)
        {
            tagCount = _cachedLibraryTagCount;
            return _cachedLookup;
        }

        _cachedLookup = BuildValueMaskLookup(out _cachedLibraryTagCount);
        tagCount = _cachedLibraryTagCount;
        return _cachedLookup;
    }

    private static Dictionary<ulong, List<LookupEntry>> BuildValueMaskLookup(out int tagCount)
    {
        tagCount = 0;
        var map = new Dictionary<ulong, List<LookupEntry>>();

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int a = 0; a < assemblies.Length; a++)
        {
            Type[] types;
            try
            {
                types = assemblies[a].GetTypes();
            }
            catch
            {
                continue;
            }

            for (int t = 0; t < types.Length; t++)
            {
                var type = types[t];
                if (type == null || !type.IsAbstract || !type.IsSealed)
                    continue;
                if (!type.Name.EndsWith("Tags", StringComparison.Ordinal))
                    continue;

                var fields = type.GetFields(
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

                for (int f = 0; f < fields.Length; f++)
                {
                    var field = fields[f];
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

                    tagCount++;
                    ulong key = MakeValueMaskKey(tag.Value, tag.Mask);
                    if (!map.TryGetValue(key, out var list))
                    {
                        list = new List<LookupEntry>(1);
                        map.Add(key, list);
                    }

                    list.Add(new LookupEntry
                    {
                        Tag = tag,
                        DisplayName = $"{tag.Domain}/{type.Name}/{field.Name}"
                    });
                }
            }
        }

        return map;
    }

    private static ulong MakeValueMaskKey(uint value, uint mask)
    {
        return GameplayTagEncoding.MakeValueMaskKey(value, mask);
    }
}
#endif
