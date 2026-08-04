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

    public UIModuleContext CreateChildContext(CancellationToken parentDestroyToken)
    {
        return new UIModuleContext(Runtime, Window, parentDestroyToken);
    }
}

public abstract class UIModuleBase : IDisposable
{
    private readonly List<UIModuleBase> _children = new();
    private readonly List<Action> _lifetimeCleanups = new();
    private readonly List<Action> _openCleanups = new();

    private CancellationTokenSource _cts;
    private CancellationTokenSource _openCts;
    private bool _started;
    private bool _disposed;

    public bool IsStarted => _started;
    public bool IsDisposed => _disposed;

    protected UIModuleContext Context { get; private set; }
    protected CancellationToken DestroyToken => _cts?.Token ?? CancellationToken.None;
    protected CancellationToken OpenToken => _openCts?.Token ?? DestroyToken;

    private UIViewBinder _binder;

    protected UIViewBinder B
    {
        get => _binder;
        set => _binder = value;
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
        try
        {
            await OnStart();
        }
        catch
        {
            _started = false;
            throw;
        }
    }

    internal void BeginOpenScope()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);

        EndOpenScope();
        _openCts = CancellationTokenSource.CreateLinkedTokenSource(DestroyToken);

        for (var i = 0; i < _children.Count; i++)
            _children[i]?.BeginOpenScope();
    }

    internal void EndOpenScope()
    {
        for (var i = _children.Count - 1; i >= 0; i--)
            _children[i]?.EndOpenScope();

        if (_openCts == null && _openCleanups.Count == 0)
            return;

        try
        {
            _openCts?.Cancel();
            RunCleanups(_openCleanups);
        }
        finally
        {
            _openCts?.Dispose();
            _openCts = null;
        }
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

        AttachChild(module);
        module.StartAsync().Forget();
        return module;
    }

    protected async UniTask<T> AddModuleAsync<T>(T module) where T : UIModuleBase
    {
        if (module == null)
            throw new ArgumentNullException(nameof(module));

        AttachChild(module);
        await module.StartAsync();
        return module;
    }

    protected void RegisterChild(UIModuleBase module)
    {
        if (module == null)
            throw new ArgumentNullException(nameof(module));

        AttachChild(module);
    }

    protected void AddCleanup(Action cleanup)
    {
        if (cleanup != null)
            _lifetimeCleanups.Add(cleanup);
    }

    protected void RunTask(Func<CancellationToken, UniTask> task)
    {
        if (task == null)
            return;

        RunTaskInternal(task, OpenToken).Forget();
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

        BindClick(button, () =>
        {
            action?.Invoke();
            return UniTask.CompletedTask;
        });
    }

    protected void BindClick(Button button, Func<UniTask> asyncAction)
    {
        if (button == null)
            return;

        UnityEngine.Events.UnityAction listener = () =>
        {
            if (!OpenToken.IsCancellationRequested)
                asyncAction?.Invoke().Forget();
        };

        button.onClick.AddListener(listener);
        AddOpenCleanup(() =>
        {
            if (button != null)
                button.onClick.RemoveListener(listener);
        });
    }

    private void AttachChild(UIModuleBase module)
    {
        module.Attach(Context.CreateChildContext(DestroyToken));
        _children.Add(module);

        if (_openCts != null)
            module.BeginOpenScope();
    }

    private void AddOpenCleanup(Action cleanup)
    {
        if (cleanup == null)
            return;

        if (_openCts != null)
            _openCleanups.Add(cleanup);
        else
            _lifetimeCleanups.Add(cleanup);
    }

    private static void RunCleanups(List<Action> cleanups)
    {
        for (var i = cleanups.Count - 1; i >= 0; i--)
        {
            try
            {
                cleanups[i]?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        cleanups.Clear();
    }

    private static async UniTaskVoid RunTaskInternal(
        Func<CancellationToken, UniTask> task,
        CancellationToken token)
    {
        try
        {
            await task(token);
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
            EndOpenScope();
            _cts?.Cancel();

            for (var i = _children.Count - 1; i >= 0; i--)
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

            RunCleanups(_lifetimeCleanups);

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
