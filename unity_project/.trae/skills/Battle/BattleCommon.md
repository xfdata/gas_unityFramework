# BattleCommon

相关源码：

```text
Assets/Scripts/HotUpdate.Game/BattleCommon/Core/CombatPrimitives.cs
Assets/Scripts/HotUpdate.Game/BattleCommon/Entity/CombatActor.cs
Assets/Scripts/HotUpdate.Game/BattleCommon/Entity/CombatLoadoutDefinition.cs
Assets/Scripts/HotUpdate.Game/BattleCommon/Combat/CombatComponents.cs
Assets/Scripts/HotUpdate.Game/BattleCommon/Combat/CombatContracts.cs
Assets/Scripts/HotUpdate.Game/BattleCommon/Combat/CombatSystems.cs
Assets/Scripts/HotUpdate.Game/BattleCommon/Combat/AnimationTimeScaleSystem.cs
Assets/Scripts/HotUpdate.Game/BattleCommon/AI/CombatAI.cs
Assets/Scripts/HotUpdate.Game/BattleCommon/Assets/CombatAssetCache.cs
Assets/Scripts/HotUpdate.Game/BattleCommon/Physics/CombatPhysicsSystem.cs
Assets/Scripts/HotUpdate.Game/BattleCommon/Navigation/CombatPathfindingSystem.cs
Assets/Scripts/HotUpdate.Game/BattleCommon/Projectile/CombatProjectileSystem.cs
Assets/Scripts/HotUpdate.Game/BattleCommon/GAS/Ability/BornAbility.cs
Assets/Scripts/HotUpdate.Game/BattleCommon/GAS/Ability/MeleeAttackAbility.cs
Assets/Scripts/HotUpdate.Game/BattleCommon/GAS/Ability/RemoteAttackAbility.cs
Assets/Scripts/HotUpdate.Game/BattleCommon/GAS/Definition/MeleeHitDefinition.cs
Assets/Scripts/HotUpdate.Game/BattleCommon/GAS/Definition/RangedProjectileDefinition.cs
Assets/Scripts/HotUpdate.Game/BattleCommon/GAS/Effect/CombatDamageExecution.cs
Assets/Scripts/HotUpdate.Game/BattleCommon/GAS/Runtime/ProjectileRuntime.cs
Assets/Scripts/HotUpdate.Game/BattleCommon/GAS/Task/AbilityTaskApplyMeleeHit.cs
Assets/Scripts/HotUpdate.Game/BattleCommon/GAS/Task/AbilityTaskPlayMontage.cs
Assets/Scripts/HotUpdate.Game/BattleCommon/GAS/Task/AbilityTaskPlayTimeline.cs
Assets/Scripts/HotUpdate.Game/BattleCommon/GAS/Task/AbilityTaskSpawnProjectile.cs
```

BattleCommon 在 BattleFoundation 之上实现了战斗通用逻辑：CombatActor 及其 Component、AI、目标查询、物理、寻路、弹道，以及基于 GAS 的技能/效果扩展。

## 1. CombatPrimitives

### CombatAttributeIds

```csharp
public static class CombatAttributeIds
{
    public const int HP = 1, MaxHP = 2, Attack = 3, Defense = 4;
    public const int MoveSpeed = 5, AttackRange = 6, AttackInterval = 7;
    public const int CritRate = 8, CritDamage = 9, DamageReduce = 10;
    public const int DamageReduce1 = 11, DamageReduce2 = 12, AbsoluteReduce = 13;
    public const int DamageUp1 = 14, DamageUp2 = 15;
}
```

### CombatAbilityIds

```csharp
public static class CombatAbilityIds
{
    public const int Born = 1001;
    public const int Attack = 1002;
    public const int Death = 1003;
    public const int Skill = 2001;
}
```

### CombatTargetPriority

```csharp
public enum CombatTargetPriority
{
    Nearest,      // 最近
    LowestHP,     // 血量最低
    HighestHP,    // 血量最高
    Random,       // 随机
}
```

## 2. CombatActor

`CombatActor` 继承 `BattleEntity`，同时实现多个 GAS 接口。

```csharp
public class CombatActor : BattleEntity,
    IGameplayAttributeOwner,
    IGameplayAttributeSetProvider,
    ICombatTarget,
    IMeleeSource,
    IAnimancerProvider,
    ITimelineProvider
{
    public GameObject GameObject { get; set; }
    public Transform Transform { get; set; }
    public Animator Animator { get; set; }
    public ICombatAbilityServices AbilityServices { get; set; }
    public Vector3 SpawnPosition { get; set; }
    public AnimancerComponent Animancer { get; }          // 惰性查找
    public PlayableDirector Director { get; }              // 惰性查找
    public virtual float HitRadius { get; } = 0.5f;

    public override Vector3 Position { get; set; }        // Transform 优先
    public override Quaternion Rotation { get; set; }
    public override bool IsAlive { get; set; }            // Health.IsAlive 优先

    // IGameplayAttributeOwner / IGameplayAttributeSetProvider
    public virtual AttributeSet AttributeSet { get; }
    public virtual GameplayEffectRuntime Effects { get; }
    public virtual float GetAttribute(int attributeId);
    public virtual AttributeModifierHandle AddModifier(...);
    public virtual void RemoveModifier(AttributeModifierHandle handle);

    // IMeleeSource
    public Vector3 MeleeOrigin => Position;
    public Vector3 MeleeForward => Transform?.forward ?? Rotation * Vector3.forward;

    // 回收判断
    public virtual bool CanRecycle => Health.IsDead;

    public void ApplyLoadout(CombatLoadoutDefinition loadout);
    public IReadOnlyList<IRangedTarget> GetMeleeTargets(MeleeHitDefinition hitDefinition);
    public void MoveTo(Vector3 destination);
    public void StopMove();
}
```

