using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.SceneSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class UIRuntime : IDisposable
{
    private const int RenderLayerStride = 2048;

    public static UIRuntime Instance { get; private set; }

    private readonly List<UIWindow> _allWindows = new();
    private readonly List<UIWindow> _popupStack = new();
    private readonly Dictionary<UILayer, List<UIWindow>> _layerGroups = new();
    private int _windowIndex;
    private UIRootAdapt _rootAdapter;
    private bool _disposed;

    public UIRoot Root { get; }
    public IUIAssetService Asset { get; }
    public UIMaskService Mask { get; }
    public UIBlurService Blur { get; }
    public UIInputBlockService InputBlock { get; }
    public IReadOnlyList<UIWindow> PopupStack => _popupStack;
    public IReadOnlyList<UIWindow> AllWindows => _allWindows;

    public UIRuntime(
        UIRoot root,
        IUIAssetService asset,
        UIMaskService mask,
        UIBlurService blur,
        UIInputBlockService inputBlock)
    {
        if (Instance != null)
            throw new InvalidOperationException("[UIRuntime] Duplicate runtime. Dispose the previous runtime before creating another one.");

        Root = root ?? throw new ArgumentNullException(nameof(root));
        Asset = asset ?? throw new ArgumentNullException(nameof(asset));
        Mask = mask ?? throw new ArgumentNullException(nameof(mask));
        Blur = blur ?? throw new ArgumentNullException(nameof(blur));
        InputBlock = inputBlock ?? throw new ArgumentNullException(nameof(inputBlock));

        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        Instance = this;
    }

    public async UniTask<TView> Open<TView>(object param = null) where TView : ViewBase
    {
        ThrowIfDisposed();

        var viewType = typeof(TView);
        var config = UIViewRegistry.Get(viewType);

        if (config.CacheMode != UICacheMode.DestroyOnClose)
        {
            var reusable = FindReusableWindow(viewType);
            if (reusable != null)
            {
                BringToFront(reusable);

                if (reusable.IsOpening)
                {
                    await reusable.OpenAsync(viewType, param);
                    if (reusable.IsCached)
                        await reusable.OpenAsync(viewType, param);
                    else if (reusable.IsReady)
                        await reusable.RefreshAsync(param);
                }
                else
                {
                    await reusable.OpenAsync(viewType, param);
                }

                return reusable.View as TView;
            }
        }

        var window = CreateWindow(viewType, config);
        try
        {
            await window.OpenAsync(viewType, param);
            return window.View as TView;
        }
        catch
        {
            RemoveWindow(window);
            throw;
        }
    }

    public async UniTask Preload<TView>() where TView : ViewBase
    {
        ThrowIfDisposed();

        var viewType = typeof(TView);
        var config = UIViewRegistry.Get(viewType);
        if (config.CacheMode != UICacheMode.Preload)
        {
            throw new InvalidOperationException(
                $"[UIRuntime] {viewType.Name} must use UICacheMode.Preload before calling Preload<TView>().");
        }

        var existing = FindReusableWindow(viewType);
        if (existing != null)
        {
            if (existing.IsOpening)
                await existing.PreloadAsync(viewType);
            return;
        }

        var window = CreateWindow(viewType, config);
        try
        {
            await window.PreloadAsync(viewType);
        }
        catch
        {
            RemoveWindow(window);
            throw;
        }
    }

    public void Close<TView>(object result = null) where TView : ViewBase
    {
        FindTopWindow(typeof(TView))?.CloseAsync(result).Forget();
    }

    public TView Get<TView>() where TView : ViewBase
    {
        return FindTopWindow(typeof(TView))?.View as TView;
    }

    public bool IsOpen<TView>() where TView : ViewBase
    {
        return FindTopWindow(typeof(TView))?.IsReady == true;
    }

    public void HandleEsc()
    {
        GetTopPopup()?.HandleEsc();
    }

    public UIWindow GetTopPopup()
    {
        for (var i = _popupStack.Count - 1; i >= 0; i--)
        {
            var window = _popupStack[i];
            if (window != null && window.IsReady)
                return window;
        }

        return null;
    }

    public void AttachRootAdapter(UIRootAdapt adapter)
    {
        if (_rootAdapter == adapter)
            return;

        if (_rootAdapter != null)
            _rootAdapter.LayoutChanged -= HandleRootLayoutChanged;

        _rootAdapter = adapter;
        if (_rootAdapter != null)
        {
            _rootAdapter.LayoutChanged += HandleRootLayoutChanged;
            HandleRootLayoutChanged();
        }
    }

    internal void RefreshPresentation()
    {
        if (_disposed)
            return;

        RefreshRenderOrder();
        RefreshCoverState();
        Mask.Refresh();
    }

    internal void RemoveWindow(UIWindow window)
    {
        if (window == null)
            return;

        _allWindows.Remove(window);
        _popupStack.Remove(window);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        AttachRootAdapter(null);

        for (var i = _allWindows.Count - 1; i >= 0; i--)
            _allWindows[i]?.Dispose();

        _allWindows.Clear();
        _popupStack.Clear();
        Mask.Dispose();
        Blur.Dispose();
        InputBlock.Dispose();

        if (Instance == this)
            Instance = null;
    }

    private UIWindow CreateWindow(Type viewType, UIViewConfig config)
    {
        var window = new UIWindow(this, viewType, config, ++_windowIndex);
        window.Attach(new UIModuleContext(this, window, CancellationToken.None));
        _allWindows.Add(window);

        if (config.EnterPopupStack)
            InsertSorted(_popupStack, window);

        return window;
    }

    private UIWindow FindReusableWindow(Type viewType)
    {
        for (var i = _allWindows.Count - 1; i >= 0; i--)
        {
            var window = _allWindows[i];
            if (window != null &&
                window.ViewType == viewType &&
                window.State != UIWindowState.Closed &&
                window.State != UIWindowState.Disposed)
            {
                return window;
            }
        }

        return null;
    }

    private UIWindow FindTopWindow(Type viewType)
    {
        UIWindow result = null;
        for (var i = 0; i < _allWindows.Count; i++)
        {
            var window = _allWindows[i];
            if (window == null ||
                window.ViewType != viewType ||
                window.State == UIWindowState.Closed ||
                window.State == UIWindowState.Disposed)
            {
                continue;
            }

            if (result == null || window.SortOrder > result.SortOrder)
                result = window;
        }

        return result;
    }

    private void BringToFront(UIWindow window)
    {
        if (window == null)
            return;

        window.UpdateWindowIndex(++_windowIndex);
        if (window.Config.EnterPopupStack)
        {
            _popupStack.Remove(window);
            InsertSorted(_popupStack, window);
        }
    }

    private void RefreshRenderOrder()
    {
        foreach (var list in _layerGroups.Values)
            list.Clear();

        foreach (var window in _allWindows)
        {
            if (window == null || window.GameObject == null || window.IsCached)
                continue;

            if (!_layerGroups.TryGetValue(window.Config.Layer, out var list))
            {
                list = new List<UIWindow>();
                _layerGroups[window.Config.Layer] = list;
            }

            list.Add(window);
        }

        foreach (var pair in _layerGroups)
        {
            var group = pair.Value;
            group.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));

            var layerBase = GetLayerRenderBase(pair.Key);
            for (var i = 0; i < group.Count; i++)
            {
                var transform = group[i].GameObject.transform;
                if (transform != null && transform.GetSiblingIndex() != i)
                    transform.SetSiblingIndex(i);

                var renderOrder = layerBase + Mathf.Min(i, RenderLayerStride - 1);
                group[i].SetRenderOrder(renderOrder);
            }
        }
    }

    private static int GetLayerRenderBase(UILayer layer)
    {
        var ordinal = Mathf.Max(0, (int)layer / 10);
        return ordinal * RenderLayerStride;
    }

    private void RefreshCoverState()
    {
        UIWindow topFullScreen = null;

        for (var i = _popupStack.Count - 1; i >= 0; i--)
        {
            var window = _popupStack[i];
            if (window == null || !window.IsReady)
                continue;

            if (window.Config.FullScreen)
            {
                topFullScreen = window;
                break;
            }
        }

        if (topFullScreen == null)
        {
            SceneMgr.Instance?.SetCurrentSceneCoveredByUI(false);

            foreach (var window in _allWindows)
                window?.ReShowByCover();
            return;
        }

        SceneMgr.Instance?.SetCurrentSceneCoveredByUI(true);

        var pauseLower = topFullScreen.Config.PauseLowerView;
        var topSortOrder = topFullScreen.SortOrder;

        foreach (var window in _allWindows)
        {
            if (window == null || window == topFullScreen)
                continue;

            if (window.SortOrder < topSortOrder && pauseLower)
                window.HideByCover();
            else
                window.ReShowByCover();
        }
    }

    private void HandleRootLayoutChanged()
    {
        if (_disposed)
            return;

        Root.SetSideOffset(_rootAdapter != null ? _rootAdapter.SideVal : 0f);
        foreach (var window in _allWindows)
        {
            if (window?.IsReady == true)
                window.View?.AdaptRootTransform();
        }
    }

    private void HandleActiveSceneChanged(Scene previous, Scene current)
    {
        var snapshot = _allWindows.ToArray();
        foreach (var window in snapshot)
        {
            if (window == null || !window.Config.CloseWhenSceneChange)
                continue;

            if (window.IsCached)
            {
                window.Dispose();
                RemoveWindow(window);
            }
            else
            {
                window.CloseAsync().Forget();
            }
        }

        RefreshPresentation();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(UIRuntime));
    }

    private static void InsertSorted(List<UIWindow> windows, UIWindow window)
    {
        var index = windows.BinarySearch(window, WindowSortOrderComparer.Instance);
        if (index < 0)
            index = ~index;
        windows.Insert(index, window);
    }

    private sealed class WindowSortOrderComparer : IComparer<UIWindow>
    {
        public static readonly WindowSortOrderComparer Instance = new();

        public int Compare(UIWindow x, UIWindow y)
        {
            return x.SortOrder.CompareTo(y.SortOrder);
        }
    }
}

