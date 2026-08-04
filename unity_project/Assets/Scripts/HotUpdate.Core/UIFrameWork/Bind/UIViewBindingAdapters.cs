using System;
using System.Collections.Generic;
using UnityEngine;

public static class UIViewBindingAdapterIds
{
    public const string SchemaGenerated = "schema-generated";
    public const string LegacyAttributes = "legacy-attributes";
}

public enum UIViewBindingAdapterPhase
{
    Resolve,
    Enhance,
}

public enum UIViewBindingAdapterResult
{
    NotApplicable,
    Applied,
    Stop,
}

/// <summary>
/// Extends UI binding without coupling UIWindow or ViewBase to a prefab/binding implementation.
/// Resolver adapters select one primary binding source; enhancer adapters may add generated or legacy fields.
/// </summary>
public interface IUIViewBindingAdapter
{
    string AdapterId { get; }
    int Priority { get; }
    UIViewBindingAdapterPhase Phase { get; }

    UIViewBindingAdapterResult Bind(UIViewBindingContext context);
}

public sealed class UIViewBindingContext
{
    public ViewBase View { get; }
    public Type ViewType { get; }
    public GameObject Root { get; }
    public Transform RootTransform => Root != null ? Root.transform : null;
    public string PreferredAdapterId { get; }
    public UIViewBinder Binder { get; private set; }

    internal UIViewBindingContext(ViewBase view, Type viewType, GameObject root)
    {
        View = view ?? throw new ArgumentNullException(nameof(view));
        ViewType = viewType ?? view.GetType();
        Root = root ?? throw new ArgumentNullException(nameof(root));
        PreferredAdapterId = root.GetComponent<UIViewBindingAdapterSelector>()?.PreferredAdapterId?.Trim();
    }

    public void SetBinder(UIViewBinder binder)
    {
        if (binder == null)
            throw new ArgumentNullException(nameof(binder));
        if (Binder != null)
            throw new InvalidOperationException(
                $"A UI binder has already been resolved for {ViewType.FullName}. " +
                "Return Stop from the replacing resolver or use a higher-priority adapter.");

        Binder = binder;
        View.SetBinderInternal(binder);
    }
}

[DisallowMultipleComponent]
public sealed class UIViewBindingAdapterSelector : MonoBehaviour
{
    [Tooltip("Optional primary binding adapter id. Empty uses the highest-priority applicable adapter.")]
    public string PreferredAdapterId;
}

public static class UIViewBindingAdapters
{
    private static readonly List<IUIViewBindingAdapter> Adapters = new();
    private static readonly SchemaGeneratedBindingAdapter SchemaGenerated = new();
    private static readonly GeneratedPartialBindingAdapter GeneratedPartial = new();
    private static readonly LegacyAttributeBindingAdapter LegacyAttributes = new();
    private static bool _defaultsRegistered;

    public static void Register(IUIViewBindingAdapter adapter, bool replaceSameId = true)
    {
        if (adapter == null)
            throw new ArgumentNullException(nameof(adapter));
        if (string.IsNullOrWhiteSpace(adapter.AdapterId))
            throw new ArgumentException("Binding adapter id is required.", nameof(adapter));

        EnsureDefaults();
        if (replaceSameId)
            Adapters.RemoveAll(candidate => string.Equals(candidate.AdapterId, adapter.AdapterId, StringComparison.Ordinal));

        Adapters.Add(adapter);
    }

    public static bool Unregister(IUIViewBindingAdapter adapter)
    {
        if (adapter == null)
            return false;

        EnsureDefaults();
        return Adapters.Remove(adapter);
    }

    public static void Bind(ViewBase view, GameObject root)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (root == null)
            throw new ArgumentNullException(nameof(root));

        EnsureDefaults();
        var context = new UIViewBindingContext(view, view.GetType(), root);
        var sorted = new List<IUIViewBindingAdapter>(Adapters);
        sorted.Sort((left, right) => Compare(left, right, context.PreferredAdapterId));

