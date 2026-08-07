# BattleCommon 代码说明

> 代码目录：`unity_project/Assets/Scripts/HotUpdate.Core/Battle/BattleCommon`  
> 定位：L2 通用战斗实现层。本文面向技术审查，说明基于 BattleFoundation 和 GAS 构建的 Actor、战斗行为和表现桥接。

## 1. 整体框架

BattleCommon 把 BattleFoundation 的通用实体和帧驱动能力落实为通用战斗行为。它不包含具体英雄、怪物、关卡、编队或 PVE 胜负规则。

```text
应用/玩法层
  -> 创建单位、提供具体配置、调用业务 SkillId、实现具体 UI/特效
                         |
                         v
BattleCommon
  -> CombatActor / 组件 / AI / 目标查询 / 投射物 / 表现桥接 / GAS 业务门面
                    |                                     |
                    v                                     v
BattleFoundation：生命周期、Tick、实体、事件、回放        GAS：Ability、Effect、Tag、Attribute 的真实状态
```

BattleCommon 包含以下部分：

| 部分 | 负责内容 |
| --- | --- |
| Actor 和组件 | 单位的属性、生命、移动、攻击、技能、状态、动画和表现。 |
| 战斗系统 | Actor 生命周期、目标查询、阵营关系、投射物、物理、寻路和动画时间缩放。 |
| GAS 接入 | SkillId/EffectId/BuffId 目录、业务门面、单位配置、战斗专用 Ability/Task/Effect。 |
| AI | 通用待机、追击、攻击、逃离、巡逻和技能行为。 |
| 表现桥接 | 将逻辑事件统一转换为 `IBattlePresentationSink`，由 Unity 或 UI 层消费。 |
| 资源与回放 | Addressables 资源缓存、CombatActor 动态实体回放适配。 |

依赖约束：BattleCommon 可依赖 BattleFoundation 和 GAS；GAS 不能反向依赖 BattleCommon。具体 PVE 类型和关卡逻辑必须留在应用/玩法层。

## 2. 关键运行流程

### 2.1 Actor 生命周期

```text
玩法层配置 CombatActor
  -> CombatActorSystem.Spawn
  -> EntityManager.AddEntity
  -> actor.Initialize
  -> 战斗运行中则 actor.Start
  -> ActorSpawnedEvent

每帧
  -> CombatActorSystem.Update
  -> actor.Update
  -> 各组件 Update

死亡/场景清理
  -> CombatActorSystem.Despawn
  -> ReclaimActorState / DeactivateForPool
  -> ActorDiedEvent
  -> EntityManager.RemoveEntity
  -> actor.Dispose
```

`CombatActorSystem` 在遍历 Actor 时延迟处理 Spawn/Despawn，避免修改实体集合。旧的 `AddActor`、`RemoveActor`、`RecycleActor`、`DisposeActor` 仅保留兼容，已标记 `Obsolete`；新代码只使用 `Spawn/Despawn`。

### 2.2 技能、伤害和 Buff

```text
AI/业务选择 SkillId 与目标
  -> actor.Gameplay.Skills.TryCast(skillId, target)
  -> BattleGameplayFacade 解析 Catalog 并转换参数
  -> CombatAbilityComponent 激活 GAS Ability
  -> Ability 通过 GameplayEffect 结算伤害、治疗、Buff 或状态
  -> GAS Attribute / Tag / Active Effect 更新
  -> CombatAbilityComponent 转换运行时事件
  -> IBattlePresentationSink 分发给表现层
```

GAS 是技能、冷却、Buff、Tag、属性和 Effect 的唯一真实数据源。业务门面只做 ID 映射、参数转换、失败原因转换和只读视图，不能维护第二份 Buff 列表、Tick、冷却或属性字典。

### 2.3 投射物和表现

```text
远程 Ability
  -> ProjectileRuntime.Spawn
  -> CombatProjectileSystem.Tick
  -> 目标/扫掠命中查询（阵营过滤）
  -> GameplayEffect 伤害结算
  -> Damage / Cue / Attribute / Tag 事件
  -> BattlePresentationSink
  -> ActorPresentationComponent、ActorAnimationComponent、ActorViewBinder、应用层 Listener
```

表现事件只走 `IBattlePresentationSink`。表现组件不直接订阅 GAS，避免重复播放和已销毁对象仍被回调。

## 3. 目录和核心类

### 3.1 `Entity/`、`BattleLogic/`：战斗实体与公共契约

