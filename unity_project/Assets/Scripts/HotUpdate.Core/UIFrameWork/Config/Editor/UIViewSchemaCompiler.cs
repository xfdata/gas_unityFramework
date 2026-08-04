#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

public enum UIViewValidationSeverity
{
    Info,
    Warning,
    Error,
}

public sealed class UIViewValidationIssue
{
    public UIViewValidationSeverity Severity { get; }
    public string Message { get; }

    public UIViewValidationIssue(UIViewValidationSeverity severity, string message)
    {
        Severity = severity;
        Message = message;
    }

    public override string ToString() => $"[{Severity}] {Message}";
}

public static class UIViewSchemaCompiler
{
    public const int CompilerVersion = 1;

    [MenuItem("Tools/UI Schema/Compile Selected", true)]
    private static bool CanCompileSelected() => Selection.activeObject is UIViewSchema;

    [MenuItem("Tools/UI Schema/Compile Selected")]
    private static void CompileSelected()
    {
        Compile((UIViewSchema)Selection.activeObject);
    }

    [MenuItem("Tools/UI Schema/Validate Selected", true)]
    private static bool CanValidateSelected() => Selection.activeObject is UIViewSchema;

    [MenuItem("Tools/UI Schema/Validate Selected")]
    private static void ValidateSelected()
    {
        LogIssues((UIViewSchema)Selection.activeObject, UIViewFrameworkValidator.Validate((UIViewSchema)Selection.activeObject));
    }

    [MenuItem("Tools/UI Schema/Validate All")]
    private static void ValidateAll()
    {
        var errorCount = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:UIViewSchema"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var schema = AssetDatabase.LoadAssetAtPath<UIViewSchema>(path);
            var issues = UIViewFrameworkValidator.Validate(schema);
            errorCount += issues.Count(issue => issue.Severity == UIViewValidationSeverity.Error);
            LogIssues(schema, issues);
        }

        if (errorCount > 0)
            throw new InvalidOperationException($"UI schema validation failed with {errorCount} error(s).");

