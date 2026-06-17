using BattleCommon;
using BattleFoundation;
using GAS;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 防御塔Actor — 静态BattleEntity，通过GAS/投射物系统攻击。
    /// 
    /// 攻击链路：TDTargetingComponent索敌 → TowerAttackComponent冷却判定 
    /// → CombatAbilityComponent.TryActivateAttackAbility() → RemoteAttackAbilityDefinition 
    /// → AbilityTaskSpawnProjectile → ProjectileRuntime → CombatDamageExecution
    /// 
    /// 实现IRangedAttackSourceProvider以对接现有投射物系统。
    /// </summary>
    public class TowerActor : BattleEntity, IRangedAttackSourceProvider
    {
        private TowerConfig _config;
        private ProjectileRuntime _projectileRuntime;
        private CombatAbilityComponent _ability;
        private CombatAttributeComponent _attributes;
        private TowerAttackComponent _attack;
        private TDTargetingComponent _targeting;
        private TowerUpgradeComponent _upgrade;

        public TowerConfig Config => _config;
        public TowerAttackComponent Attack => _attack ??= Get<TowerAttackComponent>();
        public TDTargetingComponent Targeting => _targeting ??= Get<TDTargetingComponent>();
        public TowerUpgradeComponent Upgrade => _upgrade ??= Get<TowerUpgradeComponent>();
        public CombatAttributeComponent Attributes => _attributes ??= Get<CombatAttributeComponent>();

        /// <summary>
        /// 炮塔旋转节点（可选，用于视觉追踪敌人）
        /// </summary>
        public Transform ViewTransform { get; set; }

        public ETDTowerType TowerType => _config?.TowerType ?? ETDTowerType.None;
        public int TowerLevel => _config?.TowerLevel ?? 1;

        // ===== IRangedAttackSourceProvider =====
        public bool HasRangedWeapon => _config?.AttackAbility != null || _config?.ProjectileDefinition != null;
        public Vector3 FirePosition => ViewTransform != null
            ? ViewTransform.position + Vector3.up * 0.5f
            : Position + Vector3.up * 0.5f;
        public RangedProjectileDefinition ProjectileDefinition => _config?.ProjectileDefinition;
        public ProjectileRuntime ProjectileRuntime => _projectileRuntime;

        /// <summary>
        /// 初始化防御塔（新建或从池中复用）
        /// </summary>
        public void InitTower(TowerConfig config, Vector3 position, ProjectileRuntime projectileRuntime = null)
        {
            _config = config;
            _projectileRuntime = projectileRuntime;
            SetCamp(EEntityCamp.Ally);
            SetEntityType(EEntityType.Structure);

            // 属性组件
            _attributes = Get<CombatAttributeComponent>();
            if (_attributes == null)
                _attributes = AddComponent<CombatAttributeComponent>();
            _attributes.Attack = config.AttackDamage;
            _attributes.AttackRange = config.AttackRange;
            _attributes.AttackInterval = config.AttackInterval;

            // GAS能力组件（轻量模式，通过投射物系统攻击）
            _ability = Get<CombatAbilityComponent>();
            if (_ability == null)
                _ability = AddComponent<CombatAbilityComponent>();
            _ability.RuntimeMode = CombatAbilityRuntimeMode.Lightweight;
            _ability.Initialize();
            if (config.AttackAbility != null)
                _ability.GrantAbility(config.AttackAbility);

            // 索敌组件
            _targeting = Get<TDTargetingComponent>();
            if (_targeting == null)
                _targeting = AddComponent<TDTargetingComponent>();
            _targeting.Init(config.AttackRange, config.TargetPriority);

            // 攻击组件（冷却判定与攻击触发）
            _attack = Get<TowerAttackComponent>();
            if (_attack == null)
                _attack = AddComponent<TowerAttackComponent>();
            _attack.Init(config);
            _attack.SetViewTransform(ViewTransform);

            // 升级组件（等级链管理）
            _upgrade = Get<TowerUpgradeComponent>();
            if (_upgrade == null)
                _upgrade = AddComponent<TowerUpgradeComponent>();
            _upgrade.Init(config, projectileRuntime);

            Position = position;
            IsAlive = true;

            // 实例化GameObject
            if (config.Prefab != null)
            {
                var go = Object.Instantiate(config.Prefab, position, Quaternion.identity);
                ViewTransform = go.transform;
                _attack.SetViewTransform(go.transform);
            }

            base.Initialize();
            base.Start();
        }

        // Upgrade 逻辑已迁移到 TowerUpgradeComponent，保留此方法作为兼容桥梁
        [System.Obsolete("Use TowerUpgradeComponent.TryUpgrade() instead.")]
        public void UpgradeTo(TowerConfig newConfig, ProjectileRuntime projectileRuntime = null)
        {
            // 代理到 TowerUpgradeComponent
            var ctx = Engine?.Context as TDBattleContext;
            Upgrade?.TryUpgrade(ctx);
        }

        public override void DeactivateForPool()
        {
            _config = null;
            _projectileRuntime = null;
            _ability = null;
            _attributes = null;
            _attack = null;
            _targeting = null;
            _upgrade = null;
            ViewTransform = null;
            base.DeactivateForPool();
        }
    }
}
