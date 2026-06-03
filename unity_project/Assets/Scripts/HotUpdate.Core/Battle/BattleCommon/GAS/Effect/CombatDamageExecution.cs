using GAS;
using UnityEngine;

namespace BattleCommon
{
    public static class CombatDamageKeys
    {
        public const int AttackFactor = 1;
        public const int Attack = 2;
        public const int DamageUp1 = 3;
        public const int DamageUp2 = 4;
    }

    [CreateAssetMenu(menuName = "BattleCommon/GAS/Execution/Damage")]
    public class CombatDamageExecution : GameplayEffectExecution
    {
        public override void Execute(GameplayEffectSpec spec)
        {
            if (spec == null) return;
            var target = (spec.Target?.AttributeOwner as IGameplayAttributeSetProvider)?.AttributeSet;
            var source = (spec.Source?.AttributeOwner as IGameplayAttributeSetProvider)?.AttributeSet;
            if (target == null) return;

            float hp = target.GetAttribute(CombatAttributeIds.HP);
            if (hp <= 0f) return;

            float attack = spec.GetSetByCaller(CombatDamageKeys.Attack, source?.GetAttribute(CombatAttributeIds.Attack) ?? 0f);
            float factor = spec.GetSetByCaller(CombatDamageKeys.AttackFactor, 1f);
            float increases = 1f +
                spec.GetSetByCaller(CombatDamageKeys.DamageUp1, source?.GetAttribute(CombatAttributeIds.DamageUp1) ?? 0f) +
                spec.GetSetByCaller(CombatDamageKeys.DamageUp2, source?.GetAttribute(CombatAttributeIds.DamageUp2) ?? 0f);
            float reduction = 1f -
                target.GetAttribute(CombatAttributeIds.DamageReduce) -
                target.GetAttribute(CombatAttributeIds.DamageReduce1) -
                target.GetAttribute(CombatAttributeIds.DamageReduce2);
            float damage = Mathf.Max(1f,
                attack * factor * Mathf.Max(0f, increases) * Mathf.Max(0f, reduction) -
                target.GetAttribute(CombatAttributeIds.Defense) -
                target.GetAttribute(CombatAttributeIds.AbsoluteReduce));

            target.SetBaseValue(CombatAttributeIds.HP, Mathf.Max(0f, hp - damage));
        }
    }
}
