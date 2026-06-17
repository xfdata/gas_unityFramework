# Battle 框架

Battle 框架分为两层：

- **BattleFoundation** — 核心基础框架（引擎、实体、系统、事件、回放、配置、规则、随机）。
- **BattleCommon** — 战斗通用实现（Actor、Component、AI、GAS 扩展、Physics、Pathfinding、Projectile）。

## 分层职责

| 层 | 职责 | 依赖 |
|---|---|---|
| BattleFoundation | Engine/Context/Entity/System/Event/Replay/Random | Framework |
| BattleCommon | CombatActor/Component/AI/GAS-Ability/Projectile/Physics | BattleFoundation + GAS |

## 整体调用链

### 初始化

```text
BattleFoundationConfig (ScriptableObject)
  -> BattleRuntimeSettings (TickMode / FrameSyncStep / EnableReplay / RandomSeed)

BattleEngine.Initialize()
  -> CreateContext() -> BattleContext
       -> EntityManager
       -> BattleEventBus
       -> BattleSystemManager
       -> BattleRandom(seed)
  -> Create Systems (IBattleSystem[]) 添加到 Context.SystemManager
  -> BattleRecorder (如果 EnableReplay)
  -> 进入 EBattlePhase.Ready
```

### 战斗开始

```text
BattleEngine.StartBattle()
  -> ChangePhase(EBattlePhase.Running)
  -> Context.Start() -> SystemManager.Start() (所有 System.Start())
  -> Recorder.StartRecording()
  -> Record initial frame
```

### Tick 循环

```text
BattleEngine.UpdateFromUnity(unityDeltaTime)
  -> 根据 EBattleTickMode:
     - RealTime:  TickSimulation(unityDeltaTime * TimeScale)
     - FrameSync: 积攒到 FrameSyncStep 后 TickSimulation(FrameSyncStep)
     - TurnBased: TickTurn() -> TickSimulation(0f)

  TickSimulation(deltaTime):
    1. ExecutePendingCommands()
    2. Context.Update(deltaTime) -> SystemManager.Update(deltaTime)
       -> EntityManager.All[i].Update(deltaTime)
          -> EntityComponent[].Update(deltaTime)
    3. OnUpdate(deltaTime) (子类重写)
    4. Context.LateUpdate(deltaTime) -> SystemManager.LateUpdate(deltaTime)
    5. CheckEndConditions() -> Rules[].Update(deltaTime)
    6. Recorder.RecordFrame(...)
```

### Entity Component 生命周期

```text
AddComponent<T>(component)
  -> component.Attach(entity)
  -> entity.Initialize()
     -> component.Initialize()
     -> 遍历所有 component.Update(deltaTime)

对象池回收:
  entity.DeactivateForPool()
    -> component.DeactivateForPool()

对象池取出:
  entity.ActivateForPool(id, camp, type)
    -> component.ActivateForPool(entity)
```

### 回放流程

```text
BattleEngine.StartReplay(replayData)
  -> BattlePlayback.Initialize(record, context, adapter, onCompleted)
  -> ChangePhase(EBattlePhase.Replaying)
  -> Playback.Update(deltaTime)
     -> 逐帧 ApplyFrame(frameData)
        -> 创建/更新 Entity
        -> 移除不在帧中的 Entity
```

### GAS 集成

```text
CombatAbilityComponent.Initialize()
  -> new GameplayAbilitySystem(entityId, Owner, null, abilityCatalog, cueManager)
  -> Grant initial abilities (from CombatLoadoutDefinition)

CombatAbilityComponent.Update(deltaTime)
  -> GAS.Tick(deltaTime)
     -> effectRuntime.Tick(deltaTime)
     -> abilityRuntime.Tick(deltaTime)

CombatActor 实现 IGameplayAttributeOwner, IGameplayAttributeSetProvider, IMeleeSource
  -> GAS 通过 AttributeOwner 访问 CombatAttributeComponent.AttributeSet
  -> GAS 通过 IMeleeSource.GetMeleeTargets() 获取近战目标
```

## 核心类速查

### BattleFoundation

| 类 | 职责 |
|---|---|
| `BattleEngine` | 战斗主循环，管理 Phase 切换、Tick Mode、Pause/Resume、Replay、CommandQueue |
| `BattleContext` | 运行时共享状态容器（Engine/EntityManager/EventBus/SystemManager/Random） |
| `BattleEntity` | 实体基类，支持 Component 模型（Add/Get/Remove）+ 对象池 |
| `EntityManager` | 实体生命周期管理（Add/Remove/GetById/GetByCamp/FindInRange） |
| `IBattleSystem` | 系统接口（Initialize/Start/Update/LateUpdate/Dispose） |
| `BattleSystemManager` | 系统注册和遍历（按注册顺序 Update/LateUpdate） |
| `BattleEventBus` | 类型安全的事件总线（On/Off/Emit，按 eventId 路由） |
| `BattleCommand` | 帧同步命令抽象（Execute 一次后 Consumed） |
| `BattleRuntimeSettings` | TickMode / FrameSyncStep / EnableReplay / RandomSeed |
| `BattleFoundationConfig` | ScriptableObject 配置资产 |
| `BattleRuleBase` | 胜负规则基类（IsTriggered/Result），子类：TimeoutRule / AllEnemiesDeadRule |
| `BattleRandom` | 确定性随机数（xorshift 算法） |
| `BattleRecorder` | 逐帧录像（FrameRecordData[]） |
| `BattlePlayback` | 录像回放（按 Timestamp 推进帧） |
| `BattleAttribute` | 单个属性（BaseValue + Modifier + MinMax + 脏标记缓存） |
| `BattleAttributeSet` | 属性集合（Register/Get/Set/Snapshot/Restore） |

