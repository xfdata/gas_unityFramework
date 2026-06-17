using BattleCommon;
using BattleFoundation;
using Framework;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 防御塔自动攻击组件 — 冷却驱动，通过GAS/投射物系统攻击。
    /// 
    /// 攻击链路：TDTargetingComponent索敌 → 冷却判定 
    /// → CombatAbilityComponent.TryActivateAttackAbility() → RemoteAttackAbilityDefinition 
    /// → AbilityTaskSpawnProjectile → ProjectileRuntime → CombatDamageExecution
    /// 
    /// AOE/穿透/减速等效果由RangedProjectileDefinition和GameplayEffectDefinition配置，
    /// 本组件不再内嵌伤害计算逻辑。
    /// 
    /// 性能设计：
    /// - 冷却计时器驱动，非每帧执行攻击逻辑
    /// - 索敌委托给TDTargetingComponent（定期扫描，非每帧）
    /// </summary>
    public class TowerAttackComponent : EntityComponent
    {
        private TowerConfig _config;
        private Transform _viewTransform;
        private float _cooldownTimer;
        private TowerActor _towerOwner;
        private CombatAbilityComponent _ability;
        private TDTargetingComponent _targeting;

        public TowerConfig Config => _config;
        public float AttackRange => _config?.AttackRange ?? 5f;
        public float AttackInterval => _config?.AttackInterval ?? 1f;
        public float AttackDamage => _config?.AttackDamage ?? 20f;
        public bool IsCoolingDown => _cooldownTimer > 0f;

        public TDEnemyActor CurrentTarget
        {
            get
            {
                if (_targeting == null) return null;
                var target = _targeting.CurrentTarget;
                return target != null && target.IsAlive ? target : null;
            }
        }

        /// <summary>
        /// 设置炮塔旋转Transform（用于视觉朝向敌人）
        /// </summary>
        public void SetViewTransform(Transform viewTransform)
        {
            _viewTransform = viewTransform;
        }

        public void Init(TowerConfig config)
        {
            _config = config;
            _cooldownTimer = 0f;
            // 延迟解析引用（Init可能在Attach之前调用）
            ResolveReferences();
        }

        public override void Attach(BattleEntity owner)
        {
            base.Attach(owner);
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            if (Owner == null) return;
            _towerOwner = Owner as TowerActor;
            _ability = Owner.Get<CombatAbilityComponent>();
            _targeting = Owner.Get<TDTargetingComponent>();
        }

        public override void Update(float deltaTime)
        {
            if (_config == null || deltaTime <= 0f) return;
            if (_targeting == null) return;

            // 冷却计时
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= deltaTime;
                UpdateViewRotation();
                return;
            }

            // 注意：索敌组件的 Update 由 TowerPlacementSystem 统一驱动，此处不再调用 _targeting.Update()

            // 获取当前有效目标
            if (!_targeting.IsCurrentTargetValid())
                return;

            var target = _targeting.CurrentTarget;
            if (target == null || !target.IsAlive)
                return;

            // 执行攻击（通过GAS投射物链路）
            PerformAttack(target);
            _cooldownTimer = AttackInterval;
        }

        private void PerformAttack(TDEnemyActor target)
        {
            using (new AutoProfiler("TowerDefense.TowerAttackComponent.PerformAttack"))
            {
                if (_ability == null || target == null) return;

                // 发射 TowerAttack 事件
                Owner.Engine?.Context?.EventBus?.Emit(TDEventIds.TowerAttack,
                    new TowerAttackEvent(Owner.Id, target.Id, AttackDamage,
                        _config?.TowerType ?? ETDTowerType.ArrowTower));

                // 通过GAS轻量模式激活远程攻击技能 → 生成投射物
                bool success = _ability.TryActivateAttackAbility(target);
                if (success)
                {
                    // 发射 TowerSkillCast 事件（技能成功施放）
                    Owner.Engine?.Context?.EventBus?.Emit(TDEventIds.TowerSkillCast,
                        new TowerSkillCastEvent(
                            Owner.Id,
                            target.Id,
                            _config?.AttackAbility?.AbilityId ?? 0,
                            _config?.TowerType ?? ETDTowerType.ArrowTower));
                }
                else
                {
                    Debug.LogWarning($"[TowerAttackComponent] Tower '{Owner.Id}' failed to activate attack ability on target '{target.Id}'");
                }
            }
        }

        private void UpdateViewRotation()
        {
            if (_viewTransform == null) return;

            var target = _targeting?.CurrentTarget;
            if (target == null || !target.IsAlive)
                return;

            Vector3 dir = target.Position - _viewTransform.position;
            dir.y = 0f; // 只在水平面旋转
            if (dir.sqrMagnitude > 0.001f)
            {
                _viewTransform.rotation = Quaternion.Slerp(
                    _viewTransform.rotation,
                    Quaternion.LookRotation(dir),
                    10f * Time.deltaTime);
            }
        }

        public override void DeactivateForPool()
        {
            _config = null;
            _cooldownTimer = 0f;
            _viewTransform = null;
            _towerOwner = null;
            _ability = null;
            _targeting = null;
            base.DeactivateForPool();
        }
    }
}