public static class UIRuntimeBootstrap
{
    public static UIRuntime Create(
        UIViewConfigTable configTable,
        GameObject uiRootObject,
        Camera uiCamera,
        Transform hiddenRoot,
        GameObject inputBlockObject,
        GameObject maskObject)
    {
        return Create(
            configTable,
            uiRootObject,
            uiCamera,
            hiddenRoot,
            inputBlockObject,
            maskObject,
            new AddressablesUIAssetService());
    }

    public static UIRuntime Create(
        UIViewConfigTable configTable,
        GameObject uiRootObject,
        Camera uiCamera,
        Transform hiddenRoot,
        GameObject inputBlockObject,
        GameObject maskObject,
        IUIAssetService assetService)
    {
        if (configTable == null)
            throw new ArgumentNullException(nameof(configTable));
        if (uiRootObject == null)
            throw new ArgumentNullException(nameof(uiRootObject));
        if (hiddenRoot == null)
            throw new ArgumentNullException(nameof(hiddenRoot));
        if (inputBlockObject == null)
            throw new ArgumentNullException(nameof(inputBlockObject));
        if (maskObject == null)
            throw new ArgumentNullException(nameof(maskObject));
        if (assetService == null)
            throw new ArgumentNullException(nameof(assetService));

        UIViewRegistry.Initialize(configTable);

        var root = new UIRoot(uiRootObject, uiCamera, hiddenRoot);
        RegisterDefaultLayers(root, uiRootObject.transform);

        var runtime = new UIRuntime(
            root,
            assetService,
            new UIMaskService(maskObject),
            new UIBlurService(),
            new UIInputBlockService(inputBlockObject));

        runtime.AttachRootAdapter(uiRootObject.GetComponent<UIRootAdapt>());
        return runtime;
    }

