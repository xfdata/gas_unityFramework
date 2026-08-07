---
name: battle
description: "Implement or review Unity battle code under unity_project/Assets/Scripts/HotUpdate.Core/Battle/BattleFoundation/, unity_project/Assets/Scripts/HotUpdate.Core/Battle/BattleCommon/, or unity_project/Assets/Scripts/HotUpdate.Core/GAS/. Use for battle runtime, combat actor, combat AI, projectile, melee/ranged attack, combat skill, gameplay ability/effect/task/execution, cue, attribute, replay, battle rule, 战斗, 战斗技能, 技能效果, 效果, or GAS."
---

# Battle Skill

Use this skill to modify and extend the Unity battle stack centered on `BattleFoundation`, `BattleCommon`, and `GAS`.

## First Reads

Read only the references needed for the task:

- Battle engine or lifecycle: read `references/module-overview.md`, then the relevant file under `unity_project/Assets/Scripts/HotUpdate.Core/Battle/BattleFoundation/`.
- Creating a new battle mode or runtime: read `references/battle-creation.md`.
- Combat actor/system/projectile work: read `references/module-overview.md`, then the relevant file under `unity_project/Assets/Scripts/HotUpdate.Core/Battle/BattleCommon/`.
- Combat AI work or NewPVE AI extraction: read `references/module-overview.md` and `references/ai-workflow.md`, then the relevant file under `unity_project/Assets/Scripts/HotUpdate.Core/Battle/BattleCommon/AI/`.
- GAS ability, skill, task, effect, execution, cue, or attribute work: read `references/gas/overview.md` first, then `references/gas/ability-workflow.md` or `references/gas/effect-workflow.md`.
- Risk review or bug fixing: read `references/risk-points.md`.

## Module Boundaries

Keep responsibilities separated:

- `BattleFoundation` owns deterministic battle runtime structure: engine phase, context, systems, entities, event bus, commands, rules, random, replay.
- `BattleCommon` owns concrete combat behavior: actors, attributes, health, movement, attacks, target query, AI, projectile runtime, combat asset cache, presentation bridge, and battle-specific GAS definitions/tasks/executions.
- `HotUpdate.Core/GAS` owns reusable gameplay ability/effect runtime: ability definitions/specs/tasks, effect definitions/specs/runtime, tags, cues, attribute sets, state capture/restore, and debugging.

Keep the dependency direction one-way: BattleCommon may depend on BattleFoundation and GAS; core GAS must not depend on either battle layer.

Core GAS must not reference `Foundation.Common`, `BattleFoundation`, `Framework`, combat actors, battle coordinate types, projectiles, or presentation services. Keep integration at the BattleCommon/bootstrap boundary:

- Adapt deterministic battle random to `IGameplayRandom` with `BattleGameplayRandomAdapter`; core GAS uses only `IGameplayRandom`.
- Adapt `AutoProfiler` to `GASProfiler` in bootstrap/integration code; core GAS uses only `GASProfiler`.
- Pass battle-specific hit, position, and presentation data in `GameplayEffectSpec.ContextData`; interpret it only in BattleCommon.
- In BattleCommon, route GAS cue lifecycle through `CombatAbilityComponent` and `IBattlePresentationSink`. Do not inject an `IGameplayCueManager` that directly drives actor or Unity presentation; reserve that core GAS extension point for non-battle integrations.

Place combat-specific abilities, tasks, cue handlers, effects, and runtime behavior under `BattleCommon/Abilities/`. Change core GAS only when the behavior is generic and beneficial outside combat.

For player state work, prefer representing state through `GameplayTag` definitions first. If the behavior can be implemented through GAS abilities, effects, tags, cues, or executions, prefer the GAS path before adding bespoke battle-state fields or systems.

When a feature needs a **new** GameplayTag path, follow the `gameplay-tags` skill: **only edit the `GameplayTagDatabase` asset, then Generate** (`BuildGameplayTags` / menu / Inspector). Do not invent tags with `new GameplayTag(...)` or by hand-editing `*Def.gen.cs`.

## Workflows

When creating or changing a battle runtime:

1. Identify the owning layer: foundation runtime, common combat system, or GAS.
2. Search existing implementations before adding new abstractions.
3. Preserve lifecycle order: `Initialize`, `Start`, `Update`, `LateUpdate`, `Dispose`.
4. Add systems through `BattleContext.AddSystem<T>` so initialization and registration stay consistent.
5. Route frame-driving behavior through `BattleEngine` or `IBattleSystem` instead of ad hoc Unity updates.

When creating a combat skill:

