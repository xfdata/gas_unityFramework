# Runtime Events And Cues

相关源码：

```text
Assets/Scripts/HotUpdate.Core/GAS/GameplayEffectTypes.cs
Assets/Scripts/HotUpdate.Core/GAS/GameplayEffectEvent.cs
Assets/Scripts/HotUpdate.Core/GAS/GameplayEffectRuntimeContext.cs
Assets/Scripts/HotUpdate.Core/GAS/IGameplayEffectRuntime.cs
Assets/Scripts/HotUpdate.Core/GAS/GameplayCueTypes.cs
Assets/Scripts/HotUpdate.Core/GAS/Cue/GameplayCue.cs
Assets/Scripts/HotUpdate.Core/GAS/Cue/IGameplayCueManager.cs
Assets/Scripts/HotUpdate.Core/GAS/GameplayStateTypes.cs
Assets/Scripts/HotUpdate.Core/GAS/Debug/GASDebugger.cs
Assets/Scripts/HotUpdate.Core/GAS/Editor/GASDebuggerWindow.cs
```

## 1. RuntimeContext

`IGameplayEffectRuntimeContext` 管理 GAS 运行时公共状态：

- 当前 frame（`CurrentFrame`）。
- 当前 deltaTime（`DeltaTime`）。
- 事件列表（`Events`，每帧 BeginTick 清空）。
- 随机数生成器（`Random`）。
- runtime id 生成（SpecId / RuntimeEffectId / AbilitySpecId / AbilityTaskId / ProjectileId）。
- entityId 到 runtime 的映射（`RegisterEntity` / `UnregisterEntity` / `ResolveEntity`）。
- 事件订阅和分发（`Subscribe` / `Unsubscribe`）。

默认实现：

```csharp
DefaultGameplayEffectRuntimeContext
```

### Tick Frame

`BeginTick(deltaTime)`：

- 设置 `DeltaTime`。
- `CurrentFrame++`。
- 清空本帧 `events`。

`EndTick()`：

- 清空 `DeltaTime`（设为 0）。

`GameplayAbilitySystem.Tick(deltaTime)` 会统一包住 effectRuntime 和 abilityRuntime。

### Runtime Id 生成

```csharp
int NewSpecId();
int NewRuntimeEffectId();
int NewAbilitySpecId();
int NewAbilityTaskId();
int NewProjectileId();
```

恢复状态后会调用 `EnsureNextIds(...)`，避免 id 冲突：

```csharp
void EnsureNextIds(
    int nextSpecId,
    int nextRuntimeEffectId,
    int nextAbilitySpecId,
    int nextAbilityTaskId,
    int nextProjectileId);
```

每个 id 取 `Math.Max(current, next)`。

### Entity 映射

```csharp
void RegisterEntity(long entityId, IGameplayEffectRuntime runtime);
void UnregisterEntity(long entityId, IGameplayEffectRuntime runtime);
IGameplayEffectRuntime ResolveEntity(long entityId);
```

用于跨 entity 操作（如 DamageExecution 中通过 entityId 查找 source/target）。

### 确定性模式

```csharp
void InitRandom(int seed)     // 用 seed 重建 System.Random
void RestoreFrame(int frame)  // 恢复帧号
```

仅在 `DefaultGameplayEffectRuntimeContext` 上可用。

## 2. Recorded Events

### GameplayEffectEventType 枚举

```csharp
public enum GameplayEffectEventType : byte
{
    // Effect 事件
    EffectApplied,
    EffectExecuted,
    EffectStackChanged,
    EffectRemoved,

    // 属性事件
    AttributeChanged,
    ModifierAdded,
    ModifierRemoved,

    // Tag 事件
    TagAdded,
    TagRemoved,

    // Cue 事件
    CueTriggered,

    // Ability 事件
    AbilityActivated,
    AbilityCommitted,
    AbilityEnded,
    AbilityFailed,
    AbilityTaskStarted,
    AbilityTaskEnded,

    // Projectile 事件
    ProjectileSpawned,
    ProjectileHit,
    ProjectileCancelled,
    ProjectileTimedOut,
    ProjectileTargetInvalid,

    // Melee 事件
    MeleeWindowStarted,
    MeleeHit,
    MeleeWindowEnded,

    // 恢复事件
    RestoreEffectSkipped,
    RestoreAbilitySkipped,
}
```

