using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.SceneSystem;
using UnityEngine;

public sealed class UIRuntime : IDisposable
{
    public static UIRuntime Instance { get; private set; }

    private readonly List<UIWindow> _allWindows = new();
    private readonly List<UIWindow> _popupStack = new();
    private readonly Dictionary<UILayer, List<UIWindow>> _layerGroups = new();
    private int _windowIndex;

    public UIRoot Root { get; }
    public IUIAssetService Asset { get; }
    public UIMaskService Mask { get; }
    public UIBlurService Blur { get; }
    public UIInputBlockService InputBlock { get; }
    public IReadOnlyList<UIWindow> PopupStack => _popupStack;

    public UIRuntime(
        UIRoot root,
        IUIAssetService asset,
        UIMaskService mask,
        UIBlurService blur,
        UIInputBlockService inputBlock)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        Asset = asset ?? throw new ArgumentNullException(nameof(asset));
        Mask = mask ?? throw new ArgumentNullException(nameof(mask));
        Blur = blur ?? throw new ArgumentNullException(nameof(blur));
        InputBlock = inputBlock ?? throw new ArgumentNullException(nameof(inputBlock));
        Instance = this;
    }

    public async UniTask<TView> Open<TView>(object param = null) where TView : ViewBase
    {
        var viewType = typeof(TView);
        var config = UIViewRegistry.Get(viewType);

        var window = new UIWindow(this, viewType, config, ++_windowIndex);
        window.Attach(new UIModuleContext(this, window, CancellationToken.None));
        _allWindows.Add(window);

        if (config.EnterPopupStack)
            InsertSorted(_popupStack, window);

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

    internal void RefreshPresentation()
    {
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

    private UIWindow FindTopWindow(Type viewType)
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

    public IReadOnlyList<UIWindow> AllWindows => _allWindows;

    public UIWindow GetTopPopup()
    {
        for (var i = _popupStack.Count - 1; i >= 0; i--)
        {
            var win = _popupStack[i];
            if (win != null && win.IsReady)
                return win;
        }

        return null;
    }

    public void Dispose()
    {
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

    private void RefreshRenderOrder()
    {
        foreach (var list in _layerGroups.Values)
            list.Clear();

        foreach (var window in _allWindows)
        {
            if (window == null || window.GameObject == null)
                continue;

            if (!_layerGroups.TryGetValue(window.Config.Layer, out var list))
            {
                list = new List<UIWindow>();
                _layerGroups[window.Config.Layer] = list;
            }

            list.Add(window);
        }

        foreach (var group in _layerGroups.Values)
        {
            group.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));

            for (var i = 0; i < group.Count; i++)
            {
                var transform = group[i].GameObject.transform;
                if (transform != null && transform.GetSiblingIndex() != i)
                    transform.SetSiblingIndex(i);
            }
        }
    }

    private void RefreshCoverState()
    {
        UIWindow topFullScreen = null;

        for (var i = _popupStack.Count - 1; i >= 0; i--)
        {
            var win = _popupStack[i];
            if (win == null || !win.IsReady)
                continue;

            if (win.Config.FullScreen)
            {
                topFullScreen = win;
                break;
            }
        }

        if (topFullScreen == null)
        {
            SceneMgr.Instance?.SetCurrentSceneCoveredByUI(false);

            foreach (var win in _allWindows)
            {
                if (win != null)
                    win.ReShowByCover();
            }
            return;
        }

        SceneMgr.Instance?.SetCurrentSceneCoveredByUI(true);

        var pauseLower = topFullScreen.Config.PauseLowerView;
        var topSortOrder = topFullScreen.SortOrder;

        foreach (var win in _allWindows)
        {
            if (win == null || win == topFullScreen)
                continue;

            if (win.SortOrder < topSortOrder)
            {
                if (pauseLower)
                    win.HideByCover();
                else
                    win.ReShowByCover();
            }
            else
            {
                win.ReShowByCover();
            }
        }
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

        var mask = new UIMaskService(maskObject);
        var blur = new UIBlurService();
        var inputBlock = new UIInputBlockService(inputBlockObject);

        return new UIRuntime(root, assetService, mask, blur, inputBlock);
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
