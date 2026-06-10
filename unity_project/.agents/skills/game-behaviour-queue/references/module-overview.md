# GameBehaviourQueue Module Overview

Target folder: `Assets/Scripts/HotUpdate.Core/GameBehaviourQueue/`

This folder implements the core gameplay behavior/presentation queue. It does not own most business presentations; it owns the scheduler, action contract, common lifecycle behavior, conflict rules, tag/condition gates, and two built-in queue actions.

## Responsibilities

- Hold pending gameplay presentation actions.
- Sort pending actions by `Priority`, `SubPriority`, then enqueue timestamp.
- Dispatch actions while `GamePlayMgr` is ready.
- Gate dispatch through gameplay tags and action/global conditions.
- Resolve conflicts between incoming and running action types.
- Tick running actions and dispose them after completion/cancel/fault.
- Provide manual activation for queued actions that should wait for explicit triggers.
- Provide immediate actions for popup tracking and deliberate queue blocking.

## Main Types

- `IQueuedAction`: action interface used by the scheduler and execution slots.
- `IActionCondition`: integer-parameter condition interface used by normal and manual activation conditions.
- `ActionEndReason`: `None`, `Completed`, `Canceled`, `Faulted`.
- `GameplayActionType`: integer constants for queue categories such as `OpenedUI`, `ActionStuck`, `Guide`, `MainFuncUnlock`, `MainAuthorityUnlock`, `ProgressDisplay`, `PaluEvolution`.
- `QueuedActionBase`: default implementation of `IQueuedAction`, state flags, profiler scope, cancellation token, `Finish`, `Cancel`, `End`, `Dispose`, and `OnDispose`.
- `QueueActionAsyncBase`: async template action that awaits `OnExecute()`, finishes on success/cancellation, and faults on exception.
- `QueuedActionScheduler`: pending list, running slots, ready state, enqueue/immediate/manual activation/cancel/clear/tick/dispatch/conflict logic.
- `ActionExecutionSlot`: wrapper around one running action; calls `Execute`, `Tick`, cancellation/fault/end/dispose.
- `ActionConflictRule`: serializable rule with `Incoming`, `Running`, and `Result`.
- `ActionConflictResult`: `Allow`, `Interrupt`, `Reject`.
- `ActionCancelResult`: cancel API result enum.
- `GamePlayActionConflictRules`: default conflict table for presentation scheduler.
- `FuncActionCondition`: wraps `Func<int, bool>`.
- `GamePlaySwitchingCheck`: blocks global dispatch while `GamePlayMgr.Instance.IsSwitching`.
- `ActionOpenPopupUI`: immediate action that stays running while a popup mediator is open.
- `ActionStuck`: immediate blocking action released through `TriggerEnd()`.

## External Entry Points

- `GamePlayMgr` owns `PresentationScheduler`.
- `GamePlayMgr.EnqueueAction(IQueuedAction action, bool isImediate = false)` routes actions into the scheduler.
- `GamePlayMgr.Tick(float deltaTime)` calls scheduler `Tick`.
- `GamePlayMgr.SetReady(bool isEnter)` turns dispatch on/off; `false` also clears queued/running actions.
- `UIManager` wraps popup mediators in `ActionOpenPopupUI` and enqueues them immediately when `mediator.IsPopupWin`.
- Guide systems use `GuideQueueAction : QueuedActionBase` and add/remove `GameplayTags.Guide` on `GamePlayMgr.TagContainer`.

## File Roles

Entry/framework files:

- `QueuedActionScheduler.cs`
- `IQueuedAction.cs`
- `QueuedActionBase.cs`
- `QueueActionAsyncBase.cs`
- `ActionExecutionSlot.cs`

Configuration/rules:

- `ActionConflictRule.cs`
- `GamePlayActionConflictRules.cs`
- `GameplayActionType` constants inside `IQueuedAction.cs`

Conditions:

- `Conditions/FuncActionCondition.cs`
- `Conditions/GamePlaySwitchingCheck.cs`

Built-in actions:

- `Actions/ActionOpenPopupUI.cs`
- `Actions/ActionStuck.cs`

Generated Unity metadata:

- `*.meta` files. Do not edit these for queue logic changes.

## Related Business Examples

`Assets/Scripts/HotUpdate.Game/GameBehaviourQueue/Actions/MainFunctionUnlockAction.cs` demonstrates city-only, guide-aware, manually activated behavior:

- `ActionType = GameplayActionType.MainFuncUnlock`
- `Priority = PopPriority.MainFuncUnlock`
- `SubPriority = data.rank`
- `RequireTags = GameplayTags.GameType_City`
- `Conditions` check guide state and unlock nodes
- `ManualActivationConditions` check order parameter
- `Execute` awaits `FunctionUnlockPopupMediator` and then calls `Finish()`

`Assets/Scripts/HotUpdate.Game/GameBehaviourQueue/Actions/MainAuthorityUnlockAction.cs` is similar but uses `GameplayActionType.MainAuthorityUnlock` and no manual activation condition.
