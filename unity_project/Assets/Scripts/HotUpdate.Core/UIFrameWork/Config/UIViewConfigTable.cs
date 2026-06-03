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
        _typeMap.Clear();

        foreach (var cfg in Views)
        {
            if (cfg == null)
                continue;

            if (string.IsNullOrEmpty(cfg.ViewTypeName))
                continue;

            var type = Type.GetType(cfg.ViewTypeName);
            if (type == null)
            {
                Debug.LogError($"[UIViewConfigTable] ViewType not found: {cfg.ViewTypeName}");
                continue;
            }

            _typeMap[type] = cfg;
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