        Debug.Log("[UIViewSchemaCompiler] All UI schemas are valid.");
    }

    public static void Compile(UIViewSchema schema)
    {
        if (schema == null)
            throw new ArgumentNullException(nameof(schema));

        if (schema.ViewConfig == null)
            throw new InvalidOperationException($"UI schema '{schema.name}' requires ViewConfig.");

        var viewType = UIViewFrameworkValidator.ResolveType(schema.ViewConfig.ViewTypeName);
        if (viewType != null)
            ApplyDefaults(schema, viewType);

        var basicIssues = UIViewFrameworkValidator.Validate(schema, false);
        ThrowOnErrors(schema, basicIssues);

        var prefabRoot = PrefabUtility.LoadPrefabContents(schema.PrefabPath);
        try
        {
            ValidateBindingTargets(schema, prefabRoot.transform);
            ConfigurePrefab(schema, viewType, prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, schema.PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        var prefabGuid = AssetDatabase.AssetPathToGUID(schema.PrefabPath);
        schema.ViewConfig.PrefabReference = new AssetReferenceGameObject(prefabGuid);
        SynchronizeConfig(schema);
        RegisterAddressable(schema, prefabGuid, viewType.Name);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(schema.PrefabPath);
        var binder = prefab != null ? prefab.GetComponent<CSharpUIBindBehaviour>() : null;
        if (binder == null)
            throw new InvalidOperationException($"Compiled prefab has no CSharpUIBindBehaviour: {schema.PrefabPath}");

        var generatedChanged = CSharpUIBindCodeGenerator.Generate(binder, false);
        if (schema.GeneratePartialViewBindings)
            generatedChanged |= UIViewPartialBindingCodeGenerator.Generate(binder, false);

        schema.LastCompilerVersion = CompilerVersion;
        schema.LastCompiledHash = ComputeSchemaHash(schema);
        EditorUtility.SetDirty(schema);
        EditorUtility.SetDirty(schema.ConfigTable);
        AssetDatabase.SaveAssets();
        UIGeneratedCodeWriter.RefreshIfChanged(generatedChanged);

        var finalIssues = UIViewFrameworkValidator.Validate(schema, true);
        ThrowOnErrors(schema, finalIssues);
        LogIssues(schema, finalIssues);
        Debug.Log($"[UIViewSchemaCompiler] Compiled {viewType.FullName} from {schema.name}.", schema);
    }

    private static void ApplyDefaults(UIViewSchema schema, Type viewType)
    {
        schema.SchemaVersion = UIViewSchema.CurrentVersion;
        schema.ViewConfig.ViewTypeName = viewType.AssemblyQualifiedName;

        if (string.IsNullOrWhiteSpace(schema.ViewClassName))
            schema.ViewClassName = viewType.Name;
        if (string.IsNullOrWhiteSpace(schema.ViewNamespace))
            schema.ViewNamespace = viewType.Namespace ?? string.Empty;
        if (string.IsNullOrWhiteSpace(schema.BinderClassName))
            schema.BinderClassName = viewType.Name + "Binder";
        if (string.IsNullOrWhiteSpace(schema.GeneratedFolder))
            schema.GeneratedFolder = FindViewScriptFolder(viewType) ?? "Assets/Scripts/UI/Generated";
        if (string.IsNullOrWhiteSpace(schema.AddressablesGroup))
            schema.AddressablesGroup = "Prefabs_UI";

        if (string.IsNullOrWhiteSpace(schema.BindingAdapterId))
            schema.BindingAdapterId = UIViewBindingAdapterIds.SchemaGenerated;
        foreach (var binding in schema.Bindings)
            binding?.EnsureStableId();

        EditorUtility.SetDirty(schema);
    }

    private static void ValidateBindingTargets(UIViewSchema schema, Transform root)
    {
        foreach (var binding in schema.Bindings)
        {
            if (binding == null)
                continue;

            var target = FindRelative(root, binding.RelativePath);
            if (target == null)
                throw new InvalidOperationException($"Binding path not found: {binding.RelativePath}");

            foreach (var typeName in binding.RequiredComponentTypeNames)
            {
                var componentType = UIViewFrameworkValidator.ResolveType(typeName);
                if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
                    throw new InvalidOperationException($"Invalid component type '{typeName}' for binding '{binding.Key}'.");
                if (target.GetComponent(componentType) == null)
                    throw new InvalidOperationException($"Missing {componentType.Name} at '{binding.RelativePath}'.");
            }
        }
    }

    private static void ConfigurePrefab(UIViewSchema schema, Type viewType, GameObject root)
    {
        root.name = viewType.Name;
        if (root.GetComponent<Canvas>() == null)
            root.AddComponent<Canvas>();
        if (root.GetComponent<GraphicRaycaster>() == null)
            root.AddComponent<GraphicRaycaster>();

        var adapterSelector = root.GetComponent<UIViewBindingAdapterSelector>();
        if (adapterSelector == null)
            adapterSelector = root.AddComponent<UIViewBindingAdapterSelector>();
        adapterSelector.PreferredAdapterId = schema.BindingAdapterId;

        var binder = root.GetComponent<CSharpUIBindBehaviour>();
        if (binder == null)
            binder = root.AddComponent<CSharpUIBindBehaviour>();
        binder.GeneratedNamespace = schema.GeneratedNamespace;
        binder.GeneratedClassName = schema.BinderClassName;
        binder.GeneratedFolder = schema.GeneratedFolder;
        binder.GeneratedViewNamespace = schema.ViewNamespace;
        binder.GeneratedViewClassName = schema.ViewClassName;
        binder.AutoGenerateOnPrefabSave = false;
        binder.AutoGenerateViewBindingsOnPrefabSave = false;

        foreach (var binding in schema.Bindings)
        {
            if (binding == null)
                continue;

            var target = FindRelative(root.transform, binding.RelativePath);
            var node = target.GetComponent<UIBindNode>();
            if (node == null)
                node = target.gameObject.AddComponent<UIBindNode>();

            node.ApplySchemaInEditor(binding);
        }

        binder.RefreshBindingsInEditor(true);
        EditorUtility.SetDirty(binder);
    }

    private static void SynchronizeConfig(UIViewSchema schema)
    {
        var target = schema.ConfigTable.Views.FirstOrDefault(config =>
            config != null && config.ViewTypeName == schema.ViewConfig.ViewTypeName);

        if (target == null)
        {
            target = new UIViewConfig();
            schema.ConfigTable.Views.Add(target);
        }

        CopyConfig(schema.ViewConfig, target);
        schema.ConfigTable.BuildIndex();
    }

    private static void CopyConfig(UIViewConfig source, UIViewConfig target)
    {
        target.ViewTypeName = source.ViewTypeName;
        target.PrefabReference = source.PrefabReference;
        target.Layer = source.Layer;
        target.CacheMode = source.CacheMode;
        target.FullScreen = source.FullScreen;
        target.EnterPopupStack = source.EnterPopupStack;
        target.PauseLowerView = source.PauseLowerView;
        target.HideLowerView = source.HideLowerView;
        target.BlurMode = source.BlurMode;
        target.MaskMode = source.MaskMode;
        target.CloseByEsc = source.CloseByEsc;
        target.CloseByMask = source.CloseByMask;
        target.CloseWhenSceneChange = source.CloseWhenSceneChange;
        target.SafeAreaMode = source.SafeAreaMode;
        target.SortOffset = source.SortOffset;
    }

    private static void RegisterAddressable(UIViewSchema schema, string prefabGuid, string address)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
            throw new InvalidOperationException("AddressableAssetSettings is not available.");

        var group = settings.FindGroup(schema.AddressablesGroup);
        if (group == null)
            throw new InvalidOperationException($"Addressables group not found: {schema.AddressablesGroup}");

        var entry = settings.CreateOrMoveEntry(prefabGuid, group, false, false);
        entry.address = address;
        EditorUtility.SetDirty(settings);
    }

    private static Transform FindRelative(Transform root, string path)
    {
        if (root == null)
            return null;
        if (string.IsNullOrWhiteSpace(path) || path == ".")
            return root;
        return root.Find(path.Replace("\\", "/"));
    }

    private static string FindViewScriptFolder(Type viewType)
    {
        foreach (var guid in AssetDatabase.FindAssets($"{viewType.Name} t:MonoScript"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (script != null && script.GetClass() == viewType)
                return Path.GetDirectoryName(path)?.Replace("\\", "/");
        }

        return null;
    }

    private static string ComputeSchemaHash(UIViewSchema schema)
    {
        var previousHash = schema.LastCompiledHash;
        var previousVersion = schema.LastCompilerVersion;
        schema.LastCompiledHash = string.Empty;
        schema.LastCompilerVersion = 0;

        string json;
        try
        {
            json = EditorJsonUtility.ToJson(schema, false);
        }
        finally
        {
            schema.LastCompiledHash = previousHash;
            schema.LastCompilerVersion = previousVersion;
        }

        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
        return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static void ThrowOnErrors(UIViewSchema schema, IReadOnlyList<UIViewValidationIssue> issues)
    {
        var errors = issues.Where(issue => issue.Severity == UIViewValidationSeverity.Error).ToArray();
        if (errors.Length == 0)
            return;

        LogIssues(schema, issues);
        throw new InvalidOperationException(
            $"UI schema '{schema.name}' has {errors.Length} validation error(s). Fix them before compiling.");
    }

    private static void LogIssues(UIViewSchema schema, IReadOnlyList<UIViewValidationIssue> issues)
    {
        if (issues.Count == 0)
        {
            Debug.Log($"[UIViewSchema] {schema.name}: valid.", schema);
            return;
        }

        foreach (var issue in issues)
        {
            var message = $"[UIViewSchema] {schema.name}: {issue}";
            switch (issue.Severity)
            {
                case UIViewValidationSeverity.Error:
                    Debug.LogError(message, schema);
                    break;
                case UIViewValidationSeverity.Warning:
                    Debug.LogWarning(message, schema);
                    break;
                default:
                    Debug.Log(message, schema);
                    break;
            }
        }
    }
}

public static class UIViewFrameworkValidator
{
    public static List<UIViewValidationIssue> Validate(UIViewSchema schema, bool requireCompiledArtifacts = true)
    {
        var issues = new List<UIViewValidationIssue>();
        if (schema == null)
        {
            issues.Add(Error("Schema is null."));
            return issues;
        }

        if (schema.SchemaVersion != UIViewSchema.CurrentVersion)
            issues.Add(Error($"Unsupported schema version {schema.SchemaVersion}; expected {UIViewSchema.CurrentVersion}."));
        if (schema.ViewConfig == null)
            issues.Add(Error("ViewConfig is required."));
        if (schema.ConfigTable == null)
            issues.Add(Error("ConfigTable is required."));
        if (string.IsNullOrWhiteSpace(schema.PrefabPath) || !schema.PrefabPath.StartsWith("Assets/", StringComparison.Ordinal))
            issues.Add(Error("PrefabPath must be an Assets-relative .prefab path."));
        else if (!File.Exists(schema.PrefabPath))
            issues.Add(Error($"Prefab does not exist: {schema.PrefabPath}"));

        var viewType = ResolveType(schema.ViewConfig?.ViewTypeName);
        if (viewType == null)
            issues.Add(Error($"View type cannot be resolved: {schema.ViewConfig?.ViewTypeName}"));
        else if (!typeof(ViewBase).IsAssignableFrom(viewType))
            issues.Add(Error($"View type must inherit ViewBase: {viewType.FullName}"));

        ValidateBindings(schema, issues);
        if (!requireCompiledArtifacts || issues.Any(issue => issue.Severity == UIViewValidationSeverity.Error))
            return issues;

        ValidateCompiledPrefab(schema, viewType, issues);
        ValidateConfig(schema, issues);
        ValidateAddressable(schema, issues);
        return issues;
    }

    public static Type ResolveType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return null;

        var type = Type.GetType(typeName, false);
        if (type != null)
            return type;

        var fullName = typeName.Split(',')[0].Trim();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType(fullName, false);
            if (type != null)
                return type;
        }

        return null;
    }

    private static void ValidateBindings(UIViewSchema schema, List<UIViewValidationIssue> issues)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var stableIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in schema.Bindings)
        {
            if (binding == null)
            {
                issues.Add(Error("Binding entry is null."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(binding.Key))
                issues.Add(Error($"Binding at '{binding.RelativePath}' must have an explicit Key."));
            else if (!keys.Add(binding.Key))
                issues.Add(Error($"Duplicate binding Key: {binding.Key}"));

            if (string.IsNullOrWhiteSpace(binding.StableId))
                issues.Add(Error($"Binding '{binding.Key}' has no StableId."));
            else if (!stableIds.Add(binding.StableId))
                issues.Add(Error($"Duplicate binding StableId: {binding.StableId}"));

            foreach (var componentTypeName in binding.RequiredComponentTypeNames)
            {
                var componentType = ResolveType(componentTypeName);
                if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
                    issues.Add(Error($"Binding '{binding.Key}' has invalid component type: {componentTypeName}"));
            }
        }
    }

    private static void ValidateCompiledPrefab(UIViewSchema schema, Type viewType, List<UIViewValidationIssue> issues)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(schema.PrefabPath);
        if (prefab == null)
        {
            issues.Add(Error($"Cannot load prefab: {schema.PrefabPath}"));
            return;
        }

        if (viewType != null && prefab.name != viewType.Name)
            issues.Add(Error($"Prefab root must be named {viewType.Name}, found {prefab.name}."));
        if (prefab.GetComponent<Canvas>() == null)
            issues.Add(Error("Prefab root requires Canvas."));
        if (prefab.GetComponent<GraphicRaycaster>() == null)
            issues.Add(Error("Prefab root requires GraphicRaycaster."));

        var binder = prefab.GetComponent<CSharpUIBindBehaviour>();
        if (binder == null)
        {
            issues.Add(Error("Prefab root requires CSharpUIBindBehaviour."));
            return;
        }

        var nodesById = new Dictionary<string, UIBindNode>(StringComparer.Ordinal);
        foreach (var node in prefab.GetComponentsInChildren<UIBindNode>(true))
        {
            if (node == null || string.IsNullOrWhiteSpace(node.BindingId))
                continue;

            if (nodesById.ContainsKey(node.BindingId))
            {
                issues.Add(Error($"Prefab contains duplicate BindingId: {node.BindingId}."));
                continue;
            }

            nodesById.Add(node.BindingId, node);
        }

        foreach (var binding in schema.Bindings)
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.StableId))
                continue;

            if (!nodesById.TryGetValue(binding.StableId, out var node))
            {
                issues.Add(Error($"Compiled binding node missing: {binding.Key} ({binding.StableId})."));
                continue;
            }

            if (node.Key != UIBindNameUtility.ToSafeIdentifier(binding.Key))
                issues.Add(Error($"Binding key mismatch for {binding.StableId}: schema={binding.Key}, prefab={node.Key}."));
        }
    }

    private static void ValidateConfig(UIViewSchema schema, List<UIViewValidationIssue> issues)
    {
        if (schema.ConfigTable == null || schema.ViewConfig == null)
            return;

        var matches = schema.ConfigTable.Views.Where(config =>
            config != null && config.ViewTypeName == schema.ViewConfig.ViewTypeName).ToArray();
        if (matches.Length != 1)
            issues.Add(Error($"Config table must contain exactly one entry for {schema.ViewConfig.ViewTypeName}; found {matches.Length}."));
    }

    private static void ValidateAddressable(UIViewSchema schema, List<UIViewValidationIssue> issues)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var guid = AssetDatabase.AssetPathToGUID(schema.PrefabPath);
        var entry = settings != null ? settings.FindAssetEntry(guid) : null;
        if (entry == null)
            issues.Add(Error($"Prefab is not Addressable: {schema.PrefabPath}"));
        else if (entry.parentGroup == null || entry.parentGroup.Name != schema.AddressablesGroup)
            issues.Add(Error($"Prefab must be in Addressables group '{schema.AddressablesGroup}'."));
    }

    private static UIViewValidationIssue Error(string message) =>
        new(UIViewValidationSeverity.Error, message);
}
#endif