### GAS 接口桥接

- `AttributeSet` -> `CombatAttributeComponent.AttributeSet`
- `Effects` -> `CombatAbilityComponent.GAS.Effects`
- `GetAttribute()` -> `CombatAttributeComponent.GetAttribute()`
- `GetMeleeTargets()` -> `CombatTargetQuerySystem.FindMeleeTargets()`

### ApplyLoadout

```csharp
public void ApplyLoadout(CombatLoadoutDefinition loadout)
{
    HitRadius = loadout.HitRadius;
    CombatAttributeComponent.ApplyLoadout(loadout);
    CombatAbilityComponent.SetInitialAbilities(loadout.Abilities);
    CombatAIComponent.SetProfile(loadout.AIProfile);
}
```

## 3. CombatLoadoutDefinition

```csharp
[CreateAssetMenu(menuName = "BattleCommon/Combat Loadout")]
public class CombatLoadoutDefinition : ScriptableObject
{
    // Attributes
    public float MaxHP = 100f, Attack = 10f, Defense, MoveSpeed = 3f;
    public float AttackRange = 2f, AttackInterval = 1.5f;
    public float CritRate, CritDamage = 1.5f, DamageReduce;

    // Collision
    public float HitRadius = 0.5f;

    // Abilities & AI
    public List<GameplayAbilityDefinition> Abilities;
    public CombatAIProfile AIProfile;
}
```

## 4. Combat Components

所有 Component 继承 `CombatComponentBase`，后者继承 `EntityComponent`：

```csharp
public abstract class CombatComponentBase : EntityComponent
{
    public new CombatActor Owner => base.Owner as CombatActor;
}
```

### CombatAttributeComponent

封装 GAS 的 `AttributeSet`，实现 `IGameplayAttributeOwner` 和 `IGameplayAttributeSetProvider`。

```csharp
public class CombatAttributeComponent : CombatComponentBase,
    IGameplayAttributeOwner, IGameplayAttributeSetProvider
{
    public AttributeSet AttributeSet { get; }
    public event Action<int, float, float> OnAttributeChanged;

    // 快捷属性访问
    public float HP, MaxHP, Attack, Defense, MoveSpeed, AttackRange;
    public float AttackInterval, CritRate, CritDamage, CritDamageMul, DamageReduce;

    public void ApplyLoadout(CombatLoadoutDefinition loadout);
    public float Get(int attributeId);
    public void Set(int attributeId, float value);
    public void SetBaseValue(int attributeId, float value);
    public void AddBaseValue(int attributeId, float delta);
    public AttributeModifierHandle AddModifier(int attrId, AttributeModifierOp op, float value, object source);
    public void RemoveModifier(AttributeModifierHandle handle);
    public void ClearAllModifiers();
}
```

### CombatHealthComponent

```csharp
public class CombatHealthComponent : CombatComponentBase
{
    public float HP { get; set; }          // clamp 到 [0, MaxHP]
    public float MaxHP { get; }
    public bool IsDead => HP <= 0f;
    public bool IsAlive => !IsDead;
    public float HPPercent => MaxHP > 0f ? HP / MaxHP : 0f;

    public event Action<CombatActor> OnDeath;
    public event Action<float, CombatActor> OnDamaged;
    public event Action<float> OnHealed;

    public void TakeDamage(float rawDamage, CombatActor source);
    public void Heal(float amount);
    public void SetFullHP();
}
```

`TakeDamage` 流程：

```csharp
public void TakeDamage(float rawDamage, CombatActor source)
{
    if (IsDead) return;
    float finalDamage = Mathf.Max(1f, rawDamage - Defense); // 最低 1 点
    HP -= finalDamage;
    OnDamaged?.Invoke(finalDamage, source);
    if (HP <= 0f) Die(source);
}
```

`Die` 流程：

```csharp
private void Die(CombatActor killer)
{
    if (_hasDied) return;       // 防止重复死亡
    _hasDied = true;
    CombatAbilityComponent.TryActivateDeathAbility(killer);
    OnDeath?.Invoke(killer);
}
```

同时监听 `OnAttributeChanged(HP)`，当 HP 从 >0 变为 <=0 时也会触发 `Die()`。

### CombatAbilityComponent

