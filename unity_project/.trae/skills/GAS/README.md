# GAS 总览

GAS 是项目里的 Gameplay Ability System。它把"技能、效果、属性、标签、表现事件"拆成一套运行时框架。

核心入口：

```csharp
GAS.GameplayAbilitySystem
```

## 1. 分层职责

### GameplayAbilitySystem

门面类，负责：

- 构造函数直接初始化 EffectRuntime 和 AbilityRuntime。
- 暴露 Grant / Revoke / Activate Ability。
- 暴露 Apply Effect 到自身或目标。
- Tick 运行时（支持 TickFixed）。
- 保存和恢复状态（含 AttributeSet）。
- 持有 DefinitionCatalog 和 RuntimeContext。
- 分发 GameplayEvent。
- 设置 CueManager / DefinitionCatalog。

### GameplayAbilityRuntime

Ability 运行时，负责：

- 已授予 Ability 集合。
- 当前 active Ability。
- Ability 激活条件检查。
- Cost / Cooldown commit。
- Ability 互斥、阻塞、取消。
- AbilityTask Tick。
- 事件触发型 Ability。
- Granted/Active Ability 状态捕获和恢复。

### GameplayEffectRuntime

Effect 运行时，负责：

- 当前 OwnedTags。
- 当前 ActiveEffects。
- Persistent 属性 modifier。
- Instant / Duration / Infinite Effect。
- Stack（含 StackBySource / StackByTarget）。
- Period Tick（单帧最大 64 次循环）。
- GameplayCue 分发。
- 运行时事件记录。
- 随机数（支持确定性模式）。

### GameplayTags

标签系统，负责：

- 层级标签匹配。
- TagContainer（含引用计数、层级计数、listener）。
- TagQuery 条件判断。
- 自动生成常量类。

### AttributeSet

属性集，负责：

- 属性 base value 管理。
- modifier 添加/移除/按 source 批量移除。
- 属性值缓存 + 脏标记增量计算。
- 属性变更回调事件。

## 2. 常见调用链

### 初始化

```text
new GameplayAbilitySystem(entityId, attributeOwner, context, catalog, cueManager)
  -> effectRuntime = new GameplayEffectRuntime()
  -> abilityRuntime = new GameplayAbilityRuntime()
  -> effectRuntime.Initialize(entityId, attributeOwner, context)
  -> abilityRuntime.Initialize(effectRuntime)
```

构造函数直接初始化，不需要单独调用 Initialize。

### 激活技能

```text
GrantAbility(ability)
  -> ActivateAbility(ability, target, level)
  -> HasAbility 检查
  -> Block check（IsIgnoreBlock 可绕过）
  -> CanActivateAbility
  -> CanCommitAbility
  -> Commit Cost/Cooldown Effects
  -> CancelAbilitiesWithTag
  -> MarkActive
  -> activeAbilities.Add(spec)
  -> AddActivationOwnedTags
  -> AddBlockedTags（引用计数）
  -> Record AbilityActivated
  -> ability.ActivateAbility(spec)
```

### 应用效果

```text
ApplyEffectToTarget(effect, targetGas, level)
  -> MakeOutgoingSpec
  -> ApplySpecToTarget
  -> target.ApplySpecToSelf
  -> CanApply (source/target tag requirements)
  -> Instant: ExecuteEffect (modifiers + executions)
     -> 返回 InstantEffect 结果
  -> Duration/Infinite: 查找可堆叠 ActiveEffect
     -> 找到：ApplyStack
     -> 未找到：创建 ActiveGameplayEffect
     -> AddGrantedTags / AddPersistentModifiers
     -> ExecuteOnApply 时执行 ExecuteEffect
     -> SendCues
     -> 返回 ActiveEffect 结果
```

### Tick

```text
GameplayAbilitySystem.Tick(deltaTime)
  -> RuntimeContext.BeginTick (CurrentFrame++, ClearEvents)
  -> GameplayEffectRuntime.Tick (duration 衰减, period 执行, 过期移除)
  -> GameplayAbilityRuntime.Tick (ability tasks tick)
  -> RuntimeContext.EndTick
```

## 3. 主要数据资产

- `GameplayAbilityDefinition`
  - 技能定义（ScriptableObject）。
  - 配置 AbilityId / AbilityTag。
  - Source/Target/Activation tag requirements（TagQuery）。
  - ActivationOwnedTags / CancelAbilitiesWithTag / BlockAbilitiesWithTag。
  - AbilityTriggers 事件触发。
  - CostEffects / CooldownEffects。
  - EffectsOnActivate（含 TargetPolicy 和 EndAbilityAfterApply）。
  - DelayedEffects（含 Delay / TargetPolicy / EndAbilityAfterApply）。
- `GameplayEffectDefinition`
  - 效果定义（ScriptableObject）。
  - 配置 EffectId / EffectTag。
  - DurationPolicy（Instant/Duration/Infinite）。
  - Duration / Period / ExecuteOnApply。
  - StackPolicy（None/StackBySource/StackByTarget）/ MaxStack。
  - RefreshDurationOnStack / ReapplyModifiersOnStack。
  - Source/Target tag requirements（TagQuery）。
  - GrantedTags。
  - Modifiers（AttributeId / Op / Value / ScaleByStack）。
  - Executions（GameplayEffectExecution 列表）。
  - Cues（CueTag / Policy / OnApply / OnExecute / OnRemove）。
- `GameplayDefinitionCatalog`
  - ScriptableObject，通过 id 管理 Ability 和 Effect 定义。
  - 支持 RegisterEffect / RegisterAbility 动态注册。
  - 内部维护 effectMap / abilityMap 字典，lazy build。

## 4. 常见扩展点

- 自定义 Ability：继承或配置 `GameplayAbilityDefinition`，重写 `CanActivateAbility` / `ActivateAbility`。
- 自定义 Effect：创建 `GameplayEffectDefinition` 资产。
- 自定义 Execution：继承 `GameplayEffectExecution`，实现 `Execute(GameplayEffectSpec spec)`。
- 自定义 Cue：继承 `GameplayCueNotify`，重写 OnExecute / OnActive / WhileActive / OnRemove。
- 自定义 AttributeOwner：实现 `IGameplayAttributeOwner` 或继承 `AttributeSet`。
- 自定义 RuntimeContext：实现 `IGameplayEffectRuntimeContext`。
- 自定义 AbilityTask：继承 `AbilityTask` 或 `AbilityTaskWaitDelay`。

## 5. 注意事项

- `GameplayAbilitySystem` 构造函数直接初始化，无需额外调用 `Initialize()`。
- 用 id 激活 Ability/Effect 前必须设置 `GameplayDefinitionCatalog`。
- `GameplayTagsDef.gen.cs` / `PVEGameTagsDef.gen.cs` 是自动生成，不要手动改。
- Ability 默认只有 Grant 后才能 Activate。
- Effect 的 TagRequirement 会阻止不满足条件的应用。
- Duration/Infinite Effect 会持有 persistent modifier 和 granted tags，移除时要清理。
- Period Tick 单帧最多 64 次循环，防止帧间隔异常导致死循环。
- `AbilityTaskWaitDelay` 是 public 类，业务可继承扩展。
- `IGameplayAttributeSetProvider` 接口允许非 AttributeSet 的 attribute owner 暴露内部的 AttributeSet。
- Cue Policy 为 Static 时只响应 Execute；Active 时不响应 Execute，只响应 OnActive/WhileActive/Removed。
- 确定性模式通过 `DeterministicMode` 属性和 `InitDeterministicRandom(seed)` 控制。