### GameplayEffectEvent 结构

```csharp
public struct GameplayEffectEvent
{
    public int Frame;
    public GameplayEffectEventType Type;

    public long SourceEntityId;
    public long TargetEntityId;

    public int EffectId;
    public int SpecId;
    public int RuntimeEffectId;

    public int AbilityId;
    public int AbilitySpecId;
    public int AbilityTaskId;

    public int ProjectileId;
    public int ProjectileDefinitionId;
    public int MeleeDefinitionId;

    public int AttributeId;
    public float OldValue;
    public float NewValue;
    public float Delta;

    public GameplayTag GameplayTag;
    public GameplayTag CueTag;

    public Vector3 Position;
    public float Magnitude;
}
```

这些事件可用于：

- 调试（GASDebugger）。
- 表现同步。
- 回放。
- 确定性验证。

## 3. Subscribe / Unsubscribe

```csharp
context.Subscribe(GameplayEffectEventType.AttributeChanged, OnAttributeChanged);
context.Unsubscribe(GameplayEffectEventType.AttributeChanged, OnAttributeChanged);
```

`RecordEvent(...)` 会写入 events 列表，并同步通知所有订阅者。

每个事件类型可以注册多个 handler，重复注册同一个 handler 会被忽略。

## 4. GameplayCue 系统

### 核心类型

- `GameplayCuePayload`（struct）— Cue 触发携带的数据。
- `GameplayCueNotify`（abstract ScriptableObject）— Cue 表现基类。
- `GameplayCueEntry`（class）— CueTag 与 Notify 的配对。
- `GameplayCueSet`（ScriptableObject）— Cue 配置集合。
- `GameplayCueManager`（MonoBehaviour）— Cue 分发管理器。
- `IGameplayCueManager`（interface）— Cue 管理器接口。

### GameplayCuePayload

```csharp
public struct GameplayCuePayload
{
    public GameplayTag CueTag;

    public GameplayEffectRuntime Source;
    public GameplayEffectRuntime Target;
    public long SourceEntityId;
    public long TargetEntityId;

    public GameplayEffectSpec Spec;
    public GameplayEffectDefinition EffectDefinition;

    public int SpecId;
    public int RuntimeEffectId;

    public float Magnitude;
    public Vector3 Position;

    public object UserData;
}
```

### GameplayCueNotify

```csharp
public abstract class GameplayCueNotify : ScriptableObject
{
    public void HandleCue(GameplayCueEventType eventType, in GameplayCuePayload payload)
    {
        switch (eventType)
        {
            case Execute:    OnExecute(payload);    break;
            case OnActive:   OnActive(payload);     break;
            case WhileActive: WhileActive(payload); break;
            case Removed:    OnRemove(payload);     break;
        }
    }

    protected virtual void OnExecute(in GameplayCuePayload payload) { }
    protected virtual void OnActive(in GameplayCuePayload payload) { }
    protected virtual void WhileActive(in GameplayCuePayload payload) { }
    protected virtual void OnRemove(in GameplayCuePayload payload) { }
}
```

### GameplayCueEventType

```csharp
public enum GameplayCueEventType : byte
{
    Execute,      // 效果执行时（Instant 应用 / Period 触发 / ExecuteOnApply）
    OnActive,     // Active Effect 添加或堆叠时
    WhileActive,  // Active Effect 每帧 Tick（仅 WhileActive cue）
    Removed,      // Active Effect 移除时
}
```

### GameplayCuePolicy

