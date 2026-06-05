using System.Collections.Generic;
using BattleFoundation;
using GAS;
using UnityEngine;

namespace BattleCommon
{
    public class CombatProjectileSystem : IBattleSystem
    {
        private IBattleContext _context;

        public ProjectileRuntime Runtime { get; private set; } = new ProjectileRuntime();

        public void Initialize(IBattleContext context)
        {
            _context = context;
            Runtime.CollisionQuery = QueryEnemiesInRange;
        }

        public void Start() { }
        public void Update(float deltaTime) => Runtime?.Tick(deltaTime);
        public void LateUpdate(float deltaTime) { }

        public void Dispose()
        {
            Runtime.CollisionQuery = null;
            Runtime = null;
            _context = null;
        }

        private List<IRangedTarget> QueryEnemiesInRange(Vector3 center, float radius)
        {
            var results = new List<IRangedTarget>();

            if (_context?.EntityManager == null)
                return results;

            float radiusSqr = radius * radius;
            var all = _context.EntityManager.All;

            for (int i = 0; i < all.Count; i++)
            {
                if (!(all[i] is CombatActor actor) || !actor.IsAlive)
                    continue;

                if ((actor.Position - center).sqrMagnitude <= radiusSqr)
                    results.Add(actor);
            }

            return results;
        }
    }
}
