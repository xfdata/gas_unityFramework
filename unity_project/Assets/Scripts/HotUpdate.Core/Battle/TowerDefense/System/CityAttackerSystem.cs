using System.Collections.Generic;
using BattleFoundation;
using Framework;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 城市攻击者驱动系统 — 集中驱动所有 CityAttackerComponent 的 UpdateAttack。
    /// 
    /// 性能设计：
    /// - 单点Update，遍历所有正在攻击主城的敌人
    /// - 使用缓存列表避免每帧分配
    /// </summary>
    public class CityAttackerSystem : IBattleSystem
    {
        private IBattleContext _context;
        private EntityManager _entityManager;
        private readonly List<BattleEntity> _attackers = new List<BattleEntity>(16);

        public void Initialize(IBattleContext context)
        {
            _context = context;
            _entityManager = context.EntityManager;
        }

        public void Start() { }

        public void Update(float deltaTime)
        {
            using (new AutoProfiler("TowerDefense.CityAttackerSystem.Update"))
            {
                // 获取所有敌人
                var enemies = _entityManager.GetByCamp(EEntityCamp.Enemy);
                if (enemies == null) return;

                for (int i = 0; i < enemies.Count; i++)
                {
                    var entity = enemies[i];
                    if (!entity.IsAlive) continue;

                    var attacker = entity.Get<CityAttackerComponent>();
                    if (attacker != null && attacker.IsAttacking)
                    {
                        attacker.UpdateAttack(deltaTime);
                    }
                }
            }
        }

        public void LateUpdate(float deltaTime) { }

        public void Dispose()
        {
            _attackers.Clear();
            _entityManager = null;
            _context = null;
        }
    }
}
