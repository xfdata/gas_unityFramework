using System;
using System.Collections.Generic;
using System.Threading;

public abstract class QueuedActionBase : IQueuedAction
{
    public string Name { get; protected set; }
    public int ActionType { get; protected set; } = GameplayActionType.ActionStuck;
    public int Priority { get; set; }
    public int SubPriority { get; set; }
    public float EnqueueTimeStamp { get; set; }
    public bool IsImmediate { get; set; }
    public GameplayTagContainer Tags { get; protected set; }
    public TagQuery RequireTags { get; protected set; }
    public TagQuery BlockTags { get; protected set; }
    public List<IActionCondition> Conditions { get; }
    public List<IActionCondition> ManualActivationConditions { get; }

    protected ActionEndReason endReason;
    protected bool disposed;

    public bool IsEnded => endReason != ActionEndReason.None;
    public ActionEndReason EndReason => endReason;
    public bool IsCompleted => endReason == ActionEndReason.Completed;
    public bool IsCanceled => endReason == ActionEndReason.Canceled;
    public bool IsDisposed => disposed;
    
    protected CancellationTokenSource ActionCancelToken;

#if ENABLE_PROFILER
    private string _actionProfilerName;
    private string _schedulerTickProfilerName;
    private string _tickProfilerName;
    private string _endProfilerName;
    private string _disposeProfilerName;
    private string _runProfilerName;
    private string _abortProfilerName;
#endif

    protected QueuedActionBase()
    {
        Name = GetType().Name;
        Tags = new GameplayTagContainer();
        Conditions = new List<IActionCondition>();
        ManualActivationConditions = new List<IActionCondition>();
    }

    protected QueuedActionBase(string name, int priority, int subPriority = 0) : this()
    {
        Name = string.IsNullOrEmpty(name) ? GetType().Name : name;
        Priority = priority;
        SubPriority = subPriority;
    }

    public virtual void Execute()
    {
    }

    public virtual void Tick(float deltaTime)
    {
        using var _ = ProfileAction("Tick", this);
        if (IsEnded || disposed) return;
    }

    public void End(ActionEndReason reason)
    {
        using var _ = ProfileAction("End", this);
        if (IsEnded || disposed || reason == ActionEndReason.None) return;
        endReason = reason;
        Framework.Log.Debug($"[Action End] {Name} => {reason}");
    }

    public void Finish()
    {
        End(ActionEndReason.Completed);
    }

    public void Cancel()
    {
        End(ActionEndReason.Canceled);
    }

    public void Dispose()
    {
        using var _ = ProfileAction("Dispose", this);
        if (disposed) return;
        disposed = true;
        try
        {
            if (ActionCancelToken is { IsCancellationRequested: false })
            {
                ActionCancelToken.Cancel();
            }
            ActionCancelToken?.Dispose();
            ActionCancelToken = null;
            OnDispose();
        }
        catch (Exception e)
        {
            Framework.Log.Error($"[Action Dispose Exception] {Name} {e}");
        }
    }

    protected virtual void OnDispose()
    {
    }

    public static ActionProfilerScope ProfileAction(string stage, IQueuedAction action)
    {
        return new ActionProfilerScope(stage, action);
    }

#if ENABLE_PROFILER
    public string SchedulerTickProfilerName =>
        _schedulerTickProfilerName ??= $"BehaviourQueue.Scheduler.Tick/ {GetType().Name}";

    private string ActionProfilerName
    {
        get
        {
            if (_actionProfilerName != null)
                return _actionProfilerName;

            var actionName = string.IsNullOrEmpty(Name)
                ? GetType().Name
                : Name;

            _actionProfilerName = $"{actionName}({ActionType})";
            return _actionProfilerName;
        }
    }

    private string GetProfilerName(string stage)
    {
        switch (stage)
        {
            case "Tick":
                return _tickProfilerName ??= $"BehaviourQueue.Action.Tick.{ActionProfilerName}";
            case "End":
                return _endProfilerName ??= $"BehaviourQueue.Action.End.{ActionProfilerName}";
            case "Dispose":
                return _disposeProfilerName ??= $"BehaviourQueue.Action.Dispose.{ActionProfilerName}";
            case "Run":
                return _runProfilerName ??= $"BehaviourQueue.Action.Run.{ActionProfilerName}";
            case "Abort":
                return _abortProfilerName ??= $"BehaviourQueue.Action.Abort.{ActionProfilerName}";
            default:
                return $"BehaviourQueue.Action.{stage}.{ActionProfilerName}";
        }
    }

    private static string GetProfilerName(string stage, IQueuedAction action)
    {
        if (action == null)
            return $"BehaviourQueue.Action.{stage}.NullAction";

        if (action is QueuedActionBase queuedAction)
            return queuedAction.GetProfilerName(stage);

        return $"BehaviourQueue.Action.{stage}.{GetActionProfilerName(action)}";
    }

    private static string GetActionProfilerName(IQueuedAction action)
    {
        var actionName = string.IsNullOrEmpty(action.Name)
            ? action.GetType().Name
            : action.Name;

        return $"{actionName}({action.ActionType})";
    }
#endif

    public readonly struct ActionProfilerScope : IDisposable
    {
        private readonly Framework.AutoProfiler _profiler;

        public ActionProfilerScope(string stage, IQueuedAction action)
        {
#if ENABLE_PROFILER
            _profiler = new Framework.AutoProfiler(GetProfilerName(stage, action));
#else
            _profiler = default;
#endif
        }

        public void Dispose()
        {
            _profiler.Dispose();
        }
    }
}
