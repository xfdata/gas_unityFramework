# GAS 代码说明

> 代码目录：`unity_project/Assets/Scripts/HotUpdate.Core/GAS`  
> 定位：可复用的 Gameplay Ability System。它是技能、Effect、Buff、Tag、属性和冷却状态的唯一真实数据源，不依赖 BattleFoundation、BattleCommon 或具体玩法。

## 1. GAS 在整体框架中的位置

```text
应用/玩法层：英雄、怪物、AI、关卡、UI
                    |
                    v
BattleCommon：业务门面、CombatActor、战斗 Ability/Effect、表现桥接
                    |
                    v
GAS Core：Ability、Effect、Tag、Attribute、Cue、快照、事件、运行时
                    ^
                    |
Foundation 仅提供帧、实体、随机数；通过 BattleCommon 适配后供 GAS 使用
```

GAS 核心只处理通用能力，不知道英雄、怪物、投射物、战斗坐标、Unity 表现或 PVE 规则。依赖方向如下：

```text
BattleCommon -> GAS
BattleFoundation -> 无 GAS 依赖
GAS -> 仅系统库、Unity 基础 ScriptableObject/序列化能力、GameplayTags
```

核心约束：

- GAS 目录不得引用 `BattleFoundation`、`BattleCommon`、`Framework`、`Foundation.Common`、CombatActor 或战斗向量类型。
- 随机数经 `IGameplayRandom` 注入；BattleCommon 使用 `BattleGameplayRandomAdapter` 适配 BattleRandom。
- 性能采样只使用 `GASProfiler`；`AutoProfiler` 由集成层适配。
- 战斗私有数据通过 `GameplayEffectSpec.ContextData` 传入，只在 BattleCommon 解释。
- BattleCommon 中 Cue、属性、Tag、Ability 事件统一经 `CombatAbilityComponent -> IBattlePresentationSink` 进入表现层；核心的 `IGameplayCueManager` 保留给非战斗集成。

## 2. 核心运行流程

### 2.1 初始化与 Tick

```text
创建 AttributeSet / IGameplayAttributeOwner
  -> 创建 GameplayAbilitySystem(entityId, owner, context, catalog)
  -> 初始化 GameplayEffectRuntime 和 GameplayAbilityRuntime
  -> 授予 Ability Definition

每个逻辑帧
  -> RuntimeContext.BeginTick（共享 Context 时仅一次）
  -> 每个 GAS 实例 Tick(deltaTime, false)
     -> GameplayEffectRuntime.Tick：周期 Effect、持续时间、到期移除
     -> GameplayAbilityRuntime.Tick：活跃 Ability 与 Task
  -> RuntimeContext.EndTick（共享 Context 时仅一次）
```

单实例可直接调用 `GameplayAbilitySystem.Tick(deltaTime)`，它会推进自身 RuntimeContext。多个单位共享同一个 RuntimeContext 时，外部必须只调用一次 `BeginTick/EndTick`，每个 GAS 实例改用 `Tick(deltaTime, false)`。

### 2.2 Ability 生命周期

```text
GrantAbility
  -> AbilityRuntime 保存可用 Definition
  -> TryActivateAbility
  -> 创建 GameplayAbilitySpec
  -> 校验 Source/Target/Activation Tag Query
  -> 添加 ActivationOwnedTags、支付 Cost、施加 Cooldown
  -> AbilityDefinition.ActivateAbility
  -> 立即施加 Effect 或创建 AbilityTask
  -> Task 完成/取消或 Ability 主动结束
  -> 移除拥有 Tag、结束 Task、记录 AbilityEnded 事件
```

### 2.3 Effect、Buff 与属性生命周期

```text
MakeOutgoingSpec
  -> GameplayEffectSpec（来源、目标、等级、SetByCaller、Capture、ContextData）
  -> 标签和免疫校验
  -> Instant：执行 Execution / Modifier 后结束
  -> Duration 或 Infinite：创建 ActiveGameplayEffect
     -> 添加 Modifier、GrantedTags、Cue
     -> 周期 Tick 时执行 Execution
  -> 到期、驱散或 Remove
     -> 移除 Modifier、GrantedTags、Cue
     -> 记录 EffectRemoved 事件
```

Duration、Stack、周期、Modifier、GrantedTag、Cue 和移除都由 `GameplayEffectRuntime` 管理。外部代码不能维护第二份 Buff 计时器、层数或属性值。

## 3. 核心门面和运行时类

