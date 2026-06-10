# Battle Module Overview

Target folders:

- `Assets/Scripts/HotUpdate.Game/BattleFoundation/`
- `Assets/Scripts/HotUpdate.Game/BattleCommon/`
- `Assets/Scripts/HotUpdate.Core/GAS/`

## Layer Model

`BattleFoundation` is the runtime skeleton. It owns battle phase, frame ticking, commands, systems, entities, events, random seed, rules, and replay.

`BattleCommon` is the concrete combat implementation. It owns actor components, AI, target query, navigation/physics/projectiles, combat attributes, combat-domain ability definitions, and combat-domain effect executions.

`HotUpdate.Core/GAS` is the reusable gameplay ability system. It owns definitions, specs, runtimes, tasks, tags, cues, effect application, state capture/restore, and debugging.

## BattleFoundation Main Types

- `BattleEngine`: phase machine, runtime settings, tick mode, command execution, rule checks, replay, and disposal.
- `BattleContext`: creates `EntityManager`, `BattleEventBus`, `BattleSystemManager`, and `BattleRandom`.
- `IBattleSystem`: system lifecycle contract: `Initialize`, `Start`, `Update`, `LateUpdate`, `Dispose`.
- `BattleSystemManager`: stores and drives registered systems.
- `BattleEntity` and `EntityComponent`: entity/component base model.
- `EntityManager`: entity registration and lookup.
- `BattleEventBus`: event dispatch by integer event id.
- `BattleCommand` and `CommandQueue`: deferred command execution before systems update.
- `BattleRuleBase`, `WinLoseConditionBase`, `TimeoutRule`, `AllEnemiesDeadRule`: battle result rules.
- `BattleRecorder`, `BattlePlayback`, `IBattleReplayAdapter`: capture and replay state.
- `BattleRuntimeSettings`, `BattleFoundationConfig`: tick mode, seed, time scale, replay settings.

## BattleCommon Main Types

- `CombatActor`: `BattleEntity` implementation that bridges Unity object state, attributes, target interfaces, animation provider, and effects.
- `CombatAttributeComponent`: wraps `GAS.AttributeSet` and exposes combat attribute helpers.
- `CombatHealthComponent`: clamps HP, emits damage/heal/death events, and activates death ability.
- `CombatAbilityComponent`: owns full `GameplayAbilitySystem` or lightweight effects/ability mode.
- `CombatAttackComponent`: attack interval/range gate and attack ability activation.
- `CombatMovementComponent`: movement component with `IMovementMotor` support.
- `CombatTargetQuerySystem`: target lookup and melee target collection.
- `CombatActorSystem`: actor update/management system.
- `CombatAIComponent` and behavior classes: idle, attack, chase, flee, skill behavior.
- `CombatAIProfile` and `CombatAIPresetBuilder`: reusable AI tuning and preset construction.
- `CombatProjectileSystem` and `ProjectileRuntime`: projectile spawning and hit lifecycle.
- `CombatDamageExecution`: combat damage formula and damage block integration.

## GAS Main Types

- `GameplayAbilitySystem`: facade over ability runtime and effect runtime.
- `GameplayAbilityDefinition`: ScriptableObject ability data and default activation behavior.
- `GameplayAbilitySpec`: active ability instance, target, tasks, and effect application.
- `AbilityTask`: asynchronous ability task base.
- `GameplayEffectDefinition`: ScriptableObject effect data, modifiers, duration, stack, tags, executions, cues.
- `GameplayEffectRuntime`: active effect application, ticking, stacking, tag/cue handling.
- `GameplayEffectSpec`: runtime effect payload with set-by-caller, captured values, position, user data.
- `GameplayEffectExecution`: effect formula hook.
- `GameplayDefinitionCatalog`: ability/effect lookup by id.
- `AttributeSet`: base values, modifiers, capture/restore.

## File Role Map

Foundation:

- `Core/`: engine, context, phases, system manager.
- `Entity/`: battle entity and entity manager.
- `Event/`: event buses and event ids.
- `Command/`: battle commands.
- `Rule/`: win/loss and timeout rules.
- `Replay/`: recorder/playback state.
- `Config/`: runtime settings.
- `Data/`: legacy/foundation attribute model.
- `Utils/`: deterministic random.

Common:

- `Combat/`: components, contracts, actor/target systems, animation time scale.
- `Entity/`: `CombatActor`.
- `AI/`: combat behavior profiles and components.
- `GAS/Ability/`: combat ability definitions.
- `GAS/Definition/`: combat hit/projectile definitions and provider interfaces.
- `GAS/Task/`: combat ability tasks.
- `GAS/Effect/`: combat-specific effect executions.
- `GAS/Runtime/`: projectile runtime.
- `Projectile/`, `Physics/`, `Navigation/`, `Assets/`: support systems.

Core GAS:

- `Ability/`: generic ability definitions/runtime/tasks.
- `Effect/`: generic effect definitions/specs/active effects.
- `EffectExecution/`: generic executions.
- `Cue/`: cue data and manager.
- `Debug/`, `Editor/`: debugging tools.

## NewPVE Extraction Notes

`NewPVE` should remain the battle-mode implementation layer. Keep mode runtime, input, formation/rush/section logic, NewPVE config adapters, and test/debug code there. Move mode-neutral combat behavior into `BattleCommon`, and move deterministic engine concerns into `BattleFoundation`.

High-value `NewPVE` extraction candidates:

- AI enums/config/behaviors/decision/presets under `NewPVE/AI/` should be represented by `BattleCommon/AI` types. Keep only NewPVE profile/preset adapters in NewPVE.
- Actor presentation and animation components should use `BattleCommon/Combat/Components` instead of duplicate NewPVE classes.
- Gameplay cue handling for hit and poison should use `BattleCommon/GAS/Cue/CombatGameplayCueManager`.
- Damage execution should use `BattleCommon/GAS/Effect/CombatDamageExecution` or a shared combat formula service; avoid PVE-only damage duplicates unless the formula is truly mode-specific.
- Actor factory/pool/manager and spawn wave logic can be partially extracted after removing NewPVE table, monster-type, section, and rush dependencies.

See `references/ai-workflow.md` for the detailed AI extraction audit and migration checklist.
