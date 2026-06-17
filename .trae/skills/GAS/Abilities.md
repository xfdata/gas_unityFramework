# Abilities

相关源码：

```text
Assets/Scripts/HotUpdate.Core/GAS/Ability/GameplayAbility.cs
Assets/Scripts/HotUpdate.Core/GAS/Ability/GameplayAbilityRuntime.cs
Assets/Scripts/HotUpdate.Core/GAS/Ability/AbilityTask.cs
Assets/Scripts/HotUpdate.Core/GAS/GameplayAbilityTypes.cs
```

## 1. GameplayAbilityDefinition

`GameplayAbilityDefinition` 是 ScriptableObject 技能定义。

主要配置：

- `AbilityId`
- `AbilityTag`
- `SourceRequiredTags` / `SourceBlockedTags`（TagQuery，默认 All / NotAll）
- `TargetRequiredTags` / `TargetBlockedTags`（TagQuery，默认 All / NotAll）
- `ActivationRequiredTags` / `ActivationBlockedTags`（TagQuery，默认 All / NotAll）
- `ActivationOwnedTags`（GameplayTagContainer）
- `CancelAbilitiesWithTag`（TagQuery，默认 Any）
- `BlockAbilitiesWithTag`（TagQuery，默认 Any）
- `IsIgnoreBlock`
- `AbilityTriggers`（List\<GameplayTag\>）
- `CostEffects`（List\<GameplayEffectDefinition\>）
- `CooldownEffects`（List\<GameplayEffectDefinition\>）
- `EffectsOnActivate`（List\<EffectApplication\>）
- `DelayedEffects`（List\<DelayedEffectApplication\>）

### EffectApplication

```csharp
public class EffectApplication
{
    public GameplayEffectDefinition Effect;
    public GameplayAbilityTargetPolicy TargetPolicy = Target;
    public bool EndAbilityAfterApply;
}
```

### DelayedEffectApplication

```csharp
public class DelayedEffectApplication
{
    [Min(0f)] public float Delay;
    public GameplayEffectDefinition Effect;
    public GameplayAbilityTargetPolicy TargetPolicy = Target;
    public bool EndAbilityAfterApply = true;
}
```

### TargetPolicy

```csharp
public enum GameplayAbilityTargetPolicy : byte
{
    Self,    // Source
    Target,  // 如果有 target 用 target，否则 fallback Source
    Source,  // 等价于 Self
}
```

## 2. 激活条件

`CanActivateAbility(spec)` 检查（全部通过才能激活）：

- SourceRequiredTags.Match(spec.Source.OwnedTags)
- SourceBlockedTags.Match(spec.Source.OwnedTags)（NotAll 语义：命中任何节点返回 false）
- TargetRequiredTags.Match(spec.Target.OwnedTags)
- TargetBlockedTags.Match(spec.Target.OwnedTags)
- ActivationRequiredTags.Match(spec.Source.OwnedTags)
- ActivationBlockedTags.Match(spec.Source.OwnedTags)

## 3. GameplayAbilityRuntime 激活流程

```text
ActivateAbility(ability, target, level, triggerEventData)
  -> ability 不为空
  -> HasAbility 检查（已 Grant）
  -> IsAbilityBlocked 检查（IsIgnoreBlock 可绕过）
  -> 创建 GameplayAbilitySpec（生成 AbilitySpecId，设置 triggerEventData）
  -> ability.CanActivateAbility(spec)
  -> CanCommitAbility（检查 Cost/Cooldown 是否都能 apply）
  -> CommitAbility（apply Cost + Cooldown effects 到 Self）
  -> CancelAbilitiesMatchingQuery（CancelAbilitiesWithTag）
  -> MarkActive
  -> activeAbilities.Add(spec)
  -> AddActivationOwnedTags（加到 Source.OwnedTags）
  -> AddBlockedTags（引用计数加到 blockedAbilityTagCounts）
  -> Record AbilityActivated
  -> ability.ActivateAbility(spec)
```

