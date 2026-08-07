using System;
using System.Collections.Generic;
using BattleFoundation;
using Framework;
using GAS;
using UnityEngine;

namespace BattleCommon
{
    public class DefaultCombatRelationResolver : ICombatRelationResolver
    {
        public bool AreEnemies(CombatActor source, CombatActor target)
        {
            return source != null && target != null && source.Camp != target.Camp && target.Camp != EEntityCamp.Neutral;
        }
    }

    public class CombatTargetQuerySystem : IBattleSystem, ICombatTargetQuery
    {
        private IBattleContext _context;
        private ICombatRelationResolver _relations;

        public CombatTargetQuerySystem(ICombatRelationResolver relations = null)
        {
            _relations = relations ?? new DefaultCombatRelationResolver();
        }

        public void Initialize(IBattleContext context) => _context = context;

        public void Start() { }
        public void Update(float deltaTime) { }
        public void LateUpdate(float deltaTime) { }

        public CombatActor FindTarget(CombatActor source, Func<CombatActor, bool> filter, CombatTargetPriority priority, float range)
        {
            if (source == null || _context?.EntityManager == null) return null;
            if (source?.Get<CombatStateComponent>() is {} state && !state.CanAct) return null;

            float rangeSqr = range * range;
            var candidates = GetCandidateEntities(source);
            CombatActor result = null;
            int validCount = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                if (!(candidates[i] is CombatActor candidate) ||
                    candidate == source ||
                    !candidate.IsAlive ||
                    !CanBeCombatTarget(candidate) ||
                    !_relations.AreEnemies(source, candidate) ||
                    (candidate.Position - source.Position).sqrMagnitude > rangeSqr ||
                    (filter != null && !filter(candidate)))
                {
                    continue;
                }

                validCount++;
                if (result == null)
                {
                    result = candidate;
                    continue;
                }

                switch (priority)
                {
                    case CombatTargetPriority.LowestHP:
                        if (Health(candidate) < Health(result)) result = candidate;
                        break;
                    case CombatTargetPriority.HighestHP:
                        if (Health(candidate) > Health(result)) result = candidate;
                        break;
                    case CombatTargetPriority.Random:
                        if (_context.Random.Range(validCount) == 0) result = candidate;
                        break;
                    default:
                        if ((candidate.Position - source.Position).sqrMagnitude < (result.Position - source.Position).sqrMagnitude)
                            result = candidate;
                        break;
                }
            }

            return result;
        }