```csharp
public enum GameplayCuePolicy : byte
{
    Static,  // 只响应 Execute
    Active,  // 不响应 Execute，只响应 OnActive / WhileActive / Removed
}
```

### GameplayCueSet

```csharp
[CreateAssetMenu(menuName = "PVE/GAS/Gameplay Cue Set")]
public class GameplayCueSet : ScriptableObject
{
    public List<GameplayCueEntry> Entries;
}
```

### GameplayCueManager

```csharp
public class GameplayCueManager : MonoBehaviour, IGameplayCueManager
{
    public void Initialize(GameplayCueSet set);
    public void HandleCue(GameplayTag cueTag, GameplayCueEventType eventType, in GameplayCuePayload payload);
}
```

匹配逻辑：

1. `cueTag.Matches(entry.CueTag)` 判断（支持层级匹配）。
2. 调用 `entry.Notify.HandleCue(eventType, payload)`。

### ShouldTriggerCue 逻辑

Effect 中配置的 Cue 触发条件：

```csharp
// Static Policy
if (eventType != Execute) return false;

// Active Policy
if (eventType == Execute) return false;

// OnApply / OnExecute / OnRemove 开关
switch (eventType)
{
    case OnActive:   return cue.OnApply;
    case Execute:    return cue.OnExecute;
    case Removed:    return cue.OnRemove;
    case WhileActive: return true;  // 总是触发
}
```

## 5. IGameplayEffectRuntime

```csharp
public interface IGameplayEffectRuntime
{
    long EntityId { get; }
    GameplayTagContainer OwnedTags { get; }
    IGameplayAttributeOwner AttributeOwner { get; }
}
```

用于 RuntimeContext 的 entity 映射，使 `ResolveEntity(entityId)` 返回的类型可被转换为 `GameplayEffectRuntime`。

## 6. 状态保存/恢复中的事件

`GameplayAbilitySystem.CaptureState()` 捕获：

- frame。
- entityId。
- owned tags（排除 active effect/ability 动态授予的）。
- attribute set state。
- granted ability ids。
- active ability states（含 task 状态）。
- active effect states。

`RestoreState(...)` 使用 catalog 通过 id 找回定义。

如果找不到定义，会记录 Skip 事件：

- `RestoreEffectSkipped` — Effect 定义未找到。
- `RestoreAbilitySkipped` — Ability 定义未找到。

## 7. GASDebugger 和 GASDebuggerWindow

### GASDebugger

`GASDebugger` 是 `[ExecuteAlways]` 的 MonoBehaviour，提供运行时调试 UI。

功能：

- 自动发现或手动指定 `GameplayAbilitySystem` 实例。
- OnGUI 绘制调试窗口（F3 切换显示）。
- 显示 ActiveEffects、ActiveAbilities、Attributes、OwnedTags、RecordedEvents。
- 事件支持按类型过滤、限制显示数量。
- 自动刷新（Editor 模式）。

### GASDebuggerWindow

位于 `Assets/Scripts/HotUpdate.Core/GAS/Editor/GASDebuggerWindow.cs`，是 Editor 窗口版本。

通过 `GameplayAbilitySystem.EditorInstances` 静态列表发现所有活跃的 GAS 实例。

## 8. 注意事项

- 多个 GAS 实例需要共享同一个 context 才能互相 `ResolveEntity`。
- 需要确定性随机时调用 `InitDeterministicRandom(seed)`。
- Cue 表现逻辑应放在 `GameplayCueNotify` 子类，重写对应的 virtual 方法。
- Runtime events 在每帧 `BeginTick` 时清空（`events.Clear()`），只保留当前帧记录。
- `RecordEvent` 在 `suppressRuntimeEvents` 模式下会跳过（用于状态恢复）。
- `ProjectileId` / `MeleeDefinitionId` 等字段是为 Projectile 和 Melee 系统预留的，在纯 GAS 层不使用。
- `GASDebugger.EditorInstances` 仅在 `UNITY_EDITOR` 宏下可用。