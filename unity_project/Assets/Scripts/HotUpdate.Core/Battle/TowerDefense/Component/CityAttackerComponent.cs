using BattleFoundation;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 城市攻击者组件 — 敌人到达路径终点后挂载，驱动持续攻击主城的逻辑。
    /// 
    /// 职责：
    /// - 定时对主城造成伤害
    /// - 管理攻击间隔
    /// - 敌人被击杀时自动移除
    /// 
    /// 使用方案B：独立组件 + 独立System驱动
    /// 优点：职责清晰，可扩展（未来可加"自爆"、"治疗主城"等行为）
    /// </summary>
    public class CityAttackerComponent : EntityComponent
    {
        private MainCityActor _targetCity;
        private float _attackInterval;
        private int _attackDamage;
        private float _attackTimer;
        private bool _isAttacking;

        /// <summary>
        /// 是否正在攻击主城
        /// </summary>
        public bool IsAttacking => _isAttacking;

        /// <summary>
        /// 攻击目标（主城）
        /// </summary>
        public MainCityActor TargetCity => _targetCity;

        public override void Attach(BattleEntity owner)
        {
            base.Attach(owner);
        }

        /// <summary>
        /// 开始攻击主城
        /// </summary>
        /// <param name="city">主城引用</param>
        /// <param name="attackInterval">攻击间隔（秒）</param>
        /// <param name="attackDamage">每次攻击伤害</param>
        public void StartAttack(MainCityActor city, float attackInterval, int attackDamage)
        {
            if (city == null || !city.IsAlive)
            {
                BattleLog.Warning($"Cannot start attack: city is null or dead.");
                return;
            }

            _targetCity = city;
            _attackInterval = Mathf.Max(0.1f, attackInterval);
            _attackDamage = attackDamage;
            _attackTimer = 0f; // 立即发起第一次攻击
            _isAttacking = true;

            BattleLog.State($"Enemy {Owner.Id} started attacking city. Interval: {_attackInterval}s, Damage: {_attackDamage}");
        }

        /// <summary>
        /// 停止攻击（主城被摧毁或敌人被击杀时调用）
        /// </summary>
        public void StopAttack()
        {
            _isAttacking = false;
            _targetCity = null;
        }

        /// <summary>
        /// 驱动攻击逻辑（由 CityAttackerSystem 调用）
        /// </summary>
        public void UpdateAttack(float deltaTime)
        {
            if (!_isAttacking || _targetCity == null || !_targetCity.IsAlive)
            {
                // 主城已被摧毁，停止攻击
                StopAttack();
                return;
            }

            _attackTimer += deltaTime;

            if (_attackTimer >= _attackInterval)
            {
                _attackTimer -= _attackInterval;
                PerformAttack();
            }
        }

        /// <summary>
        /// 执行一次攻击
        /// </summary>
        private void PerformAttack()
        {
            if (_targetCity == null || !_targetCity.IsAlive)
                return;

            var health = _targetCity.Get<MainCityHealthComponent>();
            if (health != null)
            {
                float actualDamage = health.TakeDamage(_attackDamage);
                BattleLog.State($"Enemy {Owner.Id} attacked city for {actualDamage} damage. City HP: {health.HP}/{health.MaxHP}");
            }
        }

        public override void DeactivateForPool()
        {
            StopAttack();
            base.DeactivateForPool();
        }

        protected override void OnDispose()
        {
            StopAttack();
            base.OnDispose();
        }
    }
}
