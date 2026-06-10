# GAS Overview

Use this for ability, skill, effect, cue, tag, attribute, and combat skill work.

## Core GAS Folder

`Assets/Scripts/HotUpdate.Core/GAS/` contains reusable runtime code:

- `GameplayAbilitySystem`: grants, revokes, activates abilities; applies effects; ticks ability/effect runtime; captures/restores state.
- `Ability/GameplayAbility.cs`: `GameplayAbilityDefinition` and `GameplayAbilitySpec`.
- `Ability/GameplayAbilityRuntime.cs`: granted and active ability management.
- `Ability/AbilityTask.cs`: base and delay task.
- `Effect/GameplayEffect.cs`: `GameplayEffectDefinition`.
- `Effect/GameplayEffectSpec.cs`: runtime effect payload.
- `Effect/ActiveGameplayEffect.cs`: active duration/stacked effect.
- `GameplayEffectRuntime.cs`: effect application, stacking, modifier/tag/cue handling.
- `GameplayEffectExecution.cs`: formula hook.
- `AttributeSet.cs`: base values, modifiers, capture/restore.
- `GameplayDefinitionCatalog.cs`: id lookup for abilities/effects.
- `Cue/`: gameplay cue payload, set, manager.
- `GameplayStateTypes.cs`: state structs used by capture/restore.

## Combat GAS Folder

`Assets/Scripts/HotUpdate.Game/BattleCommon/GAS/` contains battle-specific extensions:

- `Ability/`: `BornAbilityDefinition`, `DeathAbilityDefinition`, `MeleeAttackAbilityDefinition`, `RemoteAttackAbilityDefinition`, `DamageBlockAbilityDefinition`.
- `Definition/`: `MeleeHitDefinition`, `RangedProjectileDefinition`, provider/target interfaces.
- `Task/`: animation/timeline/melee/projectile ability tasks.
- `Effect/`: `CombatDamageExecution` and combat damage set-by-caller keys.
- `Runtime/`: projectile runtime.

## Runtime Shape

`CombatAbilityComponent` creates either:

- full GAS mode: `GameplayAbilitySystem` with ability runtime plus effect runtime;
- lightweight mode: direct `GameplayEffectRuntime` plus simplified ability activation for born, death, melee, and ranged attacks.

Abilities usually apply effects through `GameplayAbilitySpec.ApplyGameplayEffect`. Effects mutate tags/attributes or run executions through `GameplayEffectRuntime`.

## Tags And Queries

Ability and effect definitions use `TagQuery` fields:

- source required/blocked tags;
- target required/blocked tags;
- activation required/blocked tags;
- activation owned tags;
- cancel/block ability tags;
- effect granted tags.

Use tags for state gates such as attacking, dead, immune, shielded, stunned, cooldown, or ability-specific blocking.

## Catalogs And Ids

When granting or activating by id, make sure `GameplayDefinitionCatalog` contains the new `GameplayAbilityDefinition` or `GameplayEffectDefinition`.

Use explicit `AbilityId` and `EffectId` values. Search existing ids before choosing a new one.

## Where To Put New Code

- New combat ability: `BattleCommon/GAS/Ability/`.
- New combat task: `BattleCommon/GAS/Task/`.
- New hit/projectile/shape data: `BattleCommon/GAS/Definition/`.
- New combat effect execution: `BattleCommon/GAS/Effect/`.
- Generic ability/effect runtime feature: `HotUpdate.Core/GAS/`.
- Generic execution not tied to combat attributes: `HotUpdate.Core/GAS/EffectExecution/`.
