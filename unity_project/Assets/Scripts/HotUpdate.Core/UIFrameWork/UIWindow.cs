using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class UIWindow : UIModuleBase
{
    private readonly UIRuntime _runtime;
    private readonly string _openBlockReason;
    private readonly int _sortOrder;

    private UniTask _openTask;
    private UniTask _closeTask;
    private bool _hiddenForCache;
    private bool _featureModulesCreated;
    private IUIWindowOpenBlocker _maskOpenBlocker;
    private IUIWindowOpenBlocker _blurOpenBlocker;
    private int _renderOrder = -1;

    public Type ViewType { get; }
    public UIViewConfig Config { get; }
    public ViewBase View { get; private set; }
    public GameObject GameObject { get; private set; }
    public UIWindowState State { get; private set; } = UIWindowState.None;
    public int WindowIndex { get; }

    public int SortOrder => _sortOrder;
    public bool IsReady => State == UIWindowState.Opened;
    public bool IsCached => State == UIWindowState.Hidden && _hiddenForCache;
    public bool IsOpening => State == UIWindowState.Loading || State == UIWindowState.Opening;

    public int RenderOrder
    {
        get => _renderOrder;
        set => _renderOrder = value;
    }

    public UIWindow(UIRuntime runtime, Type viewType, UIViewConfig config, int windowIndex)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        ViewType = viewType ?? throw new ArgumentNullException(nameof(viewType));
        Config = config ?? throw new ArgumentNullException(nameof(config));
        WindowIndex = windowIndex;
        _openBlockReason = $"Open_{viewType.Name}";
        _sortOrder = (int)config.Layer * 1000000 + config.SortOffset * 10000 + windowIndex;
    }

    public UniTask OpenAsync(Type viewType, object param)
    {
        if (IsOpening)
            return _openTask;

        if (State != UIWindowState.None && !IsCached)
            return UniTask.CompletedTask;

        _openTask = OpenCoreAsync(viewType, param, IsCached).Preserve();
        return _openTask;
    }

    public async UniTask RefreshAsync(object param)
    {
        if (State == UIWindowState.Hidden && !_hiddenForCache)
            ReShowByCover();

        if (State != UIWindowState.Opened || View == null)
            return;

        await View.RefreshInternal(param);
    }

    public UniTask CloseAsync(object result = null)
    {
        if (State == UIWindowState.Closing)
            return _closeTask;

        if (State == UIWindowState.Closed || State == UIWindowState.Disposed || IsCached)
            return UniTask.CompletedTask;

        _closeTask = CloseCoreAsync(result).Preserve();
        return _closeTask;
    }

    public void HideByCover()
    {
        if (State != UIWindowState.Opened)
            return;

        State = UIWindowState.Hiding;
        if (GameObject != null && Config.HideLowerView)
            GameObject.SetActive(false);
        State = UIWindowState.Hidden;
    }

    public void ReShowByCover()
    {
        if (State != UIWindowState.Hidden || _hiddenForCache)
            return;

        if (GameObject != null)
            GameObject.SetActive(true);
        State = UIWindowState.Opened;
    }

    public bool HandleEsc()
    {
        if (!Config.CloseByEsc)
            return true;

        if (View != null && View.EscInternal())
            return true;

        CloseAsync().Forget();
        return true;
    }

    private async UniTask OpenCoreAsync(Type viewType, object param, bool reuseCachedInstance)
    {
        _runtime.InputBlock.AddRef(_openBlockReason);

        try
        {
            if (reuseCachedInstance)
            {
                if (GameObject == null || View == null)
                    throw new InvalidOperationException("[UIWindow] Cached window is missing its instance or view.");

                GameObject.SetActive(true);
            }
            else
            {
                State = UIWindowState.Loading;

                var instance = await _runtime.Asset.InstantiateAsync(
                    Config.PrefabReference,
                    _runtime.Root.HiddenRoot,
                    DestroyToken);

                if (IsDisposed)
                {
                    _runtime.Asset.Release(instance);
                    throw new OperationCanceledException("[UIWindow] Window was disposed while loading.");
                }

                GameObject = instance ??
                    throw new InvalidOperationException($"[UIWindow] Instantiated object is null: {Config.PrefabReference.AssetGUID}");
                GameObject.transform.SetParent(_runtime.Root.HiddenRoot, false);

                View = CreateViewModule(viewType, GameObject);
                await View.StartAsync();
            }

            ThrowIfDisposed();
            State = UIWindowState.Opening;

            await View.OpenInternal(param);
            ThrowIfDisposed();

            if (!_featureModulesCreated)
                await CreateWindowFeatureModulesAsync();

            AttachToLayer();
            View.AdaptRootTransform();
            RenderOrder = _sortOrder;
            ApplyRenderOrder();
            await PrepareWindowFeatureModulesBeforeShow();
            ThrowIfDisposed();

            await View.PlayOpenAnimationInternal();
            ThrowIfDisposed();

            _hiddenForCache = false;
            State = UIWindowState.Opened;
            View.ShownInternal();
            _runtime.RefreshPresentation();
        }
        catch
        {
            if (!IsDisposed)
                Dispose();

            _runtime.RemoveWindow(this);
            _runtime.RefreshPresentation();
            throw;
        }
        finally
        {
            _openTask = default;
            _runtime.InputBlock.RemoveRef(_openBlockReason);
        }
    }

    private async UniTask CloseCoreAsync(object result)
    {
        if (State == UIWindowState.None || IsOpening)
        {
            Dispose();
            _runtime.RemoveWindow(this);
            _runtime.RefreshPresentation();
            _closeTask = default;
            return;
        }

        State = UIWindowState.Closing;
        _runtime.Mask.Hide(this);
        _runtime.Blur.Detach(this);

        if (View != null)
        {
            await View.PlayCloseAnimationInternal();
            await View.CloseInternal(result);
        }

        if (IsDisposed)
        {
            _closeTask = default;
            return;
        }

        if (Config.CacheMode == UICacheMode.HideOnClose ||
            Config.CacheMode == UICacheMode.Preload)
        {
            HideForCache();
        }
        else
        {
            State = UIWindowState.Closed;
            Dispose();
            _runtime.RemoveWindow(this);
        }

        _closeTask = default;
        _runtime.RefreshPresentation();
    }

    private ViewBase CreateViewModule(Type viewType, GameObject gameObject)
    {
        var bind = gameObject.GetComponent<CSharpUIBindBehaviour>();
        var binder = bind != null ? UIViewBinderFactory.Create(viewType, bind) : null;
        var view = (ViewBase)Activator.CreateInstance(viewType);

        view.BindView(this, gameObject, binder);
        RegisterChild(view);
        return view;
    }

    private async UniTask CreateWindowFeatureModulesAsync()
    {
        if (Config.MaskMode != UIMaskMode.None)
        {
            var module = await AddModuleAsync(new UIMaskWindowModule());
            _maskOpenBlocker = module;
        }

        if (Config.BlurMode != UIBlurMode.None)
        {
            var module = await AddModuleAsync(new UIBlurWindowModule());
            _blurOpenBlocker = module;
        }

        _featureModulesCreated = true;
    }

    private async UniTask PrepareWindowFeatureModulesBeforeShow()
    {
        if (_maskOpenBlocker != null)
            await _maskOpenBlocker.PrepareBeforeShow();
        if (_blurOpenBlocker != null)
            await _blurOpenBlocker.PrepareBeforeShow();
    }

    private void AttachToLayer()
    {
        var parent = _runtime.Root.GetLayerRoot(Config.Layer);
        GameObject.transform.SetParent(parent, false);
        GameObject.transform.SetAsLastSibling();
    }

    private void ApplyRenderOrder()
    {
        if (_renderOrder == -1 || GameObject == null)
            return;

        var canvas = GameObject.GetComponent<Canvas>();
        if (canvas == null)
            return;

        canvas.overrideSorting = true;
        canvas.sortingOrder = _renderOrder;

        var dynRenders = GameObject.GetComponentsInChildren<DynCanvaRenderOrder>(true);
        foreach (var render in dynRenders)
        {
            render.SetRenderOrder(canvas.sortingLayerID, _renderOrder);
        }
    }

    private void HideForCache()
    {
        if (GameObject == null)
            return;

        GameObject.SetActive(false);
        GameObject.transform.SetParent(_runtime.Root.HiddenRoot, false);
        _hiddenForCache = true;
        State = UIWindowState.Hidden;
    }

    private void ThrowIfDisposed()
    {
        if (IsDisposed)
            throw new OperationCanceledException("[UIWindow] Window has been disposed.");

        DestroyToken.ThrowIfCancellationRequested();
    }

    protected override void OnStop()
    {
        _maskOpenBlocker = null;
        _blurOpenBlocker = null;

        State = UIWindowState.Disposed;
        _runtime.Mask.Hide(this);
        _runtime.Blur.Detach(this);

        if (GameObject != null)
        {
            _runtime.Asset.Release(GameObject);
            GameObject = null;
        }
    }
}
