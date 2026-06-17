using BattleFoundation;
using Framework;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 防御塔索敌组件 — 策略模式驱动目标选择。
    /// 
    /// 性能设计：
    /// - 使用TargetCheckInterval控制扫描频率（默认0.3s），非每帧全局扫描
    /// - 缓存当前目标，仅在目标无效或到期时重新扫描
    /// - 按阵营查询EntityManager，利用现有空间查询
    /// - 策略模式接口调用开销可忽略（非虚方法调用）
    /// 
    /// 事件：
    /// - 目标切换时发射 TowerTargetSwitch(6017) 事件
    /// </summary>
    public class TDTargetingComponent : EntityComponent
    {
        private float _targetCheckTimer;
        private float _targetCheckInterval = 0.3f;
        private TDEnemyActor _currentTarget;
        private ITargetingStrategy _strategy;
        private float _range;

        /// <summary>当前锁定的目标（缓存，无需每帧查询）</summary>
        public TDEnemyActor CurrentTarget => _currentTarget;

        /// <summary>当前策略（可运行时切换）</summary>
        public ITargetingStrategy Strategy => _strategy;

        /// <summary>攻击距离</summary>
        public float Range { get => _range; set => _range = Mathf.Max(0.1f, value); }

        /// <summary>
        /// 目标扫描间隔（秒），默认0.3s。值越小响应越快但性能开销越大。
        /// </summary>
        public float TargetCheckInterval
        {
            get => _targetCheckInterval;
            set => _targetCheckInterval = Mathf.Max(0.05f, value);
        }

        /// <summary>
        /// 初始化索敌组件。
        /// </summary>
        /// <param name="range">攻击距离</param>
        /// <param name="priority">目标优先级枚举（将自动映射到对应策略）</param>
        /// <param name="checkInterval">扫描间隔</param>
        public void Init(float range, ETDTargetPriority priority = ETDTargetPriority.MostProgressed, float checkInterval = 0.3f)
        {
            _range = Mathf.Max(0.1f, range);
            _targetCheckInterval = Mathf.Max(0.05f, checkInterval);
            _currentTarget = null;
            _targetCheckTimer = 0f;
            _strategy = CreateStrategy(priority);
        }

        /// <summary>
        /// 运行时切换目标选择策略。
        /// 切换后强制立即重新扫描。
        /// </summary>
        public void SetStrategy(ITargetingStrategy strategy)
        {
            if (strategy == null) return;
            _strategy = strategy;
            ForceRescan();
        }

        /// <summary>
        /// 根据枚举创建对应策略实例（策略工厂）。
        /// 支持未来新增枚举值自动映射。
        /// </summary>
        public static ITargetingStrategy CreateStrategy(ETDTargetPriority priority)
        {
            switch (priority)
            {
                case ETDTargetPriority.Nearest:
                    return new NearestStrategy();
                case ETDTargetPriority.FarthestProgress:
                    return new FarthestProgressStrategy();
                case ETDTargetPriority.LowestHP:
                    return new LowestHPStrategy();
                case ETDTargetPriority.PriorityBoss:
                    // PriorityBoss 保留兼容：优先Boss（回退到MostProgressed）
                    // 未来可扩展为独立 BossPriorityStrategy
                    return new FarthestProgressStrategy();
                case ETDTargetPriority.MostProgressed:
                default:
                    return new FarthestProgressStrategy();
            }
        }

        public override void Update(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            // 定期更新目标
            _targetCheckTimer += deltaTime;
            if (_currentTarget == null || !_currentTarget.IsAlive || _targetCheckTimer >= _targetCheckInterval)
            {
                _targetCheckTimer = 0f;
                RescanAndNotify();
            }
        }

        private void RescanAndNotify()
        {
            var prevTarget = _currentTarget;
            _currentTarget = FindBestTarget();

            // 仅在目标真正变化时发射事件（前目标 != 新目标）
            int prevId = prevTarget != null && prevTarget.IsAlive ? prevTarget.Id : -1;
            int newId = _currentTarget != null ? _currentTarget.Id : -1;

            if (prevId != newId)
            {
                Owner?.Engine?.Context?.EventBus?.Emit(
                    TDEventIds.TowerTargetSwitch,
                    new TowerTargetSwitchEvent(
                        Owner?.Id ?? 0,
                        prevId,
                        newId,
                        _strategy?.StrategyName ?? "Unknown"));
            }
        }

        /// <summary>
        /// 强制立即重新扫描目标（例如升级后范围/策略变化）。
        /// </summary>
        public void ForceRescan()
        {
            _targetCheckTimer = _targetCheckInterval;
            _currentTarget = null;
        }

        /// <summary>
        /// 检查当前目标是否有效（存活且在范围内）。
        /// </summary>
        public bool IsCurrentTargetValid()
        {
            if (_currentTarget == null || !_currentTarget.IsAlive)
                return false;
            if (!IsInRange(_currentTarget))
                return false;
            return true;
        }

        /// <summary>
        /// 使用当前策略寻找最佳目标。
        /// </summary>
        private TDEnemyActor FindBestTarget()
        {
            if (Owner == null || _strategy == null) return null;

            var entityManager = Owner.Engine?.Context?.EntityManager;
            if (entityManager == null) return null;

            var enemies = entityManager.GetByCamp(EEntityCamp.Enemy);
            if (enemies.Count == 0) return null;

            float rangeSqr = _range * _range;
            return _strategy.FindBestTarget(enemies, Owner, rangeSqr);
        }

        private bool IsInRange(TDEnemyActor enemy)
        {
            if (enemy == null || Owner == null) return false;
            float rangeSqr = _range * _range;
            return (enemy.Position - Owner.Position).sqrMagnitude <= rangeSqr;
        }

        public override void DeactivateForPool()
        {
            _currentTarget = null;
            _targetCheckTimer = 0f;
            _strategy = null;
            base.DeactivateForPool();
        }
    }
}