    private static void RegisterDefaultLayers(UIRoot root, Transform rootTransform)
    {
        TryRegister(root, rootTransform, UILayer.Scene, "Canvas_Scene");
        TryRegister(root, rootTransform, UILayer.World, "Canvas_World");
        TryRegister(root, rootTransform, UILayer.Hud, "Canvas_Hud");
        TryRegister(root, rootTransform, UILayer.HudTop, "Canvas_Hud_Top");
        TryRegister(root, rootTransform, UILayer.Normal, "Canvas_Normal");
        TryRegister(root, rootTransform, UILayer.Top, "Canvas_Top");
        TryRegister(root, rootTransform, UILayer.Mask, "Canvas_Mask");
        TryRegister(root, rootTransform, UILayer.Guide, "Canvas_Guide");
        TryRegister(root, rootTransform, UILayer.Tip, "Canvas_Tip");
        TryRegister(root, rootTransform, UILayer.Overlay, "Canvas_Overlay");
        TryRegister(root, rootTransform, UILayer.Debug, "Canvas_Debug");
    }

    private static void TryRegister(UIRoot root, Transform rootTransform, UILayer layer, string path)
    {
        var transform = rootTransform.Find(path);
        if (transform != null)
            root.RegisterLayer(layer, transform);
    }
}