1. Prefer a `GameplayAbilityDefinition` subclass under `unity_project/Assets/Scripts/HotUpdate.Core/Battle/BattleCommon/Abilities/Ability/`.
2. Put reusable hit/projectile/target shape data under `BattleCommon/Abilities/Definition/`.
3. Put long-running or animation/timeline/projectile logic in `AbilityTask` subclasses under `BattleCommon/Abilities/Task/`.
4. Apply damage, buffs, cooldowns, and state changes through `GameplayEffectDefinition` and executions rather than mutating attributes directly from the ability.
5. Wire activation through `CombatAbilityComponent` only when the skill needs a new combat entry point.

When creating a gameplay effect:

1. Use `GameplayEffectDefinition` for data and duration/stack/tag/modifier setup.
2. Use `GameplayEffectExecution` when the effect needs formula logic, set-by-caller values, block/counter logic, or multiple attributes.
3. Keep combat formulas under `BattleCommon/Abilities/Effect/`; keep generic effect runtime behavior under `HotUpdate.Core/GAS/`.
4. Update catalogs or asset references when the new ability/effect must be grantable by id.

## Implementation Guardrails

- Treat combat config objects and profiles as shared data unless proven otherwise. Clone runtime copies before applying per-actor overrides such as boss skill ids, patrol points, flee flags, cooldowns, or decision tuning.
- Pair every event, Unity callback, and `AttributeSet` subscription with unsubscription during task end, effect removal, component recycle, or `Dispose`.
- A shared `IGameplayEffectRuntimeContext` may receive exactly one `BeginTick`/`EndTick` pair per frame. Call each GAS instance with `Tick(deltaTime, false)` inside that pair.
- Projectile and area queries must use `ICombatRelationResolver` or equivalent source-aware relation checks. Do not rely on distance-only queries for damage or collision because they can hit allies or neutral actors.
- Asset cache eviction must respect live instance references. Capacity pressure may skip entries with `RefCount > 0`; explicit battle teardown can release after instances have been recycled or destroyed.
- Async combat loading must handle battle teardown. After each awaited load, verify the cache/system is still alive and that the loaded entry is still the current entry before writing back runtime state. Resource-handle release must be idempotent: clear the owned handle before `Addressables.Release`, because disposal and an awaiting loader can both enter cleanup.
- Pooled request objects must clear callbacks and transient references before returning to the pool. Keep result data alive only when existing callbacks are expected to consume it after invocation.
- Do not use `UnityEngine.Random` in simulation. Inject `IGameplayRandom` or fail explicitly where randomness is required.
- Keep remove/recycle/dispose semantics explicit for actors and systems. `Remove` should mean unregister only; use separate methods or clearly named paths for pool deactivation and final disposal.
- Do not mutate `EntityManager.All` while iterating it. Actor systems should queue add/remove/recycle/dispose operations during actor updates, skip actors pending removal, and flush structural changes after the loop.
- Keep the combat AI at the business facade boundary: choose a `SkillId` and target, then call `actor.Gameplay.Skills.TryCast`. AI must not construct GAS Specs/TargetData, query cooldown tags, or manually end abilities. When a behavior is removed or becomes inactive, run its `Exit` path before dropping the reference so movement and other transient behavior state are released.

## Validation

After code changes, use the narrowest reliable validation:

- Search exact references for changed public types, ids, tags, and asset menu paths.
- Compile the narrowest affected Unity assembly when possible.
- When checking GAS dependencies, use `rg -uu` because the repository ignores `GAS/Debug/` through a broad debug ignore rule.
- Core GAS must have no `Foundation.Common`, `BattleFoundation`, or `Framework` reference. Also search for removed battle math, vector, and random type names.
- For gameplay changes, exercise activation, tick, removal/end, and pooled reuse or dispose paths. For effects, cover duration/stack combinations and cleanup of modifiers, tags, cues, and subscriptions.
- For async asset-cache changes, cover completion after cache disposal, duplicate cleanup attempts, and capacity eviction while an instantiated asset has `RefCount > 0`.
- Do not edit Unity `.meta`, prefab, scene, or asset files unless the request explicitly needs asset wiring.

## References

- `references/module-overview.md`: battle stack map, main types, and file roles.
- `references/ai-workflow.md`: combat AI ownership, NewPVE extraction audit, and migration checklist.
- `references/battle-creation.md`: workflow for creating a new battle mode/runtime/system.
- `references/gas/overview.md`: core GAS and combat GAS map.
- `references/gas/ability-workflow.md`: workflow for creating combat abilities and tasks.
- `references/gas/effect-workflow.md`: workflow for creating gameplay effects and executions.
- `references/risk-points.md`: common bugs and review checklist.
