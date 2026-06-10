# GameBehaviourQueue Risk Points

Use this checklist when changing `Assets/Scripts/HotUpdate.Core/GameBehaviourQueue/` or adding actions that rely on it.

## Lifecycle Risks

- Missing `Finish()` leaves an action running forever and can block lower-priority or conflicting actions.
- Event-backed actions must unsubscribe in `OnDispose`; `ActionOpenPopupUI` is the local pattern.
- `ActionExecutionSlot` disposes actions after `IsEnded` or `IsDisposed`; do not bypass `End`, `Finish`, `Cancel`, or `Dispose`.
- `QueueActionAsyncBase` treats `OperationCanceledException` as completed, not canceled. Be careful if a caller expects cancellation telemetry.
- `async void Execute()` implementations must catch their own exceptions or rely on awaited methods that do not throw unexpectedly. `QueueActionAsyncBase` is safer for reusable async actions.
- `ActionCancelToken` is only created by `QueueActionAsyncBase.EnsureActionCancelToken`; derived actions that use the token directly must ensure it exists.

## Dispatch and Activation Risks

- Scheduler readiness matters. Pending actions will not dispatch until `_isReady` is true.
- `GamePlaySwitchingCheck` blocks normal dispatch while `GamePlayMgr.Instance.IsSwitching`.
- Manual activation ignores normal tags, normal conditions, global dispatch conditions, and conflict checks. Put only trigger-specific checks in `ManualActivationConditions`, and make sure enqueue-time conditions already covered eligibility.
- `TryActivateQueuedActionType` activates the first pending action of that type. Same type plus different business parameters can activate the wrong item if `ManualActivationConditions` are not discriminating enough.
- Direct calls to `PresentationScheduler.Enqueue` bypass `GamePlayMgr.EnqueueAction` but still use scheduler behavior. Check both forms when tracing callers.

## Priority Risks

- Higher numeric `Priority` wins.
- Higher numeric `SubPriority` wins when priority ties.
- Older `EnqueueTimeStamp` wins when both priority values tie.
- Changing `ComparePriority` affects every business action that uses `PopPriority`, not only this folder.
- `PopPriority.MainFuncUnlock` is used by both `MainFunctionUnlockAction` and `MainAuthorityUnlockAction`; use `SubPriority` or manual activation where order matters.

## Conflict Rule Risks

- No matching conflict rule means `Reject`.
- `ConflictRules == null` means `Reject`.
- Empty `Incoming` or `Running` arrays match all action types.
- `ActionConflictRule.AnyActionType` also matches all action types.
- Rule order matters because `ResolveConflict` returns the first match.
- `ExecuteImmediate` calls `HandleConflicts(action, true)`, so reject results do not block immediate actions.
- `Interrupt` cancels running actions after scanning; do not mutate `runningSlots` while iterating elsewhere.

## Tag and Condition Risks

- `RequireTags` and `BlockTags` compare against `QueuedActionScheduler.GlobalTagContextTags`, which is `GamePlayMgr.TagContainer`.
- `GamePlayMgr.SetGameType` removes child tags under `GameplayTags.GameType` and adds one of City, World, or PVE.
- Guide actions add/remove `GameplayTags.Guide` globally while running, so guide-blocked actions must use `BlockTags = new TagQuery(new[] { GameplayTags.Guide }, TagQueryOp.Any)`.
- A `TagQuery` with no nodes matches all; do not rely on an empty query to block dispatch.
- `FuncActionCondition` does not null-check the delegate; never construct it with null.
- `CheckConditions` passes `0`; only manual activation conditions receive the activation parameter.

## Public Contract Risks

- `IQueuedAction` is used by scheduler, slots, guide actions, gameplay actions, and PlayMaker actions. Changes ripple broadly.
- `GameplayActionType` constants are integer IDs. Avoid changing existing numeric values; add new values at the end unless the user explicitly requests migration.
- Adding an action type often requires:
  - a new `GameplayActionType` constant;
  - a matching business action `ActionType`;
  - a `PopPriority` choice in `Assets/Scripts/HotUpdate.Game/PopupQueue/PopPriority.cs` if business ordering needs a named priority;
  - a default conflict rule when it should coexist with, block, or interrupt existing actions;
  - PlayMaker/manual activation updates if guides trigger it by type.

## Files to Avoid

Do not modify these for queue-framework work unless explicitly requested:

- prefab, scene, `.asset`, and other Unity resource files;
- `Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`;
- unrelated business actions under `Assets/Scripts/HotUpdate.Game/GameBehaviourQueue/Actions/` when the task is only about scheduler infrastructure.

## Review Checks

Before finishing a change:

- Search exact references for touched public APIs and action types.
- Verify every new or changed action reaches `Finish`, `Cancel`, or `End(Faulted)`.
- Verify event subscriptions and callbacks are removed in `OnDispose`.
- Verify conflict behavior for action types that can run concurrently.
- Verify tag gates match the intended game state and guide state.
- Verify manual activation parameters cannot wake the wrong queued action.
- Compile the touched C# assembly when feasible.