| 类/文件 | 作用 | 主要使用方式 |
| --- | --- | --- |
| `GameplayAbilitySystem` | GAS 总门面，组合 Ability Runtime 与 Effect Runtime，提供授予、激活、施加 Effect、Tick、查询和快照恢复。 | 框架/集成层创建每个单位实例；普通战斗业务改用 BattleCommon 的 `actor.Gameplay` 门面。 |
| `GameplayAbilityRuntime` (`Ability/GameplayAbilityRuntime.cs`) | 已授予 Ability、活跃 Spec、激活校验、取消、事件触发与 Task Tick 管理。 | 由 `GameplayAbilitySystem` 内部使用；复杂框架代码可通过 `Abilities` 查询。 |
| `GameplayEffectRuntime` | Effect 的核心执行器，处理 Spec、持续/周期 Effect、堆叠、Tag、Modifier、Cue、事件、移除和状态恢复。 | 通过 `GameplayAbilitySystem` 或 Ability Spec 施加 Effect，不手工创建 Active Effect。 |
| `GameplayEffectRuntimeContext` | 帧号、Id 分配、事件记录/订阅、随机数和 Tick 边界的契约实现。 | 单位可共享 Context；默认实现可独立运行并使用 DefaultRandom。 |
| `DefaultGameplayEffectRuntimeContext` | 默认 Context，提供帧推进、事件缓存、监听和随机数注入。 | 独立使用 GAS 时默认创建；战斗使用时由 BattleCommon 提供共享 Context/随机适配。 |
| `IGameplayEffectRuntime` | Effect Runtime 的抽象接口。 | 用于不希望依赖具体 Runtime 的通用代码和测试。 |
| `GameplayEffectEvent` | Ability、Effect、属性、Tag、Cue、投射物等运行时事件载荷。 | 订阅 Context 后消费；BattleCommon 负责把战斗相关事件转换成表现契约。 |
| `GameplayEffectApplyResult` | Effect 施加结果，包含成功状态、即时/持续属性和运行时 Effect Id。 | 用于判断施加是否成功；业务层由 `BattleEffectResult` 再次包装。 |
| `GameplayStateTypes` | Ability、Task、Active Effect、系统的可序列化状态结构。 | `CaptureState/RestoreState` 用于回放、存档或诊断；不要手工篡改运行时集合。 |

## 4. Ability 目录与类

### 4.1 Ability Definition 与 Spec

| 类/文件 | 作用 | 主要使用方式 |
| --- | --- | --- |
| `GameplayAbilityDefinition` (`Ability/GameplayAbility.cs`) | 技能配置 ScriptableObject。定义 AbilityId、Tag Query、Cost、Cooldown、激活 Effect、延迟 Effect、拥有 Tag 与默认激活行为。 | 通过 CreateAssetMenu 创建资产，加入 Catalog，再由 GAS 授予。战斗专用子类放在 BattleCommon。 |
| `GameplayAbilitySpec` | 单次激活的运行时实例。保存来源、目标、等级、任务、结束原因和上下文。 | 由 Runtime 创建；Ability 内可用来施加 Effect、添加 Task、结束 Ability。普通业务不直接构造。 |
| `GameplayAbilityActivationResult` | 激活成功/失败的结构化结果及失败原因。 | 框架或业务门面把底层失败原因转换为易理解的结果。 |
| `GameplayAbilityTypes` | Ability 目标、事件数据、延迟 Effect、目标策略、结束原因等基础类型。 | 配置 Ability 时使用；避免把具体战斗对象写入核心类型。 |

### 4.2 Ability Runtime 与 Task

| 类/文件 | 作用 | 主要使用方式 |
| --- | --- | --- |
| `AbilityTask` (`Ability/AbilityTask.cs`) | 技能异步任务基类，管理激活、Tick、结束和所属 Spec。 | 一个 Task 只负责一个异步关注点；结束、取消或释放时必须清理订阅和回调。 |
| `AbilityTaskDelay` | 通用延时 Task。 | 用于延迟施加 Effect 或结束 Ability；不要在 Unity Coroutine 中另行驱动技能时间。 |
| `GameplayAbilityRuntime` | Tick 活跃 Spec，结束时清理 Task 和 ActivationOwnedTags。 | 确保 Ability 不会因忘记结束而永久占有状态 Tag。 |

复杂技能开发者可以在 `GameplayAbilityDefinition` 子类内部继续使用 `GameplayAbilitySpec`、`AbilityTask`、`GameplayEffectSpec`、`GameplayTagContainer` 等高级能力；这些类型不应暴露给普通 AI、单位、UI 和关卡业务。

## 5. Effect 目录与类

