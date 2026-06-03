using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class CSharpUIBindBehaviour : MonoBehaviour
{
    [Header("Export To Parent")] public bool ExportToParent = true;
    public string ParentBindName;

    [Header("Code Generation")] public bool AutoGenerateOnPrefabSave = false;
    public string GeneratedNamespace = "Game.UI.Generated";
    public string GeneratedClassName;
    public string GeneratedFolder;

    [Header("AI Partial View Generation")]
    public bool AutoGenerateViewBindingsOnPrefabSave = false;
    public string GeneratedViewNamespace;
    public string GeneratedViewClassName;

    [SerializeField] private List<UIBindItem> _items = new();

    private Dictionary<string, UIBindItem> _cache;

    [System.NonSerialized] private UIViewBinder _binder;

    public IReadOnlyList<UIBindItem> Items => _items;

    public UIViewBinder Binder
    {
        get
        {
            if (_binder == null)
                _binder = UIViewBinderFactory.Create(GetType(), this);
            return _binder;
        }
    }

    public string BinderClassName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(GeneratedClassName))
                return UIBindNameUtility.ToSafeIdentifier(GeneratedClassName.Trim());

            return UIBindNameUtility.ToSafeIdentifier(UIBindNameUtility.CleanGameObjectName(gameObject.name) +
                                                      "Binder");
        }
    }

    public string BinderFullTypeName
    {
        get
        {
            var ns = string.IsNullOrWhiteSpace(GeneratedNamespace) ? string.Empty : GeneratedNamespace.Trim();
            return string.IsNullOrEmpty(ns) ? BinderClassName : ns + "." + BinderClassName;
        }
    }

    public string ParentKey
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ParentBindName))
                return UIBindNameUtility.ToSafeIdentifier(ParentBindName.Trim());

            var node = GetComponent<UIBindNode>();
            if (node != null && !string.IsNullOrWhiteSpace(node.BindName))
                return node.Key;

            return UIBindNameUtility.ToSafeIdentifier(gameObject.name);
        }
    }

    public UIBindItem GetItem(string key)
    {
        BuildCacheIfNeeded();

        if (_cache.TryGetValue(key, out var item))
            return item;

        throw new Exception(BuildMissingKeyMessage(key));
    }

    public bool TryGetItem(string key, out UIBindItem item)
    {
        BuildCacheIfNeeded();
        return _cache.TryGetValue(key, out item);
    }

    public IEnumerable<string> GetKeys()
    {
        BuildCacheIfNeeded();
        return _cache.Keys;
    }

    private void BuildCacheIfNeeded()
    {
        if (_cache != null)
            return;

        _cache = new Dictionary<string, UIBindItem>();

        foreach (var item in _items)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Key))
                continue;

            if (_cache.ContainsKey(item.Key))
                Debug.LogError($"[CSharpUIBindBehaviour] Duplicate key: {item.Key}, prefab={name}", this);

            _cache[item.Key] = item;
        }
    }

    private string BuildMissingKeyMessage(string key)
    {
        var keys = new List<string>(GetKeys());
        var suggestion = FindClosestKey(key, keys);
        var msg = $"[CSharpUIBindBehaviour] Bind key not found: {key}, prefab={name}";
        if (!string.IsNullOrEmpty(suggestion))
            msg += $". Did you mean: {suggestion}?";
        msg += $"\nAvailable keys: {string.Join(", ", keys)}";
        return msg;
    }

    private static string FindClosestKey(string key, List<string> keys)
    {
        if (string.IsNullOrEmpty(key) || keys == null || keys.Count == 0)
            return null;

        var best = null as string;
        var bestScore = int.MaxValue;

        foreach (var candidate in keys)
        {
            var score = LevenshteinDistance(key, candidate);
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return bestScore <= 3 ? best : null;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var dp = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) dp[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost
                );
            }
        }

        return dp[a.Length, b.Length];
    }

