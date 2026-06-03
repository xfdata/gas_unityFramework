using System;
using System.Threading.Tasks;

public class Disposable : IDisposable
{
    private bool m_IsDisposed;
    protected virtual void OnDispose() { }
    public bool IsDisposed => m_IsDisposed;
    public void Dispose()
    {
        if (m_IsDisposed)
            return;

        m_IsDisposed = true;
        OnDispose();
    }
}

public class AsyncDisposable : IAsyncDisposable
{
    private bool m_IsDisposed;
    protected virtual ValueTask OnDisposeAsync() => new ValueTask();
    public bool IsDisposed => m_IsDisposed;
    public async ValueTask DisposeAsync()
    {
        if (m_IsDisposed)
            return;

        m_IsDisposed = true;
        await OnDisposeAsync();
    }
}
