using BattleFoundation;
using Framework;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 主城系统 — 管理主城的生成、生命周期。
    /// 监听EnemyReachedEndEvent并向主城施加伤害。
    /// </summary>
    public class MainCitySystem : IBattleSystem
    {
        private IBattleContext _context;
        private EntityManager _entityManager;

        /// <summary>
        /// 主城Actor快捷引用
        /// </summary>
        public MainCityActor MainCity { get; private set; }

        /// <summary>
        /// 主城是否还存活
        /// </summary>
        public bool IsCityAlive => MainCity != null && MainCity.IsAlive;

        public void Initialize(IBattleContext context)
        {
            _context = context;
            _entityManager = context.EntityManager;

            // 监听敌人到达终点事件
            _context.EventBus.On<EnemyReachedEndEvent>(TDEventIds.EnemyReachedEnd, OnEnemyReachedEnd);
        }

        public void Start() { }

        /// <summary>
        /// 生成主城到指定位置
        /// </summary>
        public void SpawnMainCity(MainCityConfig config, Vector3 position)
        {
            if (config == null) return;
            if (MainCity != null) return;  // 已存在

            MainCity = new MainCityActor();
            MainCity.InitCity(config, position);
            _entityManager.AddEntity(MainCity);

            Debug.Log($"[MainCitySystem] Main city '{config.CityName}' spawned. HP: {config.MaxHp}");
        }

        public void Update(float deltaTime) { }

        public void LateUpdate(float deltaTime) { }

        /// <summary>
        /// 敌人到达终点 → 主城受伤害
        /// </summary>
        private void OnEnemyReachedEnd(EnemyReachedEndEvent evt)
        {
            if (MainCity == null || !MainCity.IsAlive) return;

            float actualDamage = MainCity.Health.TakeDamage(evt.DamageToCity);

            // 被摧毁时不重复处理（TakeDamage内部已发射销毁事件）
            if (MainCity.Health.IsDestroyed)
            {
                Debug.Log($"[MainCitySystem] Main city destroyed! Last enemy: {evt.EnemyId}");
            }
        }

        public void Dispose()
        {
            if (_context != null)
            {
                _context.EventBus.Off<EnemyReachedEndEvent>(TDEventIds.EnemyReachedEnd, OnEnemyReachedEnd);
            }
            MainCity = null;
            _entityManager = null;
            _context = null;
        }
    }
}
