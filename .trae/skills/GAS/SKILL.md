# GAS Skill

当问题涉及 `GameplayAbilitySystem`、Ability、Effect、GameplayTag、Attribute、Cue、战斗表现事件、状态保存/恢复、调试时，优先使用本 skill。

## 文档顺序

1. `README.md`
   - GAS 总览和核心调用链。
2. `GameplayAbilitySystem.md`
   - GAS 门面、初始化、Tick、状态保存/恢复、AttributeSet、DefinitionCatalog。
3. `Abilities.md`
   - Ability 定义、激活、Commit、Spec、Task、事件触发、状态恢复。
4. `Effects.md`
   - Effect 定义、Spec、Runtime、ActiveEffect、Modifier（Instant/Persistent）、Stack、Execution、Cue。
5. `GameplayTags.md`
   - GameplayTag、Container、TagQuery、生成代码规则。
6. `RuntimeEventsAndCues.md`
   - RuntimeContext、事件记录、Cue（Notify/Payload/Manager）、GASDebugger、确定性和回放相关。

## 核心模型

```text
GameplayAbilitySystem
  -> GameplayEffectRuntime
  -> GameplayAbilityRuntime
  -> GameplayDefinitionCatalog
  -> IGameplayEffectRuntimeContext
  -> AttributeSet / IGameplayAttributeOwner

Ability
  -> Grant -> CanActivate -> Commit Cost/Cooldown -> Activate
  -> Apply Effects (EffectsOnActivate / DelayedEffects)
  -> Run AbilityTasks -> EndAbility

Effect
  -> MakeOutgoingSpec -> CloneForTarget -> ApplySpecToSelf
  -> Instant: execute modifiers + executions -> done
  -> Duration/Infinite: find stackable or create ActiveGameplayEffect
  -> Persistent Modifiers / GrantedTags / Cues

GameplayTags
  -> GameplayTag (层级匹配)
  -> GameplayTagContainer (引用计数 / 层级计数 / listener)
  -> TagQuery (All / Any / NotAll)
  -> 用于 Ability/Effect 条件、阻塞、触发和匹配

Cue
  -> GameplayCueNotify (OnExecute / OnActive / WhileActive / OnRemove)
  -> GameplayCuePayload / GameplayCueSet / GameplayCueManager
  -> Static Policy (Execute only) vs Active Policy (OnActive/WhileActive/Removed)

Events
  -> GameplayEffectEvent (25 种事件类型)
  -> Effect / Attribute / Tag / Cue / Ability / Projectile / Melee 事件
  -> Subscribe / Unsubscribe for real-time responses
```

## 关键词

- `GameplayAbilitySystem`
- `GameplayAbilityRuntime`
- `GameplayEffectRuntime`
- `GameplayAbilityDefinition`
- `GameplayEffectDefinition`
- `GameplayEffectSpec`
- `ActiveGameplayEffect`
- `AbilityTask` / `AbilityTaskWaitDelay`
- `GameplayAbilitySpec`
- `GameplayTag` / `GameplayTagContainer`
- `TagQuery` / `TagQueryOp`
- `GameplayCueNotify` / `GameplayCuePayload` / `GameplayCueManager`
- `GameplayCuePolicy` / `GameplayCueEventType`
- `GameplayEffectEvent` / `GameplayEffectEventType`
- `DefaultGameplayEffectRuntimeContext`
- `AttributeSet` / `IGameplayAttributeOwner` / `IGameplayAttributeSetProvider`
- `AttributeModifierHandle` / `AttributeModifierOp`
- `GameplayDefinitionCatalog`
- `GameplayEffectApplyResult`
- `GameplayEffectExecution`
- `GASDebugger` / `GASDebuggerWindow`
- `GameplayAbilitySystemState` / `CaptureState` / `RestoreState`
- `DeterministicMode` / `InitDeterministicRandom`

## 相关源码

- `Assets/Scripts/HotUpdate.Core/GAS/`
- `Assets/Scripts/HotUpdate.Core/GameplayTags/`