```csharp
public class CombatAbilityComponent : CombatComponentBase
{
    public GameplayAbilitySystem GAS { get; }
    public bool IsDead => HasTag(CombatGameplayTags.State_Dead);

    public void SetInitialAbilities(IEnumerable<GameplayAbilityDefinition> abilities);
    public void GrantAbility(GameplayAbilityDefinition ability);
    public void AddTag(GameplayTag tag);
    public void RemoveTag(GameplayTag tag);
    public bool HasTag(GameplayTag tag);

    public bool TryActivateBornAbility();     // 查找 Ability_Born tag
    public bool TryActivateAttackAbility(CombatActor target); // 近战或远程
    public bool TryActivateDeathAbility(CombatActor killer);  // 查找 Ability_Death tag
    public bool TryActivateById(int abilityId);

    public override void Update(float deltaTime) => GAS?.Tick(deltaTime);
}
```

`Initialize()` 流程：

```csharp
public override void Initialize()
{
    _gas?.Dispose();
    _gas = new GameplayAbilitySystem(
        Owner.Id,
        Owner,              // IGameplayAttributeOwner
        null,               // context = null (暂不使用事件记录)
        services.AbilityCatalog,
        services.GameplayCueManager);
    for (int i = 0; i < _initialAbilities.Count; i++)
        GrantAbility(_initialAbilities[i]);
}
```

Attack ability 查找优先判断 `Owner is IRangedAttackSourceProvider && HasRangedWeapon`，是则查找 `RemoteAttackAbilityDefinition` 类型，否则找 `Ability_Attack` tag。

### CombatAttackComponent

```csharp
public class CombatAttackComponent : CombatComponentBase
{
    public CombatActor CurrentTarget { get; }

    public bool TryAttack(CombatActor target);
    public CombatActor FindTarget(Func<CombatActor, bool> filter,
        CombatTargetPriority priority = CombatTargetPriority.Nearest);

    public override void Update(float deltaTime);
}
```

攻击间隔由 `AttackInterval` 属性和 `_attackTimer` 控制。`TryAttack` 先检查距离条件（`AttackRange`），再通过 `CombatAbilityComponent.TryActivateAttackAbility(target)` 触发。

### CombatMovementComponent

```csharp
public class CombatMovementComponent : CombatComponentBase
{
    public bool IsMoving { get; }
    public float RemainingDistance { get; }

    public void SetMotor(IMovementMotor motor);
    public void SetNavAgent(NavMeshAgent navAgent);   // 创建 NavMeshMovementMotor
    public void MoveTo(Vector3 destination);
    public void StopMove();
    public void FollowPath(IReadOnlyList<Vector3> path);
    public void Teleport(Vector3 position);
}
```

`NavMeshMovementMotor`：

```csharp
public sealed class NavMeshMovementMotor : IMovementMotor
{
    public bool IsMoving { get; }
    public bool HasArrived { get; }
    public float RemainingDistance { get; }

    public void MoveTo(Vector3 destination, float speed);
    public void Stop();
    public void Teleport(Vector3 position);
}
```

## 5. CombatContracts

```csharp
// 战斗目标接口（空接口，标记 IRangedTarget）
public interface ICombatTarget : IRangedTarget { }

// 近战攻击源
public interface IMeleeSource : IMeleeAttackSourceProvider { }

// 远程攻击源
public interface IRangedSource : IRangedAttackSourceProvider { }

// 移动马达接口
public interface IMovementMotor
{
    bool IsMoving { get; }
    bool HasArrived { get; }
    float RemainingDistance { get; }
    void MoveTo(Vector3 destination, float speed);
    void Stop();
    void Teleport(Vector3 position);
}

// 阵营关系判定接口
public interface ICombatRelationResolver
{
    bool AreEnemies(CombatActor source, CombatActor target);
}

// 目标查询接口
public interface ICombatTargetQuery
{
    CombatActor FindTarget(CombatActor source, Func<CombatActor, bool> filter,
        CombatTargetPriority priority, float range);
    int FindInRange(CombatActor source, float range, List<CombatActor> results);
}

// 技能服务接口
public interface ICombatAbilityServices
{
    GameplayDefinitionCatalog AbilityCatalog { get; }
    IGameplayCueManager GameplayCueManager { get; }
    ProjectileRuntime ProjectileRuntime { get; }
}
```

## 6. CombatSystems

### DefaultCombatRelationResolver

```csharp
public class DefaultCombatRelationResolver : ICombatRelationResolver
{
    public bool AreEnemies(CombatActor source, CombatActor target)
    {
        return source.Camp != target.Camp && target.Camp != EEntityCamp.Neutral;
    }
}
```

### CombatTargetQuerySystem

```csharp
public class CombatTargetQuerySystem : IBattleSystem, ICombatTargetQuery
{
    public CombatActor FindTarget(CombatActor source, Func<CombatActor, bool> filter,
        CombatTargetPriority priority, float range);

    public int FindInRange(CombatActor source, float range, List<CombatActor> results);

    public void FindMeleeTargets(IMeleeSource source, MeleeHitDefinition hitDefinition,
        List<IRangedTarget> results);
}
```

`FindTarget` 逻辑：

