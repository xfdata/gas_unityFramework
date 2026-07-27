using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "UI/UIView Schema")]
public sealed class UIViewSchema : ScriptableObject
{
    public const int CurrentVersion = 1;

    [Min(1)] public int SchemaVersion = CurrentVersion;
    public UIViewConfig ViewConfig = new();
    public UIViewConfigTable ConfigTable;

    [Header("Generated Assets")]
    public string PrefabPath;
    public string GeneratedFolder;
    public string GeneratedNamespace = "Game.UI.Generated";
    public string BinderClassName;
    public string ViewClassName;
    public string ViewNamespace;
    public string AddressablesGroup = "Prefabs_UI";
    public bool GeneratePartialViewBindings;
    [HideInInspector] public int LastCompilerVersion;
    [HideInInspector] public string LastCompiledHash;


    [Header("Binding Contract")]
    public List<UIViewBindingSchema> Bindings = new();
}

[Serializable]
public sealed class UIViewBindingSchema
{
    [Tooltip("Immutable identity used to reconcile renamed nodes.")]
    public string StableId;

    [Tooltip("Explicit generated/runtime binding key.")]
    public string Key;

    [Tooltip("Path relative to the prefab root.")]
    public string RelativePath;

    public bool Export = true;
    public bool IsSubBinder;
    public string NestedBinderTypeName;
    public List<string> RequiredComponentTypeNames = new();

    public void EnsureStableId()
    {
        if (string.IsNullOrWhiteSpace(StableId))
            StableId = Guid.NewGuid().ToString("N");
    }
}
