using System;

public class ActionExecutionSlot
{
    public IQueuedAction CurrentAction;
    public bool IsEmpty => CurrentAction == null;

    public void Tick(float deltaTime)
    {
        if (CurrentAction == null) return;
        var action = CurrentAction;

        try
        {
#if ENABLE_PROFILER
            UnityEngine.Profiling.Profiler.BeginSample(action is QueuedActionBase queuedAction
                ? queuedAction.SchedulerTickProfilerName
                : "BehaviourQueue.Scheduler.Tick");
#endif
            try
            {
                action.Tick(deltaTime);
            }
            finally
            {
#if ENABLE_PROFILER
                UnityEngine.Profiling.Profiler.EndSample();
#endif
            }
        }
        catch (Exception e)
        {
            LogActionException("Tick", action, e);
            FaultAndDisposeSafely(action);
            return;
        }

        if (action.IsEnded || action.IsDisposed)
        {
            DisposeSafely(action);
        }
    }

    public void RunAction(IQueuedAction action)
    {
        CurrentAction = action;
        try
        {
            action.Execute();
        }
        catch (Exception e)
        {
            LogActionException("Execute", action, e);
            FaultAndDisposeSafely(action);
        }
    }

    public void CancelCurrent()
    {
        if (CurrentAction == null) return;
        var action = CurrentAction;
        CancelAndDisposeSafely(action);
    }

    private void CancelAndDisposeSafely(IQueuedAction action)
    {
        try
        {
            action.End(ActionEndReason.Canceled);
        }
        catch (Exception e)
        {
            LogActionException("End(Canceled)", action, e);
        }

        DisposeSafely(action);
    }

    private void FaultAndDisposeSafely(IQueuedAction action)
    {
        try
        {
            action.End(ActionEndReason.Faulted);
        }
        catch (Exception e)
        {
            LogActionException("End(Faulted)", action, e);
        }

        DisposeSafely(action);
    }

    private void DisposeSafely(IQueuedAction action)
    {
        try
        {
            action.Dispose();
        }
        catch (Exception e)
        {
            LogActionException("Dispose", action, e);
        }
        finally
        {
            if (ReferenceEquals(CurrentAction, action))
                CurrentAction = null;
        }
    }

    private static void LogActionException(string stage, IQueuedAction action, Exception e)
    {
        Framework.Log.Error($"[ActionSlot {stage} Exception] {action?.Name ?? "UnknownAction"}");
        Framework.Log.Error(e);
    }
}