### BattleCommon

| 类 | 职责 |
|---|---|
| `CombatActor` | 战斗实体（GameObject/Transform/Animator，实现 IGameplayAttributeOwner/IMeleeSource 等） |
| `CombatAttributeComponent` | 属性组件（封装 AttributeSet，ApplyLoadout） |
| `CombatHealthComponent` | 血量组件（TakeDamage/Heal/Die/HPPercent） |
| `CombatAbilityComponent` | GAS 组件（创建 GameplayAbilitySystem，Grant/Activate ability） |
| `CombatAttackComponent` | 攻击组件（TryAttack/FindTarget/AttackTimer） |
| `CombatMovementComponent` | 移动组件（MoveTo/StopMove/FollowPath/NavMeshAgent） |
| `CombatAIComponent` | AI 组件（Behavior 优先级决策：Flee > Skill > Attack > Chase > Idle） |
| `CombatLoadoutDefinition` | ScriptableObject 配置（属性、Abilities、AI Profile） |
| `CombatTargetQuerySystem` | 目标查询（FindTarget/FindInRange/FindMeleeTargets） |
| `CombatActorSystem` | Actor 管理（Add/Remove + CanRecycle 检查 + Update 遍历） |
| `CombatPhysicsSystem` | 物理系统（OverlapSphere/RaycastGround/LayerCollision） |
| `CombatPathfindingSystem` | 寻路系统（NavMesh.CalculatePath + 异步请求队列） |
| `CombatProjectileSystem` | 弹道系统（持有 ProjectileRuntime，提供 CollisionQuery） |
| `AnimationTimeScaleSystem` | 动画时缩系统（Animator/Animancer/PlayableDirector 速度控制） |
| `CombatAssetCache` | 资产缓存（LRU，支持预加载/延迟加载/Instantiate/Pin） |

### GAS 扩展（BattleCommon）

| 类 | 职责 |
|---|---|
| `BornAbilityDefinition` | 出生技能（Timeline/Montage + SelfBornEffect + TargetBornEffect） |
| `MeleeAttackAbilityDefinition` | 近战攻击（Animation/Timeline + MeleeHit + DamageEffect） |
| `RemoteAttackAbilityDefinition` | 远程攻击（Projectile + DamageEffect + Fire Event） |
| `MeleeHitDefinition` | 近战判定参数（Range/Radius/MaxTargets） |
| `RangedProjectileDefinition` | 弹道参数（Speed/MaxLifeTime/Trajectory/Explosion/Sweep） |
| `CombatDamageExecution` | 伤害计算（Attack * Factor * Increase * Reduction - Defense - Absolute） |
| `ProjectileRuntime` | 确定性弹道运行时（线性/抛物，sweep 碰撞，AOE） |
| `AbilityTaskApplyMeleeHit` | 近战命中 Task（MeleeWindowStarted -> ApplyTargets -> MeleeWindowEnded） |
| `AbilityTaskSpawnProjectile` | 生成弹道 Task（Spawn + OnCompleted 回调） |
| `AbilityTaskPlayMontage` | Animancer 动画 Task（事件回调：EnableCollision/DisableCollision + 自定义事件） |
| `AbilityTaskPlayTimeline` | Timeline 动画 Task（Marker 事件 + 自动结束） |

## 注意事项

- BattleFoundation 不依赖 GAS 和 Unity 场景相关 API，可脱离 Unity 独立测试。
- `BattleEngine` 是 abstract 类，具体战斗模式（PVE/PVP）需子类化并重写 `CreateContext()`、`OnInitialize()`、`OnBattleStart()` 等方法。
- `IBattleSystem` 的 Update 和 LateUpdate 在同一个 TickSimulation 帧中调用，顺序为 Update(全部) -> LateUpdate(全部)。
- `BattleSystemManager` 按注册顺序遍历系统，早注册的 System 先 Update/LateUpdate。
- `BattleRandom` 使用 xorshift 算法，种子相同则序列相同，支持确定性模拟和帧同步。
- `BattleRecorder` 每帧捕获所有 Entity 的 EntitySnapshot，`BattlePlayback` 按 Timestamp 重放。
- `IBattleReplayAdapter` 允许业务层自定义回放实体创建/销毁逻辑。
- `CombatAttackComponent.TryAttack` 依赖 CombatAbilityComponent 触发 GAS Ability，实际伤害由 `CombatDamageExecution` 计算。
- `ProjectileRuntime` 是纯逻辑的确定性弹道系统，与表现分离，`CombatProjectileSystem` 负责桥接 Unity 碰撞查询。
- `CombatAIComponent` 行为优先级从高到低：Flee（逃走）-> Skill（技能）-> Attack（攻击）-> Chase（追击）-> Idle（待机），只在 CanEnter 且进入后立即执行。
- `CombatActor` 通过 `CanRecycle`（Health.IsDead）决定是否回收，由 `CombatActorSystem` 在 Update 中检查。
- `AnimationTimeScaleSystem` 通过 `IAnimationTimeScaleServices` 获取全局时缩参数，统一控制 Animator.speed / Animancer.Playable.Speed / PlayableDirector 速度。