| 类/文件 | 作用 | 主要使用方式 |
| --- | --- | --- |
| `CombatActor` (`Entity/CombatActor.cs`) | `BattleEntity` 的战斗实现，聚合组件、逻辑位置/旋转、目标、近战来源和表现绑定。 | 单位工厂配置后由 `CombatActorSystem.Spawn` 加入战斗；逻辑位置是权威数据，Transform 只是投影。 |
| `CombatComponentBase` | 战斗组件公共基类。 | 新组件在此基础上实现 Attach、Initialize、回收与释放。 |
| `CombatAttributeComponent` | 对 GAS `AttributeSet` 的战斗属性封装，并保存最后伤害来源等战斗信息。 | 生命、攻击、范围、攻速等从这里读取；正式修改优先经 GameplayEffect。 |
| `CombatStateComponent` | 传统战斗状态兼容和辅助判断。 | 业务状态优先从 GAS Tag 投影，不能由 AI 维护重复状态 bool。 |
| `CombatActorLifecycle` | Actor Spawn、死亡原因等生命周期事件/类型。 | 表现和玩法层通过事件契约观察 Actor 生命周期。 |
| `CombatPrimitives` | 通用战斗枚举、属性 Id 和基础数据类型。 | 提供属性、阵营、目标优先级等共同语义，不放入具体玩法枚举。 |
| `CombatAbilityRuntimeMode` | Ability 组件的 Full GAS / Lightweight 运行方式枚举。 | 新技能要明确是否支持 Lightweight，并保持关键 Tag、Effect、死亡语义一致。 |
| `ICombatActor`、`ICombatHealthComponent`、`ICombatAbilityComponent` | 跨模块战斗对象契约。 | 业务代码优先依赖接口，避免扩散具体 Actor 类型。 |
| `IActorViewBinding` | 逻辑 Actor 到 View 的单向绑定契约。 | 逻辑层调用同步和表现命令；View 不反向改写权威战斗状态。 |

### 3.2 `Combat/`：组件、Actor 系统和目标选择

| 类/文件 | 作用 | 主要使用方式 |
| --- | --- | --- |
| `CombatActorSystem` (`Combat/CombatSystems.cs`) | Actor 的唯一 Spawn/Despawn 门面和每帧驱动系统。 | 管理注册、启动、死亡回收、事件发送和延迟结构变更。 |
| `CombatTargetQuerySystem` | 按阵营关系、存活状态、距离和优先级查找目标；支持近战范围查询。 | AI、近战 Ability 和范围攻击使用它，不直接遍历所有实体做业务判断。 |
| `ICombatRelationResolver` / `DefaultCombatRelationResolver` | 敌我关系解析。 | 投射物和区域伤害必须使用它，不能只按距离命中。 |
| `CombatAbilityComponent` | 持有 Full GAS 或 Lightweight 运行时；授予/激活 Ability 并订阅 GAS 事件。 | 是 BattleCommon 唯一 GAS Runtime 事件订阅点，统一向 Presentation Sink 转发。 |
| `CombatAbilityServices` | 向战斗 Ability 提供目录、投射物、目标查询等服务。 | 由组合根注入，核心 GAS 不直接持有这些战斗服务。 |
| `CombatHealthComponent` | 生命值夹取、治疗、死亡状态和死亡 Ability 触发。 | HP 归零后触发死亡逻辑，最终由 ActorSystem 完成 Despawn。 |
| `CombatAttackComponent` | 普攻距离、间隔、目标和普通攻击 SkillId。 | 普攻也作为技能处理；优先通过 `Gameplay.Skills.TryCast` 激活。 |
| `CombatMovementComponent` | 移动意图、路径跟随和 `IMovementMotor` 接入。 | 逻辑层下达 `MoveTo/StopMove`，实际 NavMesh/Unity 行为由 Motor 或 View 适配。 |
| `IMovementMotor`、`IUnitAnimationConfig` | 移动执行和单位动画配置的抽象契约。 | 用接口隔离 Unity/NavMesh、Animator、Animancer 等表现依赖。 |
| `ActorPresentationComponent` | 处理出生、受击、死亡、材质颜色等表现状态。 | 注册为 Presentation Sink listener，不直接订阅 GAS。 |
| `ActorAnimationComponent` | 处理技能结束后的 Idle 恢复和动画配置查询。 | 注册为 Presentation Sink listener，并向 Ability 提供动画资源。 |
| `AnimationTimeScaleSystem` | 统一应用战斗时间缩放到 Animator、Animancer 和 Timeline。 | 只处理表现时间，不改变权威战斗 Tick。 |
| `CombatContracts.cs` | 目标、攻击来源、关系和动画等跨组件契约。 | 新功能优先复用接口，不直接耦合实现类。 |

### 3.3 `Gameplay/`：面向业务的 GAS 门面

