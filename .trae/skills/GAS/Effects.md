# Effects

相关源码：

```text
Assets/Scripts/HotUpdate.Core/GAS/Effect/GameplayEffect.cs
Assets/Scripts/HotUpdate.Core/GAS/Effect/GameplayEffectSpec.cs
Assets/Scripts/HotUpdate.Core/GAS/Effect/ActiveGameplayEffect.cs
Assets/Scripts/HotUpdate.Core/GAS/GameplayEffectRuntime.cs
Assets/Scripts/HotUpdate.Core/GAS/GameplayEffectExecution.cs
Assets/Scripts/HotUpdate.Core/GAS/GameplayEffectEnums.cs
Assets/Scripts/HotUpdate.Core/GAS/GameplayEffectApplyResult.cs
Assets/Scripts/HotUpdate.Core/GAS/EffectExecution/DamageExecution.cs
Assets/Scripts/HotUpdate.Core/GAS/AttributeModifierHandle.cs
```

## 1. GameplayEffectDefinition

`GameplayEffectDefinition` 是 ScriptableObject 效果定义。

主要配置：

- `EffectId`
- `EffectTag`
- `DurationPolicy`（Instant / Duration / Infinite）
- `Duration`（[Min(0f)]）
- `Period`（[Min(0f)]）
- `ExecuteOnApply`（Active Effect 应用时立即执行一次 execution）
- `StackPolicy`（None / StackBySource / StackByTarget）
- `MaxStack`（[Min(1)]，默认 1）
- `RefreshDurationOnStack`（默认 true）
- `ReapplyModifiersOnStack`（默认 true）
- `SourceRequiredTags` / `SourceBlockedTags`（TagQuery，默认 All / NotAll）
- `TargetRequiredTags` / `TargetBlockedTags`（TagQuery，默认 All / NotAll）
- `GrantedTags`（GameplayTagContainer）
- `Modifiers`（List\<Modifier\>）
- `Executions`（List\<GameplayEffectExecution\>）
- `Cues`（List\<Cue\>）

### Modifier

```csharp
public class Modifier
{
    public int AttributeId;
    public AttributeModifierOp Op = Add;  // Add / Multiply / Override
    public float Value;
    public bool ScaleByStack = true;
}
```

### Cue

```csharp
public class Cue
{
    public GameplayTag CueTag;
    public GameplayCuePolicy Policy = Static;  // Static / Active

    public bool OnApply = true;
    public bool OnExecute = true;
    public bool OnRemove = true;
}
```

## 2. DurationPolicy

- `Instant`
  - 立即执行 modifiers + executions，不进入 ActiveEffects。
- `Duration`
  - 有持续时间，Tick 中 TimeLeft 衰减，过期后移除。
- `Infinite`
  - 无限持续（TimeLeft = float.PositiveInfinity），直到手动移除。

## 3. GameplayEffectApplyResult

```csharp
public struct GameplayEffectApplyResult
{
    public bool Success;
    public bool WasInstant;
    public int RuntimeEffectId;

    public static readonly GameplayEffectApplyResult Failed;
    public static GameplayEffectApplyResult InstantEffect();
    public static GameplayEffectApplyResult ActiveEffect(int runtimeEffectId);
}
```

- Instant Effect 返回 `InstantEffect()`，`Success = true, WasInstant = true, RuntimeEffectId = 0`。
- Duration/Infinite Effect 返回 `ActiveEffect(runtimeEffectId)`。
- 失败返回 `Failed`。

## 4. Apply 流程

```text
source.MakeOutgoingSpec(target, effect, level)
  -> 创建 GameplayEffectSpec（设置 SpecId / SourceEntityId / TargetEntityId）

source.ApplySpecToTarget(spec, target)
  -> targetSpec = spec.CloneForTarget(target)
  -> target.ApplySpecToSelf(targetSpec)

target.ApplySpecToSelf(spec)
  -> spec.Target = this
  -> 补全 SpecId（如果为 0 则生成）
  -> CanApply(spec)   // 检查 source/target tag requirements
  -> 失败返回 Failed

  -> Instant:
     -> ExecuteEffect(spec, 0)
     -> SendCues(spec, Execute, ...)
     -> 返回 InstantEffect()

  -> Duration/Infinite:
     -> existing = FindStackableEffect(spec)
     -> 找到:
        -> ApplyStack(existing, spec)  // 增加 stack / 刷新 duration / reapply modifiers
        -> SendCues(OnActive, ...)
        -> 返回 ActiveEffect(existing.RuntimeEffectId)
     -> 未找到:
        -> 创建 ActiveGameplayEffect(runtimeEffectId, spec, timeLeft, periodLeft)
        -> activeEffects.Add(active)
        -> Record EffectApplied
        -> AddGrantedTags(active)
        -> AddPersistentModifiers(active)
        -> ExecuteOnApply: ExecuteEffect + SendCues(Execute)
        -> SendCues(OnActive, ...)
        -> 返回 ActiveEffect(runtimeEffectId)
```

