using BattleCommon;
using BattleFoundation;
using GAS;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 防御塔升级组件 — 从TowerActor中解耦升级逻辑。
    /// 
    /// 职责：
    /// - 管理等级链（Lv1 → Lv2 → Lv3，通过 TowerConfig.UpgradeConfig 链式引用）
    /// - 升级时替换配置、更新属性、重新授权 GAS 技能、更新索敌策略
    /// - 替换视觉 Prefab
    /// - 发射 TowerUpgraded 事件
    /// 
    /// 性能：升级是一次性操作，不在 Update 循环中有开销。
    /// </summary>
    public class TowerUpgradeComponent : EntityComponent
    {
        private TowerConfig _currentConfig;
        private ProjectileRuntime _projectileRuntime;
        private TowerActor _towerOwner;

        /// <summary>当前配置</summary>
        public TowerConfig CurrentConfig => _currentConfig;

        /// <summary>是否可继续升级</summary>
        public bool CanUpgrade => _currentConfig?.CanUpgrade ?? false;

        /// <summary>当前等级</summary>
        public int CurrentLevel => _currentConfig?.TowerLevel ?? 1;

        /// <summary>升级到下一级需要的金币</summary>
        public int UpgradeCost => _currentConfig?.UpgradeCost ?? 0;

        /// <summary>
        /// 初始化升级组件（在 InitTower 中调用）。
        /// </summary>
        public void Init(TowerConfig config, ProjectileRuntime projectileRuntime = null)
        {
            _currentConfig = config;
            _projectileRuntime = projectileRuntime;
        }

        public override void Attach(BattleEntity owner)
        {
            base.Attach(owner);
            _towerOwner = owner as TowerActor;
        }

        /// <summary>
        /// 尝试升级防御塔。
        /// 升级流程：检查可升级 → 消费金币 → 应用新配置 → 发射事件。
        /// </summary>
        /// <param name="ctx">TD战斗上下文（用于金币操作）</param>
        /// <returns>是否升级成功</returns>
        public bool TryUpgrade(TDBattleContext ctx)
        {
            if (!CanUpgrade || ctx == null || _towerOwner == null)
                return false;

            int cost = _currentConfig.UpgradeCost;
            if (!ctx.SpendGold(cost))
                return false;

            var newConfig = _currentConfig.UpgradeConfig;
            ApplyUpgrade(newConfig);

            // 发射升级事件
            Owner?.Engine?.Context?.EventBus?.Emit(TDEventIds.TowerUpgraded, _towerOwner);
            Debug.Log($"[TowerUpgradeComponent] Tower '{newConfig.TowerName}' upgraded to Lv.{newConfig.TowerLevel}, cost: {cost}");

            return true;
        }

        /// <summary>
        /// 应用升级配置到 TowerActor 的所有组件。
        /// </summary>
        private void ApplyUpgrade(TowerConfig newConfig)
        {
            if (newConfig == null || _towerOwner == null) return;

            var oldGO = _towerOwner.ViewTransform?.gameObject;
            _currentConfig = newConfig;

            // 1. 更新属性组件
            var attributes = _towerOwner.Get<CombatAttributeComponent>();
            if (attributes != null)
            {
                attributes.Attack = newConfig.AttackDamage;
                attributes.AttackRange = newConfig.AttackRange;
                attributes.AttackInterval = newConfig.AttackInterval;
            }

            // 2. 更新 GAS 技能（重新 Initialize 并 Grant 新技能）
            var ability = _towerOwner.Get<CombatAbilityComponent>();
            if (ability != null && newConfig.AttackAbility != null)
            {
                ability.Initialize();
                ability.GrantAbility(newConfig.AttackAbility);
            }

            // 3. 更新索敌组件（范围 + 策略）
            var targeting = _towerOwner.Get<TDTargetingComponent>();
            if (targeting != null)
            {
                targeting.Range = newConfig.AttackRange;
                targeting.SetStrategy(TDTargetingComponent.CreateStrategy(newConfig.TargetPriority));
                targeting.ForceRescan();
            }

            // 4. 更新攻击组件配置
            var attack = _towerOwner.Get<TowerAttackComponent>();
            if (attack != null)
            {
                attack.Init(newConfig);
            }

            // 5. 替换视觉
            if (newConfig.Prefab != null)
            {
                var newGO = Object.Instantiate(newConfig.Prefab, _towerOwner.Position, Quaternion.identity);
                _towerOwner.ViewTransform = newGO.transform;
                attack?.SetViewTransform(newGO.transform);
            }

            // 6. 更新投射物运行时引用（如有新的）
            if (_projectileRuntime != null)
            {
                // 由外部传入（TowerPlacementSystem 持有）
            }

            if (oldGO != null)
                Object.Destroy(oldGO);
        }

        /// <summary>
        /// 设置投射物运行时（由 TowerPlacementSystem 注入）。
        /// </summary>
        public void SetProjectileRuntime(ProjectileRuntime runtime)
        {
            _projectileRuntime = runtime;
        }

        public override void Update(float deltaTime)
        {
            // 升级是一次性操作，不在 Update 中处理
        }

        public override void DeactivateForPool()
        {
            _currentConfig = null;
            _projectileRuntime = null;
            _towerOwner = null;
            base.DeactivateForPool();
        }
    }
}