1. `FindInRange(source, range, cache)` 筛选范围内的敌人。
2. 应用 filter 过滤。
3. 按 `CombatTargetPriority` 选择最佳目标。

`FindMeleeTargets` 使用扇形判定：

1. 遍历所有敌人。
2. 计算目标在 MeleeForward 上的投影距离（`forwardDistance`）。
3. 范围检查：`forwardDistance` 在 `[-radius, range + radius]` 则进入。
4. 最近点距离检查：`closest = MeleeOrigin + MeleeForward * clamp(forwardDistance, 0, range)`。
5. `|target - closest| <= radius + target.HitRadius` 则命中。

### CombatActorSystem

```csharp
public class CombatActorSystem : IBattleSystem
{
    public void AddActor(CombatActor actor);     // EntityManager.Add + actor.Initialize
    public void RemoveActor(CombatActor actor);  // EntityManager.Remove

    public void Update(float deltaTime);
    // 遍历所有 Actor.Update，检查 CanRecycle -> OnRecycleRequested
}
```

## 7. CombatAI

### CombatAIProfile

```csharp
[CreateAssetMenu(menuName = "BattleCommon/AI Profile")]
public class CombatAIProfile : ScriptableObject
{
    public float DecisionInterval = 0.2f;      // 决策间隔
    public float RetargetCooldown = 2f;        // 重新锁敌冷却
    public float FleeHealthThreshold = 0.2f;   // 逃跑血线
    public float FleeDistance = 10f;           // 逃跑距离
    public float SkillMinInterval = 2f;        // 技能最小间隔
    public CombatTargetPriority TargetPriority = CombatTargetPriority.Nearest;
    public bool CanFlee;
    public List<int> SkillAbilityIds;
}
```

### CombatBehaviorBase

```csharp
public abstract class CombatBehaviorBase
{
    public CombatAIBehaviorType Type { get; }    // Idle, Chase, Attack, Flee, Patrol, Skill
    public CombatAIBehaviorState State { get; }  // Inactive, Running, Cooldown

    public virtual void Setup(CombatActor owner, CombatAIProfile profile);
    public virtual bool CanEnter(CombatActor target);
    public virtual void Enter(CombatActor target);
    public virtual void Update(float deltaTime, CombatActor target);
    public virtual void Exit();
}
```

预置 Behavior：

| Behavior | Type | CanEnter 条件 | Enter 动作 |
|---|---|---|---|
| `CombatFleeBehavior` | Flee | CanFlee && HPPercent <= FleeHealthThreshold | 朝远离目标方向跑 |
| `CombatSkillBehavior` | Skill | abilityId > 0 && cooldown >= SkillMinInterval | ActivatedAbility(abilityId) |
| `CombatAttackBehavior` | Attack | target != null && target.IsAlive | TryAttack(target) |
| `CombatChaseBehavior` | Chase | target != null && target.IsAlive | MoveTo(target.Position) |
| `CombatIdleBehavior` | Idle | Owner.IsAlive | StopMove() |

### CombatAIComponent

```csharp
public class CombatAIComponent : CombatComponentBase
{
    public void SetProfile(CombatAIProfile profile);
    public override void Update(float deltaTime);
}
```

AI 决策流程（每 `DecisionInterval` 秒执行一次）：

1. 如果 `_target == null || retargetTimer >= RetargetCooldown`，重新 `FindTarget`。
2. 遍历 `_behaviors`（按优先级排列：Flee -> Skill -> Attack -> Chase -> Idle）。
3. 对每个 behavior 先 `Update(deltaTime, target)`。
4. 如果 `CanEnter(target)`，执行 `Enter(target)` + `Update(deltaTime, target)`，然后 `return`（只执行最高优先级）。

## 8. CombatPhysicsSystem

```csharp
public class CombatPhysicsSystem : IBattleSystem
{
    public void RegisterLayerCollision(int layerA, int layerB);
    public bool CanLayersCollide(int layerA, int layerB);
    public int OverlapSphere(Vector3 center, float radius, int layerMask,
        QueryTriggerInteraction triggerInteraction, Action<Collider[], int> callback);
    public void RecordCollision(CombatActor source, CombatActor target, Vector3 point);
    public static bool RaycastGround(Vector3 origin, out Vector3 hitPoint, float maxDistance = 100f);
}
```

## 9. CombatPathfindingSystem

```csharp
public class CombatPathfindingSystem : IBattleSystem
{
    public bool CalculatePath(Vector3 start, Vector3 end, List<Vector3> resultPath);
    public void RequestPathAsync(Vector3 start, Vector3 end,
        Action<bool, List<Vector3>> callback);

    public static bool IsPositionOnNavMesh(Vector3 position);
    public static bool TrySamplePosition(Vector3 position, out Vector3 result, float maxDistance = 10f);
    public Vector3 GetRandomPointOnNavMesh(Vector3 center, float radius);
}
```

异步寻路：每帧处理 `_processPerFrame`（默认 2）个请求，使用 `_requestPool` 对象池。

`CombatNavMeshUtility` 工具类：

