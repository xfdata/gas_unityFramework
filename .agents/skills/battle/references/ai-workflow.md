# Combat AI Workflow

Use this when creating, moving, or reviewing combat AI code.

## Ownership

`BattleCommon/AI` owns reusable combat AI:

- AI enums and profiles: behavior type, behavior state, priority, threat level, and tunable profile data.
- Behavior primitives: idle, attack, chase, flee, patrol, skill, and other mode-neutral combat decisions.
- AI components: target selection, decision interval, retarget cooldown, active behavior lifecycle, pooling cleanup, and links to movement/attack/ability/health components.
- Preset builders: common melee, ranged, boss, patrol, player-hero, aggressive, and defensive presets.

`NewPVE/AI` should only keep NewPVE adapters:

- Mapping table data, monster data, or battle-mode data into a `CombatAIProfile`.
- Selecting a common preset from NewPVE monster type, attack type, wave data, or mode config.
- One-off mode behaviors that depend on NewPVE-only concepts such as rush segments, formation slots, section ids, scripted debug flows, or fixed hero path targets.

Do not duplicate `BattleCommon` AI classes in `NewPVE`. If an AI behavior only needs `CombatActor`, `CombatMovementComponent`, `CombatAttackComponent`, `CombatHealthComponent`, `CombatAbilityComponent`, target priority, or GAS ability ids, keep it in `BattleCommon`.

## NewPVE Extraction Audit

Prefer this split when cleaning `Assets/Scripts/HotUpdate.Game/NewPVE/`:

Move to `BattleCommon` directly:

- `AI/AIEnums.cs`: replace with `CombatAIBehaviorType`, `CombatAIBehaviorState`, `CombatAIBehaviorPriority`, and `CombatAIThreatLevel`.
- `AI/AIConfig.cs`: fold the generic fields into `CombatAIProfile`; keep only NewPVE-specific config adapters in NewPVE.
- `AI/AIBehaviorBase.cs`: fold common idle, chase, attack, flee, patrol, and skill behaviors into `BattleCommon/AI/CombatAI.cs` or split them into separate files under `BattleCommon/AI/Behaviors/`.
- `AI/AIDecisionComponent.cs`: merge its richer decision loop into `CombatAIComponent`, especially active behavior tracking, threat level, home position, retarget cooldown, and pool reset.
- `AI/AIPresetBuilder.cs`: merge preset construction into `CombatAIPresetBuilder`; NewPVE should call common presets instead of constructing behavior lists itself.
- `Actor/Components/ActorAnimationComponent.cs` and `Actor/Components/ActorPresentationComponent.cs`: these are combat presentation components and should live under `BattleCommon/Combat/Components/`.
- `GAS/Cue/NewPVEGameplayCueManager.cs`: use or extend `BattleCommon/GAS/Cue/CombatGameplayCueManager` for hit and poison cues.

Move to `BattleCommon` after extracting mode dependencies:

- `Actor/BattleActor.cs`: most actor state helpers, born/death hooks, GAS access, movement wrappers, animation/presentation access, and recycle checks are common; keep only `PVEContext`, `NewPVEWorld`, and NewPVE camp/type adapters in a mode subclass.
- `Actor/ActorManager.cs`: common actor registration, target range queries, alive filters, and camp queries can become a combat actor system/query service; NewPVE-specific monster-type indexing stays in NewPVE.
- `Actor/ActorObjectPool.cs` and `Actor/BattleActorFactory.cs`: pooling, common component assembly, NavMesh motor setup, and ability/component defaults can become a combat actor factory/pool; table lookups and hero/monster config conversion stay in NewPVE.
- `Spawn/SpawnerBase.cs`, `Spawn/SpawnSystem.cs`, and spawn data structs: generic wave/slot timing and spawn-area selection can become reusable battle spawn utilities; section id, rush segment, NewPVE monster type, and table conversion stay in NewPVE.
- `GAS/Effect/PVEMeleeDamageExecution.cs`: damage block and HP application are common. If the project must use `AttributeUtil.CalcFinalDamage`, expose that formula through `CombatDamageExecution` or a combat formula service instead of keeping a PVE-only duplicate.
- `GameplayTags/CombatGameplayTagsDef.gen.cs` and `GameplayTags/Editor/CombatGameplayTags.asset`: combat tags should live with `BattleCommon` when shared by combat abilities, cues, AI state, born/death, poison, hit, and damage block.

Keep in `NewPVE`:

- `Core/NewPVEWorld.cs`, `PVEContext.cs`, `NewPVEBattleConfig.cs`, and `NewPVEEnum.cs`: mode runtime, config, and NewPVE-specific enums.
- `Mode/*`: defend position, formation advance, rush mode, and their mode-only configs.
- `Formation/*`: formation slots and hero layout unless another battle mode needs the same formation contract.
- `Input/*`: PVE player input, camera zoom, and control mapping.
- `Core/RushMapManager.cs` and `Core/RushSegment.cs`: rush-map scene logic.
- `Replay/ReplaySystem.cs`: keep the NewPVE payload and adapter, but reuse foundation replay contracts.
- `TestBattle/*` and `AttackTypeExample.cs`: debug/example code.

## AI Migration Steps

When migrating NewPVE AI into `BattleCommon`:

1. Merge the profile data first, then update presets to consume `CombatAIProfile`.
2. Move behavior logic next; keep behavior code typed to `CombatActor` and common combat components.
3. Move the decision loop last, preserving pool cleanup and active behavior exit semantics.
4. Replace NewPVE call sites with `CombatAIPresetBuilder` and a small NewPVE profile adapter.
5. Search for old names (`AIDecisionComponent`, `AIConfig`, `AIBehaviorBase`, `HuntHeroAI`, `PlayerHeroAI`) and remove duplicate behavior once all call sites use common AI.

## Review Checklist

- AI behavior does not reference `NewPVEWorld`, `PVEContext`, NewPVE enums, section ids, formation slots, or rush segments unless it intentionally stays in NewPVE.
- Target selection goes through `CombatAttackComponent` or a common target query service.
- Movement goes through `CombatMovementComponent` or `IMovementMotor`.
- Ability activation goes through `CombatAbilityComponent`.
- Pooling calls exit the active behavior, clear target/threat timers, and release behavior references.
- Retarget and decision timers use battle delta time; deterministic AI should use `BattleRandom` from the context.
