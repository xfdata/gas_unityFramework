---
name: game-behaviour-queue
description: "Use this skill when the user works on Assets/Scripts/HotUpdate.Core/GameBehaviourQueue/ or mentions GameBehaviourQueue, QueuedActionScheduler, IQueuedAction, QueuedActionBase, QueueActionAsyncBase, ActionExecutionSlot, ActionConflictRule, GamePlayActionConflictRules, GameplayActionType, ActionOpenPopupUI, ActionStuck, priority, tags, conflicts, manual activation, gameplay presentation queue scheduling, or asks to create/add/补充/制作 a queued action/排队action like MainFunctionUnlockAction or MainAuthorityUnlockAction. This skill is for the core gameplay behavior/presentation action queue framework and business actions that rely on it: enqueue/dispatch/tick/cancel/finish/dispose, conflict rules, tag gates, global dispatch conditions, action lifecycle, and action construction workflow. Do not use it for unrelated UI prefab/scene/asset edits, pure popup visual layout, business-only HotUpdate.Game action content, or PopPriority tuning unless the task touches how actions are queued, blocked, activated, canceled, or scheduled by this folder."
---

# Game Behaviour Queue Skill

Use this skill to reason about and safely modify the gameplay presentation action queue under `Assets/Scripts/HotUpdate.Core/GameBehaviourQueue/`.

## First Reads

Read these files before changing queue behavior:

- `QueuedActionScheduler.cs`: queue owner, priority insertion, dispatch loop, conflict handling, cancel/clear/dispose paths.
- `IQueuedAction.cs`: action contract plus `ActionEndReason`, `IActionCondition`, and `GameplayActionType` constants.
- `QueuedActionBase.cs`: default action state, `Finish`, `Cancel`, `End`, `Dispose`, cancellation token, profiler naming.
- `ActionExecutionSlot.cs`: runtime slot wrapper that executes, ticks, cancels, faults, and disposes actions.
- `ActionConflictRule.cs` and `GamePlayActionConflictRules.cs`: conflict result model and default rules.
- `Conditions/*.cs`: `FuncActionCondition` and `GamePlaySwitchingCheck`.
- `Actions/*.cs`: built-in queue actions `ActionOpenPopupUI` and `ActionStuck`.

If the task needs broader context, read references in this skill:

- `references/module-overview.md` for responsibilities and file roles.
- `references/call-flow.md` for enqueue, dispatch, activation, cancel, and dispose flows.
- `references/risk-points.md` for common failure modes and review checks.
- `references/queued-action-workflow.md` when creating or reviewing a business queued action, especially actions modeled on `MainFunctionUnlockAction` or `MainAuthorityUnlockAction`.

## Module Model

Treat this folder as the core scheduler for gameplay presentation actions. Business actions usually live outside this folder and inherit `QueuedActionBase`, for example `Assets/Scripts/HotUpdate.Game/GameBehaviourQueue/Actions/MainFunctionUnlockAction.cs`.

`GamePlayMgr` constructs the scheduler as `PresentationScheduler` with:

- `GlobalTagContextTags = TagContainer`
- `GlobalDispatchConditions = new List<IActionCondition> { new GamePlaySwitchingCheck() }`
- `ConflictRules = GamePlayActionConflictRules.CreateDefault()`

`GamePlayMgr.EnqueueAction(action, isImediate)` routes normal actions to `PresentationScheduler.Enqueue(action)` and immediate actions to `PresentationScheduler.ExecuteImmediate(action)`. `GamePlayMgr.Tick(deltaTime)` drives `PresentationScheduler.Tick(deltaTime)`. `GamePlayMgr.SetReady(false)` clears the scheduler.

## Lifecycle Rules

When adding or reviewing an action:

1. Set `ActionType` to an existing or newly-added `GameplayActionType` constant.
2. Set `Priority` and, when same-priority ordering matters, `SubPriority`.
3. Use `RequireTags` and `BlockTags` for gameplay-state gating. City-only actions commonly use `RequireTags = new TagQuery(new[] { GameplayTags.GameType_City }, TagQueryOp.All)`. Guide-blocked actions often use `BlockTags = new TagQuery(new[] { GameplayTags.Guide }, TagQueryOp.Any)`.
4. Put automatic dispatch checks in `Conditions`; put guide/manual trigger checks in `ManualActivationConditions`.
5. In `Execute`, call `Finish()` when the presentation is complete. If waiting on events, unsubscribe in `OnDispose`.
6. For async actions, prefer `QueueActionAsyncBase.OnExecute()` when the action can be expressed as a `UniTask`; it catches exceptions and finishes/faults consistently.
7. Never assume `Execute()` runs only once after enqueue: immediate actions call through `ExecuteImmediate`, and manual activation can remove a pending action then run it.