        public int FindInRange(CombatActor source, float range, List<CombatActor> results)
        {
            results.Clear();
            if (source == null || _context?.EntityManager == null) return 0;
            if (source?.Get<CombatStateComponent>() is {} state && !state.CanAct) return 0;

            float rangeSqr = range * range;
            var candidates = GetCandidateEntities(source);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (!(candidates[i] is CombatActor target) ||
                    target == source ||
                    !target.IsAlive ||
                    !CanBeCombatTarget(target) ||
                    !_relations.AreEnemies(source, target))
                {
                    continue;
                }

                if ((target.Position - source.Position).sqrMagnitude <= rangeSqr)
                    results.Add(target);
            }
            return results.Count;
        }

        public void FindMeleeTargets(IMeleeSource source, MeleeHitDefinition hitDefinition, List<GAS.IRangedTarget> results)
        {
            results.Clear();
            if (!(source is CombatActor actor) || hitDefinition == null || _context?.EntityManager == null) return;
            if (actor?.Get<CombatStateComponent>() is {} state && !state.CanAct) return;

            float range = Mathf.Max(0f, hitDefinition.Range);
            float radius = Mathf.Max(0f, hitDefinition.Radius);
            var candidates = GetCandidateEntities(actor);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (!(candidates[i] is CombatActor target) ||
                    !target.IsAlive ||
                    !CanBeCombatTarget(target) ||
                    !_relations.AreEnemies(actor, target))
                {
                    continue;
                }

                Vector3 toTarget = new Vector3(target.Position.x, target.Position.y, target.Position.z) - source.MeleeOrigin;
                float forwardDistance = Vector3.Dot(source.MeleeForward, toTarget);
                if (forwardDistance < -radius || forwardDistance > range + radius) continue;
                Vector3 closest = source.MeleeOrigin + source.MeleeForward * Mathf.Clamp(forwardDistance, 0f, range);
                float allowedRadius = radius + target.HitRadius;
                Vector3 targetPos = new Vector3(target.Position.x, target.Position.y, target.Position.z);
                if ((targetPos - closest).sqrMagnitude <= allowedRadius * allowedRadius)
                    results.Add(target);
            }
        }

        private IReadOnlyList<BattleEntity> GetCandidateEntities(CombatActor source)
        {
            if (source == null || _context?.EntityManager == null)
                return Array.Empty<BattleEntity>();

            switch (source.Camp)
            {
                case EEntityCamp.Ally:
                    return _context.EntityManager.GetByCamp(EEntityCamp.Enemy);
                case EEntityCamp.Enemy:
                    return _context.EntityManager.GetByCamp(EEntityCamp.Ally);
                default:
                    return _context.EntityManager.All;
            }
        }

        private static float Health(CombatActor target) => target.Get<CombatHealthComponent>()?.HP ?? 0f;

        private static bool CanBeCombatTarget(CombatActor target)
        {
            if (target?.Gameplay?.States.CanBeTargeted() == false)
                return false;

            return target?.Get<CombatStateComponent>() is not {} state || state.CanBeAttacked;
        }

        public void Dispose()
        {
            _context = null;
            _relations = null;
        }
    }

    public class CombatActorSystem : IBattleSystem
    {
        private enum PendingActorOperationType
        {
            Spawn,
            Despawn,
        }

        private struct PendingActorOperation
        {
            public CombatActor Actor;
            public DeathReason Reason;
            public int KillerId;
            public PendingActorOperationType Type;
        }

        private IBattleContext _context;
        private readonly List<CombatActor> _pendingRecycle = new List<CombatActor>();
        private readonly List<PendingActorOperation> _pendingActorOperations = new List<PendingActorOperation>();
        private bool _isIteratingActors;

        /// <summary>
        /// R3-S5: 向后兼容字段。原 OnRecycleRequested 回调保留，
        /// 但推荐使用 ActorDiedEvent 事件订阅（经 BattleEventBus）。
        /// S6 集中回收完成后评估是否移除。
        /// </summary>
        public Action<CombatActor> OnRecycleRequested;

        public void Initialize(IBattleContext context) => _context = context;

        public void Start()
        {
            var entities = _context?.EntityManager?.All;
            if (entities == null)
                return;

            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i] is CombatActor actor)
                    actor.Start();
            }
        }

        // ===== R3-S5: 唯一 spawn/销毁门面 API =====

        /// <summary>
        /// R3-S5: 唯一 spawn 入口。失败全量回滚，不允许半成品实体残留（决策 1）。
        /// 成功后通过 BattleEventBus 发送 ActorSpawnedEvent。
        /// </summary>
        public void Spawn(CombatActor actor)
        {
            if (actor == null || _context == null) return;
            if (QueueSpawnOperation(actor))
                return;

            SpawnNow(actor);
        }

        /// <summary>
        /// R3-S5: 唯一销毁入口。先 ReclaimActorState 集中回收关联状态，
        /// 再移除实体并发送 ActorDiedEvent（决策 1/3）。
        /// </summary>
        public void Despawn(CombatActor actor, DeathReason reason = DeathReason.Killed, int killerId = 0)
        {
            if (actor == null || _context == null) return;
            if (QueueDespawnOperation(actor, reason, killerId))
                return;

            DespawnNow(actor, reason, killerId);
        }

        /// <summary>
        /// R3-S5: 场景级强制清理。战斗结束/场景卸载时批量销毁全部 actor。
        /// 所有 actor 标记为 SceneCleanup 原因。
        /// </summary>
        public void DespawnAll(DeathReason reason = DeathReason.SceneCleanup)
        {
            if (_context?.EntityManager == null) return;

            var entities = _context.EntityManager.All;
            for (int i = entities.Count - 1; i >= 0; i--)
            {
                if (entities[i] is CombatActor actor)
                    Despawn(actor, reason);
            }
        }

        // ===== 向后兼容 API（委托到新 API）=====

        [Obsolete("Use Spawn instead.")]
        public void AddActor(CombatActor actor) => Spawn(actor);

        [Obsolete("Use Despawn instead.")]
        public void RemoveActor(CombatActor actor)
        {
            // Backward-compatible name; removal now follows the same cleanup path as Despawn.
            Despawn(actor, DeathReason.SceneCleanup);
        }

        [Obsolete("Use Despawn instead.")]
        public void RecycleActor(CombatActor actor) => Despawn(actor, DeathReason.Killed);

        [Obsolete("Use Despawn instead.")]
        public void DisposeActor(CombatActor actor) => Despawn(actor, DeathReason.SceneCleanup);

        public void Update(float deltaTime)
        {
            using (new AutoProfiler("BattleCommon.CombatActorSystem.Update"))
            {
                FlushPendingActorOperations();
                _pendingRecycle.Clear();
                var entities = _context?.EntityManager?.All;
                if (entities == null) return;

                _isIteratingActors = true;
                try
                {
                    for (int i = 0; i < entities.Count; i++)
                    {
                        if (!(entities[i] is CombatActor actor) || IsPendingRemoval(actor))
                            continue;

                        actor.Update(deltaTime);
                        if (!IsPendingRemoval(actor) && actor.CanRecycle)
                            _pendingRecycle.Add(actor);
                    }
                }
                finally
                {
                    _isIteratingActors = false;
                }

                FlushPendingActorOperations();

                // R3-S5: CanRecycle 触发的销毁走 Killed 原因（HP 归零）。
                for (int i = 0; i < _pendingRecycle.Count; i++)
                {
                    var actor = _pendingRecycle[i];
                    OnRecycleRequested?.Invoke(actor);
                    Despawn(actor, DeathReason.Killed);
                }

                FlushPendingActorOperations();
            }
        }

        public void LateUpdate(float deltaTime) { }

        public void Dispose()
        {
            // Context disposes systems before EntityManager. Route every remaining
            // combat actor through Despawn while the event bus is still available.
            _isIteratingActors = false;
            FlushPendingActorOperations();
            DespawnAll(DeathReason.SceneCleanup);
            _pendingRecycle.Clear();
            _pendingActorOperations.Clear();
            OnRecycleRequested = null;
            _context = null;
        }

        // ===== 内部实现 =====

        private void SpawnNow(CombatActor actor)
        {
            // R3-S5: Spawn 失败全量回滚（决策 1）。
            // 流程：AddEntity -> Initialize -> 失败则逆序回滚。
            try
            {
                _context.EntityManager.AddEntity(actor);
                if (_context.EntityManager.GetById(actor.Id) != actor)
                    throw new InvalidOperationException($"Actor {actor.Id} could not be registered.");

                actor.Initialize();
                var phase = _context.Engine?.Phase;
                if (phase == EBattlePhase.Running ||
                    phase == EBattlePhase.Paused ||
                    phase == EBattlePhase.Replaying)
                {
                    actor.Start();
                }
            }
            catch (Exception)
            {
                // 回滚：移除已注册的实体 + 清理关联状态 + Dispose
                RollbackSpawn(actor);
                throw;
            }

            // 成功：发送 spawn 事件
            _context.EventBus?.Emit(CombatActorEventIds.ActorSpawned,
                new ActorSpawnedEvent(actor.Id, actor.Camp, actor.EntityType));
        }

        private void DespawnNow(CombatActor actor, DeathReason reason, int killerId)
        {
            // R3-S5: ReclaimActorState（决策 1）。
            // 销毁前集中清理关联状态，避免散落各处的反注册。
            ReclaimActorState(actor);

            // 发送死亡事件（决策 3）
            _context.EventBus?.Emit(CombatActorEventIds.ActorDied,
                new ActorDiedEvent(actor.Id, killerId, reason));

            // 移除实体 + Dispose
            _context.EntityManager.RemoveEntity(actor);
            actor.Dispose();
        }

        /// <summary>
        /// R3-S5: Spawn 失败回滚。从 EntityManager 移除 + 清理关联状态 + Dispose。
        /// 确保无半成品实体残留。
        /// </summary>
        private void RollbackSpawn(CombatActor actor)
        {
            _context.EntityManager.RemoveEntity(actor);
            ReclaimActorState(actor);
            actor.Dispose();
        }

        /// <summary>
        /// R3-S6: 关联状态集中回收（决策 1，ReclaimActorState 模式）。
        /// 对照 AbilityKit 实体销毁前清理 6 项关联状态，每项显式独立、可审计。
        /// 在 actor.DeactivateForPool() 兜底之前，按清单顺序执行显式回收。
        /// </summary>
        private void ReclaimActorState(CombatActor actor)
        {
            // ── 1. 技能（ActiveAbilities）──
            // GAS 模式：_gas.Dispose() 清理 abilityRuntime + effectRuntime（含 activeAbilities）。
            // Lightweight 模式：_lightActiveAbilities.Clear()。
            // 由 CombatAbilityComponent.DeactivateForPool 覆盖，无需额外显式调用。

            // ── 2. Buff（ActiveEffects）──
            // GAS 模式：_gas.Dispose() → effectRuntime.Dispose() → 遍历 RemoveActiveEffectAt。
            // Lightweight 模式：_lightEffects.Dispose()。
            // 由 CombatAbilityComponent.DeactivateForPool 覆盖，无需额外显式调用。

            // ── 3. Tag（OwnedTags）──
            // effect granted tags 在 effect 移除时自动移除；
            // 外部直接 AddTag 的 tag 需显式 Clear，避免残留到下一帧观察者。
            ReclaimTags(actor);

            // ── 4. 护盾（ShieldComponent）──
            // 工程现状：无 ShieldComponent，保留为扩展点。
            // 未来引入护盾系统时，在此处显式回收护盾实例与事件。

            // ── 5. 事件订阅 ──
            // CombatHealthComponent.DeactivateForPool 反订阅 OnAttributeChanged + 清空 OnDeath/OnHealed。
            // 由组件 DeactivateForPool 覆盖，无需额外显式调用。

            // ── 6. 索引（空间索引）──
            // 工程现状：CombatTargetQuerySystem 无空间索引，FindMeleeTargets 直接遍历 All。
            // 保留为扩展点；S8 NavMesh 迁移后若引入空间索引，在此处显式移除。

            // ── 兜底：触发组件级 DeactivateForPool ──
            // 统一触发各组件 DeactivateForPool（技能/Buff/事件订阅由各组件实现清理）。
            // DeactivateForPool 幂等，与上述显式清理重复调用安全。
            actor.DeactivateForPool();
        }

        /// <summary>
        /// 显式清空 OwnedTags 中外部直接持有的 tag。
        /// effect granted tags 由 GAS effect 移除时自动清理，此处只处理外部 AddTag 的残留。
        /// </summary>
        private void ReclaimTags(CombatActor actor)
        {
            actor.Effects?.OwnedTags?.Clear();
        }

        private bool QueueSpawnOperation(CombatActor actor)
        {
            if (!_isIteratingActors)
                return false;

            // 已有终态操作（Despawn）则不再 Spawn
            for (int i = 0; i < _pendingActorOperations.Count; i++)
            {
                if (_pendingActorOperations[i].Actor == actor &&
                    _pendingActorOperations[i].Type == PendingActorOperationType.Despawn)
                    return true;
            }

            _pendingActorOperations.Add(new PendingActorOperation
            {
                Actor = actor,
                Type = PendingActorOperationType.Spawn,
            });
            return true;
        }

        private bool QueueDespawnOperation(CombatActor actor, DeathReason reason, int killerId)
        {
            if (!_isIteratingActors)
                return false;

            // 移除已有的 Spawn 操作（Spawn 未执行就 Despawn，直接抵消）
            for (int i = _pendingActorOperations.Count - 1; i >= 0; i--)
            {
                if (_pendingActorOperations[i].Actor == actor &&
                    _pendingActorOperations[i].Type == PendingActorOperationType.Spawn)
                {
                    _pendingActorOperations.RemoveAt(i);
                }
            }

            // 已有 Despawn 则不再重复
            for (int i = 0; i < _pendingActorOperations.Count; i++)
            {
                if (_pendingActorOperations[i].Actor == actor &&
                    _pendingActorOperations[i].Type == PendingActorOperationType.Despawn)
                    return true;
            }

            _pendingActorOperations.Add(new PendingActorOperation
            {
                Actor = actor,
                Reason = reason,
                KillerId = killerId,
                Type = PendingActorOperationType.Despawn,
            });
            return true;
        }

        private void FlushPendingActorOperations()
        {
            if (_pendingActorOperations.Count == 0 || _context == null)
                return;

            for (int i = 0; i < _pendingActorOperations.Count; i++)
            {
                var operation = _pendingActorOperations[i];
                if (operation.Actor == null)
                    continue;

                switch (operation.Type)
                {
                    case PendingActorOperationType.Spawn:
                        SpawnNow(operation.Actor);
                        break;
                    case PendingActorOperationType.Despawn:
                        DespawnNow(operation.Actor, operation.Reason, operation.KillerId);
                        break;
                }
            }

            _pendingActorOperations.Clear();
        }

        private bool IsPendingRemoval(CombatActor actor)
        {
            for (int i = 0; i < _pendingActorOperations.Count; i++)
            {
                if (_pendingActorOperations[i].Actor == actor &&
                    _pendingActorOperations[i].Type == PendingActorOperationType.Despawn)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
