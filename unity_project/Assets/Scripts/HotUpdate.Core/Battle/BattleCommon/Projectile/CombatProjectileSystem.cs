using System.Collections.Generic;
using BattleFoundation;
using Framework;
using GAS;
using UnityEngine;

namespace BattleCommon
{
    public class CombatProjectileSystem : IBattleSystem
    {
        private IBattleContext _context;
        private readonly ICombatRelationResolver _relations;
        private readonly List<IRangedTarget> _queryResults = new List<IRangedTarget>(32);

        public ProjectileRuntime Runtime { get; private set; } = new ProjectileRuntime();

        public CombatProjectileSystem(ICombatRelationResolver relations = null)
        {
            _relations = relations ?? new DefaultCombatRelationResolver();
        }

        public void Initialize(IBattleContext context)
        {
            _context = context;
            Runtime.CollisionQuery = QueryEnemiesInRange;
        }

        public void Start() { }
        public void Update(float deltaTime)
        {
            using (new AutoProfiler("BattleCommon.CombatProjectileSystem.Update"))
            {
                Runtime?.Tick(deltaTime);
            }
        }
        public void LateUpdate(float deltaTime) { }

        public void Dispose()
        {
            Runtime.CollisionQuery = null;
            Runtime = null;
            _context = null;
        }

        private List<IRangedTarget> QueryEnemiesInRange(GameplayEffectRuntime source, Vector3 center, float radius)
        {
            _queryResults.Clear();

            if (_context?.EntityManager == null)
                return _queryResults;

            var sourceActor = source?.AttributeOwner as CombatActor;
            if (sourceActor == null)
                return _queryResults;

            float radiusSqr = radius * radius;
            var all = _context.EntityManager.All;

            for (int i = 0; i < all.Count; i++)
            {
                if (!(all[i] is CombatActor actor) ||
                    actor == sourceActor ||
                    !actor.IsAlive ||
                    !CanBeCombatTarget(actor) ||
                    !_relations.AreEnemies(sourceActor, actor))
                {
                    continue;
                }

                // actor.Position 为 Float3，center 为 Vector3，统一转 Float3 运算
                var actorPos = actor.Position;
                float dx = actorPos.x - center.x;
                float dy = actorPos.y - center.y;
                float dz = actorPos.z - center.z;
                if (dx * dx + dy * dy + dz * dz <= radiusSqr)
                    _queryResults.Add(actor);
            }

            return _queryResults;
        }

        private static bool CanBeCombatTarget(CombatActor target)
        {
            if (target?.Gameplay?.States.CanBeTargeted() == false)
                return false;

            return target?.Get<CombatStateComponent>() is not {} state || state.CanBeAttacked;
        }
    }
}
