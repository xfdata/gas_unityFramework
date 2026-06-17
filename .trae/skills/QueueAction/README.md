# QueueAction 使用说明

QueueAction 是游戏表现调度系统。入口是：

```csharp
GamePlayMgr.Instance.EnqueueAction(action);
```

底层由：

```csharp
GamePlayMgr.Instance.PresentationScheduler
```

也就是 `QueuedActionScheduler` 管理。

## 1. 适用场景

适合：

- UI 弹窗表现。
- 引导流程。
- 功能解锁表现。
- 奖励展示。
- 结算展示。
- 需要排队、并行、互斥、打断的表现。

不适合：

- 纯数据计算。
- 必须同步返回结果的逻辑。
- 不参与表现冲突的普通业务方法。

## 2. 最小 Action

```csharp
public class MyAction : QueuedActionBase
{
    public MyAction()
    {
        ActionType = GameplayActionType.ProgressDisplay;
        Priority = 100;
    }

    public override void Execute()
    {
        // 启动表现
        Finish();
    }

    protected override void OnDispose()
    {
        // 解绑事件、清理资源
    }
}
```

投递：

```csharp
GamePlayMgr.Instance.EnqueueAction(new MyAction());
```

立即执行：

```csharp
GamePlayMgr.Instance.EnqueueAction(new MyAction(), true);
```

## 3. 普通执行流程

```text
EnqueueAction
  -> QueuedActionScheduler.Enqueue
  -> 按优先级进入 pendingActions
  -> Tick
  -> CheckTags
  -> CheckConditions
  -> HandleConflicts
  -> RunAction
  -> ActionExecutionSlot.RunAction
  -> action.Execute
```

执行中：

```text
ActionExecutionSlot.Tick
  -> action.Tick
  -> action.IsEnded / IsDisposed
  -> action.Dispose
  -> slot 回收
```

## 4. 必须 Finish

Action 不会自动完成。表现结束后必须调用：

```csharp
Finish();
```

否则 Action 会一直处于 running，影响后续冲突判断。

取消时调用：

```csharp
Cancel();
```

## 5. 优先级

排序规则：

```text
Priority 高的先执行
SubPriority 高的先执行
EnqueueTimeStamp 早的先执行
```

即：

```text
Priority desc -> SubPriority desc -> EnqueueTimeStamp asc
```

## 6. 条件和标签

Action 可配置：

- `RequireTags`
- `BlockTags`
- `Conditions`
- `ManualActivationConditions`

普通条件示例：

```csharp
Conditions.Add(new FuncActionCondition(() => CanShow()));
```

全局调度条件默认包含：

```csharp
new GamePlaySwitchingCheck()
```

所以 `GamePlayMgr.Instance.IsSwitching == true` 时，pending Action 不会被普通派发。

## 7. 立即执行

```csharp
GamePlayMgr.Instance.EnqueueAction(action, true);
```

特点：

- 不进入 pending。
- 不检查全局派发条件。
- 会检查 Action 自己的 Tag 和 Conditions。
- 冲突处理忽略 `Reject`，但仍处理 `Interrupt`。

UI 弹窗的 `ActionOpenPopupUI` 就是立即执行。

## 8. 手动激活

按实例：

```csharp
PresentationScheduler.TryActivateQueuedAction(action);
```

只检查 `ManualActivationConditions`。

按类型：

```csharp
PresentationScheduler.TryActivateQueuedActionType(type, param);
```

会检查：

- `IManualActivatedQueuedAction.ManualCondition(param)`
- Tag
- Conditions
- ConflictRules

## 9. 取消

取消实例：

```csharp
PresentationScheduler.TryCancelAction(action);
```

取消类型：

```csharp
PresentationScheduler.TryCancelActionType(GameplayActionType.PaluEvolution);
```

返回：

- `CanceledRunning`
- `CanceledQueued`
- `NotFound`
- `Failed`

## 10. 冲突规则

默认规则在：

```text
GamePlayActionConflictRules.CreateDefault()
```

结果：

- `Allow`：允许并行。
- `Interrupt`：取消 running，让 incoming 执行。
- `Reject`：拒绝 incoming。

没有规则匹配时默认 `Reject`。

新增 Action 类型后，如果希望它能和某些 running Action 并行，必须补规则。

## 11. 常见坑

- 忘记 `Finish()`。
- 新 ActionType 没有冲突规则。
- `ExecuteImmediate` 会忽略 `Reject`。
- `TryActivateQueuedAction` 和 `TryActivateQueuedActionType` 检查条件不同。
- `SetReady(false)` 会清空 pending 和 running。
- 清理监听要写在 `OnDispose()`，不能只依赖正常完成路径。

## 12. UI 弹窗相关

如果问题和 UI 弹窗有关，先看：

```text
UIManagerRelation.md
```

弹窗会变成 running `OpenedUI` Action，可能挡住其他 Action。
