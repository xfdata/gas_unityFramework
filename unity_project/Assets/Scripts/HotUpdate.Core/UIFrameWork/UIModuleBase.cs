using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public sealed class UIModuleContext
{
    public UIRuntime Runtime { get; }
    public UIWindow Window { get; }
    public CancellationToken DestroyToken { get; }

    public UIRoot Root => Runtime.Root;

    public UIModuleContext(UIRuntime runtime, UIWindow window, CancellationToken destroyToken)
    {
        Runtime = runtime;
        Window = window;
        DestroyToken = destroyToken;
    }

    public UIModuleContext CreateChildContext()
    {
        return new UIModuleContext(Runtime, Window, DestroyToken);
    }
}

public abstract class UIModuleBase : IDisposable
{
    private readonly List<UIModuleBase> _children = new();
    private readonly List<Action> _cleanups = new();

    private CancellationTokenSource _cts;
    private bool _started;
    private bool _disposed;

    public bool IsStarted => _started;
    public bool IsDisposed => _disposed;

    protected UIModuleContext Context { get; private set; }
    protected CancellationToken DestroyToken => _cts?.Token ?? CancellationToken.None;

    private UIViewBinder _binder;

    protected UIViewBinder B
    {
        get => _binder;
        set
        {
            _binder = value;

            if (_binder?.Source != null)
                UIViewAutoBind.Bind(this, _binder.Source.transform);

            if (_binder != null && this is IUIViewGeneratedBinding generatedBinding)
                generatedBinding.BindGeneratedUI(_binder);
        }
    }

    protected UIObjectRef Get(string key) => B.Get(key);
    protected UIButtonRef Btn(string key) => B.Btn(key);
    protected UITextRef Txt(string key) => B.Txt(key);
    protected UIImageRef Img(string key) => B.Img(key);
    protected UIScrollRef Scroll(string key) => B.Scroll(key);
    protected T Get<T>(string key) where T : Component => B.Get<T>(key);
    protected UIViewBinder GetBinder(string key) => B.GetBinder(key);
    protected TBinder GetBinder<TBinder>(string key) where TBinder : UIViewBinder => B.GetBinder<TBinder>(key);

    protected UIButtonRef Cache(ref UIButtonRef field, string key) => field ??= Btn(key);
    protected UITextRef Cache(ref UITextRef field, string key) => field ??= Txt(key);
    protected UIImageRef Cache(ref UIImageRef field, string key) => field ??= Img(key);
    protected UIScrollRef Cache(ref UIScrollRef field, string key) => field ??= Scroll(key);
    protected UIObjectRef Cache(ref UIObjectRef field, string key) => field ??= Get(key);
    protected T Cache<T>(ref T field, string key) where T : Component => field ??= Get<T>(key);

    protected void BindFields(Transform root)
    {
        UIViewAutoBind.Bind(this, root);
    }

    protected void BindFields(GameObject root)
    {
        if (root != null)
            UIViewAutoBind.Bind(this, root.transform);
    }

    internal void Attach(UIModuleContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        Context = context;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(context.DestroyToken);
    }

    internal async UniTask StartAsync()
    {
        if (_disposed || _started)
            return;

        _started = true;
        await OnStart();
    }

    protected virtual UniTask OnStart()
    {
        return UniTask.CompletedTask;
    }

    protected virtual void OnStop()
    {
    }

    protected T AddModule<T>(T module) where T : UIModuleBase
    {
        if (module == null)
            throw new ArgumentNullException(nameof(module));

        module.Attach(Context.CreateChildContext());
        _children.Add(module);
        module.StartAsync().Forget();
        return module;
    }

    protected async UniTask<T> AddModuleAsync<T>(T module) where T : UIModuleBase
    {
        if (module == null)
            throw new ArgumentNullException(nameof(module));

        module.Attach(Context.CreateChildContext());
        _children.Add(module);
        await module.StartAsync();
        return module;
    }

    protected void RegisterChild(UIModuleBase module)
    {
        if (module == null)
            throw new ArgumentNullException(nameof(module));

        module.Attach(Context.CreateChildContext());
        _children.Add(module);
    }

    protected void AddCleanup(Action cleanup)
    {
        if (cleanup != null)
            _cleanups.Add(cleanup);
    }

    protected void RunTask(Func<CancellationToken, UniTask> task)
    {
        if (task == null)
            return;

        RunTaskInternal(task).Forget();
    }

    protected void Delay(float seconds, Action callback)
    {
        RunTask(async token =>
        {
            await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: token);
            if (!token.IsCancellationRequested)
                callback?.Invoke();
        });
    }

    protected void Every(float seconds, Func<UniTask> callback, bool immediately = false)
    {
        RunTask(async token =>
        {
            if (immediately && callback != null)
                await callback();

            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: token);
                if (!token.IsCancellationRequested && callback != null)
                    await callback();
            }
        });
    }

    protected void BindClick(Button button, Action action)
    {
        if (button == null)
            return;

        BindClick(button, () => { action?.Invoke(); return UniTask.CompletedTask; });
    }

    protected void BindClick(Button button, Func<UniTask> asyncAction)
    {
        if (button == null)
            return;

        UnityEngine.Events.UnityAction listener = () =>
        {
            if (!DestroyToken.IsCancellationRequested)
                asyncAction?.Invoke().Forget();
        };

        button.onClick.AddListener(listener);
        AddCleanup(() =>
        {
            if (button != null)
                button.onClick.RemoveListener(listener);
        });
    }

    private async UniTaskVoid RunTaskInternal(Func<CancellationToken, UniTask> task)
    {
        try
        {
            await task(DestroyToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            _cts?.Cancel();

            for (int i = _children.Count - 1; i >= 0; i--)
            {
                try
                {
                    _children[i]?.Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
            _children.Clear();

            for (int i = _cleanups.Count - 1; i >= 0; i--)
            {
                try
                {
                    _cleanups[i]?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            _cleanups.Clear();

            try
            {
                OnStop();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
        }
    }
}