失败时记录 AbilityFailed 事件。

## 4. Commit

Commit 包含 CostEffects 和 CooldownEffects，均通过 `ApplyGameplayEffect` 应用到 Self。

流程：

```text
CanCommitAbility
  -> CanApplyCommitEffects(CostEffects)   // 每个 effect.CanApplySpecToSelf
  -> CanApplyCommitEffects(CooldownEffects)

CommitAbility
  -> ApplyCommitEffects(CostEffects)      // 每个 effect.ApplySpecToTarget -> self
  -> ApplyCommitEffects(CooldownEffects)
  -> Record AbilityCommitted
```

Cost/Cooldown 都是标准 GameplayEffect，其中一个失败则整个 Commit 失败。

## 5. ActivateAbility 默认行为

`GameplayAbilityDefinition.ActivateAbility(spec)` 默认：

```text
ApplyConfiguredEffects
  -> 遍历 EffectsOnActivate
  -> spec.ApplyGameplayEffect(effect, targetPolicy)
  -> 如果 EndAbilityAfterApply，立即 EndAbility(Completed) 并 return

StartDelayedEffects
  -> 遍历 DelayedEffects
  -> spec.AddTask(new AbilityTaskDelayedEffect(index, application))

如果没有 ActiveTasks，EndAbility(Completed)
```

## 6. GameplayAbilitySpec

一次 Ability 激活实例。

持有：

- `AbilitySpecId`
- `Ability`（GameplayAbilityDefinition）
- `AbilityRuntime`（GameplayAbilityRuntime）
- `Source` / `Target`（GameplayEffectRuntime，客户端缓存）
- `SourceEntityId` / `TargetEntityId`（权威身份，用于同步/回放）
- `Level`
- `IsActive` / `IsEnded` / `EndReason`
- `HasActiveTasks` / `ActiveTasks`
- `TriggerEventData`
- `RuntimeContext`

常用方法：

- `ApplyGameplayEffect(effect, targetPolicy)` — 通过 Source.MakeOutgoingSpec + ApplySpecToTarget 应用效果。
- `CanApplyGameplayEffect(effect, targetPolicy)` — 通过 CanApplySpecToSelf 检查是否可应用。
- `AddTask<T>(task)` — 添加并激活 AbilityTask，返回 task。
- `EndAbility(reason)` — 结束 Ability，触发所有 task 的 EndTask，清理 tasks。
- `SetTarget(target)` — 更新 target 引用和 TargetEntityId。
- `SetAuthorityEntityIds(sourceEntityId, targetEntityId)` — 设置权威身份。

内部 `RestoreTask<T>(task, taskId)` 用于状态恢复。

### TargetPolicy 解析

```csharp
ResolveTarget(targetPolicy)
  Self   -> Source
  Source -> Source
  Target -> Target != null ? Target : Source (fallback)
```

## 7. AbilityTask

`AbilityTask` 是 Ability 内部异步/延迟流程。

生命周期：

```text
Initialize(spec, taskId)
  -> Activate()
  -> OnActivate()
  -> Tick(deltaTime)    // 每帧
  -> OnTick(deltaTime)
  -> EndTask()
  -> OnEnd()
```

基类：

```csharp
public abstract class AbilityTask
{
    public int TaskId { get; }
    public GameplayAbilitySpec AbilitySpec { get; }
    public bool IsActive { get; }
    public bool IsFinished { get; }

    void Activate();
    void Tick(float deltaTime);
    void EndTask();

    protected virtual void OnActivate() { }
    protected virtual void OnTick(float deltaTime) { }
    protected virtual void OnEnd() { }

    internal virtual GameplayAbilityTaskState CaptureState();
}
```

### AbilityTaskWaitDelay

等待指定时长后完成的 task（public 类）：