## 5. GameplayEffectSpec

运行时效果规格。

包含：

- `Asset`（GameplayEffectDefinition）
- `Source` / `Target`（GameplayEffectRuntime）
- `SpecId`（由 RuntimeContext 生成）
- `SourceEntityId` / `TargetEntityId`
- `RuntimeEffectId`
- `Level`
- `Stack`
- `Duration` / `Period`
- `Position`（Vector3）
- `RandomSeed`
- `UserData`
- `RuntimeContext`

动态值方法：

- `SetByCaller(key, value)` / `GetSetByCaller(key, defaultValue)` — 运行时动态数值字典。
- `CaptureValue(key, value)` / `GetCapturedValue(key, defaultValue)` — 快照值字典。

复制方法：

- `CloneForTarget(target)` — 深拷贝 spec，替换 target。
- `CopyDynamicValuesFrom(other, copyPeriod)` — 复制 Position / RandomSeed / UserData / SetByCaller / CapturedValues。

## 6. ActiveGameplayEffect

Duration/Infinite Effect 应用后生成的 active 实例。

属性：

- `RuntimeEffectId`
- `Spec`（GameplayEffectSpec）
- `TimeLeft`（Duration 为有限值，Infinite 为 float.PositiveInfinity）
- `PeriodLeft`
- `Stack`
- `ModifierHandles`（IReadOnlyList\<AttributeModifierHandle\>）
- `Definition` -> Spec.Asset

预计算的缓存标志：

- `HasAnyCue` — 是否配置了任意 Cue
- `HasWhileActiveCue` — 是否有 Policy=Active 的 Cue
- `HasDuration` — DurationPolicy == Duration
- `HasPeriod` — Period > 0
- `IsInfinite` — DurationPolicy == Infinite
- `IsExpired` — HasDuration && TimeLeft <= 0

内部方法：

- `AddModifierHandle(handle)` / `ClearModifierHandles()` — modifier 生命周期管理。
- `CaptureState()` — 序列化为 `ActiveGameplayEffectState`。

移除时会：

- `RemovePersistentModifiers`
- `RemoveGrantedTags`
- `SendCues(Removed)`
- `Record EffectRemoved`

## 7. Modifiers 和 AttributeModifierOp

```csharp
public enum AttributeModifierOp : byte
{
    Add,        // base += value
    Multiply,   // base *= value
    Override,   // base = value
}
```

### Instant Effect 的 modifier

直接修改 base value（通过 `ApplyAttributeBaseValue`）：

```csharp
// Add:  base += value * stack
// Multiply: base += base * (value - 1) * stack
// Override: base += (value * stack) - base (即设置为 value * stack)
```

### Duration/Infinite Effect 的 persistent modifier

通过 `AttributeOwner.AddModifier(attributeId, op, value, active)` 添加。
保存 `AttributeModifierHandle`，移除时通过 `AttributeOwner.RemoveModifier(handle)` 清理。

`ScaleByStack` 为 true 时 value 乘以 `active.Stack`。

## 8. Stack

StackPolicy：

- `None` — 不允许堆叠。
- `StackBySource` — 相同 Asset + 相同 Source 的 Effect 堆叠。
- `StackByTarget` — 相同 Asset 的 Effect 堆叠（不区分 Source）。

`FindStackableEffect(newSpec)` 查找逻辑：

- MaxStack <= 1 或 StackPolicy == None 时返回 null。
- StackByTarget：匹配 Definition 相同。
- StackBySource：匹配 Definition 相同 + SourceEntityId 或 Source 引用相同。

堆叠时：

- stack 未达上限则 +1。
- `RefreshDurationOnStack` 为 true 时刷新 TimeLeft（仅 Duration policy）。
- `ReapplyModifiersOnStack` 为 true 时先移除再重新添加 persistent modifiers。
- `CopyDynamicValuesFrom(incomingSpec, false)` 拷贝动态值（不拷贝 Period）。

