using System;
using GAS;

namespace BattleCommon
{
    public class CombatAttributeComponent : CombatComponentBase, IGameplayAttributeOwner, IGameplayAttributeSetProvider
    {
        private readonly AttributeSet _attributeSet = new AttributeSet();

        public AttributeSet AttributeSet => _attributeSet;

        public event Action<int, float, float> OnAttributeChanged
        {
            add => _attributeSet.OnAttributeChanged += value;
            remove => _attributeSet.OnAttributeChanged -= value;
        }

        public float HP { get => Get(CombatAttributeIds.HP); set => Set(CombatAttributeIds.HP, value); }
        public float MaxHP { get => Get(CombatAttributeIds.MaxHP); set => Set(CombatAttributeIds.MaxHP, value); }
        public float Attack { get => Get(CombatAttributeIds.Attack); set => Set(CombatAttributeIds.Attack, value); }
        public float Defense { get => Get(CombatAttributeIds.Defense); set => Set(CombatAttributeIds.Defense, value); }
        public float MoveSpeed { get => Get(CombatAttributeIds.MoveSpeed); set => Set(CombatAttributeIds.MoveSpeed, value); }
        public float AttackRange { get => Get(CombatAttributeIds.AttackRange); set => Set(CombatAttributeIds.AttackRange, value); }
        public float AttackInterval { get => Get(CombatAttributeIds.AttackInterval); set => Set(CombatAttributeIds.AttackInterval, value); }
        public float CritRate { get => Get(CombatAttributeIds.CritRate); set => Set(CombatAttributeIds.CritRate, value); }
        public float CritDamage { get => Get(CombatAttributeIds.CritDamage); set => Set(CombatAttributeIds.CritDamage, value); }
        public float CritDamageMul { get => CritDamage; set => CritDamage = value; }
        public float DamageReduce { get => Get(CombatAttributeIds.DamageReduce); set => Set(CombatAttributeIds.DamageReduce, value); }

        public float Get(int attributeId) => _attributeSet.GetAttribute(attributeId);
        public float GetAttribute(int attributeId) => _attributeSet.GetAttribute(attributeId);
        public void Set(int attributeId, float value) => _attributeSet.SetBaseValue(attributeId, value);
        public void SetBaseValue(int attributeId, float value) => _attributeSet.SetBaseValue(attributeId, value);
        public void AddBaseValue(int attributeId, float delta) => _attributeSet.AddAttributeBaseValue(attributeId, delta);
        public void AddAttributeBaseValue(int attributeId, float delta) => _attributeSet.AddAttributeBaseValue(attributeId, delta);
        public AttributeModifierHandle AddModifier(int attributeId, AttributeModifierOp op, float value, object source)
            => _attributeSet.AddModifier(attributeId, op, value, source);
        public void RemoveModifier(AttributeModifierHandle handle) => _attributeSet.RemoveModifier(handle);
        public void ClearAllModifiers() => _attributeSet.ClearAllModifiers();

        public override void DeactivateForPool()
        {
            base.DeactivateForPool();
            _attributeSet.ClearAllModifiers();
        }

        protected override void OnDispose()
        {
            _attributeSet.Clear();
            base.OnDispose();
        }
    }
}
