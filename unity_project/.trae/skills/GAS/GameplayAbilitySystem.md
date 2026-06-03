# GameplayAbilitySystem

源码：

```text
Assets/Scripts/HotUpdate.Core/GAS/GameplayAbilitySystem.cs
Assets/Scripts/HotUpdate.Core/GAS/GameplayStateTypes.cs
Assets/Scripts/HotUpdate.Core/GAS/AttributeModifierHandle.cs
Assets/Scripts/HotUpdate.Core/GAS/AttributeSet.cs
```

`GameplayAbilitySystem` 是 GAS 对外门面。业务通常通过它操作 Ability 和 Effect。

## 1. 组成

内部持有：

- `GameplayEffectRuntime effectRuntime`
- `GameplayAbilityRuntime abilityRuntime`
- `GameplayDefinitionCatalog definitionCatalog`
- `IGameplayEffectRuntimeContext runtimeContext`

常用属性：

- `EntityId`
- `Effects` -> effectRuntime
- `Abilities` -> abilityRuntime
- `DefinitionCatalog`
- `RuntimeContext`
- `OwnedTags`
- `AttributeOwner`
- `ActiveEffects`
- `ActiveAbilities`
- `RecordedEvents`
- `IsInitialized`
- `DeterministicMode`

## 2. 初始化

构造函数直接初始化：

```csharp
public GameplayAbilitySystem(
    long entityId,
    IGameplayAttributeOwner attributeOwner,
    IGameplayEffectRuntimeContext context = null,
    GameplayDefinitionCatalog catalog = null,
    IGameplayCueManager cueManager = null)
```

流程：

```text
如果已初始化，先 Dispose
  -> 创建 GameplayEffectRuntime / GameplayAbilityRuntime
  -> 设置 runtimeContext（未提供则使用 DefaultGameplayEffectRuntimeContext）
  -> 设置 definitionCatalog
  -> 设置 cueManager
  -> effectRuntime.DeterministicMode = deterministicMode
  -> effectRuntime.Initialize(entityId, attributeOwner, runtimeContext)
  -> abilityRuntime.Initialize(effectRuntime)
  -> isInitialized = true
```

`attributeOwner` 必须实现 `IGameplayAttributeOwner`：

```csharp
float GetAttribute(int attributeId);
void AddAttributeBaseValue(int attributeId, float delta);
AttributeModifierHandle AddModifier(int attributeId, AttributeModifierOp op, float value, object source);
void RemoveModifier(AttributeModifierHandle handle);
```

如果 `attributeOwner` 是 `AttributeSet` 或实现了 `IGameplayAttributeSetProvider`，`CaptureState`/`RestoreState` 会自动处理属性集状态。

## 3. Ability 操作

授予：

```csharp
GrantAbility(GameplayAbilityDefinition ability)
GrantAbility(int abilityId)
```

移除：

```csharp
RevokeAbility(ability, cancelActive: true)
RevokeAbility(abilityId, cancelActive: true)
```

激活：

```csharp
ActivateAbility(ability, target, level)
ActivateAbility(abilityId, target, level)
```

`int abilityId` 版本依赖 `definitionCatalog.GetAbility(abilityId)`。

查询：

```csharp
HasAbility(GameplayAbilityDefinition ability)
HasAbility(int abilityId)
```

## 4. Effect 操作

对自己：

```csharp
ApplyEffectToSelf(effect, level)
ApplyEffectToSelf(effectId, level)
```

对目标：

```csharp
ApplyEffectToTarget(effect, targetGas, level)
ApplyEffectToTarget(effectId, targetGas, level)
```

移除：

```csharp
RemoveActiveEffect(runtimeEffectId)
RemoveActiveEffectsByTag(effectTag, includeChildren)
```

查询：

```csharp
HasActiveEffect(effectTag, includeChildren)
GetActiveEffect(runtimeEffectId)
GetActiveEffectByTag(effectTag, includeChildren)
GetActiveEffectStackCount(runtimeEffectId)
GetActiveEffectTimeRemaining(runtimeEffectId)
```

## 5. Gameplay Event

```csharp
SendGameplayEvent(GameplayTag eventTag, GameplayEventData eventData)
```

会进入：

```text
GameplayAbilityRuntime.HandleGameplayEvent
  -> 遍历 GrantedAbilities
  -> 匹配 AbilityTriggers
  -> ActivateAbility(...)
```

## 6. Tick