## Scheduler Semantics

Priority order is high `Priority` first, then high `SubPriority`, then older `EnqueueTimeStamp` first. `QueuedActionScheduler.ComparePriority` implements this.

Dispatch is gated in this order:

1. scheduler is ready and not disposed;
2. global dispatch conditions pass, especially `GamePlaySwitchingCheck`;
3. action tag queries pass against `GlobalTagContextTags`;
4. action `Conditions` pass;
5. conflict rules allow or interrupt running actions.

Manual activation uses `TryActivateQueuedAction` or `TryActivateQueuedActionType`. It checks only `ManualActivationConditions` through `CheckTriggerConditions`, removes the pending action, and runs it. It does not re-run normal `Conditions`, tag checks, or conflict handling.

Conflict rules default to `Reject` when no rule matches or `ConflictRules` is null. Add explicit rules in `GamePlayActionConflictRules.CreateDefault()` when a new action type must coexist with or interrupt running actions.

## Safe Change Workflow

Use this workflow for edits in this module:

1. Identify whether the change is scheduler behavior, an action contract change, a conflict rule, a condition, or a built-in action.
2. Search exact class/API references before changing public members such as `IQueuedAction`, `GameplayActionType`, `Enqueue`, `ExecuteImmediate`, `TryActivateQueuedActionType`, or `TryCancelActionType`.
3. Preserve disposal semantics: ended or disposed actions must eventually leave their `ActionExecutionSlot`, and event subscriptions must be cleaned in `OnDispose`.
4. Preserve queue ordering unless the task explicitly changes priority semantics.
5. When adding a new `GameplayActionType`, check business actions and `GamePlayActionConflictRules.CreateDefault()` for required conflicts.
6. Do not edit prefab, scene, `.asset`, `Library`, `Temp`, `Obj`, `Build`, or `Logs` for queue framework tasks unless the user explicitly asks.

## Queued Action Creation Workflow

When the user asks to add a new queued action, follow `references/queued-action-workflow.md`. The short version is:

1. Choose the business action file location, usually `Assets/Scripts/HotUpdate.Game/GameBehaviourQueue/Actions/`.
2. Inherit `QueuedActionBase` for a simple presentation action, or `QueueActionAsyncBase` if `OnExecute()` can own the async flow.
3. In the constructor, set `ActionType`, `Priority`, optional `SubPriority`, payload fields, `RequireTags`/`BlockTags`, `Conditions`, and optional `ManualActivationConditions`.
4. In `Execute`, stop mild guide if this presentation should take focus, run the mediator/effect, invoke completion callback if the example pattern needs it, then call `Finish()`.
5. Add or verify enqueue call sites through `GamePlayMgr.Instance.EnqueueAction(action)` or `PresentationScheduler.Enqueue(action)`.
6. Add `GameplayActionType`, `PopPriority`, and conflict rules only when the new behavior needs new queue identity or ordering.

## Good Examples

`MainFunctionUnlockAction` shows a typical business action:

- inherits `QueuedActionBase`;
- sets `ActionType = GameplayActionType.MainFuncUnlock`;
- uses `Priority = PopPriority.MainFuncUnlock`;
- uses config rank as `SubPriority`;
- requires city gameplay tags;
- blocks during guide unless guide unlock conditions match;
- uses `ManualActivationConditions` for ordered manual activation;
- awaits a mediator and calls `Finish()`.

`MainAuthorityUnlockAction` shows the same city/guide gate without manual activation:

- inherits `QueuedActionBase`;
- sets `ActionType = GameplayActionType.MainAuthorityUnlock`;
- uses `Priority = PopPriority.MainFuncUnlock`;
- reads `NativeConfigIntf.mainui_button` to decide `isGuideShow`;
- requires city gameplay tags;
- checks guide unlock nodes or `!GuideMgr.Instance.IsInGuide`;
- awaits `AuthorityUnlockPopupMediator`;
- invokes `OnDisplayend?.Invoke()` and then calls `Finish()`.

`ActionOpenPopupUI` shows an event-backed immediate action:

- wraps a popup `MediatorBase`;
- sets `ActionType = GameplayActionType.OpenedUI`;
- finishes when `mediator.OnMediatorDisposeEnd` fires;
- unsubscribes in `OnDispose`.

`ActionStuck` is a deliberate blocking action:

- immediate by default;
- defaults to `GameplayActionType.ActionStuck` through `QueuedActionBase`;
- finishes only when `TriggerEnd()` invokes its private finish callback.
