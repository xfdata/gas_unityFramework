using BattleCommon;
using BattleFoundation;

namespace TowerDefense
{
    /// <summary>
    /// 主城血量组件 — 管理主城的生命值、受伤和销毁逻辑。
    /// 
    /// 复用CombatAttributeComponent的HP/Defense属性系统。
    /// 到达终点的敌人会对此造成伤害。
    /// </summary>
    public class MainCityHealthComponent : EntityComponent
    {
        private CombatAttributeComponent _attributes;

        public float HP => _attributes?.HP ?? 0f;
        public float MaxHP => _attributes?.MaxHP ?? 0f;
        public float Defense => _attributes?.Defense ?? 0f;
        public bool IsDestroyed => HP <= 0f;
        public float HPPercent => MaxHP > 0f ? HP / MaxHP : 0f;

        public override void Attach(BattleEntity owner)
        {
            base.Attach(owner);
            _attributes = Owner?.Get<CombatAttributeComponent>();
        }

        /// <summary>
        /// 初始化主城血量
        /// </summary>
        public void Init(float maxHp, float defense)
        {
            if (_attributes != null)
            {
                _attributes.MaxHP = maxHp;
                _attributes.HP = maxHp;
                _attributes.Defense = defense;
            }
        }

        /// <summary>
        /// 承受伤害（来自到达终点的敌人）
        /// </summary>
        public float TakeDamage(int rawDamage)
        {
            if (IsDestroyed) return 0f;

            // 护甲减伤公式：实际伤害 = max(1, 原始伤害 - 防御)
            float finalDamage = UnityEngine.Mathf.Max(1f, rawDamage - Defense);

            float hpBefore = HP;
            _attributes.HP = UnityEngine.Mathf.Max(0f, hpBefore - finalDamage);
            float actualDamage = hpBefore - HP;

            // 发射事件
            var context = Owner?.Engine?.Context;
            context?.EventBus.Emit(TDEventIds.MainCityDamaged,
                new MainCityDamagedEvent(Owner.Id, actualDamage, HP, MaxHP));

            if (IsDestroyed)
            {
                context?.EventBus.Emit(TDEventIds.MainCityDestroyed,
                    new MainCityDestroyedEvent(Owner.Id));
            }

            return actualDamage;
        }

        public override void DeactivateForPool()
        {
            _attributes = null;
            base.DeactivateForPool();
        }
    }
}
