# GameBehaviourQueue Call Flow

## Construction and Ready State

1. `new GamePlayMgr()` creates `PresentationScheduler = new QueuedActionScheduler()`.
2. The scheduler receives:
   - `GlobalTagContextTags = TagContainer`
   - `GlobalDispatchConditions = new List<IActionCondition> { new GamePlaySwitchingCheck() }`
   - `ConflictRules = GamePlayActionConflictRules.CreateDefault()`
3. `GamePlayMgr.SetReady(true)` sets `IsEnterGame`, calls `PresentationScheduler.SetReady(true)`, and the scheduler immediately ticks once.
4. `GamePlayMgr.SetReady(false)` calls `PresentationScheduler.Clear()`, canceling pending and running actions.
5. `GamePlayMgrTickDriver.Update()` calls `GamePlayMgr.Tick(Time.deltaTime)`, which calls `PresentationScheduler.Tick(deltaTime)`.

## Normal Enqueue Flow

1. Caller invokes `GamePlayMgr.Instance.EnqueueAction(action)` or directly calls `PresentationScheduler.Enqueue(action)`.
2. `QueuedActionScheduler.Enqueue` validates non-null, stamps `EnqueueTimeStamp = Time.realtimeSinceStartup`, then calls `InsertByPriority`.
3. `InsertByPriority` binary-inserts into `pendingActions` using `ComparePriority`.
4. On scheduler `Tick`, `DispatchAvailableActions()` runs if ready and not disposed.
5. `DispatchAvailableActions()` checks `EvaluateGlobalDispatchConditions()`.
6. `TryDispatch()` scans pending actions from highest priority.
7. For a candidate action, it checks:
   - `CheckTags(action)`
   - `CheckConditions(action)`
   - `HandleConflicts(action)`
8. If all pass, `RemovePendingAt(i, action)` removes it and `RunAction(action)` creates/gets an `ActionExecutionSlot`.
9. `ActionExecutionSlot.RunAction(action)` sets `CurrentAction` and calls `action.Execute()`.

## Immediate Execution Flow

1. Caller invokes `GamePlayMgr.Instance.EnqueueAction(action, true)` or `PresentationScheduler.ExecuteImmediate(action)`.
2. `ExecuteImmediate` validates non-null.
3. It checks `CheckTags` and `CheckConditions`.
4. It calls `HandleConflicts(action, true)`.
5. `ignoreReject = true` means `Reject` conflict results do not block the incoming action, but `Interrupt` still cancels matched running slots.
6. `RunAction(action)` runs the action in a slot immediately.

## Manual Activation Flow

Manual activation is for pending actions that should wait for an explicit trigger or parameter.

`TryActivateQueuedAction(action, param)`:

1. Fails if scheduler is disposed or not ready.
2. Finds the exact pending action instance by reference.
3. Calls `CheckTriggerConditions(action, param)`.
4. Removes the pending action.
5. Runs the action through `RunAction(action)`.

`TryActivateQueuedActionType(type, param)`:

1. Fails if scheduler is disposed or not ready.
2. Finds the first pending action whose `ActionType` matches.
3. Calls `CheckTriggerConditions(action, param)`.
4. Removes and runs it.

Important: manual activation checks `ManualActivationConditions` only. It does not re-check tags, normal `Conditions`, global dispatch conditions, or conflicts at activation time.

## Running Tick and Completion Flow

1. `QueuedActionScheduler.Tick` iterates `runningSlots` from the end.
2. Each slot calls `slot.Tick(deltaTime)`.
3. `ActionExecutionSlot.Tick` calls `action.Tick(deltaTime)`.
4. If `action.IsEnded` or `action.IsDisposed`, the slot calls `DisposeSafely(action)` and clears `CurrentAction`.
5. Scheduler sees empty slots and returns them to `slotPool`.
6. Returning a slot triggers another dispatch attempt, allowing the next pending action to start quickly.

## Cancel, Clear, and Dispose Flow

`TryCancelAction(action)`:

- If action is running, `slot.CancelCurrent()` calls `End(Canceled)` and `Dispose()`.
- If action is pending, `RemovePendingAt(..., endReason)` ends and disposes it.

`TryCancelActionType(type)`:

- Cancels all running actions of that type.
- Cancels all pending actions of that type.
- Returns `CanceledRunning`, `CanceledQueued`, `NotFound`, or `Failed`.

`Clear()`:

- Cancels and disposes every pending action.
- Cancels every running slot and returns slots to the pool.
- Clears the interrupted-slot buffer.

`QueuedActionScheduler.OnDispose()`:

- Cancels/disposes pending actions.
- Cancels running slots.
- Clears `pendingActions`, `runningSlots`, and `slotPool`.

## Conflict Flow

1. `HandleConflicts(incoming)` scans current running slots.
2. `ResolveConflict(incoming, running)` checks `ConflictRules` in array order.
3. A rule matches when both `Incoming` and `Running` match the action types.
4. Empty arrays or `ActionConflictRule.AnyActionType` match all action types.
5. `Reject` blocks normal dispatch unless `ignoreReject` is true.
6. `Interrupt` collects running slots and cancels them after the scan.
7. `Allow` lets the incoming action run alongside the running action.
8. No matching rule defaults to `Reject`.

Default rules currently:

- Anything running against `ActionStuck` rejects.
- Guide against Guide rejects.
- ProgressDisplay against ProgressDisplay rejects.
- TaskProgressDisplay against TaskProgressDisplay rejects.
- ProgressDisplay/TaskProgressDisplay against anything allows.
- Guide/HeroViewUnlockDisplay against OpenedUI allows.
- MainAuthorityUnlock/MainFuncUnlock against Guide allows.
