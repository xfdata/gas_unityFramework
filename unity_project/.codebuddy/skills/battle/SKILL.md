---
name: battle
description: "Use this skill when the user works on Unity battle code under Assets/Scripts/HotUpdate.Game/BattleFoundation/, Assets/Scripts/HotUpdate.Game/BattleCommon/, Assets/Scripts/HotUpdate.Core/GAS/, or asks to create/add/modify/review a battle, combat actor, battle system, combat AI, projectile, melee/ranged attack, combat skill, gameplay ability, gameplay effect, GAS task, GAS execution, damage/block effect, cue, attribute, replay, battle rule, 战斗, 战斗技能, 技能效果, 效果, or GAS. This skill is for BattleFoundation engine/context/entity/event/rule/replay work, BattleCommon combat systems and combat-domain GAS extensions, and HotUpdate.Core GAS ability/effect runtime work."
---

# Battle Skill

Use this skill to modify and extend the Unity battle stack centered on `BattleFoundation`, `BattleCommon`, and `GAS`.

## First Reads

Start with the smallest set that matches the task:

- Battle engine or lifecycle: read `references/module-overview.md`, then `Assets/Scripts/HotUpdate.Game/BattleFoundation/Core/BattleEngine.cs`, `BattleContext.cs`, `BattleSystemManager.cs`, and `BattleCore.cs`.
- Creating a new battle mode or runtime: read `references/battle-creation.md`.
- Combat actor/system/projectile work: read `references/module-overview.md`, then the relevant file under `Assets/Scripts/HotUpdate.Game/BattleCommon/`.
- Combat AI work or NewPVE AI extraction: read `references/module-overview.md` and `references/ai-workflow.md`, then the relevant file under `Assets/Scripts/HotUpdate.Game/BattleCommon/AI/`.
- GAS ability, skill, task, effect, execution, cue, or attribute work: read `references/gas/overview.md` first, then `references/gas/ability-workflow.md` or `references/gas/effect-workflow.md`.
- Risk review or bug fixing: read `references/risk-points.md`.

## Module Boundaries

Keep responsibilities separated:

- `BattleFoundation` owns deterministic battle runtime structure: engine phase, context, systems, entities, event bus, commands, rules, random, replay.
- `BattleCommon` owns concrete combat behavior: actors, attributes, health, movement, attacks, target query, AI, projectile runtime, combat asset cache, and battle-specific GAS definitions/tasks/executions.
- `HotUpdate.Core/GAS` owns reusable gameplay ability/effect runtime: ability definitions/specs/tasks, effect definitions/specs/runtime, tags, cues, attribute sets, state capture/restore, and debugging.

Prefer extending `BattleCommon/GAS` for combat-specific abilities/effects before changing core `HotUpdate.Core/GAS`. Change core GAS only when the behavior is truly generic and all combat usages benefit from the new contract.

For player state work, prefer representing state through `GameplayTag` definitions first. If the behavior can be implemented through GAS abilities, effects, tags, cues, or executions, prefer the GAS path before adding bespoke battle-state fields or systems.

## Workflows

When creating or changing a battle runtime:

1. Identify the owning layer: foundation runtime, common combat system, or GAS.
2. Search existing implementations before adding new abstractions.
3. Preserve lifecycle order: `Initialize`, `Start`, `Update`, `LateUpdate`, `Dispose`.
4. Add systems through `BattleContext.AddSystem<T>` so initialization and registration stay consistent.
5. Route frame-driving behavior through `BattleEngine` or `IBattleSystem` instead of ad hoc Unity updates.

When creating a combat skill:

1. Prefer a `GameplayAbilityDefinition` subclass under `Assets/Scripts/HotUpdate.Game/BattleCommon/GAS/Ability/`.
2. Put reusable hit/projectile/target shape data under `BattleCommon/GAS/Definition/`.
3. Put long-running or animation/timeline/projectile logic in `AbilityTask` subclasses under `BattleCommon/GAS/Task/`.
4. Apply damage, buffs, cooldowns, and state changes through `GameplayEffectDefinition` and executions rather than mutating attributes directly from the ability.
5. Wire activation through `CombatAbilityComponent` only when the skill needs a new combat entry point.

When creating a gameplay effect:

1. Use `GameplayEffectDefinition` for data and duration/stack/tag/modifier setup.
2. Use `GameplayEffectExecution` when the effect needs formula logic, set-by-caller values, block/counter logic, or multiple attributes.
3. Keep combat formulas under `BattleCommon/GAS/Effect/`; keep generic effect runtime behavior under `HotUpdate.Core/GAS/`.
4. Update catalogs or asset references when the new ability/effect must be grantable by id.

## Implementation Guardrails

- Treat combat config objects and profiles as shared data unless proven otherwise. Clone runtime copies before applying per-actor overrides such as boss skill ids, patrol points, flee flags, cooldowns, or decision tuning.
- Projectile and area queries must use `ICombatRelationResolver` or equivalent source-aware relation checks. Do not rely on distance-only queries for damage or collision because they can hit allies or neutral actors.
- Asset cache eviction must respect live instance references. Capacity pressure may skip entries with `RefCount > 0`; explicit battle teardown can release after instances have been recycled or destroyed.
- Async combat loading must handle battle teardown. After each awaited load, verify the cache/system is still alive and that the loaded entry is still the current entry before writing back runtime state.
- Pooled request objects must clear callbacks and transient references before returning to the pool. Keep result data alive only when existing callbacks are expected to consume it after invocation.
- Avoid `UnityEngine.Random` fallbacks in simulation code. If `BattleRandom` or context is unavailable, prefer deterministic failure/center/default behavior over hidden nondeterminism.
- Keep remove/recycle/dispose semantics explicit for actors and systems. `Remove` should mean unregister only; use separate methods or clearly named paths for pool deactivation and final disposal.
- Do not mutate `EntityManager.All` while iterating it. Actor systems should queue add/remove/recycle/dispose operations during actor updates, skip actors pending removal, and flush structural changes after the loop.

## Validation

After code changes, use the narrowest reliable validation:

- Search exact references for changed public types, ids, tags, and asset menu paths.
- Compile the touched assembly when possible, especially `HotUpdate.Game` or `HotUpdate.Core`.
- For gameplay changes, reason through one frame of activation/tick/end/dispose and one pooled reuse path.
- Do not edit Unity `.meta`, prefab, scene, or asset files unless the request explicitly needs asset wiring.

## References

- `references/module-overview.md`: battle stack map, main types, and file roles.
- `references/ai-workflow.md`: combat AI ownership, NewPVE extraction audit, and migration checklist.
- `references/battle-creation.md`: workflow for creating a new battle mode/runtime/system.
- `references/gas/overview.md`: core GAS and combat GAS map.
- `references/gas/ability-workflow.md`: workflow for creating combat abilities and tasks.
- `references/gas/effect-workflow.md`: workflow for creating gameplay effects and executions.
- `references/risk-points.md`: common bugs and review checklist.
