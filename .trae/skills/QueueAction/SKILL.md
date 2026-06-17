# QueueAction Skill

当问题涉及游戏表现队列、`QueuedActionScheduler`、`QueuedActionBase`、Action 优先级、冲突规则、手动激活、取消、或 UI 弹窗影响队列时，优先使用本 skill。

## 文档顺序

1. `README.md`
   - 使用说明：如何新增、投递、取消、手动激活 Action。
2. `QueuedActionBase.md`
   - Action 基类：生命周期、属性、推荐写法。
3. `UIManagerRelation.md`
   - UI 弹窗和队列的关系。
4. `SKILL.md`
   - 当前入口和系统地图。

## 核心模型

```text
GamePlayMgr
  -> 持有 PresentationScheduler
  -> EnqueueAction(action)
  -> Tick(deltaTime)

QueuedActionScheduler
  -> pendingActions
  -> runningSlots
  -> 检查 Tag / Condition / ConflictRule

ActionExecutionSlot
  -> Execute()
  -> Tick()
  -> End / Dispose

QueuedActionBase
  -> 业务 Action 的推荐基类
```

## 关键词

- `GamePlayMgr.EnqueueAction`
- `PresentationScheduler`
- `QueuedActionScheduler`
- `QueuedActionBase`
- `ActionExecutionSlot`
- `ActionConflictRule`
- `GameplayActionType`
- `ActionOpenPopupUI`
- `TryActivateQueuedAction`
- `TryCancelActionType`

## 相关源码

- `Assets/Scripts/HotUpdate.Core/Manager/GamePlayMgr.cs`
- `Assets/Scripts/HotUpdate.Core/GameBehaviourQueue/QueuedActionScheduler.cs`
- `Assets/Scripts/HotUpdate.Core/GameBehaviourQueue/QueuedActionBase.cs`
- `Assets/Scripts/HotUpdate.Core/GameBehaviourQueue/IQueuedAction.cs`
- `Assets/Scripts/HotUpdate.Core/GameBehaviourQueue/ActionExecutionSlot.cs`
- `Assets/Scripts/HotUpdate.Core/GameBehaviourQueue/ActionConflictRule.cs`
- `Assets/Scripts/HotUpdate.Core/GameBehaviourQueue/GamePlayActionConflictRules.cs`
- `Assets/Scripts/HotUpdate.Core/GameBehaviourQueue/Actions/ActionOpenPopupUI.cs`
