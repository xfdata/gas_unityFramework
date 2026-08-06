using System;
using BattleFoundation;
using UnityEngine;

namespace BattleCommon
{
    public class CombatHealthComponent : CombatComponentBase, ICombatHealthComponent
    {
        private CombatAttributeComponent _attributes;
        // R2-S10：原 _lastDamageSource 死逻辑已移除（TakeDamage 删除后恒为 null）。
        // killer 信息改由 CombatAttributeComponent.LastDamageResult 携带，死亡时按 SourceEntityId 解析。
        private bool _hasDied;

        public float HP
        {
            get => _attributes?.HP ?? 0f;
            set
            {
                if (_attributes != null)
                    _attributes.HP = Mathf.Clamp(value, 0f, _attributes.MaxHP);
            }
        }

        public float MaxHP => _attributes?.MaxHP ?? 0f;
        public bool IsDead => HP <= 0f;
        public bool IsAlive => !IsDead;
        public float HPPercent => MaxHP > 0f ? HP / MaxHP : 0f;

        // R3-S10 修复: OnDeath 事件已移除。
        // 死亡事件由 CombatActorSystem.Despawn 通过 EventBus 发送 ActorDiedEvent，
        // 表现层通过 IBattlePresentationSink.OnActorDied 接收，仿真层通过 EventBus 订阅。
        public event Action<float> OnHealed;

        public override void Attach(BattleEntity owner)
        {
            base.Attach(owner);
            _attributes = Owner?.Get<CombatAttributeComponent>();
        }

        public override void Initialize()
        {
            base.Initialize();
            _hasDied = false;
            if (_attributes != null)
            {
                _attributes.OnAttributeChanged -= OnAttributeChanged;
                _attributes.OnAttributeChanged += OnAttributeChanged;
            }
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            float oldHP = HP;
            HP += amount;
            OnHealed?.Invoke(HP - oldHP);
        }

        public void SetFullHP()
        {
            HP = MaxHP;
            Owner.Get<CombatStateComponent>()?.ClearDead();
            _hasDied = false;
        }

        private void Die(CombatActor killer)
        {
            if (_hasDied) return;
            _hasDied = true;
            Owner.Get<CombatStateComponent>()?.MarkDead();
            Owner.Get<CombatAbilityComponent>()?.TryActivateDeathAbility(killer);
            // R3-S10 修复: OnDeath?.Invoke 已移除，死亡事件由 CombatActorSystem.Despawn 通过 EventBus 发送。
        }

        private void OnAttributeChanged(int attributeId, float oldValue, float newValue)
        {
            if (attributeId == CombatAttributeIds.HP && oldValue > 0f && newValue <= 0f)
            {
                // R2-S10：从 LastDamageResult 解析 killer（替代原 _lastDamageSource 死逻辑）
                var killer = ResolveKiller();
                Die(killer);
            }
            else if (attributeId == CombatAttributeIds.HP && oldValue <= 0f && newValue > 0f)
            {
                _hasDied = false;
                Owner.Get<CombatStateComponent>()?.ClearDead();
            }
        }

        /// <summary>
        /// 从 CombatAttributeComponent.LastDamageSourceEntityId 通过 EntityManager 解析 killer。
        /// 替代原 _lastDamageSource 死逻辑（TakeDamage 删除后恒为 null）。
        /// </summary>
        private CombatActor ResolveKiller()
        {
            var sourceEntityId = _attributes?.LastDamageSourceEntityId ?? 0;
            if (sourceEntityId == 0)
                return null;

            // 通过 Owner.Engine.Context.EntityManager 反查 source 实体
            var entityManager = Owner?.Engine?.Context?.EntityManager;
            if (entityManager == null)
                return null;

            // spec.SourceEntityId 是 long，EntityManager.GetById 接收 int，强制转换
            return entityManager.GetById((int)sourceEntityId) as CombatActor;
        }

        public override void DeactivateForPool()
        {
            if (_attributes != null)
                _attributes.OnAttributeChanged -= OnAttributeChanged;
            _hasDied = false;
            OnHealed = null;
            base.DeactivateForPool();
        }

        protected override void OnDispose()
        {
            if (_attributes != null)
                _attributes.OnAttributeChanged -= OnAttributeChanged;
            OnHealed = null;
            _attributes = null;
            base.OnDispose();
        }
    }
}
