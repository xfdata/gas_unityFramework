using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "UI/UIView Config Table")]
public sealed class UIViewConfigTable : ScriptableObject
{
    public List<UIViewConfig> Views = new();

    private readonly Dictionary<Type, UIViewConfig> _typeMap = new();
    private bool _built;

    public void BuildIndex()
    {
        _built = false;
        _typeMap.Clear();

        foreach (var cfg in Views)
        {
            if (cfg == null)
                continue;

            if (string.IsNullOrWhiteSpace(cfg.ViewTypeName))
                throw new InvalidOperationException("[UIViewConfigTable] ViewTypeName cannot be empty.");

            var type = Type.GetType(cfg.ViewTypeName);
            if (type == null)
                throw new InvalidOperationException($"[UIViewConfigTable] ViewType not found: {cfg.ViewTypeName}");

            if (!typeof(ViewBase).IsAssignableFrom(type))
                throw new InvalidOperationException($"[UIViewConfigTable] ViewType must inherit ViewBase: {type.FullName}");

            if (cfg.PrefabReference == null || string.IsNullOrWhiteSpace(cfg.PrefabReference.AssetGUID))
                throw new InvalidOperationException($"[UIViewConfigTable] PrefabReference is missing: {type.FullName}");

            if (!_typeMap.TryAdd(type, cfg))
                throw new InvalidOperationException($"[UIViewConfigTable] Duplicate View config: {type.FullName}");
        }

        _built = true;
    }

    public UIViewConfig Get(Type viewType)
    {
        if (!_built)
            BuildIndex();

        if (_typeMap.TryGetValue(viewType, out var cfg))
            return cfg;

        throw new Exception($"[UIViewConfigTable] Config not found: {viewType.FullName}");
    }
}

public static class UIViewRegistry
{
    private static UIViewConfigTable _table;

    public static void Initialize(UIViewConfigTable table)
    {
        _table = table ?? throw new ArgumentNullException(nameof(table));
        _table.BuildIndex();
    }

    public static UIViewConfig Get(Type viewType)
    {
        if (_table == null)
            throw new Exception("[UIViewRegistry] Not initialized.");

        return _table.Get(viewType);
    }

    public static UIViewConfig Get<TView>() where TView : ViewBase
    {
        return Get(typeof(TView));
    }
}
