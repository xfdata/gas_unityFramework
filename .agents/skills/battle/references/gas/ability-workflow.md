# GAS Ability Workflow

Use this when creating or modifying a combat skill, battle skill, gameplay ability, ability task, melee/ranged attack, born/death behavior, or damage block.

## 1. Select Ability Type

Prefer an existing ability shape when possible:

- `MeleeAttackAbilityDefinition`: animation/timeline driven melee hit plus damage effect.
- `RemoteAttackAbilityDefinition`: projectile-driven ranged attack.
- `BornAbilityDefinition`: effects on spawn/birth.
- `DeathAbilityDefinition`: death tags/effects/fade out.
- `DamageBlockAbilityDefinition`: intercept incoming damage in `CombatDamageExecution`.
- `GameplayAbilityDefinition`: generic ability with configured effects and delayed effects.

Create a new subclass only when the skill needs custom activation logic, new task orchestration, or a new combat data shape.

## 2. File Placement

- Ability definition: `unity_project/Assets/Scripts/HotUpdate.Core/Battle/BattleCommon/Abilities/Ability/<Name>Ability.cs`.
- Task: `unity_project/Assets/Scripts/HotUpdate.Core/Battle/BattleCommon/Abilities/Task/AbilityTask<Name>.cs`.
- Definition data: `unity_project/Assets/Scripts/HotUpdate.Core/Battle/BattleCommon/Abilities/Definition/<Name>Definition.cs`.
- Generic task or runtime behavior: `unity_project/Assets/Scripts/HotUpdate.Core/GAS/Ability/`.

Use namespace `GAS` for ability/task/definition files unless the surrounding file uses `BattleCommon`.

## 3. Implement Activation

For a custom `GameplayAbilityDefinition` subclass:

1. Override `ActivateAbility(GameplayAbilitySpec spec)`.
2. Call `ApplyConfiguredEffects(spec)` early when configured effects should still work.
3. Stop immediately if `spec.IsEnded`.
4. Call `StartDelayedEffects(spec)` if delayed effects should run.
5. Resolve source/target from `spec.Source`, `spec.Target`, and provider interfaces on `spec.Source.AttributeOwner`.
6. Add long-running operations with `spec.AddTask(...)`.
7. End with `spec.EndAbility(GameplayAbilityEndReason.Completed)` or `Failed` when no task will end it.

Do not directly tick ability logic from Unity. Put time-based behavior in `AbilityTask.Tick`.

## 4. Ability Tasks

An `AbilityTask` should:

- own exactly one asynchronous concern such as montage, timeline, projectile, delay, or hit window;
- call `EndTask` when finished;
- clean callbacks/subscriptions in its end/dispose path;
- record enough state if restore/replay support is needed;
- avoid owning unrelated damage formulas.

Existing examples:

- `AbilityTaskPlayMontage`
- `AbilityTaskPlayTimeline`
- `AbilityTaskApplyMeleeHit`
- `AbilityTaskSpawnProjectile`

## 5. Combat Entry Points

Ordinary battle code must use the business facade on `CombatActor`:

- `actor.Gameplay.Skills.TryCast(skillId, target, level)` for skills, including normal attacks;
- `source.Gameplay.Effects.Apply(effectId, target, parameters)` for one-shot business effects;
- `target.Gameplay.Buffs.Apply(buffId, source, parameters)` for buffs. The facade owner is always the buff target.

AI, units, UI, and level code must not construct `GameplayAbilitySpec`, `GameplayEffectSpec`, or `TargetData`, and must not resolve raw ability ids or GAS handles. `GameplayAbilityDefinition` subclasses and their tasks may still use native GAS APIs internally.

`CombatAbilityComponent` exposes low-level integration APIs for framework code:

- `GrantAbility(GameplayAbilityDefinition ability)`
- `GrantAbility(int abilityId)`
- `TryActivateBornAbility()`
- `TryActivateAttackAbility(CombatActor target)`
- `TryActivateDeathAbility(CombatActor killer)`
- `TryActivateById(int abilityId)`
- `TryBlockIncomingDamage(DamageBlockContext blockContext)`

Add a new entry point only when the facade cannot express a reusable business need or the activation needs typed combat context. Do not add a PVE-specific entry point to the framework.

## 6. Asset Wiring

For a new ability asset:

- set `AbilityId`;
- set `AbilityTag`;
- configure source/target/activation tag queries;
- set `ActivationOwnedTags` for active state;
- configure `CostEffects`, `CooldownEffects`, `EffectsOnActivate`, or `DelayedEffects`;
- add custom fields such as hit definition, projectile definition, damage effect, animation clip, timeline, or event name;
- add the asset to `GameplayDefinitionCatalog` if it is granted/activated by id.

## 7. Review Checklist

- The ability cannot activate when source/target tags should block it.
- The ability ends when tasks finish, fail, or source/target become invalid.
- Effects are applied through specs so source/target ids, level, context, and set-by-caller values are preserved.
- The lightweight mode path in `CombatAbilityComponent` is considered if the ability should work outside full GAS.
- Lightweight activation preserves the requested ability/effect/projectile level just as FullGas does.
- Animation/timeline event names have fallback behavior when the event is missing.
- No task keeps stale actor, component, projectile, or Unity callback references after end.