| 类/文件 | 作用 | 主要使用方式 |
| --- | --- | --- |
| `BattleGameplayFacadeComponent` | 把 GAS 投影为 `Skills`、`Effects`、`Buffs`、`Attributes`、`States` 五类业务接口。 | 业务调用 `actor.Gameplay`，不直接构造 AbilitySpec、EffectSpec、TargetData 或操作 Handle。 |
| `BattleGameplayCatalog` | 建立 SkillId、EffectId、BuffId 到 GAS Definition 的映射。 | 策划配置和业务代码使用稳定业务 ID；Catalog 是映射的唯一入口。 |
| `BattleGameplayActorConfigurator` | 把单位业务配置、Catalog 和 Ability 授予安装到 Actor。 | 在 Spawn 前的单位装配阶段调用，不在 AI 中临时拼装 GAS 对象。 |
| `BattleUnitGameplayConfig` | 单位可授予技能、Buff/Effect 和 AI 技能列表的业务化配置。 | 第一阶段薄配置，只引用业务 ID，不暴露运行时 GAS 对象。 |
| `BattleUnitGameplayConfigEditor` | 单位业务配置的编辑器校验辅助。 | 提前提示无效 ID、重复授予或 AI 技能未授予等配置问题。 |

常用业务接口：

```csharp
BattleCastResult result = actor.Gameplay.Skills.TryCast(skillId, target);
source.Gameplay.Effects.Apply(effectId, target, parameters);
target.Gameplay.Buffs.Apply(buffId, source, parameters);
float health = target.Gameplay.Attributes.Get(BattleAttribute.Health);
bool canMove = target.Gameplay.States.CanMove();
```

### 3.4 `AI/`：通用战斗 AI

| 类/文件 | 作用 | 主要使用方式 |
| --- | --- | --- |
| `CombatAIProfile` (`AI/CombatAI.cs`) | AI 决策间隔、追击、逃离、巡逻、目标优先级和 SkillId 配置。 | 每个单位使用运行时 Clone，禁止修改共享配置对象。 |
| `CombatAIComponent` | AI 决策循环、目标维护、行为切换、池化清理。 | 挂接到 CombatActor；时间使用战斗 deltaTime。 |
| `CombatBehaviorBase` | 行为基类，定义 Setup、CanEnter、Enter、Update、Exit、Dispose。 | 新的通用 AI 行为继承该类，退出时清理移动和临时引用。 |
| `CombatIdleBehavior` | 停止移动并等待。 | 无有效行为时的兜底。 |
| `CombatAttackBehavior` | 目标在攻击范围内时请求普攻。 | 调用 AttackComponent，不手工维护 GAS 冷却。 |
| `CombatChaseBehavior` | 追击目标，超过超时或放弃距离后退出。 | 通过 MovementComponent 移动。 |
| `CombatFleeBehavior` | 低生命且允许逃离时选择确定性方向撤退。 | 随机方向使用 Context 的 `BattleRandom`。 |
| `CombatPatrolBehavior` | 无目标时在配置或随机巡逻点间移动。 | 随机巡逻点同样使用确定性随机数。 |
| `CombatSkillBehavior` | 到达间隔后向目标请求 SkillId 释放。 | 只调用 `Gameplay.Skills.TryCast`，不触碰 GAS Spec 或 Tag。 |
| `CombatAIPresetBuilder` | 构建近战、远程、Boss、巡逻等通用 AI 预设。 | PVE 仅把模式配置适配为 Profile，不复制通用 AI。 |

### 3.5 `Abilities/`、`Projectile/`：战斗专用 GAS 能力

| 类/文件 | 作用 | 主要使用方式 |
| --- | --- | --- |
| `Ability/` 中的 `MeleeAttackAbilityDefinition`、`RemoteAttackAbilityDefinition` | 近战和远程攻击 Ability。 | 普攻或攻击技能使用 GameplayEffect 结算伤害。 |
| `BornAbilityDefinition`、`DeathAbilityDefinition` | 出生和死亡 Ability。 | 与表现组件、碰撞启停及 Actor 生命周期配合。 |
| `RushAbilityDefinitions`、`DamageBlockAbilityDefinition` | 冲锋、伤害格挡等通用战斗能力。 | 仅放入不依赖具体关卡/单位的机制。 |
| `Definition/MeleeHitDefinition` | 近战形状、距离、半径和最大目标数。 | 由近战 Task 和目标查询系统使用。 |
| `Definition/RangedProjectileDefinition` | 投射物速度、轨迹、目标类型、扫掠和爆炸范围。 | 由远程 Ability/Task 配置。 |
| `Task/AbilityTaskPlayMontage` | Animancer 动画播放和命名事件回调。 | Task 结束时移除动画回调。 |
| `Task/AbilityTaskPlayTimeline` | Timeline 播放、Marker 事件和停止回调。 | Task 结束时取消 `director.stopped` 订阅。 |
| `Task/AbilityTaskApplyMeleeHit` | 近战命中时应用 Effect。 | 去重目标后通过 GAS Effect 结算。 |
| `Task/AbilityTaskSpawnProjectile` | 生成远程投射物。 | 投射物生命周期由 Runtime/System 管理。 |
| `Effect/CombatDamageExecution` | 战斗伤害公式、伤害阻挡、生命结算和伤害表现事件。 | 处理空来源/目标和数值夹取，业务不直接扣 HP。 |
| `Runtime/ProjectileRuntime` | 通用投射物飞行、命中、扫掠、范围伤害、取消和超时。 | 由 `CombatProjectileSystem` 每帧 Tick。 |
| `RangedProjectileHandle`、`RangedProjectileRequest`、`RangedProjectileResult`、`RangedProjectileState` | 投射物的业务 Handle、创建参数、结束结果和回放/调试状态。 | Task 只保留 Handle；取消或命中由 Runtime 管理，不能自行删除投射物列表。 |
| `Projectile/CombatProjectileSystem` | 为投射物提供 Actor 查询和阵营过滤。 | 注册为 BattleContext System。 |
| `Runtime/BattleGameplayRandomAdapter` | 将 Foundation 的 `IRandom` 适配为 GAS `IGameplayRandom`。 | 保持 GAS 核心不依赖 BattleFoundation。 |

