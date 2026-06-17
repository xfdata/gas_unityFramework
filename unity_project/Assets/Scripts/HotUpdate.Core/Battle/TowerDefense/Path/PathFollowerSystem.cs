using System.Collections.Generic;
using BattleFoundation;
using Framework;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 路径跟随系统 — 集中驱动所有PathFollowerComponent。
    /// 
    /// 性能设计：
    /// - 单点Update，避免每个怪物独立Update
    /// - 遍历EntityManager.Enemy阵营实体，提取PathFollowerComponent批量更新
    /// - 检测到达终点的敌人，发射EnemyReachedEndEvent
    /// </summary>
    public class PathFollowerSystem : IBattleSystem
    {
        private IBattleContext _context;
        private EntityManager _entityManager;

        // 缓存列表避免每帧分配
        private readonly List<BattleEntity> _pendingReachedEnd = new List<BattleEntity>(16);

        public void Initialize(IBattleContext context)
        {
            _context = context;
            _entityManager = context.EntityManager;
        }

        public void Start() { }

        public void Update(float deltaTime)
        {
            using (new AutoProfiler("TowerDefense.PathFollowerSystem.Update"))
            {
                var enemies = _entityManager.GetByCamp(EEntityCamp.Enemy);
                _pendingReachedEnd.Clear();

                for (int i = 0; i < enemies.Count; i++)
                {
                    var entity = enemies[i];
                    if (!entity.IsAlive) continue;

                    var follower = entity.Get<PathFollowerComponent>();
                    if (follower == null) continue;

                    var actor = entity as BattleCommon.CombatActor;
                    follower.Tick(deltaTime, actor?.Transform);

                    // 到达终点
                    if (follower.ReachedEnd)
                        _pendingReachedEnd.Add(entity);
                }

                // 后置处理到达终点事件（不在遍历中触发事件，避免集合修改）
                for (int i = 0; i < _pendingReachedEnd.Count; i++)
                {
                    HandleEnemyReachedEnd(_pendingReachedEnd[i]);
                }
            }
        }

        public void LateUpdate(float deltaTime) { }

        private void HandleEnemyReachedEnd(BattleEntity enemy)
        {
            // 防止重复触发
            var follower = enemy.Get<PathFollowerComponent>();
            if (follower != null && follower.EndEventTriggered)
                return;
            
            if (follower != null)
                follower.EndEventTriggered = true;

            // 敌人到达终点：对主城造成伤害
            var tdEnemy = enemy as TDEnemyActor;
            int leakDamage = tdEnemy?.GetLeakDamage() ?? 10;
            bool shouldAttackCity = tdEnemy != null && tdEnemy.Config != null && 
                                   tdEnemy.Config.CanAttackCity && tdEnemy.Config.CityAttackInterval > 0f;

            _context.EventBus.Emit(TDEventIds.EnemyReachedEnd,
                new EnemyReachedEndEvent(enemy.Id, leakDamage, shouldAttackCity));

            if (shouldAttackCity)
            {
                // 持续攻击主城：添加 CityAttackerComponent
                var attacker = enemy.Get<CityAttackerComponent>();
                if (attacker == null)
                    attacker = enemy.AddComponent<CityAttackerComponent>();

                // 获取主城引用
                var mainCitySystem = _context.GetSystem<MainCitySystem>();
                if (mainCitySystem != null && mainCitySystem.MainCity != null && mainCitySystem.MainCity.IsAlive)
                {
                    attacker.StartAttack(mainCitySystem.MainCity, tdEnemy.Config.CityAttackInterval, leakDamage);
                }
            }
            else
            {
                // 一次性伤害：直接死亡
                enemy.Die();
            }
        }

        public void Dispose()
        {
            _pendingReachedEnd.Clear();
            _context = null;
            _entityManager = null;
        }
    }
}
