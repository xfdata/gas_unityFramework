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

- Ability definition: `Assets/Scripts/HotUpdate.Game/BattleCommon/GAS/Ability/<Name>Ability.cs`.
- Task: `Assets/Scripts/HotUpdate.Game/BattleCommon/GAS/Task/AbilityTask<Name>.cs`.
- Definition data: `Assets/Scripts/HotUpdate.Game/BattleCommon/GAS/Definition/<Name>Definition.cs`.
- Generic task or runtime behavior: `Assets/Scripts/HotUpdate.Core/GAS/Ability/`.

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

`CombatAbilityComponent` already exposes:

- `GrantAbility(GameplayAbilityDefinition ability)`
- `GrantAbility(int abilityId)`
- `TryActivateBornAbility()`
- `TryActivateAttackAbility(CombatActor target)`
- `TryActivateDeathAbility(CombatActor killer)`
- `TryActivateById(int abilityId)`
- `TryBlockIncomingDamage(DamageBlockContext blockContext)`

Add a new entry point only when callers cannot reasonably use `TryActivateById` or the activation needs typed combat context.

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
- Animation/timeline event names have fallback behavior when the event is missing.
- No task keeps stale actor, component, projectile, or Unity callback references after end.
