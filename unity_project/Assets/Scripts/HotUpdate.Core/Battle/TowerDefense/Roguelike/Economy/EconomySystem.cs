using BattleFoundation;
using Framework;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 局内经济管理系统 (IBattleSystem)。
    /// 
    /// 职责：
    /// - 监听 EnemyKilled 事件，计算击杀金币奖励并发放
    /// - 统计总收入/总支出
    /// - 所有金币操作必须通过 TDBattleContext.AddGold/SpendGold（通过 PlayerGoldChanged 事件）
    /// 
    /// 设计：
    /// - 不直接修改数值，全部通过 BattleEvent 触发
    /// - 敌人击杀金币来自 TDEnemyConfig.GetEffectiveKillGold()
    /// - 支持未来扩展：商店系统、利息、连杀奖励等
    /// </summary>
    public class EconomySystem : IBattleSystem
    {
        private IBattleContext _context;
        private TDBattleContext _tdContext;
        private EntityManager _entityManager;

        /// <summary>战斗总收入</summary>
        public int TotalEarned { get; private set; }

        /// <summary>战斗总支出</summary>
        public int TotalSpent { get; private set; }

        /// <summary>当前金币（便捷访问）</summary>
        public int CurrentGold => _tdContext?.PlayerGold ?? 0;

        public void Initialize(IBattleContext context)
        {
            _context = context;
            _tdContext = context as TDBattleContext;
            _entityManager = context.EntityManager;

            // 订阅事件
            var eb = context.EventBus;
            eb.On<EnemyKilledEvent>(TDEventIds.EnemyKilled, OnEnemyKilled);
            eb.On<PlayerGoldChangedEvent>(TDEventIds.PlayerGoldChanged, OnGoldChanged);

            TotalEarned = 0;
            TotalSpent = 0;

            Debug.Log("[EconomySystem] Initialized.");
        }

        public void Start() { }

        public void Update(float deltaTime)
        {
            // 经济系统无需每帧操作，事件驱动即可
        }

        public void LateUpdate(float deltaTime) { }

        /// <summary>
        /// 敌人被击杀回调：查找敌人配置，发放击杀金币。
        /// </summary>
        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            if (_tdContext == null || _entityManager == null)
                return;

            // 查找被击杀敌人的配置以获取金币奖励
            var enemy = _entityManager.GetById(evt.EnemyId) as TDEnemyActor;
            if (enemy?.Config == null)
                return;

            int killGold = enemy.Config.GetEffectiveKillGold();
            if (killGold <= 0)
                return;

            _tdContext.AddGold(killGold);
            TotalEarned += killGold;

            // 注意：PlayerGoldChanged 事件由 _tdContext.AddGold 内部触发
        }

        /// <summary>
        /// 金币变化回调：追踪支出。
        /// delta < 0 表示消费，delta > 0 表示收入。
        /// </summary>
        private void OnGoldChanged(PlayerGoldChangedEvent evt)
        {
            if (evt.Delta < 0)
                TotalSpent += -evt.Delta;
            // 正数收入由 OnEnemyKilled 追踪（避免重复统计其他来源）
        }

        /// <summary>
        /// 重置统计（新战斗开始时）
        /// </summary>
        public void ResetStats()
        {
            TotalEarned = 0;
            TotalSpent = 0;
        }

        public void Dispose()
        {
            if (_context != null)
            {
                var eb = _context.EventBus;
                eb.Off<EnemyKilledEvent>(TDEventIds.EnemyKilled, OnEnemyKilled);
                eb.Off<PlayerGoldChangedEvent>(TDEventIds.PlayerGoldChanged, OnGoldChanged);
            }

            _entityManager = null;
            _tdContext = null;
            _context = null;
        }
    }
}