```csharp
public static class CombatNavMeshUtility
{
    public static bool FindClosestReachablePoint(Vector3 position, out Vector3 result, float maxDistance = 10f);
    public static float CalculatePathLength(List<Vector3> path);
    public static Vector3 GetDirectionAlongPath(List<Vector3> path, int currentIndex);
}
```

## 10. CombatProjectileSystem

```csharp
public class CombatProjectileSystem : IBattleSystem
{
    public ProjectileRuntime Runtime { get; }

    public void Update(float deltaTime) => Runtime?.Tick(deltaTime);
}
```

初始化时将 `Runtime.CollisionQuery` 设为 `QueryEnemiesInRange`（遍历 EntityManager 中所有 CombatActor）。

## 11. AnimationTimeScaleSystem

```csharp
public class AnimationTimeScaleSystem : IBattleSystem
{
    public void ApplyTimeScale(bool force = false);
}
```

通过 `IAnimationTimeScaleServices` 获取全局时缩参数：

```csharp
public interface IAnimationTimeScaleServices
{
    bool IsAnimationPaused { get; }
    float AnimationPlaybackScale { get; }
    void ForEachAnimationActor(Action<CombatActor> action);
}
```

控制对象：

- **Animator** — `animator.speed = baseSpeed * timeScale`。
- **AnimancerComponent** — `animancer.Playable.Speed = baseSpeed * timeScale`。
- **PlayableDirector** — 为每个 root playable 设置 `speed = baseSpeed * timeScale`。

启动时和 deltaTime 不为 0 时自动 `TrackActiveActors` 发现新物体。

## 12. CombatAssetCache

```csharp
public class CombatAssetCache : Disposable
{
    public int ModelCapacity { get; set; }    // 默认 20
    public int ParticleCapacity { get; set; } // 默认 10
    public int ModelCount { get; }
    public int ParticleCount { get; }

    // 预加载
    public void RegisterPreloadAsset(string path);
    public void RegisterPreloadAssets(IEnumerable<string> paths);
    public async UniTask StartPreloadAsync(IProgress<float> progress = null);
    public float GetPreloadProgress();

    // 加载（LRU 淘汰）
    public async UniTask<GameObject> LoadAssetLazyAsync(string path);
    public async UniTask<GameObject> LoadParticleLazyAsync(string path);
    public async UniTask<GameObject> GetOrLoadAssetAsync(string path);

    // 同步查询
    public GameObject GetLoadedAsset(string path);
    public bool IsAssetLoaded(string path);
    public bool IsAssetCached(string path);

    // 实例化
    public GameObject InstantiateModel(string path, Vector3 position,
        Quaternion rotation, Transform parent = null);
    public GameObject InstantiateParticle(string path, Vector3 position, Quaternion rotation);

    // 回收
    public void ReleaseInstance(string path, GameObject instance);
    public void UnloadUnusedAssets();
    public void UnloadAllLRU();

    // Pin（不会被 LRU 淘汰）
    public void PinAsset(string path);
    public void UnpinAsset(string path);
}
```

LRU 实现：双向链表（Head/Tail），访问时 `Touch` 到头部，溢出时从尾部淘汰。Model 和 Particle 独立 LRU 链表。

## 13. GAS 扩展 — Ability 定义

### BornAbilityDefinition

```csharp
[CreateAssetMenu(menuName = "BattleCommon/GAS/Ability/Born")]
public class BornAbilityDefinition : GameplayAbilityDefinition
{
    public TimelineAsset BornTimeline;                  // Timeline 优先
    public AnimationClip BornClip;                      // Montage 备选
    public float TransitionDuration = 0.1f;
    public GameplayEffectDefinition SelfBornEffect;     // 自身效果
    public GameplayEffectDefinition TargetBornEffect;   // 目标效果（EnableCollision 时施加）
}
```

激活流程：

1. `ApplyConfiguredEffects(spec)`。
2. `StartDelayedEffects(spec)`。
3. `ApplySelfEffect(spec)` — 如果 `SelfBornEffect != null`。
4. Timeline 可用则 `ActivateTimelineBorn`（创建 `AbilityTaskPlayTimeline`）；否则如果有 `BornClip` 则 `ActivateMontageBorn`（创建 `AbilityTaskPlayMontage`）。
5. 在 EnableCollision 事件触发时施加 `TargetBornEffect`。
6. 动画完成后 `EndAbility(Completed)`。

### MeleeAttackAbilityDefinition

```csharp
[CreateAssetMenu(menuName = "BattleCommon/GAS/Ability/Melee Attack")]
public class MeleeAttackAbilityDefinition : GameplayAbilityDefinition
{
    public AnimationClip AttackClip;
    public TimelineAsset AttackTimeline;  // 优先
    public float TransitionDuration = 0.1f;
    public string HitEventName = "Hit";
    public bool PreferSynchronizedMelee = true;
    public MeleeHitDefinition HitDefinition;
    public GameplayEffectDefinition DamageEffect;
}
```

激活流程：