### 3.6 `Presentation/`、`Assets/`、`Replay/` 和辅助系统

| 类/文件 | 作用 | 主要使用方式 |
| --- | --- | --- |
| `IBattlePresentationSink` | Actor 出生/死亡、伤害、属性、技能、Cue、Tag 的统一表现契约。 | 逻辑层只发送该契约；具体 UI、特效、音效实现为 Sink 或 listener。 |
| `BattlePresentationSink` | 监听 Actor 生命周期事件，转发 GAS/伤害事件，并管理 listener。 | listener 在分发期间注册/注销会延迟处理，避免遍历集合时修改。 |
| `ActorViewBinder` | 连接 CombatActor 和 Unity GameObject/Transform/Animator。 | 同步逻辑坐标和表现命令，不以 Transform 作为战斗权威数据。 |
| `IActorViewResources` | 向兼容中的表现组件暴露 Unity View 资源的只读契约。 | 由 ViewBinding 提供，不让战斗逻辑直接持有 Unity 对象。 |
| `NavMeshMovementMotor` | NavMesh 对 `IMovementMotor` 的 Unity 适配。 | 只执行移动，不参与命中或战斗判定。 |
| `CombatEffectPresentationContext` | Effect 到表现层的位置等战斗上下文数据。 | 放入 `GameplayEffectSpec.ContextData`，仅由 BattleCommon 解释。 |
| `CombatAssetCache`、`AssetCacheEntry` | 模型、粒子资源的异步加载、预加载、LRU、引用计数和 Addressables Handle 释放。 | 实例释放后递减 RefCount；异步加载完成后确认缓存仍有效；Handle 释放为幂等。 |
| `CombatReplayAdapter` | 动态 CombatActor 创建、销毁和 GAS AttributeSet 快照恢复。 | 作为 `IBattleReplayAdapter` 注入 Engine，避免 Foundation 依赖 GAS。 |
| `CombatPhysicsSystem` | 战斗物理层/碰撞设置。 | 仅维护通用战斗物理约束。 |
| `CombatPathfindingSystem` | 通用路径请求和路径辅助能力。 | 使用调用方提供的路径/环境实现，不绑定具体关卡。 |
| `CombatGameplayTags.asset` | 战斗 GameplayTag 的权威数据源。 | 新 Tag 只改数据库资源并生成定义，禁止手写 `CombatGameplayTagsDef.gen.cs`。 |

## 4. 使用和审查约束

- 普通业务、AI、UI 不直接构造 GAS Spec、Context、TargetData 或操作 GAS Handle。
- 正式伤害、治疗、Buff、属性修改通过 GameplayEffect 结算，禁止 `target.Health -= damage`。
- Actor 新增、回收和销毁只能走 `CombatActorSystem.Spawn/Despawn`。
- GAS Runtime 事件只在 `CombatAbilityComponent` 订阅，表现只经 `IBattlePresentationSink`。
- AbilityTask、AttributeSet、EventBus、Unity 回调必须在结束、池化或 Dispose 时成对反订阅。
- 投射物和范围攻击必须验证敌我关系；AI 和随机巡逻只使用战斗上下文随机数。
- 资源缓存不能淘汰仍被实例使用的资源；异步加载与销毁交错时不得重复释放 Handle。

## 5. 当前结论

BattleCommon 已将通用战斗行为集中在独立层：玩法只需要配置单位、选择目标并使用业务 SkillId；复杂 Ability/Effect 仍可在内部使用 GAS 高级能力。Actor 生命周期、GAS 事件、表现出口、投射物关系过滤、资源缓存与动态回放均有明确的统一入口，能够供多个玩法复用而不污染 Foundation 或 GAS 核心。
