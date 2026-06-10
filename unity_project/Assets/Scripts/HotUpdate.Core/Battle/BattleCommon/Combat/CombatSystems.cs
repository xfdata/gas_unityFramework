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

                Vector3 toTarget = target.Position - source.MeleeOrigin;
                float forwardDistance = Vector3.Dot(source.MeleeForward, toTarget);
                if (forwardDistance < -radius || forwardDistance > range + radius) continue;
                Vector3 closest = source.MeleeOrigin + source.MeleeForward * Mathf.Clamp(forwardDistance, 0f, range);
                float allowedRadius = radius + target.HitRadius;
                if ((target.Position - closest).sqrMagnitude <= allowedRadius * allowedRadius)
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
            Add,
            Remove,
            Recycle,
            Dispose,
        }

        private struct PendingActorOperation
        {
            public CombatActor Actor;
            public PendingActorOperationType Type;
        }

        private IBattleContext _context;
        private readonly List<CombatActor> _pendingRecycle = new List<CombatActor>();
        private readonly List<PendingActorOperation> _pendingActorOperations = new List<PendingActorOperation>();
        private bool _isIteratingActors;
        public Action<CombatActor> OnRecycleRequested;

        public void Initialize(IBattleContext context) => _context = context;
        public void Start() { }

        public void AddActor(CombatActor actor)
        {
            if (actor == null || _context == null) return;
            if (QueueActorOperation(actor, PendingActorOperationType.Add))
                return;

            AddActorNow(actor);
        }

        public void RemoveActor(CombatActor actor)
        {
            if (actor == null || _context == null) return;
            if (QueueActorOperation(actor, PendingActorOperationType.Remove))
                return;

            RemoveActorNow(actor);
        }

        public void RecycleActor(CombatActor actor)
        {
            if (actor == null || _context == null) return;
            if (QueueActorOperation(actor, PendingActorOperationType.Recycle))
                return;

            RecycleActorNow(actor);
        }

        public void DisposeActor(CombatActor actor)
        {
            if (actor == null || _context == null) return;
            if (QueueActorOperation(actor, PendingActorOperationType.Dispose))
                return;

            DisposeActorNow(actor);
        }

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

                for (int i = 0; i < _pendingRecycle.Count; i++)
                    OnRecycleRequested?.Invoke(_pendingRecycle[i]);

                FlushPendingActorOperations();
            }
        }

        public void LateUpdate(float deltaTime) { }

        public void Dispose()
        {
            _pendingRecycle.Clear();
            _pendingActorOperations.Clear();
            _isIteratingActors = false;
            OnRecycleRequested = null;
            _context = null;
        }

        private bool QueueActorOperation(CombatActor actor, PendingActorOperationType type)
        {
            if (!_isIteratingActors)
                return false;

            if (type != PendingActorOperationType.Add)
                RemovePendingAdd(actor);

            if (HasPendingTerminalOperation(actor))
                return true;

            _pendingActorOperations.Add(new PendingActorOperation
            {
                Actor = actor,
                Type = type,
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
                    case PendingActorOperationType.Add:
                        AddActorNow(operation.Actor);
                        break;
                    case PendingActorOperationType.Remove:
                        RemoveActorNow(operation.Actor);
                        break;
                    case PendingActorOperationType.Recycle:
                        RecycleActorNow(operation.Actor);
                        break;
                    case PendingActorOperationType.Dispose:
                        DisposeActorNow(operation.Actor);
                        break;
                }
            }

            _pendingActorOperations.Clear();
        }

        private void AddActorNow(CombatActor actor)
        {
            _context.EntityManager.AddEntity(actor);
            actor.Initialize();
        }

        private void RemoveActorNow(CombatActor actor)
        {
            _context.EntityManager.RemoveEntity(actor);
        }

        private void RecycleActorNow(CombatActor actor)
        {
            _context.EntityManager.RemoveEntity(actor);
            actor.DeactivateForPool();
        }

        private void DisposeActorNow(CombatActor actor)
        {
            _context.EntityManager.RemoveEntity(actor);
            actor.Dispose();
        }

        private bool IsPendingRemoval(CombatActor actor)
        {
            for (int i = 0; i < _pendingActorOperations.Count; i++)
            {
                if (_pendingActorOperations[i].Actor == actor &&
                    _pendingActorOperations[i].Type != PendingActorOperationType.Add)
                {
                    return true;
                }
            }
            return false;
        }

        private bool HasPendingTerminalOperation(CombatActor actor)
        {
            for (int i = 0; i < _pendingActorOperations.Count; i++)
            {
                if (_pendingActorOperations[i].Actor != actor)
                    continue;

                if (_pendingActorOperations[i].Type == PendingActorOperationType.Dispose ||
                    _pendingActorOperations[i].Type == PendingActorOperationType.Recycle)
                {
                    return true;
                }
            }
            return false;
        }

        private void RemovePendingAdd(CombatActor actor)
        {
            for (int i = _pendingActorOperations.Count - 1; i >= 0; i--)
            {
                if (_pendingActorOperations[i].Actor == actor &&
                    _pendingActorOperations[i].Type == PendingActorOperationType.Add)
                {
                    _pendingActorOperations.RemoveAt(i);
                }
            }
        }
    }
}