1. `ApplyConfiguredEffects(spec)`。
2. `StartDelayedEffects(spec)`。
3. 如果 `AttackTimeline` 可用 -> `ActivateTimelineMelee`。
4. 如果有 `IMeleeAttackSourceProvider` -> `ActivateSynchronizedMelee`。
5. 在动画的 `HitEventName` 事件点创建 `AbilityTaskApplyMeleeHit`。
6. 如果无法注册事件则立即 ApplyHit。

### RemoteAttackAbilityDefinition

```csharp
[CreateAssetMenu(menuName = "BattleCommon/GAS/Ability/Remote Attack")]
public class RemoteAttackAbilityDefinition : GameplayAbilityDefinition
{
    public AnimationClip AttackClip;
    public float TransitionDuration = 0.1f;
    public string FireEventName = "Fire";
    public bool RequireRangedWeapon = true;
    public RangedProjectileDefinition ProjectileDefinition;
    public GameplayEffectDefinition DamageEffect;
}
```

`CanActivateAbility` 检查：

1. `base.CanActivateAbility(spec)`。
2. `sourceProvider.ProjectileRuntime != null`。
3. `RequireRangedWeapon && !HasRangedWeapon` -> false。
4. `projectileDef == null || DamageEffect == null` -> false。
5. PositionTarget: `ResolveTargetPosition` 有值。
6. EntityTarget: target 存在且 Valid。

激活流程：

1. `ApplyConfiguredEffects(spec)`。
2. 校验 sourceProvider/projectileDefinition/DamageEffect。
3. 如果无 `AttackClip`，直接 `FireProjectile()`。
4. 有动画则在 `FireEventName` 事件点 `FireProjectile()`（创建 `AbilityTaskSpawnProjectile`）。

## 14. GAS 扩展 — 定义

### MeleeHitDefinition

```csharp
[System.Serializable]
public class MeleeHitDefinition
{
    public int MeleeDefinitionId;
    public float Range = 1.5f;      // 前方判定距离
    public float Radius = 0.5f;      // 判定半径
    public int MaxTargets = 1;       // 最大命中数量
}

public interface IMeleeAttackSourceProvider
{
    Vector3 MeleeOrigin { get; }
    Vector3 MeleeForward { get; }
    IReadOnlyList<IRangedTarget> GetMeleeTargets(MeleeHitDefinition hitDefinition);
}
```

### RangedProjectileDefinition

```csharp
public enum ProjectileTrajectoryType { Linear, Parabolic }
public enum ProjectileTargetType { EntityTarget, PositionTarget }

[CreateAssetMenu(menuName = "BattleCommon/GAS/Ranged Projectile")]
public class RangedProjectileDefinition : ScriptableObject
{
    public int ProjectileDefinitionId;
    public float Speed = 10f;
    public float MaxLifeTime = 5f;
    public float HitRadius = 0.25f;
    public string VisualKey;

    public ProjectileTrajectoryType TrajectoryType = ProjectileTrajectoryType.Linear;
    public float ArcHeight;              // 抛物线最大高度（0=自动 25% 射程）

    // 水平偏移系数：0=纯垂直弧线，1=水平抛物运动
    public float ParabolicHorizontalWeight = 0.7f;

    public ProjectileTargetType TargetType = ProjectileTargetType.EntityTarget;
    public float ExplosionRadius;        // >0 时命中后 AOE
    public float SweepInterval = 0.1f;   // 飞行中碰撞检测间隔
    public float SweepRadius = 0.5f;     // 碰撞检测半径
}

public interface IRangedAttackSourceProvider
{
    bool HasRangedWeapon { get; }
    Vector3 FirePosition { get; }
    RangedProjectileDefinition ProjectileDefinition { get; }
    ProjectileRuntime ProjectileRuntime { get; }
}

public interface IRangedTarget
{
    GameplayEffectRuntime Effects { get; }
    Vector3 Position { get; }
    float HitRadius { get; }
    bool IsValidTarget { get; }
}
```

## 15. GAS 扩展 — CombatDamageExecution

```csharp
[CreateAssetMenu(menuName = "BattleCommon/GAS/Execution/Damage")]
public class CombatDamageExecution : GameplayEffectExecution
{
    public override void Execute(GameplayEffectSpec spec)
    {
        float hp = target.GetAttribute(HP);
        if (hp <= 0f) return;

        float attack = spec.GetSetByCaller(Attack, source.GetAttribute(Attack));
        float factor = spec.GetSetByCaller(AttackFactor, 1f);
        float increases = 1f +
            spec.GetSetByCaller(DamageUp1, source.GetAttribute(DamageUp1)) +
            spec.GetSetByCaller(DamageUp2, source.GetAttribute(DamageUp2));
        float reduction = 1f -
            target.GetAttribute(DamageReduce) -
            target.GetAttribute(DamageReduce1) -
            target.GetAttribute(DamageReduce2);
        float damage = Mathf.Max(1f,
            attack * factor * Mathf.Max(0f, increases) * Mathf.Max(0f, reduction) -
            target.GetAttribute(Defense) -
            target.GetAttribute(AbsoluteReduce));

        target.SetBaseValue(HP, Mathf.Max(0f, hp - damage));
    }
}

public static class CombatDamageKeys
{
    public const int AttackFactor = 1;
    public const int Attack = 2;
    public const int DamageUp1 = 3;
    public const int DamageUp2 = 4;
}
```

