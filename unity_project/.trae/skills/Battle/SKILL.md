# Battle Skill

当问题涉及 `BattleEngine`、`BattleContext`、`BattleEntity`、`CombatActor`、`BattleSystem`、战斗框架、Entity-Component、回放、确定性随机、AI、弹道、近战判定、寻路、战斗属性时，优先使用本 skill。

## 文档顺序

1. `README.md`
   - Battle 框架总览，分层架构（Foundation/Common），整体调用链。
2. `BattleFoundation.md`
   - BattleEngine、BattleContext、EntityManager、BattleSystemManager、BattleEventBus、BattleRule、BattleRandom、回放系统（Recorder/Playback）。
3. `BattleCommon.md`
   - CombatActor、CombatComponent（Attribute/Health/Ability/Attack/Movement/AI）、CombatAI、CombatSystems、GAS 扩展（AbilityTask/Projectile/Melee/Damage）、Physics、Pathfinding、ProjectileSystem、AssetCache、AnimationTimeScale。

## 核心模型

```text
BattleEngine
  -> BattleContext
       -> EntityManager (BattleEntity / EntityComponent)
       -> BattleSystemManager (IBattleSystem[])
       -> BattleEventBus
       -> BattleRandom
       -> BattleRecorder / BattlePlayback (回放)
  -> BattleRuleBase[] (胜负条件)
  -> CommandQueue (帧同步命令)

BattleEntity / CombatActor
  -> EntityComponent[]
       -> CombatAttributeComponent
       -> CombatHealthComponent
       -> CombatAbilityComponent (GAS)
       -> CombatAttackComponent
       -> CombatMovementComponent
       -> CombatAIComponent

GAS Extensions (BattleCommon)
  -> BornAbilityDefinition / MeleeAttackAbilityDefinition / RemoteAttackAbilityDefinition
  -> AbilityTaskPlayMontage / AbilityTaskPlayTimeline
  -> AbilityTaskApplyMeleeHit / AbilityTaskSpawnProjectile
  -> ProjectileRuntime (确定性弹道)
  -> CombatDamageExecution
  -> RangedProjectileDefinition / MeleeHitDefinition

Combat Systems (IBattleSystem)
  -> CombatTargetQuerySystem
  -> CombatActorSystem
  -> CombatPhysicsSystem
  -> CombatPathfindingSystem
  -> CombatProjectileSystem
  -> AnimationTimeScaleSystem
```

## 关键词

- `BattleEngine` / `BattleContext` / `IBattleContext`
- `EBattlePhase` / `EBattleTickMode`
- `BattleEntity` / `EntityComponent` / `EEntityCamp` / `EEntityType`
- `EntityManager`
- `BattleSystemManager` / `IBattleSystem`
- `BattleEventBus` / `BattleEventIds`
- `BattleCommand` / `CommandQueue`
- `BattleRuntimeSettings` / `BattleFoundationConfig`
- `BattleRuleBase` / `TimeoutRule` / `AllEnemiesDeadRule`
- `BattleRandom`
- `BattleRecorder` / `BattlePlayback` / `BattleRecord` / `FrameRecordData`
- `EntitySnapshot` / `IBattleReplayAdapter`
- `BattleAttribute` / `BattleAttributeSet` / `AttributeSnapshot`
- `CombatActor` / `CombatComponentBase`
- `CombatAttributeComponent` / `CombatHealthComponent` / `CombatAbilityComponent`
- `CombatAttackComponent` / `CombatMovementComponent` / `CombatAIComponent`
- `CombatLoadoutDefinition`
- `CombatAIProfile` / `CombatAIComponent` / `CombatBehaviorBase`
- `CombatTargetQuerySystem` / `CombatActorSystem` / `CombatPhysicsSystem`
- `CombatPathfindingSystem` / `CombatProjectileSystem`
- `AnimationTimeScaleSystem`
- `CombatAssetCache`
- `CombatPrimitives` / `CombatAttributeIds` / `CombatAbilityIds`
- `ICombatTarget` / `IMeleeSource` / `IRangedSource` / `IMovementMotor`
- `ICombatRelationResolver` / `ICombatTargetQuery`
- `BornAbilityDefinition` / `MeleeAttackAbilityDefinition` / `RemoteAttackAbilityDefinition`
- `MeleeHitDefinition` / `IMeleeAttackSourceProvider`
- `RangedProjectileDefinition` / `IRangedAttackSourceProvider` / `IRangedTarget`
- `ProjectileRuntime` / `RangedProjectileHandle` / `RangedProjectileRequest`
- `AbilityTaskApplyMeleeHit` / `AbilityTaskSpawnProjectile`
- `AbilityTaskPlayMontage` / `AbilityTaskPlayTimeline`
- `CombatDamageExecution` / `CombatDamageKeys`
- `NavMeshMovementMotor` / `CombatNavMeshUtility`
- `IAnimationTimeScaleServices` / `IAnimancerProvider` / `ITimelineProvider`

## 相关源码

- `Assets/Scripts/HotUpdate.Game/BattleFoundation/`
- `Assets/Scripts/HotUpdate.Game/BattleCommon/`
- `Assets/Scripts/HotUpdate.Core/GAS/`