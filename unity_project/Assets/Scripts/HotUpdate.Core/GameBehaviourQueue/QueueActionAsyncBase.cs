using System;
using System.Threading;
using Cysharp.Threading.Tasks;

public class QueueActionAsyncBase : QueuedActionBase
{
    public override async void Execute()
    {
        EnsureActionCancelToken();

        try
        {
            await OnExecute();

            if (!IsEnded && !IsDisposed)
            {
                Finish();
            }
        }
        catch (OperationCanceledException)
        {
            if (!IsEnded && !IsDisposed)
            {
                Finish();
            }
        }
        catch (Exception e)
        {
            Framework.Log.Error($"[QueueActionAsyncBase Execute Exception] {Name}");
            Framework.Log.Error(e);

            if (!IsEnded && !IsDisposed)
            {
                End(ActionEndReason.Faulted);
            }
        }
    }

    protected virtual UniTask OnExecute()
    {
        return UniTask.CompletedTask;
    }

    private void EnsureActionCancelToken()
    {
        if (ActionCancelToken == null)
        {
            ActionCancelToken = new CancellationTokenSource();
            return;
        }

        if (!ActionCancelToken.IsCancellationRequested) return;

        ActionCancelToken.Dispose();
        ActionCancelToken = new CancellationTokenSource();
    }
}
