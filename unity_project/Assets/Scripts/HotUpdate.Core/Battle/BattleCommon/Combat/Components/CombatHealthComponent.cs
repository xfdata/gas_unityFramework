using System;
using BattleFoundation;
using UnityEngine;

namespace BattleCommon
{
    public class CombatHealthComponent : CombatComponentBase
    {
        private CombatAttributeComponent _attributes;
        private CombatActor _lastDamageSource;
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

        public event Action<CombatActor> OnDeath;
        public event Action<float, CombatActor> OnDamaged;
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
            _lastDamageSource = null;
            if (_attributes != null)
            {
                _attributes.OnAttributeChanged -= OnAttributeChanged;
                _attributes.OnAttributeChanged += OnAttributeChanged;
            }
        }

        public void TakeDamage(float rawDamage, CombatActor source)
        {
            if (IsDead) return;
            if (Owner?.Get<CombatStateComponent>() is {} state && !state.CanTakeDamage) return;
            if (source?.Get<CombatStateComponent>() is {} srcState && !srcState.CanAct) return;

            float finalDamage = Mathf.Max(1f, rawDamage - (_attributes?.Defense ?? 0f));
            _lastDamageSource = source;
            HP -= finalDamage;
            OnDamaged?.Invoke(finalDamage, source);
            if (HP <= 0f) Die(source);
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
            OnDeath?.Invoke(killer);
        }

        private void OnAttributeChanged(int attributeId, float oldValue, float newValue)
        {
            if (attributeId == CombatAttributeIds.HP && oldValue > 0f && newValue <= 0f)
            {
                Die(_lastDamageSource);
            }
            else if (attributeId == CombatAttributeIds.HP && oldValue <= 0f && newValue > 0f)
            {
                _hasDied = false;
                Owner.Get<CombatStateComponent>()?.ClearDead();
            }
        }

        public override void DeactivateForPool()
        {
            if (_attributes != null)
                _attributes.OnAttributeChanged -= OnAttributeChanged;
            _lastDamageSource = null;
            _hasDied = false;
            OnDeath = null;
            OnDamaged = null;
            OnHealed = null;
            base.DeactivateForPool();
        }

        protected override void OnDispose()
        {
            if (_attributes != null)
                _attributes.OnAttributeChanged -= OnAttributeChanged;
            OnDeath = null;
            OnDamaged = null;
            OnHealed = null;
            _attributes = null;
            base.OnDispose();
        }
    }
}