#if UNITY_EDITOR
    private const string LegacyGeneratedFolder = "Assets/Scripts/UI/Generated";

    public string GetGeneratedCodeFolderInEditor()
    {
        var configuredFolder = NormalizeAssetPath(GeneratedFolder);
        if (!string.IsNullOrWhiteSpace(configuredFolder) && !IsLegacyGeneratedFolder(configuredFolder))
            return configuredFolder;

        var viewFolder = TryFindScriptFolder(GetGeneratedViewClassNameInEditor());
        if (!string.IsNullOrWhiteSpace(viewFolder))
            return viewFolder;

        return string.IsNullOrWhiteSpace(configuredFolder) ? LegacyGeneratedFolder : configuredFolder;
    }

    public IEnumerable<string> GetGeneratedCodeFoldersInEditor()
    {
        var emitted = new HashSet<string>();
        var resolvedFolder = GetGeneratedCodeFolderInEditor();

        if (!string.IsNullOrWhiteSpace(resolvedFolder) && emitted.Add(resolvedFolder))
            yield return resolvedFolder;

        var configuredFolder = NormalizeAssetPath(GeneratedFolder);
        if (!string.IsNullOrWhiteSpace(configuredFolder) && emitted.Add(configuredFolder))
            yield return configuredFolder;

        if (emitted.Add(LegacyGeneratedFolder))
            yield return LegacyGeneratedFolder;
    }

    public int ImportBindingsFromGeneratedCodeInEditor(bool recursive = false)
    {
        _items.Clear();
        _binder = null;

        if (recursive)
        {
            foreach (var childBinder in FindDirectChildBinders())
                childBinder.ImportBindingsFromGeneratedCodeInEditor(true);
        }

        var requests = ReadGeneratedBindRequests();
        var keySet = new HashSet<string>();

        foreach (var request in requests)
        {
            var go = FindBindTarget(request.Key);
            if (go == null)
            {
                Debug.LogError(
                    $"[CSharpUIBindBehaviour] Cannot import generated bind key. key={request.Key}, root={name}",
                    this);
                continue;
            }

            var subBinder = go.GetComponent<CSharpUIBindBehaviour>();
            var isSubBinder = request.IsSubBinder || (subBinder != null && subBinder != this);
            var nestedTypeName = request.NestedBinderTypeName;

            if (isSubBinder && string.IsNullOrWhiteSpace(nestedTypeName) && subBinder != null)
                nestedTypeName = subBinder.BinderFullTypeName;

            AddItem(
                keySet,
                CreateBindItem(request.Key, go, isSubBinder, isSubBinder ? subBinder : null, nestedTypeName),
                go);
        }

        CollectDirectChildBinders(keySet);

        _items.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        _cache = null;

        EditorUtility.SetDirty(this);
        PrefabUtility.RecordPrefabInstancePropertyModifications(this);

        Debug.Log($"[CSharpUIBindBehaviour] Imported {_items.Count} bindings from generated code. root={name}", this);
        return _items.Count;
    }

    public void RefreshBindingsInEditor(bool recursive = true)
    {
        _items.Clear();
        _binder = null;

        if (recursive)
        {
            foreach (var childBinder in FindDirectChildBinders())
                childBinder.RefreshBindingsInEditor(true);
        }

        var keySet = new HashSet<string>();

        CollectOwnBindNodes(keySet);
        CollectDirectChildBinders(keySet);

        _items.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        _cache = null;

        EditorUtility.SetDirty(this);
        PrefabUtility.RecordPrefabInstancePropertyModifications(this);
    }

    private void CollectOwnBindNodes(HashSet<string> keySet)
    {
        var nodes = GetComponentsInChildren<UIBindNode>(true);

        foreach (var node in nodes)
        {
            if (node == null || !node.Export)
                continue;

            // 不展开子 Binder 内部节点。
            var ownerBinder = FindNearestBinder(node.transform);
            if (ownerBinder != this)
                continue;

            var key = node.Key;
            var go = node.gameObject;
            var subBinder = go.GetComponent<CSharpUIBindBehaviour>();
            var isSubBinder = node.IsSubBinder || (subBinder != null && subBinder != this);

            var nestedTypeName = node.NestedBinderTypeName;
            if (isSubBinder && string.IsNullOrWhiteSpace(nestedTypeName) && subBinder != null)
                nestedTypeName = subBinder.BinderFullTypeName;

            var item = CreateBindItem(key, go, isSubBinder, isSubBinder ? subBinder : null, nestedTypeName);
            AddItem(keySet, item, node);
        }
    }

    private void CollectDirectChildBinders(HashSet<string> keySet)
    {
        foreach (var childBinder in FindDirectChildBinders())
        {
            if (childBinder == null || !childBinder.ExportToParent)
                continue;

            var node = childBinder.GetComponent<UIBindNode>();
            if (node != null && !node.Export)
                continue;

            var key = childBinder.ParentKey;
            var nestedTypeName = node != null && !string.IsNullOrWhiteSpace(node.NestedBinderTypeName)
                ? node.NestedBinderTypeName.Trim()
                : childBinder.BinderFullTypeName;

            var item = CreateBindItem(key, childBinder.gameObject, true, childBinder, nestedTypeName);
            AddItem(keySet, item, childBinder);
        }
    }

    private UIBindItem CreateBindItem(
        string key,
        GameObject go,
        bool isSubBinder,
        CSharpUIBindBehaviour subBinder,
        string nestedBinderTypeName)
    {
        var item = new UIBindItem
        {
            Key = key,
            Path = UIBindNameUtility.GetRelativePath(transform, go.transform),
            IsSubBinder = isSubBinder,
            GameObject = go,
            SubBinder = subBinder,
            NestedBinderTypeName = nestedBinderTypeName,
        };

        CollectComponents(go, item);
        return item;
    }

    private void AddItem(HashSet<string> keySet, UIBindItem item, UnityEngine.Object context)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Key))
            return;

        if (!keySet.Add(item.Key))
        {
            Debug.LogError($"[CSharpUIBindBehaviour] Duplicate bind key: {item.Key}, root={name}", context);
            return;
        }

        _items.Add(item);
    }

    private List<GeneratedBindRequest> ReadGeneratedBindRequests()
    {
        var requests = new List<GeneratedBindRequest>();
        var keySet = new HashSet<string>();

        foreach (var path in GetGeneratedCodePaths())
        {
            if (!File.Exists(path))
                continue;

            var code = File.ReadAllText(path);
            CollectGeneratedBindRequests(code, requests, keySet);
        }

        return requests;
    }

    private IEnumerable<string> GetGeneratedCodePaths()
    {
        var emitted = new HashSet<string>();
        foreach (var folder in GetGeneratedCodeFoldersInEditor())
        {
            var binderClassName = BinderClassName;
            foreach (var path in GetGeneratedCodePathCandidates(folder, binderClassName, emitted))
                yield return path;

            var viewClassName = GetGeneratedViewClassNameInEditor();
            foreach (var path in GetGeneratedCodePathCandidates(folder, viewClassName, emitted))
                yield return path;
        }
    }

    private static IEnumerable<string> GetGeneratedCodePathCandidates(
        string folder,
        string className,
        HashSet<string> emitted)
    {
        if (string.IsNullOrWhiteSpace(className))
            yield break;

        var path = Path.Combine(folder, className + ".g.cs");
        if (emitted.Add(path))
            yield return path;

        if (!className.EndsWith("Binder", StringComparison.Ordinal))
        {
            path = Path.Combine(folder, className + "Binder.g.cs");
            if (emitted.Add(path))
                yield return path;
        }
    }

    private string GetGeneratedViewClassNameInEditor()
    {
        var raw = GeneratedViewClassName;

        if (string.IsNullOrWhiteSpace(raw))
            raw = GeneratedClassName;

        if (string.IsNullOrWhiteSpace(raw))
            raw = UIBindNameUtility.CleanGameObjectName(gameObject.name);

        raw = raw.Trim();
        if (raw.EndsWith("Binder", StringComparison.Ordinal) && raw.Length > "Binder".Length)
            raw = raw.Substring(0, raw.Length - "Binder".Length);

        return UIBindNameUtility.ToSafeIdentifier(raw);
    }

    private static string TryFindScriptFolder(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
            return string.Empty;

        var fallback = string.Empty;
        var guids = AssetDatabase.FindAssets($"{className} t:MonoScript");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(path) || path.EndsWith(".g.cs", StringComparison.Ordinal))
                continue;

            var fileName = Path.GetFileNameWithoutExtension(path);
            if (fileName != className)
                continue;

            if (string.IsNullOrEmpty(fallback))
                fallback = NormalizeAssetPath(Path.GetDirectoryName(path));

            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            var scriptClass = script != null ? script.GetClass() : null;
            if (scriptClass != null && scriptClass.Name == className)
                return NormalizeAssetPath(Path.GetDirectoryName(path));
        }

        return fallback;
    }

    private static bool IsLegacyGeneratedFolder(string folder)
    {
        return string.Equals(
            NormalizeAssetPath(folder),
            LegacyGeneratedFolder,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeAssetPath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Trim().Replace("\\", "/");
    }

    private static void CollectGeneratedBindRequests(
        string code,
        List<GeneratedBindRequest> requests,
        HashSet<string> keySet)
    {
        if (string.IsNullOrWhiteSpace(code))
            return;

        CollectGetBinderRequests(code, requests, keySet);
        CollectGetterRequests(code, requests, keySet);
    }

    private static void CollectGetBinderRequests(
        string code,
        List<GeneratedBindRequest> requests,
        HashSet<string> keySet)
    {
        var regex = new Regex(
            @"(?:\bbinder\.|\b)GetBinder(?:<(?<type>[^>]+)>)?\s*\(\s*""(?<key>(?:\\.|[^""\\])*)""\s*\)",
            RegexOptions.Compiled);

        foreach (Match match in regex.Matches(code))
        {
            AddGeneratedBindRequest(requests, keySet, new GeneratedBindRequest
            {
                Key = UnescapeStringLiteral(match.Groups["key"].Value),
                IsSubBinder = true,
                NestedBinderTypeName = match.Groups["type"].Success
                    ? CleanGeneratedTypeName(match.Groups["type"].Value)
                    : string.Empty,
            });
        }
    }

    private static void CollectGetterRequests(
        string code,
        List<GeneratedBindRequest> requests,
        HashSet<string> keySet)
    {
        var regex = new Regex(
            @"(?:\bbinder\.)?(?<method>Get|Btn|Txt|Img|Scroll)(?:<(?<type>[^>]+)>)?\s*\(\s*""(?<key>(?:\\.|[^""\\])*)""\s*\)",
            RegexOptions.Compiled);

        foreach (Match match in regex.Matches(code))
        {
            AddGeneratedBindRequest(requests, keySet, new GeneratedBindRequest
            {
                Key = UnescapeStringLiteral(match.Groups["key"].Value),
            });
        }
    }

    private static void AddGeneratedBindRequest(
        List<GeneratedBindRequest> requests,
        HashSet<string> keySet,
        GeneratedBindRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
            return;

        request.Key = request.Key.Trim();
        if (!request.Key.Contains("/") && !request.Key.Contains("\\"))
            request.Key = UIBindNameUtility.ToSafeIdentifier(request.Key);

        if (!keySet.Add(request.Key))
        {
            if (!request.IsSubBinder)
                return;

            for (var i = 0; i < requests.Count; i++)
            {
                if (requests[i].Key != request.Key)
                    continue;

                var existing = requests[i];
                existing.IsSubBinder = true;
                if (string.IsNullOrWhiteSpace(existing.NestedBinderTypeName))
                    existing.NestedBinderTypeName = request.NestedBinderTypeName;
                requests[i] = existing;
                return;
            }
        }

        requests.Add(request);
    }

    private GameObject FindBindTarget(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var direct = transform.Find(key);
        if (direct != null)
            return direct.gameObject;

        var normalized = key.Replace("\\", "/");
        if (normalized != key)
        {
            direct = transform.Find(normalized);
            if (direct != null)
                return direct.gameObject;
        }

        var allTransforms = GetComponentsInChildren<Transform>(true);
        foreach (var child in allTransforms)
        {
            if (child == null)
                continue;

            var ownerBinder = FindNearestBinder(child);
            if (ownerBinder != this && child != transform)
                continue;

            var node = child.GetComponent<UIBindNode>();
            if (node != null && node.Key == key)
                return child.gameObject;

            if (child.name == key || UIBindNameUtility.ToSafeIdentifier(child.name) == key)
                return child.gameObject;
        }

        return null;
    }

    private static string CleanGeneratedTypeName(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return string.Empty;

        typeName = typeName.Trim();
        return typeName.StartsWith("global::", StringComparison.Ordinal)
            ? typeName.Substring("global::".Length)
            : typeName;
    }

    private static string UnescapeStringLiteral(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("\\\"", "\"")
            .Replace("\\\\", "\\");
    }

    private struct GeneratedBindRequest
    {
        public string Key;
        public bool IsSubBinder;
        public string NestedBinderTypeName;
    }

    private List<CSharpUIBindBehaviour> FindDirectChildBinders()
    {
        var result = new List<CSharpUIBindBehaviour>();
        var binders = GetComponentsInChildren<CSharpUIBindBehaviour>(true);

        foreach (var binder in binders)
        {
            if (binder == null || binder == this)
                continue;

            if (IsDirectChildBinder(binder))
                result.Add(binder);
        }

        return result;
    }

    private bool IsDirectChildBinder(CSharpUIBindBehaviour child)
    {
        var t = child.transform.parent;
        while (t != null)
        {
            var binder = t.GetComponent<CSharpUIBindBehaviour>();
            if (binder != null)
                return binder == this;

            t = t.parent;
        }

        return false;
    }

    private static CSharpUIBindBehaviour FindNearestBinder(Transform t)
    {
        while (t != null)
        {
            var binder = t.GetComponent<CSharpUIBindBehaviour>();
            if (binder != null)
                return binder;

            t = t.parent;
        }

        return null;
    }

    private static void AddComponentIfExists<T>(GameObject go, UIBindItem item, string alias) where T : Component
    {
        var component = go.GetComponent<T>();
        if (component == null)
            return;

        item.Components.Add(new UIBindComponentRef
        {
            Alias = alias,
            TypeName = typeof(T).FullName,
            Component = component,
        });
    }

    private static void CollectComponents(GameObject go, UIBindItem item)
    {
        AddComponentIfExists<RectTransform>(go, item, "RectTransform");
        AddComponentIfExists<CanvasGroup>(go, item, "CanvasGroup");
        AddComponentIfExists<Button>(go, item, "Button");
        AddComponentIfExists<Image>(go, item, "Image");
        AddComponentIfExists<RawImage>(go, item, "RawImage");
        AddComponentIfExists<Text>(go, item, "Text");
        AddComponentIfExists<TextMeshProUGUI>(go, item, "TMPText");
        AddComponentIfExists<Toggle>(go, item, "Toggle");
        AddComponentIfExists<ToggleGroup>(go, item, "ToggleGroup");
        AddComponentIfExists<Slider>(go, item, "Slider");
        AddComponentIfExists<Scrollbar>(go, item, "Scrollbar");
        AddComponentIfExists<ScrollRect>(go, item, "ScrollRect");
        AddComponentIfExists<Dropdown>(go, item, "Dropdown");
        AddComponentIfExists<TMP_Dropdown>(go, item, "TMPDropdown");
        AddComponentIfExists<InputField>(go, item, "InputField");
        AddComponentIfExists<TMP_InputField>(go, item, "TMPInputField");
        AddComponentIfExists<Animator>(go, item, "Animator");
        AddComponentIfExists<Animation>(go, item, "Animation");

        item.MainAlias = GuessMainAlias(item);
    }

    private static string GuessMainAlias(UIBindItem item)
    {
        string[] priority =
        {
            "Button",
            "Toggle",
            "Slider",
            "ScrollRect",
            "TMPInputField",
            "InputField",
            "TMPDropdown",
            "Dropdown",
            "TMPText",
            "Text",
            "Image",
            "RawImage",
            "Animator",
            "CanvasGroup",
            "RectTransform"
        };

        foreach (var alias in priority)
        {
            foreach (var c in item.Components)
            {
                if (c.Alias == alias)
                    return alias;
            }
        }

        return string.Empty;
    }
#endif
}
