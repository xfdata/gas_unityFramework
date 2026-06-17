# QueuedActionBase

`QueuedActionBase` 是业务 Action 的推荐基类，源码在：

```text
Assets/Scripts/HotUpdate.Core/GameBehaviourQueue/QueuedActionBase.cs
```

## 1. 角色定位

```text
IQueuedAction
  -> QueuedActionBase
      -> 业务 Action
```

它提供：

- 名称和类型。
- 优先级。
- Tag 和条件。
- 结束状态。
- CancelToken 清理。
- Dispose 模板。
- profiler 包装。

## 2. 默认值

构造后默认：

- `Name = GetType().Name`
- `ActionType = GameplayActionType.ActionStuck`
- `Tags = new GameplayTagContainer()`
- `Conditions = new List<IActionCondition>()`
- `ManualActivationConditions = new List<IActionCondition>()`

注意：业务 Action 一定要设置自己的 `ActionType`。默认 `ActionStuck` 会在冲突规则中阻止其他 Action。

## 3. 关键属性

- `Name`：日志和 profiler 名称。
- `ActionType`：冲突、查询、取消的类型。
- `Priority`：主优先级。
- `SubPriority`：次优先级。
- `EnqueueTimeStamp`：入队时由调度器设置。
- `RequireTags`：需要全局上下文满足。
- `BlockTags`：命中全局上下文时阻止执行。
- `Conditions`：普通派发条件。
- `ManualActivationConditions`：手动激活条件。
- `IsEnded` / `EndReason` / `IsCompleted` / `IsCanceled` / `IsDisposed`：状态。

## 4. 生命周期

```text
Scheduler.RunAction
  -> Slot.RunAction
  -> Execute()

每帧
  -> Slot.Tick
  -> Tick(deltaTime)

结束
  -> Finish() / Cancel() / End(...)
  -> Slot 发现 IsEnded
  -> Dispose()
  -> OnDispose()
```

## 5. Execute

`Execute()` 在 Action 开始运行时调用一次。

常见写法：

```csharp
public override void Execute()
{
    if (!CanShow())
    {
        Cancel();
        return;
    }

    StartDisplay();
}
```

如果没有异步表现，直接调用 `Finish()`。

## 6. Tick

`Tick(deltaTime)` 在 running 状态每帧调用。

如果需要计时或轮询，可以重写：

```csharp
public override void Tick(float deltaTime)
{
    base.Tick(deltaTime);
    if (IsEnded || IsDisposed) return;

    _elapsed += deltaTime;
    if (_elapsed > 10f)
        Cancel();
}
```

## 7. Finish / Cancel / End

正常完成：

```csharp
Finish();
```

取消：

```csharp
Cancel();
```

底层都是：

```csharp
End(ActionEndReason reason);
```

`End` 有幂等保护，已经结束或释放后不会重复处理。

## 8. Dispose

`Dispose()` 会：

- 设置 disposed。
- Cancel 并 Dispose `ActionCancelToken`。
- 调用 `OnDispose()`。
- 捕获并记录异常。

业务清理写在：

```csharp
protected override void OnDispose()
```

适合清理：

- 事件监听。
- UI 回调。
- 异步 token。
- 临时对象。

## 9. 推荐模板

```csharp
public class ExampleAction : QueuedActionBase
{
    public ExampleAction()
    {
        ActionType = GameplayActionType.ProgressDisplay;
        Priority = 100;
    }

    public override void Execute()
    {
        StartDisplay();
    }

    private void StartDisplay()
    {
        // 表现结束时调用 Finish
        Finish();
    }

    protected override void OnDispose()
    {
        // 解绑和清理
    }
}
```

## 10. 常见模式

等 UI 关闭：

```csharp
public override void Execute()
{
    _mediator.OnMediatorDisposeEnd += Finish;
}

protected override void OnDispose()
{
    if (_mediator != null)
        _mediator.OnMediatorDisposeEnd -= Finish;
}
```

超时保护：

```csharp
public override void Tick(float deltaTime)
{
    base.Tick(deltaTime);
    if (IsEnded || IsDisposed) return;

    _time += deltaTime;
    if (_time > MaxTime)
        Cancel();
}
```

## 11. 注意事项

- 构造函数里设置 `ActionType`。
- 表现结束必须 `Finish()`。
- 清理写 `OnDispose()`。
- `IsImmediate` 属性本身不会自动立即执行，投递时仍要传 `true`。
- `Finish()` 只是标记完成，真正释放由 Slot 处理。