## 9. Period 和 ExecuteOnApply

`ExecuteOnApply`：

- Active Effect 应用时立即执行一次 `ExecuteEffect` + `SendCues(Execute)`。

`Period`：

- Tick 中按 period 循环执行 `ExecuteEffect`。
- 单帧最多循环 64 次，防止异常大步长导致死循环。
- 每次 period 触发后发送 `SendCues(Execute)`。

`ExecuteEffect` 内部：

1. 设置 `spec.RuntimeEffectId`
2. 记录 EffectExecuted 事件
3. `ApplyInstantModifiers(spec)`（仅对 Instant policy 的 asset 应用）
4. 执行所有 `Executions[i].Execute(spec)`

## 10. Tag 副作用

Duration/Infinite Effect 添加时：

- `AddGrantedTags` — 将 `GrantedTags` 中所有 tag 加到 target `OwnedTags`。

移除时：

- `RemoveGrantedTags` — 移除对应 tag。

## 11. CanApply 检查

```csharp
CanApply(spec)
  -> SourceRequiredTags.Match(source.OwnedTags)
  -> SourceBlockedTags.Match(source.OwnedTags)
  -> TargetRequiredTags.Match(target.OwnedTags)
  -> TargetBlockedTags.Match(target.OwnedTags)
```

四个条件全部通过才允许应用。

`CanApplySpecToSelf(spec)` 是 public 方法，可在应用前检查是否满足条件。

## 12. Execution

自定义执行继承：

```csharp
public abstract class GameplayEffectExecution : ScriptableObject
{
    public abstract void Execute(GameplayEffectSpec spec);
}
```

### DamageExecution 示例

配置：

- `HpAttributeId` / `ShieldAttributeId` / `AtkAttributeId` / `DefAttributeId`
- `SkillRate`（默认值 1.0）
- `FlatDamage`（默认值 0）
- SetByCaller Key 常量：`KeySkillRate = 1`, `KeyFlatDamage = 2`, `KeyLastDamage = 3`, `KeyLastShieldCost = 4`, `KeyLastHpDamage = 5`

计算流程：

```text
atk = source.GetAttribute(AtkAttributeId)
def = target.GetAttribute(DefAttributeId)
skillRate = spec.GetSetByCaller(KeySkillRate, SkillRate)
flatDamage = spec.GetSetByCaller(KeyFlatDamage, FlatDamage)
damage = max(1, atk * skillRate + flatDamage - def)
shield = target.GetAttribute(ShieldAttributeId)
shieldCost = min(shield, damage)
hpDamage = damage - shieldCost

先扣盾: target.ApplyAttributeBaseValue(spec, ShieldAttributeId, -shieldCost)
再扣血: target.ApplyAttributeBaseValue(spec, HpAttributeId, -hpDamage)

回写 SetByCaller: KeyLastDamage / KeyLastShieldCost / KeyLastHpDamage
```

`ResolveRuntime` 优先通过 `spec.RuntimeContext.ResolveEntity(entityId)` 查找，找不到则 fallback 到缓存的 runtime 引用。

## 13. 随机数

`GameplayEffectRuntime` 提供：

```csharp
int NextRandom(int minValue, int maxValue)
int NextRandom(int maxValue)
float NextRandomFloat()
float NextRandomFloat(float minValue, float maxValue)
```

通过 `RuntimeContext.Random` 获取随机数，支持 `InitDeterministicRandom(seed)` 确定性模式。

## 14. 注意事项

- `CanApply` 会检查 Source/Target tag requirements。
- Instant Effect 不进入 `ActiveEffects`，modifiers 直接修改 base value。
- Duration/Infinite Effect 的 `GrantedTags` 会加到目标 `OwnedTags`。
- Persistent modifiers 必须随 ActiveEffect 移除，否则泄漏。
- `RemoveActiveGameplayEffectsByTag` 支持 `includeChildren`（层级匹配）。
- Period Tick 单帧最大 64 次循环。
- `ReapplyModifiersOnStack` 会导致堆叠时 modifier 先移除再添加（用于 modifier value 改变的场景）。
- 非确定性模式下，移除 ActiveEffect 使用 swap-remove 优化（O(1)）；确定性模式下保持顺序（RemoveAt）。
- `ActiveGameplayEffect` 构造函数预计算 `HasAnyCue` / `HasWhileActiveCue` 等标志，避免 Tick 中重复检查。