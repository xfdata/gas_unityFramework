# GAS Effect Workflow

Use this when creating or modifying a gameplay effect, buff, debuff, damage effect, heal, shield, damage block, cooldown, stack, cue, modifier, or execution.

## 1. Select Effect Shape

Use `GameplayEffectDefinition` alone when the effect can be expressed as:

- instant, duration, or infinite duration;
- direct attribute modifiers;
- granted tags;
- stack policy;
- cues;
- simple source/target tag requirements.

Add a `GameplayEffectExecution` when the effect needs:

- a damage/heal formula;
- multiple attributes;
- random/contextual values;
- set-by-caller values;
- block/counter logic;
- conditional final application;
- custom captured values or user data.

## 2. File Placement

- Combat formula: `Assets/Scripts/HotUpdate.Game/BattleCommon/GAS/Effect/<Name>Execution.cs`.
- Generic formula: `Assets/Scripts/HotUpdate.Core/GAS/EffectExecution/<Name>Execution.cs`.
- Runtime changes to effect application/stacking/tags/cues: `Assets/Scripts/HotUpdate.Core/GAS/GameplayEffectRuntime.cs`.

Use `BattleCommon` namespace for combat executions that depend on combat actors/components/attributes. Use `GAS` namespace for generic executions.

## 3. Implement Execution

For a combat execution:

1. Inherit `GameplayEffectExecution`.
2. Override `Execute(GameplayEffectSpec spec)`.
3. Resolve source and target attributes through `IGameplayAttributeSetProvider`.
4. Read dynamic values through `spec.GetSetByCaller(key, defaultValue)`.
5. Write final dynamic values through `spec.SetByCaller(key, value)` when later logic/debugging needs them.
6. Mutate target attributes with `SetBaseValue`, `AddAttributeBaseValue`, or modifiers through `AttributeSet`.
7. Return early when source/target/effect state is invalid.

Use `CombatDamageExecution` as the main combat formula example.

## 4. Damage And Blocking

`CombatDamageExecution` uses `CombatDamageKeys`:

- `AttackFactor`
- `Attack`
- `DamageUp1`
- `DamageUp2`
- `BlockedDamage`
- `FinalDamage`

It computes damage from source attack, damage increases, target reductions, defense, and absolute reduction. It then calls `CombatAbilityComponent.TryBlockIncomingDamage` so `DamageBlockAbilityDefinition` can reduce incoming damage.

When adding new damage behavior, decide whether it belongs in:

- set-by-caller values on the effect spec;
- `CombatDamageExecution`;
- a new execution;
- `DamageBlockAbilityDefinition`;
- combat attributes in `CombatAttributeIds`.

## 5. Modifiers, Duration, And Stack

For modifier effects:

- use `AttributeModifierOp.Add`, `Multiply`, or the existing op enum values;
- set `DurationPolicy` carefully;
- set `StackPolicy`, `MaxStack`, `RefreshDurationOnStack`, and `ReapplyModifiersOnStack`;
- use granted tags for state that other abilities/effects should query;
- verify modifiers are removed when the active effect expires or is removed.

## 6. Cues

Use cues for gameplay feedback driven by effect apply/execute/remove. Configure `GameplayEffectDefinition.Cues` with:

- `CueTag`;
- `GameplayCuePolicy`;
- `OnApply`;
- `OnExecute`;
- `OnRemove`.

Make sure a cue manager is supplied to the owning `GameplayAbilitySystem` or `GameplayEffectRuntime` through combat services.

## 7. Asset Wiring

For a new effect asset:

- set `EffectId`;
- set `EffectTag`;
- configure duration, period, execute-on-apply, stack policy;
- configure source/target tag requirements;
- configure granted tags, modifiers, executions, and cues;
- add the asset to `GameplayDefinitionCatalog` if used by id or by ability catalog lookup.

## 8. Review Checklist

- Source/target tag gates match the intended status rules.
- Instant effects do not leave unwanted active state.
- Duration effects remove modifiers and granted tags on expiration/removal.
- Stack behavior is intentional.
- Executions handle null source or target safely.
- Set-by-caller defaults are sane when an ability forgets to provide a value.
- Damage/heal math clamps HP and avoids negative or NaN values.