        foreach (var adapter in sorted)
        {
            if (adapter.Phase != UIViewBindingAdapterPhase.Resolve ||
                !MatchesPreference(adapter, context.PreferredAdapterId))
                continue;

            var result = adapter.Bind(context);
            if (result != UIViewBindingAdapterResult.NotApplicable)
                break;
        }

        foreach (var adapter in sorted)
        {
            if (adapter.Phase != UIViewBindingAdapterPhase.Enhance)
                continue;

            if (adapter.Bind(context) == UIViewBindingAdapterResult.Stop)
                break;
        }
    }

    private static void EnsureDefaults()
    {
        if (_defaultsRegistered)
            return;

        _defaultsRegistered = true;
        Adapters.Add(SchemaGenerated);
        Adapters.Add(GeneratedPartial);
        Adapters.Add(LegacyAttributes);
    }

    private static bool MatchesPreference(IUIViewBindingAdapter adapter, string preferredAdapterId)
    {
        return string.IsNullOrWhiteSpace(preferredAdapterId) ||
               string.Equals(adapter.AdapterId, preferredAdapterId, StringComparison.Ordinal);
    }

    private static int Compare(IUIViewBindingAdapter left, IUIViewBindingAdapter right, string preferredAdapterId)
    {
        var leftPreferred = string.Equals(left.AdapterId, preferredAdapterId, StringComparison.Ordinal);
        var rightPreferred = string.Equals(right.AdapterId, preferredAdapterId, StringComparison.Ordinal);
        if (leftPreferred != rightPreferred)
            return leftPreferred ? -1 : 1;

        var priority = right.Priority.CompareTo(left.Priority);
        return priority != 0 ? priority : string.CompareOrdinal(left.AdapterId, right.AdapterId);
    }

    private sealed class SchemaGeneratedBindingAdapter : IUIViewBindingAdapter
    {
        public string AdapterId => UIViewBindingAdapterIds.SchemaGenerated;
        public int Priority => 100;
        public UIViewBindingAdapterPhase Phase => UIViewBindingAdapterPhase.Resolve;

        public UIViewBindingAdapterResult Bind(UIViewBindingContext context)
        {
            var source = context.Root.GetComponent<CSharpUIBindBehaviour>();
            if (source == null)
                return UIViewBindingAdapterResult.NotApplicable;

            context.SetBinder(UIViewBinderFactory.Create(context.ViewType, source));
            return UIViewBindingAdapterResult.Applied;
        }
    }

    private sealed class GeneratedPartialBindingAdapter : IUIViewBindingAdapter
    {
        public string AdapterId => "generated-partial";
        public int Priority => 100;
        public UIViewBindingAdapterPhase Phase => UIViewBindingAdapterPhase.Enhance;

        public UIViewBindingAdapterResult Bind(UIViewBindingContext context)
        {
            if (context.Binder == null || !(context.View is IUIViewGeneratedBinding generatedBinding))
                return UIViewBindingAdapterResult.NotApplicable;

            generatedBinding.BindGeneratedUI(context.Binder);
            return UIViewBindingAdapterResult.Applied;
        }
    }

    private sealed class LegacyAttributeBindingAdapter : IUIViewBindingAdapter
    {
        public string AdapterId => UIViewBindingAdapterIds.LegacyAttributes;
        public int Priority => -100;
        public UIViewBindingAdapterPhase Phase => UIViewBindingAdapterPhase.Enhance;

        public UIViewBindingAdapterResult Bind(UIViewBindingContext context)
        {
            if (!UIViewAutoBind.HasBindings(context.View))
                return UIViewBindingAdapterResult.NotApplicable;

            UIViewAutoBind.Bind(context.View, context.RootTransform);
            return UIViewBindingAdapterResult.Applied;
        }
    }
}