```csharp
Tick(deltaTime)
Tick(deltaTime, advanceRuntimeFrame)
TickFixed(fixedDeltaTime)
```

流程：

```text
if (advanceRuntimeFrame) RuntimeContext.BeginTick(deltaTime)
  -> effectRuntime.Tick(deltaTime, false)
  -> abilityRuntime.Tick(deltaTime)
if (advanceRuntimeFrame) RuntimeContext.EndTick()
```

`TickFixed(fixedDeltaTime)` 等价于 `Tick(fixedDeltaTime, true)`。

如果外部统一推进 frame，可传 `advanceRuntimeFrame: false`，自行管理 `BeginTick` / `EndTick`。

## 7. 状态保存和恢复

保存：

```csharp
GameplayAbilitySystemState CaptureState()
```

捕获：

- 当前 frame。
- EntityId。
- OwnedTags（排除 ActiveEffect 和 ActiveAbility 动态授予的 tag）。
- AttributeSetState（如果 attributeOwner 是 AttributeSet 或 IGameplayAttributeSetProvider）。
- GrantedAbilityIds。
- ActiveAbilities（含 AbilityTaskState）。
- ActiveEffects。

恢复：

```csharp
RestoreState(state)
```

恢复时会：

- 恢复 frame。
- 清空并重建 EffectRuntime / AbilityRuntime 状态。
- 恢复 AttributeSetState。
- 先恢复 OwnedTags（外部持有的），再恢复 ActiveEffects（会追加 granted tags）。
- 根据 catalog 通过 id 找回 Ability/Effect 定义。
- `EnsureRuntimeIds` 确保后续 id 不冲突。

## 8. 其他方法

```csharp
SetCueManager(IGameplayCueManager cueManager)
SetDefinitionCatalog(GameplayDefinitionCatalog catalog)

GameplayEffectDefinition GetEffectDefinition(int effectId)
GameplayAbilityDefinition GetAbilityDefinition(int abilityId)

void Dispose()
```

## 9. 确定性

```csharp
DeterministicMode { get; set; }
InitDeterministicRandom(seed)
```

`DeterministicMode` 会传给 `GameplayEffectRuntime`。随机数来自 `RuntimeContext.Random`。

`InitDeterministicRandom` 需要 `RuntimeContext` 为 `DefaultGameplayEffectRuntimeContext`。

## 10. IGameplayAttributeOwner 和 AttributeSet

`IGameplayAttributeOwner` 定义了属性读写接口。

`AttributeSet` 是默认实现，额外提供：

- `SetBaseValue(attributeId, value)` / `SetBaseValues(values)` — 设置基础值。
- `GetBaseValue(attributeId)` — 获取基础值（不含 modifier）。
- `AddModifier / RemoveModifier` — persistent modifier 管理。
- `RemoveModifiersBySource(source)` — 按 source 批量移除 modifier。
- 属性值缓存 + 脏标记：`GetAttribute` 只在属性值变化时重新计算。
- `OnAttributeBaseValueChanged` / `OnAttributeChanged` 事件回调。

`IGameplayAttributeSetProvider` 接口允许非 `AttributeSet` 的 owner 暴露内部的 `AttributeSet`：

```csharp
public interface IGameplayAttributeSetProvider
{
    AttributeSet AttributeSet { get; }
}
```

## 11. GameplayDefinitionCatalog

`GameplayDefinitionCatalog` 是管理 Ability 和 Effect 定义的 ScriptableObject。

```csharp
GameplayEffectDefinition GetEffect(int effectId)
GameplayAbilityDefinition GetAbility(int abilityId)

void RegisterEffect(GameplayEffectDefinition effect)
void RegisterAbility(GameplayAbilityDefinition ability)
void RebuildMaps()
```

内部维护 `effectMap` / `abilityMap` 字典，按需 lazy build。

## 12. 注意事项

- 构造函数直接初始化，无需额外调用 `Initialize()`。
- 所有核心操作前会 `EnsureInitialized()`，未初始化会抛异常。
- id 版本 API 依赖 `definitionCatalog`。
- Restore 依赖 catalog 能找到对应 id 的定义，否则会记录 RestoreEffectSkipped / RestoreAbilitySkipped 事件。
- `OwnedTags` 中由 ActiveEffect 和 ActiveAbility 授予的 tag 在 CaptureState 时会被排除，只保存外部直接持有的 tag。
- `Dispose()` 会清理运行时、取消所有 active ability、从 runtimeContext 注销 entity。