using System;
using System.Collections.Generic;
using BattleFoundation;
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
        private readonly List<CombatActor> _cache = new List<CombatActor>(32);

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
            FindInRange(source, range, _cache);
            if (_cache.Count == 0) return null;
            if (filter != null) _cache.RemoveAll(target => !filter(target));
            if (_cache.Count == 0) return null;

            CombatActor result = _cache[0];
            for (int i = 1; i < _cache.Count; i++)
            {
                var candidate = _cache[i];
                switch (priority)
                {
                    case CombatTargetPriority.LowestHP:
                        if (Health(candidate) < Health(result)) result = candidate;
                        break;
                    case CombatTargetPriority.HighestHP:
                        if (Health(candidate) > Health(result)) result = candidate;
                        break;
                    case CombatTargetPriority.Random:
                        return _cache[_context.Random.Range(_cache.Count)];
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
            float rangeSqr = range * range;
            var all = _context.EntityManager.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (!(all[i] is CombatActor target) || target == source || !target.IsAlive || !_relations.AreEnemies(source, target))
                    continue;
                if ((target.Position - source.Position).sqrMagnitude <= rangeSqr)
                    results.Add(target);
            }
            return results.Count;
        }

        public void FindMeleeTargets(IMeleeSource source, MeleeHitDefinition hitDefinition, List<GAS.IRangedTarget> results)
        {
            results.Clear();
            if (!(source is CombatActor actor) || hitDefinition == null || _context?.EntityManager == null) return;
            float range = Mathf.Max(0f, hitDefinition.Range);
            float radius = Mathf.Max(0f, hitDefinition.Radius);
            var all = _context.EntityManager.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (!(all[i] is CombatActor target) || !target.IsAlive || !_relations.AreEnemies(actor, target)) continue;
                Vector3 toTarget = target.Position - source.MeleeOrigin;
                float forwardDistance = Vector3.Dot(source.MeleeForward, toTarget);
                if (forwardDistance < -radius || forwardDistance > range + radius) continue;
                Vector3 closest = source.MeleeOrigin + source.MeleeForward * Mathf.Clamp(forwardDistance, 0f, range);
                float allowedRadius = radius + target.HitRadius;
                if ((target.Position - closest).sqrMagnitude <= allowedRadius * allowedRadius)
                    results.Add(target);
            }
        }

        private static float Health(CombatActor target) => target.Get<CombatHealthComponent>()?.HP ?? 0f;

        public void Dispose()
        {
            _cache.Clear();
            _context = null;
            _relations = null;
        }
    }

    public class CombatActorSystem : IBattleSystem
    {
        private IBattleContext _context;
        private readonly List<CombatActor> _pendingRecycle = new List<CombatActor>();
        public Action<CombatActor> OnRecycleRequested;

        public void Initialize(IBattleContext context) => _context = context;
        public void Start() { }

        public void AddActor(CombatActor actor)
        {
            if (actor == null || _context == null) return;
            _context.EntityManager.AddEntity(actor);
            actor.Initialize();
        }

        public void RemoveActor(CombatActor actor)
        {
            if (actor == null || _context == null) return;
            _context.EntityManager.RemoveEntity(actor);
        }

        public void Update(float deltaTime)
        {
            _pendingRecycle.Clear();
            var entities = _context?.EntityManager?.All;
            if (entities == null) return;
            for (int i = 0; i < entities.Count; i++)
            {
                if (!(entities[i] is CombatActor actor)) continue;
                actor.Update(deltaTime);
                if (actor.CanRecycle) _pendingRecycle.Add(actor);
            }

            for (int i = 0; i < _pendingRecycle.Count; i++)
                OnRecycleRequested?.Invoke(_pendingRecycle[i]);
        }

        public void LateUpdate(float deltaTime) { }

        public void Dispose()
        {
            _pendingRecycle.Clear();
            OnRecycleRequested = null;
            _context = null;
        }
    }
}