伤害公式：

```text
damage = max(1, attack * factor * max(0, increases) * max(0, reduction) - defense - absoluteReduce)
HP = max(0, HP - damage)
```

## 16. GAS 扩展 — ProjectileRuntime

### RangedProjectileHandle

```csharp
public readonly struct RangedProjectileHandle : IEquatable<RangedProjectileHandle>
{
    public static readonly RangedProjectileHandle Invalid;
    public readonly int Id;
    public bool IsValid => Id != 0;
}
```

### 生命周期

```csharp
public enum RangedProjectileEndReason
{
    Hit,               // 命中目标
    SweepCollided,    // 飞行中碰撞
    Cancelled,        // 被取消
    TimedOut,         // 超时（Elapsed >= MaxLifeTime）
    TargetInvalid,    // 目标失效
}
```

### ProjectileRuntime

```csharp
public class ProjectileRuntime
{
    public delegate List<IRangedTarget> CollisionQueryDelegate(Vector3 center, float radius);
    public CollisionQueryDelegate CollisionQuery { get; set; }
    public ProjectileSpawner Spawner { get; }
    public int ActiveCount { get; }

    public RangedProjectileHandle Spawn(RangedProjectileRequest request);
    public bool Cancel(RangedProjectileHandle handle);
    public bool IsActive(RangedProjectileHandle handle);

    public RangedProjectileState[] CaptureStates();
    public void Tick(float deltaTime);
}
```

### Tick 逻辑

每帧遍历所有 ProjectileInstance：

1. EntityTarget 检查 Target 有效性（`IsTargetValid`），无效则 `TargetInvalid`。
2. `Elapsed += deltaTime`，超时则 `TimedOut`。
3. 移动前 `TrySweepCollision`（检测飞行路径上的敌人），命中则 `SweepCollided`。
4. 计算新位置：Linear -> `MoveTowards`；Parabolic -> `CalculateParabolicPosition`。
5. 移动后再次 `TrySweepCollision`。
6. 距离目标位置 <= `HitRadius`（EntityTarget 合并 target.HitRadius），命中 -> `Hit`。

### Sweep 碰撞

`sweepTimer` 累加 deltaTime，超过 `SweepInterval` 时进行圆形碰撞查询。遇到的第一个敌人（非自身，未命中过）命中后：
- 有 `ExplosionRadius` -> 返回 -1 触发 AOE 结束。
- 无 `ExplosionRadius` -> 对单个目标 ApplyDamage 后结束。

### 命中处理

- 普通命中：`ApplyDamageToTarget(MakeOutgoingSpec -> ApplySpecToTarget)`。
- AOE 命中：遍历 `CollisionQuery(center, ExplosionRadius)`，对范围内每个敌人 `ApplyDamageToTarget`。
- 记录 `ProjectileHit` 事件。

### RangedProjectileState

```csharp
public readonly struct RangedProjectileState
{
    public readonly int ProjectileId;
    public readonly int ProjectileDefinitionId;
    public readonly long SourceEntityId;
    public readonly long TargetEntityId;
    public readonly Vector3 Position;
    public readonly float Elapsed;
}
```

用于状态捕获和恢复。

### RangedProjectileRequest

```csharp
public class RangedProjectileRequest
{
    public GameplayEffectRuntime Source;
    public IRangedTarget Target;
    public Vector3? TargetPosition;      // PositionTarget 模式使用
    public RangedProjectileDefinition Definition;
    public GameplayEffectDefinition DamageEffect;
    public int Level = 1;
    public Vector3 StartPosition;
    public object UserData;
    public int AbilityId, AbilitySpecId, AbilityTaskId;
    public Action<RangedProjectileResult> OnCompleted;
}
```

## 17. GAS 扩展 — AbilityTask

### AbilityTaskApplyMeleeHit

```csharp
public class AbilityTaskApplyMeleeHit : AbilityTask
{
    public int HitCount { get; }

    public AbilityTaskApplyMeleeHit(
        IMeleeAttackSourceProvider sourceProvider,
        MeleeHitDefinition hitDefinition,
        GameplayEffectDefinition damageEffect,
        object userData = null,
        Action<AbilityTaskApplyMeleeHit, int> onCompleted = null);
}
```

`OnActivate` 流程：

1. 记录 `MeleeWindowStarted` 事件。
2. `sourceProvider.GetMeleeTargets(hitDefinition)` 获取目标。
3. 遍历 targets（最多 `MaxTargets` 个，去重 entityId）。
4. `MakeOutgoingSpec -> ApplySpecToTarget`。
5. 记录 `MeleeHit` 事件。
6. 记录 `MeleeWindowEnded` 事件。
7. 调用 `onCompleted(this, HitCount)`，`EndTask()`。

### AbilityTaskSpawnProjectile