```csharp
public class AbilityTaskWaitDelay : AbilityTask
{
    public AbilityTaskWaitDelay(float duration, Action<AbilityTaskWaitDelay> onCompleted = null);

    protected float Duration { get; }
    protected float Elapsed { get; }
    protected float TimeLeft { get; }

    protected virtual void OnCompleted() { }
}
```

duration <= 0 时在 Activate 阶段直接完成。

支持 CaptureState（Kind = WaitDelay, TimeLeft）。

### AbilityTaskDelayedEffect（internal）

继承 `AbilityTaskWaitDelay`，到时间后应用 `DelayedEffectApplication.Effect`。

- 携带 `DefinitionIndex` 用于识別对应的 `DelayedEffectApplication`。
- 完成后检查 `EndAbilityAfterApply`。
- CaptureState 记录 Kind = DelayedEffect, DefinitionIndex, TimeLeft。

## 8. 事件触发 Ability

`GameplayAbilityDefinition.AbilityTriggers` 配置事件标签。

```csharp
GameplayAbilitySystem.SendGameplayEvent(eventTag, eventData)
```

进入 `GameplayAbilityRuntime.HandleGameplayEvent`：

```csharp
public void HandleGameplayEvent(GameplayTag eventTag, GameplayEventData eventData)
{
    foreach (var ability in grantedAbilities)
        for (int i = 0; i < ability.AbilityTriggers.Count; i++)
            if (eventTag.Matches(ability.AbilityTriggers[i]))
            {
                ActivateAbility(ability, null, 1, eventData);
                break;  // 每个 ability 只触发一次
            }
}
```

## 9. Tag 副作用

激活期间：

- `ActivationOwnedTags` 加到 Source.OwnedTags。
- `BlockAbilitiesWithTag.Nodes` 中每个 tag 作为 key 进入 `blockedAbilityTagCounts` 引用计数。

Ability 结束时会移除这些 tag/block。

### 阻塞判断

```csharp
IsAbilityBlocked(ability)
  // 遍历 blockedAbilityTagCounts，检查 ability.AbilityTag.Matches(blockedTag)
```

## 10. 状态捕获和恢复

`GameplayAbilityRuntime` 提供：

- `CaptureGrantedAbilityIds()` — 返回所有 granted ability 的 id 数组。
- `CaptureActiveAbilityStates()` — 返回 active spec 的状态数组（含 task 状态）。
- `RestoreGrantedAbilities(abilityIds, catalog)` — 通过 catalog 恢复 granted abilities。
- `RestoreActiveAbilities(states, catalog)` — 恢复 active abilities，重建 tag/block 状态、恢复 tasks。

恢复时 suppressRuntimeEvents，避免产生多余的运行时事件。

找不到定义时记录 RestoreAbilitySkipped 事件。

## 11. GameplayAbilityEndReason

```csharp
public enum GameplayAbilityEndReason : byte
{
    Completed,   // 正常完成（tasks 全部结束或 EffectsOnActivate 的 EndAbilityAfterApply）
    Cancelled,   // 被取消（CancelAbility / CancelAbilitiesWithTag / Dispose）
    Failed,      // 激活失败（未 Grant / 被阻塞 / CanActivate 失败 / Commit 失败）
}
```

## 12. 注意事项

- Ability 必须先 Grant 才能 Activate。
- `IsIgnoreBlock` 可绕过 blocked ability tag 检查。
- `CancelAbilitiesWithTag` 通过 `TagQuery.Match(abilityTag)` 匹配（Any 语义）。
- `BlockAbilitiesWithTag` 使用引用计数，多个 Ability 可阻塞同一个 tag，全部移除后才解阻塞。
- Ability 如果没有 task 且 EffectsOnActivate 没有 EndAbilityAfterApply，会在 Activate 后自动 Completed。
- Restore ActiveAbility 依赖 catalog 能通过 AbilityId 找到定义。
- `AbilityTaskWaitDelay` 是 public 类，可被业务代码继承扩展。
- `triggerEventData` 在事件触发激活时传入，可通过 spec.TriggerEventData 访问。