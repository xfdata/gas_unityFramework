# Queued Action Creation Workflow

Use this reference when creating or reviewing a business queued action that relies on `Assets/Scripts/HotUpdate.Core/GameBehaviourQueue/`.

Primary examples:

- `Assets/Scripts/HotUpdate.Game/GameBehaviourQueue/Actions/MainFunctionUnlockAction.cs`
- `Assets/Scripts/HotUpdate.Game/GameBehaviourQueue/Actions/MainAuthorityUnlockAction.cs`

## 1. Decide Whether This Should Be a Queued Action

Use a queued action when the behavior is a gameplay presentation that must be ordered, blocked, manually activated, canceled, or coordinated with other presentations. Common examples are unlock popups, guide-bound displays, progress presentations, settlement popups, and temporary queue blockers.

Do not make it a queued action for pure data changes, passive UI refreshes, or view code that does not need scheduler ordering/lifecycle control.

## 2. Pick the Base Class

Use `QueuedActionBase` when the action has custom `Execute()` behavior or follows the existing `async void Execute()` mediator pattern.

Use `QueueActionAsyncBase` when the action can cleanly put all async work into `protected override UniTask OnExecute()`. This gives consistent success, cancellation, exception logging, and `Faulted` handling.

For event-backed actions, inherit `QueuedActionBase` and unsubscribe in `OnDispose`, like `ActionOpenPopupUI`.

## 3. Define the Queue Identity

In the constructor, set:

```csharp
ActionType = GameplayActionType.MainFuncUnlock;
Priority = PopPriority.MainFuncUnlock;
SubPriority = data.rank;
```

Use an existing `GameplayActionType` only when the new action should be treated as the same queue category by conflict, cancel, and manual activation APIs.

Add a new `GameplayActionType` constant when callers need to cancel/query/activate it independently or when conflict behavior differs from existing actions. If adding a new type, review `GamePlayActionConflictRules.CreateDefault()`.

Use `PopPriority` for cross-action ordering. Use `SubPriority` for ordering within the same priority, like `MainFunctionUnlockAction` using config `rank`.

## 4. Store Payload and Config Data

Capture only the data needed to execute the presentation:

```csharp
this.buttonName = cfgName;
this.OnDisplayend = OnDisplayend;
this.OnBeginFly = OnBeginFly;
```

Read config in the constructor when it decides queue order or gating:

```csharp
NativeConfigIntf.mainui_display.GetPrimaryKey(cfgName, out var data);
SubPriority = data.rank;
_order = data.order;
```

`MainAuthorityUnlockAction` reads `NativeConfigIntf.mainui_button` to compute `isGuideShow`, then stores `buttonName`, `mainNode`, and `OnDisplayend`.

## 5. Add Gameplay Tag Gates

Use `RequireTags` when the action should only run in a gameplay state:

```csharp
RequireTags = new TagQuery(new[] { GameplayTags.GameType_City }, TagQueryOp.All);
```

Use `BlockTags` when a global state should prevent dispatch:

```csharp
BlockTags = new TagQuery(new[] { GameplayTags.Guide }, TagQueryOp.Any);
```

The scheduler compares these queries against `GamePlayMgr.TagContainer`.

## 6. Add Normal Conditions

Use `Conditions` for automatic dispatch checks. Both example actions gate guide behavior this way:

```csharp
Conditions.Add(new FuncActionCondition((p) =>
{
    if (isGuideShow)
    {
        return MainUIDataProxy.Instance.GuideUnlockNodes.Contains(buttonName);
    }
    else
        return !GuideMgr.Instance.IsInGuide;
}));
```

This means:

- guide-show actions wait until the node is in `MainUIDataProxy.Instance.GuideUnlockNodes`;
- non-guide-show actions wait until no guide is running.

Normal `Conditions` receive `0`; do not depend on `p` here unless the scheduler changes.

## 7. Add Manual Activation Only When Needed

Use `ManualActivationConditions` when a queued action must wait for an explicit activation parameter:

```csharp
ManualActivationConditions.Add(new FuncActionCondition((param) => _order == param));
```

`MainFunctionUnlockAction` uses this to activate unlock displays in config order. `MainAuthorityUnlockAction` does not use manual activation, so it can dispatch automatically after tags, conditions, and conflicts pass.

Be careful: manual activation APIs check only `ManualActivationConditions`; they do not re-check normal `Conditions`, tags, global dispatch conditions, or conflicts.

## 8. Implement Execute

The common mediator pattern is:

```csharp
public override async void Execute()
{
    GuideMgr.Instance.StopMildGuide();
    await WaitableUtil.RunMeditor<FunctionUnlockPopupMediator>((buttonName, OnDisplayend, OnBeginFly));
    Finish();
}
```

For authority unlock:

```csharp
public override async void Execute()
{
    GuideMgr.Instance.StopMildGuide();
    await WaitableUtil.RunMeditor<AuthorityUnlockPopupMediator>((buttonName, mainNode, OnDisplayend));
    OnDisplayend?.Invoke();
    Finish();
}
```

Call `GuideMgr.Instance.StopMildGuide()` when the presentation should take focus over mild guide prompts.

Call `Finish()` after all awaited presentation work and required callbacks are done. Missing `Finish()` leaves the action running.

## 9. Wire the Enqueue Site

Normal enqueue:

```csharp
GamePlayMgr.Instance?.EnqueueAction(new MainAuthorityUnlockAction(_nodeName, GetMainNode(), onDisplayEnd));
```

Direct scheduler enqueue also appears in existing UI code:

```csharp
var presentationScheduler = GamePlayMgr.Instance.PresentationScheduler;
presentationScheduler.Enqueue(new MainFunctionUnlockAction(_nodeName, buttonData.guideShow == 1, onDisplayEnd, GetMainNode));
```

Use immediate enqueue only for actions that should run immediately through `ExecuteImmediate`, such as `ActionOpenPopupUI` or `ActionStuck` patterns:

```csharp
GamePlayMgr.Instance.EnqueueAction(action, true);
```

## 10. Review Conflict and Cancel Behavior

After adding a new action, check:

- Does it need a unique `GameplayActionType`?
- Can it run while `GameplayActionType.Guide` or `GameplayActionType.OpenedUI` is running?
- Should duplicate actions of the same type reject, allow, or interrupt?
- Do callers need `TryCancelActionType`, `HasRunningActionType`, or `TryActivateQueuedActionType`?

If yes, update `GamePlayActionConflictRules.CreateDefault()` and any calling code that uses type IDs.

## 11. Final Checklist

- Constructor sets `ActionType` and `Priority`.
- `SubPriority` is set when same-priority order matters.
- Required payload fields are stored before conditions use them.
- `RequireTags`/`BlockTags` match intended gameplay state.
- `Conditions` use live state safely and do not pass null delegates to `FuncActionCondition`.
- `ManualActivationConditions` are specific enough to avoid activating the wrong pending action.
- `Execute()` always reaches `Finish()` or another end path.
- Event subscriptions are removed in `OnDispose`.
- New type IDs do not alter existing `GameplayActionType` numeric values.
- Enqueue site uses normal or immediate mode intentionally.