```csharp
public class AbilityTaskSpawnProjectile : AbilityTask
{
    public RangedProjectileHandle Handle { get; }
    public RangedProjectileResult Result { get; }

    public AbilityTaskSpawnProjectile(
        ProjectileRuntime projectileRuntime,
        IRangedTarget target,
        RangedProjectileDefinition projectileDefinition,
        GameplayEffectDefinition damageEffect,
        Vector3 startPosition,
        object userData = null,
        Action<AbilityTaskSpawnProjectile, RangedProjectileResult> onCompleted = null,
        Vector3? targetPosition = null);
}
```

`OnActivate`：

1. 校验 `projectileRuntime / projectileDefinition / source`。
2. 创建 `RangedProjectileRequest`，调用 `projectileRuntime.Spawn(request)`。
3. handle 无效则 `EndTask()`。

`OnEnd`：

- 如果弹道未完成且有效，调用 `projectileRuntime.Cancel(handle)` 确保清理。

`HandleProjectileCompleted`：

- 标记 `isProjectileComplete`，保存 Result，调用 `onCompleted`，`EndTask()`。

### AbilityTaskPlayMontage

```csharp
public interface IAnimancerProvider
{
    AnimancerComponent Animancer { get; }
}

public class AbilityTaskPlayMontage : AbilityTask
{
    public event Action OnEnableCollision;
    public event Action OnDisableCollision;

    public AbilityTaskPlayMontage(AnimationClip clip, float fadeDuration,
        Action<AbilityTaskPlayMontage> onCompleted = null);

    public bool TryRegisterEvent(string eventName, Action callback);
}
```

`OnActivate`：

1. 获取 `IAnimancerProvider.Animancer`。
2. `animancer.Play(clip, fadeDuration)`。
3. 注册 `OnEnd` 回调。
4. 尝试注册 `EnableCollision` / `DisableCollision` 事件。

`TryRegisterEvent` 检查 eventName 是否存在于 clip 的 Events 中，存在则 `SetCallback`。

### AbilityTaskPlayTimeline

```csharp
public interface ITimelineProvider
{
    PlayableDirector Director { get; }
}

public class AbilityTaskPlayTimeline : AbilityTask
{
    public event Action OnEnableCollision;
    public event Action OnDisableCollision;

    public AbilityTaskPlayTimeline(TimelineAsset timelineAsset,
        Action<AbilityTaskPlayTimeline> onCompleted = null);

    public bool TryRegisterEvent(string eventName, Action callback);
}
```

`OnActivate`：

1. 获取 `ITimelineProvider.Director`。
2. 收集所有 Marker 事件（遍历 OutputTracks + markerTrack）。
3. `director.Play()`。
4. 注册 `stopped` 回调。

`OnTick`：

1. 遍历 events，检查当前时间是否已到达（一次性的），触发 FireEvent。
2. 检查 playback 是否结束（`currentTime >= duration`）。

`FireEvent` 处理 `EnableCollision` / `DisableCollision` 内建事件和自定义事件。

## 18. 注意事项

- `CombatActor` 的 `Animancer` 和 `Director` 属性是惰性查找（首次访问时从 GameObject 获取）。
- `CombatActor.Position/Rotation` 优先使用 Transform，没有 Transform 时用 `_spawnPosition/_spawnRotation`。
- `CombatHealthComponent` 同时通过 `OnAttributeChanged(HP)` 和自身的 `TakeDamage` 两个路径处理死亡判定，通过 `_hasDied` 防重复。
- `CombatAbilityComponent` 在 `Initialize` 时创建新的 `GameplayAbilitySystem` 并销毁旧的，每次对象池取出都会重建 GAS。
- `CombatAttackComponent.FindTarget` 搜索范围是 `AttackRange * 2`（加倍搜索）。
- `CombatAIComponent` 的行为优先级硬编码为：Flee > Skill > Attack > Chase > Idle。
- `CombatPathfindingSystem` 使用 `NotifyListener` 来接收 frame update（`AddNotifyLisener`），`_updateInterval = 1/20`。
- `CombatProjectileSystem` 的 `CollisionQuery` 委托在 `Initialize` 时绑定到 `QueryEnemiesInRange`，后者遍历 EntityManager 中所有 `CombatActor`。
- `ProjectileRuntime.Tick` 按逆序遍历 projectiles（防止在遍历中 CompleteAt 导致索引错乱）。
- 弹道抛物线公式：`verticalOffset = (1 - parabolicT²) * arcHeight`，`parabolicT = t * 2 - 1`。
- `AbilityTaskApplyMeleeHit` 在 `OnActivate` 中同步完成全部逻辑后立即 `EndTask()`。
- `AbilityTaskSpawnProjectile` 在 `OnEnd` 时确保 cancel 未完成的弹道，防止泄漏。
- `AnimationTimeScaleSystem` 在 deltaTime == 0 时跳过 Apply（避免暂停时反复设置 speed=0）。
- `CombatAssetCache` 的 Preload 使用 `UniTask.WhenAll`，支持 IProgress 报告进度。
- `RemoteAttackAbilityDefinition.Target` 解析优先级：`AbilitySpec.Target.AttributeOwner as IRangedTarget` -> `TriggerEventData.UserData as IRangedTarget`。