| 类/文件 | 作用 | 主要使用方式 |
| --- | --- | --- |
| `GameplayEffectDefinition` (`Effect/GameplayEffect.cs`) | Effect 配置 ScriptableObject。定义即时/持续/无限时长、周期、堆叠、Modifier、GrantedTag、Cue、Execution 和标签门槛。 | 简单 Buff/Debuff 直接配置 Definition；复杂公式加 Execution。 |
| `GameplayEffectSpec` (`Effect/GameplayEffectSpec.cs`) | 单次施加的运行时载荷。保存来源/目标、等级、SetByCaller、捕获值、上下文和用户数据。 | 用 `MakeOutgoingSpec` 创建；复杂 Ability 设置动态参数后调用 `ApplySpecToTarget`。 |
| `ActiveGameplayEffect` (`Effect/ActiveGameplayEffect.cs`) | 持续/无限 Effect 的运行时实例，保存 RuntimeEffectId、剩余时间、周期时间、层数、Modifier Handle 和 Cue 状态。 | 只读查询或由 Runtime 移除，不能外部手工创建。 |
| `GameplayEffectExecution` | Effect 公式扩展点 ScriptableObject。 | 重写 `Execute(spec)` 处理公式、SetByCaller、捕获值和多属性变化；先处理空来源/目标。 |
| `EffectExecution/DamageExecution` | 通用伤害执行器与伤害参数键。 | 提供基础伤害计算；格挡、战斗属性和模式专用公式留在 BattleCommon。 |
| `GameplayEffectEnums` | 时长、周期、堆叠、修饰器等枚举。 | 配置 Definition 时使用，组合测试 Duration/Stack/Refresh/Reapply 行为。 |

## 6. 属性、Tag、Cue 与随机数

### 6.1 属性

| 类/文件 | 作用 | 主要使用方式 |
| --- | --- | --- |
| `AttributeSet` | 默认属性容器，保存基础值、Modifier、缓存、变更事件和快照。 | Effect Runtime 为持续 Effect 增删 Modifier；业务优先监听或通过 Effect 修改，不能遗留裸 Modifier。 |
| `AttributeModifierHandle` | Modifier 的唯一句柄及 `Add/Multiply/Override` 操作定义。 | 添加 Modifier 后由同一 Handle 移除；一般由 Active Effect 托管。 |
| `AttributeDef` | 属性 Id、默认值和边界的不可变定义。 | 为需要统一范围限制的属性创建定义。 |
| `AttributeRegistry` | 属性定义注册和统一 Clamp。 | 启动期注册；测试或重载结束时清理静态表，避免跨域残留。 |

`AttributeSet` 提供 `OnAttributeBaseValueChanged` 与 `OnAttributeChanged`。订阅者必须在组件回收或 Dispose 时反订阅。`CaptureState/RestoreState` 可保存基础值、Modifier 及下一个 Modifier Id，供回放还原。

### 6.2 Tag 与查询

| 类/文件 | 作用 | 主要使用方式 |
| --- | --- | --- |
| `GameplayTag`、`GameplayTagContainer`（GameplayTags 模块） | 层级 Tag 及其计数容器。 | Ability/Effect 使用 Tag 表示死亡、眩晕、冷却、免疫、激活状态等。 |
| `TagQuery` | Required、Blocked、All、Any、None 等标签查询。 | 用于 Ability 的来源/目标/激活门槛和 Effect 的应用门槛。 |
| `GameplayDefinitionCatalog` | AbilityId、EffectId 到 Definition 资产的查找目录。 | 需要按 Id 授予或激活时必须加入 Catalog；先查已有 Id，避免冲突。 |

新 Tag 只能修改 GameplayTagDatabase 资产后生成代码，业务代码只使用生成字段。禁止手工编辑 `*.gen.cs` 或使用 `new GameplayTag(...)` 临时创建 Tag。

### 6.3 Cue、随机数、数学和诊断

| 类/文件 | 作用 | 主要使用方式 |
| --- | --- | --- |
| `IGameplayCueManager` (`Cue/IGameplayCueManager.cs`) | 通用 Cue 分发接口。 | 非战斗集成可注入 Manager；BattleCommon 不用它直接驱动 Actor/Unity 表现。 |
| `GameplayCue`、`GameplayCueNotify`、`GameplayCueSet` | Cue 载荷、通知回调、Tag 到 Cue 的配置映射。 | Notify 可实现 Execute/Active/WhileActive/Remove；Cue 生命周期由 Effect Runtime 触发。 |
| `GameplayCueTypes` | Cue 事件类型及相关数据结构。 | 表现层应依据事件类型和 RuntimeEffectId 区分添加、执行和移除。 |
| `IGameplayRandom`、`DefaultRandom` (`GameplayRandom.cs` / Context) | 可注入随机数接口及独立 xorshift128 实现。 | 独立 GAS 可用 DefaultRandom；确定性战斗由 BattleCommon 注入适配器。 |
| `GameplayMath` | GAS 通用数值辅助。 | 只放与游戏业务无关的数学逻辑。 |
| `DamageResult` | 不可变伤害计算明细。 | 计算、日志、回放和表现可读取结果，避免反查临时 SetByCaller Key。 |
| `GASProfiler` | GAS 内部的可替换性能采样门面。 | 集成层设置后端；核心使用 `using (GASProfiler.Sample(...))`。 |
| `GASDebugger`、`GASDebuggerWindow` | 运行时 IMGUI 调试器及 Editor 窗口。 | 仅用于开发诊断，展示实例、Effect、Ability、属性、Tag 和事件。 |

