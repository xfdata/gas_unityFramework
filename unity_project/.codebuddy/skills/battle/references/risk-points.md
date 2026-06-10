# Battle Risk Points

Use this as a review checklist for battle, combat, and GAS changes.

## Lifecycle

- `BattleEngine.Initialize` should only run from `Uninitialized`.
- `BattleEngine.StartBattle` should only run from `Ready`.
- `Context.Start`, `Context.Update`, and `Context.LateUpdate` drive systems in order.
- `Dispose` must clear rules, playback, recorder, context, commands, systems, entities, events, abilities, effects, tasks, and callbacks.
- Pooled entities/components must clear events and transient references in `DeactivateForPool`.

## Determinism

- Do not use `UnityEngine.Random` for simulation authority; use `BattleRandom`.
- Commands should execute before system updates.
- Avoid wall-clock time for deterministic gameplay.
- Keep replay/capture state updated for new long-lived runtime state.
- Avoid hidden dependence on Unity transform state unless the actor is explicitly presentation-driven.

## Entity And Component Ownership

- Do not keep stale `CombatActor`, component, target, projectile, or Unity object references after recycle/dispose.
- Verify `Attach`, `Initialize`, `DeactivateForPool`, and `OnDispose` each handle owner changes.
- Avoid directly constructing components when existing entity/component attach flow is required.

## System Registration

- Register systems through `BattleContext.AddSystem`.
- Avoid duplicate system types unless `BattleSystemManager` explicitly supports them.
- Do not call system `Start` before initialization.

## GAS Activation

- Ability activation should respect source/target required and blocked tags.
- `ActivationOwnedTags` should be removed when the ability ends.
- Ability tasks should end and unregister callbacks.
- Abilities should not directly mutate combat attributes when an effect/spec should carry source, target, level, ids, context, and replay data.
- Custom abilities should handle missing target, dead target, missing animation, missing timeline event, and missing damage effect.

## GAS Effects

- Duration modifiers and granted tags must be removed when the active effect ends.
- Stack policies must not accidentally duplicate permanent modifiers.
- Executions should handle null source/target attributes.
- Damage formulas should clamp final damage and HP.
- Set-by-caller values need safe defaults.
- Cues require a cue manager; otherwise feedback may silently not play.

## Lightweight Mode

`CombatAbilityComponent` has `FullGas` and `Lightweight` modes. When adding abilities or effects:

- confirm whether the behavior must work in lightweight mode;
- add a lightweight path only for abilities that are expected there;
- keep lightweight behavior consistent with full GAS for tags, effects, and death/attack outcomes where possible.

## Unity Assets

- Do not edit `.meta`, prefab, scene, or `.asset` files unless the task requires asset wiring.
- If an ability/effect is activated by id, make sure `GameplayDefinitionCatalog` contains the asset.
- Keep `CreateAssetMenu` paths consistent with existing ability/effect assets.
