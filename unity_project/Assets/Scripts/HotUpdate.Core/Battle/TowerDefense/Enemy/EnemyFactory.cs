using System.Collections.Generic;
using BattleFoundation;
using Framework;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 敌人工厂 — 封装EntityManager的对象池逻辑。
    /// 
    /// 性能设计：
    /// - 通过EntityManager管理实体生命周期（Allocate/Recycle）
    /// - 支持预创建池（PreWarm），避免运行时Instantiate
    /// - Recycle时DeactivateForPool清理瞬态引用
    /// </summary>
    public class EnemyFactory
    {
        private IBattleContext _context;
        private EntityManager _entityManager;

        /// <summary>
        /// 按TDEnemyConfig分组的对象池栈
        /// 使用Stack而非Queue以利用缓存局部性
        /// </summary>
        private readonly Dictionary<TDEnemyConfig, Stack<TDEnemyActor>> _pools = new Dictionary<TDEnemyConfig, Stack<TDEnemyActor>>();

        /// <summary>
        /// GameObject池（按Prefab分组）
        /// </summary>
        private readonly Dictionary<GameObject, Stack<GameObject>> _goPools = new Dictionary<GameObject, Stack<GameObject>>();

        private Transform _poolRoot;

        public EnemyFactory(IBattleContext context) : this(context, null) { }

        public EnemyFactory(IBattleContext context, Transform poolRoot)
        {
            _context = context;
            _entityManager = context.EntityManager;
            _poolRoot = poolRoot;
        }

        /// <summary>
        /// 预创建敌人实例（在加载阶段调用）
        /// </summary>
        public void PreWarm(TDEnemyConfig config, int count)
        {
            if (config == null || count <= 0)
                return;

            for (int i = 0; i < count; i++)
            {
                var enemy = CreateNewEnemy(config);
                RecycleToPool(config, enemy);
            }
        }

        /// <summary>
        /// 从对象池分配一个敌人实体
        /// </summary>
        public TDEnemyActor Allocate(TDEnemyConfig config, WaypointPath path, Vector3 spawnPosition)
        {
            if (config == null) return null;

            TDEnemyActor enemy;
            if (_pools.TryGetValue(config, out var pool) && pool.Count > 0)
            {
                // 从池中复用
                enemy = pool.Pop();
                enemy.SetId(_entityManager.GenerateId());
                enemy.SetCamp(EEntityCamp.Enemy);
                enemy.ActivateForPool(enemy.Id, EEntityCamp.Enemy,
                    config.IsBoss ? EEntityType.Boss : EEntityType.Monster);

                // 从GameObject池获取或重新Instantiate
                if (config.Prefab != null)
                {
                    if (_goPools.TryGetValue(config.Prefab, out var goPool) && goPool.Count > 0)
                    {
                        var go = goPool.Pop();
                        go.SetActive(true);
                        go.transform.SetParent(null);
                        enemy.GameObject = go;
                        enemy.Transform = go.transform;
                        enemy.Animator = go.GetComponentInChildren<Animator>();
                    }
                }

                enemy.InitEnemy(config, path, spawnPosition);
                _entityManager.AddEntityFromPool(enemy);
            }
            else
            {
                // 新建
                enemy = CreateNewEnemy(config);
                enemy.SetId(_entityManager.GenerateId());
                _entityManager.AddEntity(enemy);
                enemy.InitEnemy(config, path, spawnPosition);
            }

            // 发射生成事件
            if (config.IsBoss)
                _context.EventBus.Emit(TDEventIds.BossSpawned, enemy);
            _context.EventBus.Emit(TDEventIds.EnemySpawned, enemy);

            return enemy;
        }

        /// <summary>
        /// 回收敌人实体到对象池
        /// </summary>
        public void Recycle(TDEnemyActor enemy)
        {
            if (enemy == null) return;

            var config = enemy.Config;
            if (config == null) return;

            // 回收GameObject
            if (enemy.GameObject != null)
            {
                var go = enemy.GameObject;
                go.SetActive(false);
                go.transform.SetParent(GetPoolRoot());
                go.transform.localPosition = Vector3.zero;

                if (config.Prefab != null)
                {
                    if (!_goPools.TryGetValue(config.Prefab, out var goPool))
                    {
                        goPool = new Stack<GameObject>();
                        _goPools[config.Prefab] = goPool;
                    }
                    goPool.Push(go);
                }
            }

            enemy.DeactivateForPool();
            _entityManager.RemoveEntityFromPool(enemy);

            // 放入配置对应的池
            RecycleToPool(config, enemy);
        }

        private void RecycleToPool(TDEnemyConfig config, TDEnemyActor enemy)
        {
            if (config == null || enemy == null)
                return;

            if (!_pools.TryGetValue(config, out var pool))
            {
                pool = new Stack<TDEnemyActor>();
                _pools[config] = pool;
            }
            pool.Push(enemy);
        }

        /// <summary>
        /// 批量回收所有活着的敌人
        /// </summary>
        public void RecycleAll()
        {
            var enemies = _entityManager.GetByCamp(EEntityCamp.Enemy);
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                if (enemies[i] is TDEnemyActor tdEnemy)
                    Recycle(tdEnemy);
            }
        }

        private TDEnemyActor CreateNewEnemy(TDEnemyConfig config)
        {
            var enemy = new TDEnemyActor();
            return enemy;
        }

        private Transform GetPoolRoot()
        {
            if (_poolRoot == null)
            {
                var go = new GameObject("[TD] EnemyPool");
                _poolRoot = go.transform;
            }
            return _poolRoot;
        }

        public void ClearPools()
        {
            foreach (var pool in _pools.Values)
            {
                while (pool.Count > 0)
                    pool.Pop().Dispose();
            }
            _pools.Clear();

            foreach (var goPool in _goPools.Values)
            {
                while (goPool.Count > 0)
                {
                    var go = goPool.Pop();
                    if (go != null) Object.Destroy(go);
                }
            }
            _goPools.Clear();

            if (_poolRoot != null)
                Object.Destroy(_poolRoot.gameObject);
        }
    }
}
