using System;
using System.Collections.Generic;

public class QueuedActionScheduler : Disposable
{
    private const int PendingInitialCapacity = 32;
    private const int RunningInitialCapacity = 4;
    private const int InterruptedBufferInitialCapacity = 4;

    private List<IQueuedAction> pendingActions = new(PendingInitialCapacity);
    private List<IQueuedAction> runningActions = new(RunningInitialCapacity);
    private List<IQueuedAction> _runningAddList = new(RunningInitialCapacity);
    private List<IQueuedAction> _runningRemoveList = new(RunningInitialCapacity);
    private int _runningMutationDepth;

    public ActionConflictRule[] ConflictRules;
    public GameplayTagContainer GlobalTagContextTags;
    public List<IActionCondition> GlobalDispatchConditions;

    private bool _isReady = false;
    public int PendingCount => pendingActions.Count;
    public int RunningCount => runningActions.Count + _runningAddList.Count - _runningRemoveList.Count;

    public bool HasRunningSlots => RunningCount > 0;

    public void SetReady(bool isReady)
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.SetReady");
        _isReady = isReady;
        if (isReady)
        {
            Tick(UnityEngine.Time.deltaTime);
        }
    }
    public void Enqueue(IQueuedAction action)
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.Enqueue");
        if (action == null)
        {
            Framework.Log.Error("[BehaviourQueue] Enqueue failed: action is null.");
            return;
        }

        try
        {
            action.EnqueueTimeStamp = UnityEngine.Time.realtimeSinceStartup;
            InsertByPriority(action);
        }
        catch (Exception e)
        {
            AbortActionSafely(action, "Enqueue", ActionEndReason.Faulted, e);
        }
    }

    public void ExecuteImmediate(IQueuedAction action)
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.ExecuteImmediate");
        if (action == null)
        {
            Framework.Log.Error("[BehaviourQueue] ExecuteImmediate failed: action is null.");
            return;
        }
        
        try
        {
            if (!CheckTags(action) || !CheckConditions(action))
            {
                AbortActionSafely(action, "ExecuteImmediate_ConditionFailed", ActionEndReason.Canceled);
                return;
            }

            if (!HandleConflicts(action, true)) return;
            RunAction(action);
        }
        catch (Exception e)
        {
            AbortActionSafely(action, "ExecuteImmediate", ActionEndReason.Faulted, e);
        }
    }

    public bool TryActivateQueuedAction(IQueuedAction action, int param = 0)
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.TryActivateQueuedAction");
        if (action == null)
        {
            Framework.Log.Error("[BehaviourQueue] TryActiveDequeue failed: action is null.");
            return false;
        }

        if (IsDisposed)
        {
            return false;
        }

        if (!_isReady)
        {
            return false;
        }

        int index = IndexOfPendingAction(action);
        if (index < 0)
        {
            Framework.Log.Error($"[BehaviourQueue] TryActiveDequeue failed: action not in queue. {action.Name}");
            return false;
        }

        try
        {
            if (!CheckTriggerConditions(action, param))
            {
                return false;
            }

            RemovePendingAt(index, action);
            RunAction(action);
            return true;
        }
        catch (Exception e)
        {
            if (!RemovePendingAt(index, action, "TryActiveDequeue", ActionEndReason.Faulted, e))
                AbortActionSafely(action, "TryActiveDequeue", ActionEndReason.Faulted, e);
            return false;
        }
    }
    
    public ActionCancelResult TryCancelAction(
        IQueuedAction action,
        ActionEndReason endReason = ActionEndReason.Canceled)
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.TryCancelAction");
        if (action == null)
        {
            Framework.Log.Error("[QueuedActionScheduler] TryCancelAction failed: action is null.");
            return ActionCancelResult.Failed;
        }

        if (IsDisposed)
        {
            return ActionCancelResult.Failed;
        }

        if (IsRunningAction(action))
        {
            try
            {
                CancelActionSafely(action);
                RemoveRunningAction(action);

                return ActionCancelResult.CanceledRunning;
            }
            catch (Exception e)
            {
                Framework.Log.Error($"[QueuedActionScheduler] TryCancelAction running action exception. {action.Name}");
                Framework.Log.Error(e);
                return ActionCancelResult.Failed;
            }
        }

        for (int i = pendingActions.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(pendingActions[i], action))
                continue;

            RemovePendingAt(i, action, "TryCancelAction_Queued", endReason);

            return ActionCancelResult.CanceledQueued;
        }

        return ActionCancelResult.NotFound;
    }

    public bool HasRunningActionType(int type)
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.HasRunningActionType");
        for (int i = 0; i < runningActions.Count; i++)
        {
            var action = runningActions[i];
            if (IsActiveRunningAction(action) && action.ActionType == type)
                return true;
        }

        for (int i = 0; i < _runningAddList.Count; i++)
        {
            var action = _runningAddList[i];
            if (IsActiveRunningAction(action) && action.ActionType == type)
                return true;
        }

        return false;
    }

    public bool HasRunningAction<TAction>(Func<TAction, bool> filter = null)
        where TAction : class, IQueuedAction
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.HasRunningAction");
        for (int i = 0; i < runningActions.Count; i++)
        {
            if (runningActions[i] is not TAction action || !IsActiveRunningAction(action))
            {
                continue;
            }

            if (filter == null || filter(action))
                return true;
        }

        for (int i = 0; i < _runningAddList.Count; i++)
        {
            if (_runningAddList[i] is not TAction action || !IsActiveRunningAction(action))
            {
                continue;
            }

            if (filter == null || filter(action))
                return true;
        }

        return false;
    }

    public void GetQueuedActionsByType(int type, ref List<IQueuedAction> results)
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.GetQueuedActionsByType");
        if (results == null)
        {
            Framework.Log.Error("[QueuedActionScheduler] GetQueuedActionsByType failed: results is null.");
            return;
        }

        for (int i = 0; i < pendingActions.Count; i++)
        {
            var action = pendingActions[i];
            if (action != null && action.ActionType == type)
                results.Add(action);
        }
    }

    public bool TryActivateQueuedActionType(int type, int param = 0)
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.TryActivateQueuedActionType");
        if (IsDisposed || !_isReady)
            return false;

        for (int i = 0; i < pendingActions.Count; i++)
        {
            var action = pendingActions[i];
            if (action.ActionType != type)
                continue;
            
            try
            {
                if (!CheckTriggerConditions(action, param))
                {
                    continue;
                }
                RemovePendingAt(i, action);
                RunAction(action);
                return true;
            }
            catch (Exception e)
            {
                if (!RemovePendingAt(i, action, "TryActivateQueuedActionType", ActionEndReason.Faulted, e))
                    AbortActionSafely(action, "TryActivateQueuedActionType", ActionEndReason.Faulted, e);
                return false;
            }
        }

        return false;
    }

    public ActionCancelResult TryCancelActionType(
        int type,
        ActionEndReason endReason = ActionEndReason.Canceled)
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.TryCancelActionType");
        if (IsDisposed)
        {
            return ActionCancelResult.Failed;
        }

        var result = ActionCancelResult.NotFound;

        BeginRunningMutation();
        try
        {
            for (int i = runningActions.Count - 1; i >= 0; i--)
            {
                var action = runningActions[i];
                if (action == null || action.ActionType != type || !IsRunningAction(action))
                    continue;

                CancelActionSafely(action);
                RemoveRunningAction(action);
                result = ActionCancelResult.CanceledRunning;
            }

            for (int i = _runningAddList.Count - 1; i >= 0; i--)
            {
                if (i >= _runningAddList.Count)
                    continue;

                var action = _runningAddList[i];
                if (action == null || action.ActionType != type || !IsRunningAction(action))
                    continue;

                CancelActionSafely(action);
                RemoveRunningAction(action);
                result = ActionCancelResult.CanceledRunning;
            }
        }
        catch (Exception e)
        {
            Framework.Log.Error($"[QueuedActionScheduler] TryCancelActionType running action exception. type={type}");
            Framework.Log.Error(e);
            return ActionCancelResult.Failed;
        }
        finally
        {
            EndRunningMutation();
        }

        for (int i = pendingActions.Count - 1; i >= 0; i--)
        {
            var action = pendingActions[i];
            if (action.ActionType != type)
                continue;

            RemovePendingAt(i, action, "TryCancelActionType_Queued", endReason);

            if (result == ActionCancelResult.NotFound)
                result = ActionCancelResult.CanceledQueued;
        }

        return result;
    }
    
    private bool CheckTriggerConditions(IQueuedAction action, int param)
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.CheckTriggerConditions");
        var conditions = action.ManualActivationConditions;

        if (conditions == null || conditions.Count == 0)
            return true;

        for (int i = 0; i < conditions.Count; i++)
        {
            var condition = conditions[i];

            if (condition == null)
                continue;

            if (!condition.Evaluate(param))
                return false;
        }

        return true;
    }

    public void Tick(float deltaTime)
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.Tick");
        if (IsDisposed || !_isReady) return;

        var shouldDispatch = false;
        BeginRunningMutation();
        try
        {
            for (int i = runningActions.Count - 1; i >= 0; i--)
            {
                var action = runningActions[i];
                if (!IsRunningAction(action))
                    continue;

                try
                {
                    TickActionSafely(action, deltaTime);
                }
                catch (Exception e)
                {
                    Framework.Log.Error("[BehaviourQueue] Running action tick exception.");
                    Framework.Log.Error(e);
                    CancelActionSafely(action);
                }

                if (action == null || action.IsEnded || action.IsDisposed)
                {
                    RemoveRunningAction(action);
                    shouldDispatch = true;
                    if (IsDisposed) return;
                }
            }
        }
        finally
        {
            EndRunningMutation();
        }

        if (shouldDispatch)
            DispatchAvailableActions();
        DispatchAvailableActions();
    }

    private void DispatchAvailableActions()
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.DispatchAvailableActions");
        if (IsDisposed) return;
        if (pendingActions.Count == 0) return;
        if (!EvaluateGlobalDispatchConditions()) return;
        while (!IsDisposed && TryDispatch()) ;
    }

    protected override void OnDispose()
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.OnDispose");
        for (int i = 0; i < pendingActions.Count; i++)
        {
            var action = pendingActions[i];
            AbortActionSafely(action, "QueueDispose", ActionEndReason.Canceled);
        }
        pendingActions.Clear();

        CancelAllRunningActionsSafely();
    }

    private bool TryDispatch()
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.TryDispatch");
        if (pendingActions.Count == 0)
            return false;

        for (int i = 0; i < pendingActions.Count; i++)
        {
            var action = pendingActions[i];
            try
            {
                if (!CheckTags(action) || !CheckConditions(action))
                    continue;
                if (!HandleConflicts(action))
                    continue;

                RemovePendingAt(i, action);
                RunAction(action);
                return true;
            }
            catch (Exception e)
            {
                if (!RemovePendingAt(i, action, "Dispatch", ActionEndReason.Faulted, e))
                    AbortActionSafely(action, "Dispatch", ActionEndReason.Faulted, e);
                return true;
            }
        }
        return false;
    }

    private bool CheckTags(IQueuedAction action)
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.CheckTags");
        if (GlobalTagContextTags == null) return true;
        if (action.RequireTags != null &&
            !action.RequireTags.Match(GlobalTagContextTags))
            return false;

        if (action.BlockTags != null && action.BlockTags.Nodes.Count > 0 &&
            action.BlockTags.Match(GlobalTagContextTags, TagQueryOp.Any))
            return false;

        return true;
    }

    private bool CheckConditions(IQueuedAction action)
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.CheckConditions");
        if (action.Conditions == null) return true;

        for (int i = 0; i < action.Conditions.Count; i++)
        {
            var condition = action.Conditions[i];
            if (condition != null && !condition.Evaluate(0))
                return false;
        }

        return true;
    }

    private void RunAction(IQueuedAction action)
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.RunAction");
        using (QueuedActionBase.ProfileAction("Run", action))
        {
            if (RunActionSafely(action))
            {
                AddRunningAction(action);
            }
        }
    }
    private List<IQueuedAction> _interruptedActionsBuffer = new(InterruptedBufferInitialCapacity);
    private bool HandleConflicts(IQueuedAction incoming, bool ignoreReject = false)
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.HandleConflicts");
        if (runningActions.Count == 0)
            return true;

        try
        {
            _interruptedActionsBuffer.Clear();
            for (int i = 0; i < runningActions.Count; i++)
            {
                var running = runningActions[i];
                if (!IsRunningAction(running)) continue;

                var result = ResolveConflict(incoming, running);

                if (result is ActionConflictResult.Reject && !ignoreReject)
                {
                    return false;
                }

                if (result == ActionConflictResult.Interrupt)
                    _interruptedActionsBuffer.Add(running);
            }

            for (int i = 0; i < _runningAddList.Count; i++)
            {
                var running = _runningAddList[i];
                if (!IsRunningAction(running)) continue;

                var result = ResolveConflict(incoming, running);

                if (result is ActionConflictResult.Reject && !ignoreReject)
                {
                    return false;
                }

                if (result == ActionConflictResult.Interrupt)
                    _interruptedActionsBuffer.Add(running);
            }

            for (int i = 0; i < _interruptedActionsBuffer.Count; i++)
            {
                var interruptedAction = _interruptedActionsBuffer[i];
                CancelActionSafely(interruptedAction);
                RemoveRunningAction(interruptedAction);
            }
            return true;
        }
        finally
        {
            _interruptedActionsBuffer.Clear();
        }
    }

    private bool EvaluateGlobalDispatchConditions()
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.EvaluateGlobalDispatchConditions");
        if (GlobalDispatchConditions == null) return true;
        for (int i = 0; i < GlobalDispatchConditions.Count; i++)
        {
            try
            {
                if (!GlobalDispatchConditions[i].Evaluate(0))
                    return false;
            }
            catch (Exception e)
            {
                Framework.Log.Error("[BehaviourQueue] Global dispatch condition exception.");
                Framework.Log.Error(e);
                return false;
            }
        }
        return true;
    }

    private void InsertByPriority(IQueuedAction action)
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.InsertByPriority");
        int left = 0;
        int right = pendingActions.Count;
        while (left < right)
        {
            int mid = left + ((right - left) >> 1);
            int compare = ComparePriority(action, pendingActions[mid]);
            if (compare < 0)
                right = mid;
            else
                left = mid + 1;
        }
        pendingActions.Insert(left, action);
    }

    private ActionConflictResult ResolveConflict(IQueuedAction incoming, IQueuedAction running)
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.ResolveConflict");
        var conflictRules = ConflictRules;
        if (conflictRules == null)
            return ActionConflictResult.Reject;

        for (int i = 0; i < conflictRules.Length; i++)
        {
            var rule = conflictRules[i];
            if (IsActionTypeMatched(rule.Incoming, incoming.ActionType) &&
                IsActionTypeMatched(rule.Running, running.ActionType))
            {
                return rule.Result;
            }
        }
        return ActionConflictResult.Reject;
    }

    private static bool IsActionTypeMatched(int[] ruleActionTypes, int actionType)
    {
        if (ruleActionTypes == null || ruleActionTypes.Length == 0)
            return true;

        for (int i = 0; i < ruleActionTypes.Length; i++)
        {
            var ruleActionType = ruleActionTypes[i];
            if (ruleActionType == ActionConflictRule.AnyActionType || ruleActionType == actionType)
                return true;
        }

        return false;
    }

    public static int ComparePriority(IQueuedAction a, IQueuedAction b)
    {
        var ret = b.Priority.CompareTo(a.Priority);
        if (ret != 0) return ret;
        ret = b.SubPriority.CompareTo(a.SubPriority);
        if (ret != 0) return ret;
        return a.EnqueueTimeStamp.CompareTo(b.EnqueueTimeStamp);
    }

    private int IndexOfPendingAction(IQueuedAction action)
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.IndexOfPendingAction");
        for (int i = 0; i < pendingActions.Count; i++)
        {
            if (ReferenceEquals(pendingActions[i], action))
                return i;
        }

        return -1;
    }

    private bool RemovePendingAt(int index, IQueuedAction expectedAction)
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.RemovePendingAt");
        if (index < 0 || index >= pendingActions.Count)
            return false;

        if (!ReferenceEquals(pendingActions[index], expectedAction))
            return false;

        pendingActions.RemoveAt(index);
        return true;
    }

    private bool RemovePendingAt(
        int index,
        IQueuedAction expectedAction,
        string stage,
        ActionEndReason endReason,
        Exception e = null)
    {
        if (!RemovePendingAt(index, expectedAction))
            return false;

        AbortActionSafely(expectedAction, stage, endReason, e);
        return true;
    }

    private void BeginRunningMutation()
    {
        _runningMutationDepth++;
    }

    private void EndRunningMutation()
    {
        _runningMutationDepth--;
        if (_runningMutationDepth > 0)
            return;

        FlushRunningActionChanges();
    }

    private void FlushRunningActionChanges()
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.FlushRunningActionChanges");
        for (int i = 0; i < _runningRemoveList.Count; i++)
        {
            RemoveRunningActionImmediately(_runningRemoveList[i]);
        }
        _runningRemoveList.Clear();

        for (int i = 0; i < _runningAddList.Count; i++)
        {
            AddRunningActionImmediately(_runningAddList[i]);
        }
        _runningAddList.Clear();
    }

    private void AddRunningAction(IQueuedAction action)
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.AddRunningAction");
        if (action == null)
            return;

        if (_runningMutationDepth > 0)
        {
            RemoveFromList(_runningRemoveList, action);
            if (!ContainsAction(runningActions, action) && !ContainsAction(_runningAddList, action))
                _runningAddList.Add(action);
            return;
        }

        AddRunningActionImmediately(action);
    }

    private void AddRunningActionImmediately(IQueuedAction action)
    {
        if (action == null || ContainsAction(runningActions, action))
            return;

        runningActions.Add(action);
    }

    private bool RemoveRunningAction(IQueuedAction action)
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.RemoveRunningAction");
        if (action == null)
            return false;

        if (_runningMutationDepth > 0)
        {
            var removedPendingAdd = RemoveFromList(_runningAddList, action);
            var isRunning = ContainsAction(runningActions, action);
            if (isRunning && !ContainsAction(_runningRemoveList, action))
                _runningRemoveList.Add(action);

            return removedPendingAdd || isRunning;
        }

        return RemoveRunningActionImmediately(action);
    }

    private bool RemoveRunningActionImmediately(IQueuedAction action)
    {
        for (int i = runningActions.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(runningActions[i], action))
                continue;

            runningActions.RemoveAt(i);
            return true;
        }

        return false;
    }

    private bool IsRunningAction(IQueuedAction action)
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.IsRunningAction");
        if (action == null)
            return false;

        if (ContainsAction(_runningRemoveList, action))
            return false;

        for (int i = 0; i < runningActions.Count; i++)
        {
            if (ReferenceEquals(runningActions[i], action))
                return true;
        }

        return ContainsAction(_runningAddList, action);
    }

    private bool IsActiveRunningAction(IQueuedAction action)
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.IsActiveRunningAction");
        return IsRunningAction(action) && !action.IsEnded && !action.IsDisposed;
    }

    private static bool ContainsAction(List<IQueuedAction> actions, IQueuedAction action)
    {
        if (actions == null || action == null)
            return false;

        for (int i = 0; i < actions.Count; i++)
        {
            if (ReferenceEquals(actions[i], action))
                return true;
        }

        return false;
    }

    private static bool RemoveFromList(List<IQueuedAction> actions, IQueuedAction action)
    {
        if (actions == null || action == null)
            return false;

        for (int i = actions.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(actions[i], action))
                continue;

            actions.RemoveAt(i);
            return true;
        }

        return false;
    }

    private void CancelAllRunningActionsSafely()
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.CancelAllRunningActionsSafely");
        BeginRunningMutation();
        try
        {
            for (int i = 0; i < runningActions.Count; i++)
            {
                var action = runningActions[i];
                if (!IsRunningAction(action))
                    continue;

                CancelActionSafely(action);
            }

            for (int i = _runningAddList.Count - 1; i >= 0; i--)
            {
                if (i >= _runningAddList.Count)
                    continue;

                var action = _runningAddList[i];
                if (!IsRunningAction(action))
                    continue;

                CancelActionSafely(action);
            }
        }
        finally
        {
            _runningMutationDepth--;
            runningActions.Clear();
            _runningAddList.Clear();
            _runningRemoveList.Clear();
        }
    }

    private static bool RunActionSafely(IQueuedAction action)
    {
        try
        {
            action.Execute();
            return true;
        }
        catch (Exception e)
        {
            LogActionException("Execute", action, e);
            FaultAndDisposeActionSafely(action);
            return false;
        }
    }

    private static void TickActionSafely(IQueuedAction action, float deltaTime)
    {
        if (action == null) return;

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
            FaultAndDisposeActionSafely(action);
            return;
        }

        if (action.IsEnded || action.IsDisposed)
        {
            DisposeActionSafely(action);
        }
    }

    private static void CancelActionSafely(IQueuedAction action)
    {
        if (action == null) return;

        try
        {
            action.End(ActionEndReason.Canceled);
        }
        catch (Exception e)
        {
            LogActionException("End(Canceled)", action, e);
        }

        DisposeActionSafely(action);
    }

    private static void FaultAndDisposeActionSafely(IQueuedAction action)
    {
        if (action == null) return;

        try
        {
            action.End(ActionEndReason.Faulted);
        }
        catch (Exception e)
        {
            LogActionException("End(Faulted)", action, e);
        }

        DisposeActionSafely(action);
    }

    private static void DisposeActionSafely(IQueuedAction action)
    {
        if (action == null) return;

        try
        {
            action.Dispose();
        }
        catch (Exception e)
        {
            LogActionException("Dispose", action, e);
        }
    }

    private static void LogActionException(string stage, IQueuedAction action, Exception e)
    {
        Framework.Log.Error($"[QueuedActionScheduler {stage} Exception] {action?.Name ?? "UnknownAction"}");
        Framework.Log.Error(e);
    }

    private static void  AbortActionSafely(
        IQueuedAction action,
        string stage,
        ActionEndReason endReason,
        Exception e = null)
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.AbortActionSafely");
        using var actionProfiler = QueuedActionBase.ProfileAction("Abort", action);
        if (action == null) return;

        if (e != null)
        {
            Framework.Log.Error($"[BehaviourQueue {stage} Exception] {action.Name}");
            Framework.Log.Error(e);
        }

        try
        {
            action.End(endReason);
        }
        catch (Exception cancelEx)
        {
            Framework.Log.Error($"[BehaviourQueue {stage} End({endReason}) Exception] {action.Name}");
            Framework.Log.Error(cancelEx);
        }

        try
        {
            action.Dispose();
        }
        catch (Exception disposeEx)
        {
            Framework.Log.Error($"[BehaviourQueue {stage} Dispose Exception] {action.Name}");
            Framework.Log.Error(disposeEx);
        }
    }

    public void Clear()
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.Clear");
        for (int i = pendingActions.Count - 1; i >= 0; i--)
        {
            var action = pendingActions[i];
            RemovePendingAt(i, action, "QueueClear", ActionEndReason.Canceled);
        }

        CancelAllRunningActionsSafely();

        _interruptedActionsBuffer.Clear();
    }
}