## 7. 快照、回放与事件

`GameplayAbilitySystem.CaptureState` 保存：

- Context 帧号和实体 Id。
- 直接持有的 Tag（不会重复保存由活跃 Ability/Effect 拥有的 Tag）。
- AttributeSet 状态。
- 已授予 Ability Id。
- 活跃 Ability、Task 和 Active Effect 状态。

`RestoreState` 的顺序是恢复帧、清理旧运行时、还原属性和直接 Tag、恢复活跃 Effect、授予 Ability、恢复活跃 Ability，并推进下一个运行时 Id 以避免与恢复对象冲突。

GAS 只保存自己的通用状态。BattleFoundation 回放实体快照时，由 BattleCommon 的 `CombatReplayAdapter` 捕获和恢复 CombatActor 属性；Foundation 不引用 `AttributeSetState` 类型。

## 8. BattleCommon 业务门面边界

普通战斗业务不直接使用下列 GAS 类型：

```text
GameplayAbilitySpec
GameplayEffectSpec
GameplayEffectRuntime
GameplayTagContainer
GameplayEffectRuntimeContext
ActiveGameplayEffect
AttributeModifierHandle
TargetData / 原始 GAS Handle
```

应使用 BattleCommon 提供的业务接口：

```csharp
BattleCastResult cast = actor.Gameplay.Skills.TryCast(skillId, target, level);
BattleEffectResult effect = source.Gameplay.Effects.Apply(effectId, target, parameters);
BattleEffectResult buff = target.Gameplay.Buffs.Apply(buffId, source, parameters);
bool stunned = target.Gameplay.States.Has(BattleState.Stunned);
float health = target.Gameplay.Attributes.Get(BattleAttribute.Health);
```

业务门面负责 SkillId/EffectId/BuffId 到 Definition 的映射、目标和参数转换、失败原因转换、Buff 只读视图、属性事件和 Tag 状态投影。它不保存第二份技能生命周期、冷却、Buff、Tick、属性或状态数据。

## 9. 新增功能放置规则

| 需求 | 放置位置 |
| --- | --- |
| 可跨玩法复用的 Ability/Effect/Attribute/Cue 运行时能力 | `HotUpdate.Core/GAS`。 |
| 战斗专用近战、远程、出生、死亡、冲锋 Ability | `BattleCommon/Abilities/Ability`。 |
| 动画、Timeline、近战命中、投射物 Task | `BattleCommon/Abilities/Task`。 |
| 战斗伤害、格挡、战斗属性公式 | `BattleCommon/Abilities/Effect`。 |
| 投射物、命中形状与战斗位置上下文 | `BattleCommon/Abilities/Definition` 或 `Runtime`。 |
| 具体英雄、怪物、Boss、关卡机制、PVE 胜负条件 | 应用/玩法层，不进入 GAS 或 BattleCommon。 |

## 10. 开发和审查清单

1. 新 Ability/Effect 使用稳定且未冲突的 Id，并添加至 `GameplayDefinitionCatalog`。
2. 新 Tag 只修改数据库资源并生成定义代码。
3. Ability 结束时必须移除 ActivationOwnedTags，Task 结束时必须清理回调和订阅。
4. 持续 Effect 移除后不得残留 Modifier、GrantedTag、Cue 或 Runtime 事件订阅。
5. 共享 RuntimeContext 的 Tick 边界每帧只能推进一次。
6. 确定性逻辑注入 `IGameplayRandom`，不使用 Unity 随机数。
7. 新增长生命周期状态时评估 `CaptureState/RestoreState` 和 BattleCommon 回放适配需求。
8. 普通 AI、单位、关卡和 UI 必须通过 BattleGameplayFacade 使用 GAS；发现门面不足时扩展通用业务接口，不要绕过它。

## 11. 当前结论

GAS 已形成独立的核心技能运行时：它可单独用于非战斗玩法，也可被 BattleCommon 适配进确定性战斗。核心负责真实状态和高级扩展能力；BattleCommon 负责战斗语义、业务 ID、表现和 Foundation 接入。这一边界既避免普通业务直接操作复杂 GAS 对象，也不会限制复杂技能开发者使用原生 Ability、Effect、Tag 和 Task。
