# Battle Creation Workflow

Use this when adding a new battle mode, battle runtime, combat loop, or major battle system.

## 1. Choose The Layer

- Use `BattleFoundation` for generic engine concepts: phase, tick mode, rule, command, event, replay, system lifecycle, entity lifecycle.
- Use `BattleCommon` for concrete combat behavior: actor composition, AI, target selection, attack, projectile, movement, animation, damage, battle assets.
- Use `HotUpdate.Core/GAS` only for generic ability/effect runtime contracts that are not combat-specific.

## 2. Create Or Extend The Engine

For a new battle mode, derive from `BattleEngine` when custom initialization, start, update, end, or rule wiring is needed.

Typical sequence:

1. Override `CreateRuntimeSettings` only when defaults/config need mode-specific values.
2. Override `CreateContext` only if the context type needs new services.
3. Add systems in `OnInitialize` through `Context.AddSystem(...)`.
4. Add rules with `AddRule(...)`.
5. Put per-frame mode logic in `OnUpdate` or an `IBattleSystem`; prefer systems for reusable behavior.
6. Use `EndBattle(EBattleResult)` only from rules or explicit battle-end commands.

## 3. Add Systems

An `IBattleSystem` should:

- Store `IBattleContext` during `Initialize`.
- Allocate runtime data in `Initialize` or `Start`, not constructors.
- Use `Update` for simulation and `LateUpdate` for follow-up processing.
- Clean event subscriptions and pooled references in `Dispose`.
- Avoid direct scene/prefab changes unless the system owns Unity-side presentation.

Register systems with `BattleContext.AddSystem<T>` to preserve `EnsureCanRegister`, initialization, and manager ownership.

## 4. Add Entities And Components

Use `BattleEntity` and `EntityComponent` for simulation ownership. For combat actors, prefer `CombatActor` and existing components:

- `CombatAttributeComponent` for attributes.
- `CombatHealthComponent` for HP/death.
- `CombatAbilityComponent` for GAS.
- `CombatAttackComponent` for interval/range attack activation.
- `CombatMovementComponent` for movement.
- `CombatAIComponent` for AI behavior.

For pooled actors, verify `DeactivateForPool` clears transient state, events, targets, active effects, tasks, and cached Unity references.

## 5. Use Commands And Events

Use `BattleCommand` for player/server/AI commands that should execute at the deterministic command point before system updates.

Use `BattleEventBus` for cross-system notifications. Prefer explicit payload structs when events need multiple fields.

## 6. Replay And Determinism

If replay/frame sync matters:

- Use `BattleRandom` from context, not `UnityEngine.Random`.
- Keep command execution before system update.
- Avoid wall-clock time and Unity object state as simulation authority.
- Use GAS state capture/restore where ability/effect state must replay.
- Make new state serializable or capturable by the relevant replay adapter.

## 7. Validation Checklist

- Phase transitions remain valid for `Uninitialized -> Initializing -> Ready -> Running -> Ended/Paused/Replaying`.
- `Dispose` clears systems, entities, events, rules, recorder/playback, and commands.
- Systems are initialized before `Start`.
- No duplicated system registration.
- Entity/component ownership is released on recycle/dispose.
- Rules cannot end battle before `Running` unless explicitly intended.
