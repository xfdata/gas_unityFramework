# UIManager 与 QueueAction 的关系

这份文档只讲一件事：**UI 弹窗如何进入 Action 队列，并影响其他表现调度。**

## 1. 核心结论

`UIManager` 创建弹窗时，会创建一个 `ActionOpenPopupUI`，并立即投递到：

```csharp
GamePlayMgr.Instance.PresentationScheduler
```

这个 Action 的类型是：

```csharp
GameplayActionType.OpenedUI
```

它会一直 running，直到对应 Mediator 完整释放结束。

## 2. 关系图

```text
UIManager.AttachView(...)
  -> mediator.IsPopupWin == true
  -> new ActionOpenPopupUI(mediator)
  -> GamePlayMgr.Instance.EnqueueAction(action, true)
  -> QueuedActionScheduler.ExecuteImmediate(action)
  -> runningSlots 持有 OpenedUI

Mediator 关闭
  -> MediatorBase.OnDisposeFinally()
  -> OnMediatorDisposeEnd
  -> ActionOpenPopupUI.Finish()
  -> Slot 释放 Action
```

## 3. ActionOpenPopupUI 做什么

源码：

```text
Assets/Scripts/HotUpdate.Core/GameBehaviourQueue/Actions/ActionOpenPopupUI.cs
```

行为：

- 构造时保存 mediator。
- 设置 `ActionType = GameplayActionType.OpenedUI`。
- 设置 `IsImmediate = true`。
- `Execute()` 中监听 `mediator.OnMediatorDisposeEnd += Finish`。
- `OnDispose()` 中解绑。

它不负责打开 UI。UI 已经由 `UIManager` 创建。它只负责告诉队列：**现在有弹窗正在展示**。

## 4. 为什么要这样做

因为其他表现需要知道当前是否有弹窗。

例如：

- 引导是否能在弹窗上继续。
- 功能解锁是否要等弹窗关闭。
- 奖励展示是否能和弹窗并行。
- 某些表现是否要被弹窗阻塞。

这些都通过 `QueuedActionScheduler` 的冲突规则判断。

## 5. 默认冲突规则

默认规则里允许：

```text
Guide 遇到 OpenedUI -> Allow
HeroViewUnlockDisplay 遇到 OpenedUI -> Allow
```

没有规则匹配时默认：

```text
Reject
```

所以如果某个 Action 在弹窗期间不执行，优先检查它和 `OpenedUI` 是否有冲突规则。

## 6. ExecuteImmediate 的影响

弹窗 Action 用立即执行：

```csharp
GamePlayMgr.Instance.EnqueueAction(openPopupUI, true);
```

特点：

- 不进入 pending。
- 不检查全局派发条件。
- 检查 Action 自己的 Tag 和 Conditions。
- 冲突处理时忽略 `Reject`，但仍处理 `Interrupt`。

这保证弹窗状态能立刻登记为 running。

## 7. 常见问题

### Action 为什么没执行？

先查是否有弹窗 running：

```csharp
GamePlayMgr.Instance.PresentationScheduler
    .HasRunningActionType(GameplayActionType.OpenedUI)
```

如果有，再查 `GamePlayActionConflictRules`。

### 弹窗关闭了，队列还像被占着？

检查：

- Mediator 是否正常 Dispose。
- `OnMediatorDisposeEnd` 是否触发。
- `ActionOpenPopupUI` 是否仍监听。
- 是否走了非标准关闭路径。

### 新 Action 要在弹窗期间执行？

加冲突规则：

```csharp
new()
{
    Incoming = new int[] { GameplayActionType.YourActionType },
    Running = new int[] { GameplayActionType.OpenedUI },
    Result = ActionConflictResult.Allow,
}
```

如果要等待弹窗关闭，不加规则即可，因为默认 `Reject`。

## 8. 分工

- `UIManager`：真实创建和关闭 UI。
- `MediatorBase`：触发生命周期事件。
- `ActionOpenPopupUI`：把弹窗生命周期映射为 running Action。
- `QueuedActionScheduler`：根据 running `OpenedUI` 判断其他 Action 是否能执行。
