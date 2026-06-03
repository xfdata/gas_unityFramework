using System;
using System.Collections.Generic;

public class QueuedActionScheduler : Disposable
{
    private const int PendingInitialCapacity = 32;
    private const int RunningInitialCapacity = 4;
    private const int InterruptedBufferInitialCapacity = 4;

    private Framework.ObjectPool<ActionExecutionSlot> slotPool = new()
    {
        CreateAction = null,
        DisposeAction = (slot) => slot.CancelCurrent(),
    };
    private List<IQueuedAction> pendingActions = new(PendingInitialCapacity);
    private List<ActionExecutionSlot> runningSlots = new(RunningInitialCapacity);

    public ActionConflictRule[] ConflictRules;
    public GameplayTagContainer GlobalTagContextTags;
    public List<IActionCondition> GlobalDispatchConditions;

    private bool _isReady = false;
    public int PendingCount => pendingActions.Count;
    public int RunningCount => runningSlots.Count;

    public bool HasRunningSlots => runningSlots.Count > 0;

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

        for (int i = runningSlots.Count - 1; i >= 0; i--)
        {
            var slot = runningSlots[i];

            if (!ReferenceEquals(slot.CurrentAction, action))
                continue;

            try
            {
                slot.CancelCurrent();

                if (slot.IsEmpty)
                {
                    ReturnSlotAt(i);
                }

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
        for (int i = 0; i < runningSlots.Count; i++)
        {
            var action = runningSlots[i].CurrentAction;
            if (action != null && action.ActionType == type && !action.IsEnded && !action.IsDisposed)
                return true;
        }

        return false;
    }

    public bool HasRunningAction<TAction>(Func<TAction, bool> filter = null)
        where TAction : class, IQueuedAction
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.HasRunningAction");
        for (int i = 0; i < runningSlots.Count; i++)
        {
            if (runningSlots[i].CurrentAction is not TAction action ||
                action.IsEnded ||
                action.IsDisposed)
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

        for (int i = runningSlots.Count - 1; i >= 0; i--)
        {
            var slot = runningSlots[i];
            var action = slot.CurrentAction;

            if (action == null || action.ActionType != type)
                continue;

            try
            {
                slot.CancelCurrent();

                if (slot.IsEmpty)
                {
                    ReturnSlotAt(i);
                }

                result = ActionCancelResult.CanceledRunning;
            }
            catch (Exception e)
            {
                Framework.Log.Error($"[QueuedActionScheduler] TryCancelActionType running action exception. type={type}");
                Framework.Log.Error(e);
                return ActionCancelResult.Failed;
            }
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

        for (int i = runningSlots.Count - 1; i >= 0; i--)
        {
            var slot = runningSlots[i];
            try
            {
                // using var actionProfiler = QueuedActionBase.ProfileAction("SlotTick", slot.CurrentAction);
                slot.Tick(deltaTime);
            }
            catch (Exception e)
            {
                Framework.Log.Error("[BehaviourQueue] Slot tick exception.");
                Framework.Log.Error(e);
                slot.CancelCurrent();
            }

            if (slot.IsEmpty)
            {
                ReturnSlotAt(i);
                DispatchAvailableActions();
                if (IsDisposed) return;
            }
        }

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

        for (int i = 0; i < runningSlots.Count; i++)
        {
            var slot = runningSlots[i];
            slot.CancelCurrent();
        }
        runningSlots.Clear();
        slotPool.Clear();
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
            var slot = slotPool.Get();
            slot.RunAction(action);
            runningSlots.Add(slot);
        }
    }
    private List<ActionExecutionSlot> _interruptedSlotsBuffer = new(InterruptedBufferInitialCapacity);
    private bool HandleConflicts(IQueuedAction incoming, bool ignoreReject = false)
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.HandleConflicts");
        if (runningSlots.Count == 0)
            return true;

        try
        {
            _interruptedSlotsBuffer.Clear();
            for (int i = 0; i < runningSlots.Count; i++)
            {
                var slot = runningSlots[i];
                var running = slot.CurrentAction;
                if (running == null) continue;

                var result = ResolveConflict(incoming, running);

                if (result is ActionConflictResult.Reject && !ignoreReject)
                {
                    return false;
                }

                if (result == ActionConflictResult.Interrupt)
                    _interruptedSlotsBuffer.Add(slot);
            }

            for (int i = 0; i < _interruptedSlotsBuffer.Count; i++)
                _interruptedSlotsBuffer[i].CancelCurrent();
            return true;
        }
        finally
        {
            _interruptedSlotsBuffer.Clear();
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

    private void ReturnSlotAt(int index)
    {
        using var profiler = new Framework.AutoProfiler("BehaviourQueue.Scheduler.ReturnSlotAt");
        var slot = runningSlots[index];
        runningSlots.RemoveAt(index);
        slotPool.Return(slot);
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

        for (int i = runningSlots.Count - 1; i >= 0; i--)
        {
            var slot = runningSlots[i];
            slot.CancelCurrent();
            ReturnSlotAt(i);
        }

        _interruptedSlotsBuffer.Clear();
    }
}
