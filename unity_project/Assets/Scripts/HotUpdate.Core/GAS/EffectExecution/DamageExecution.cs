using UnityEngine;

namespace GAS
{
    public class DamageExecution : GameplayEffectExecution
    {
        public int HpAttributeId;
        public int ShieldAttributeId;
        public int AtkAttributeId;
        public int DefAttributeId;

        public float SkillRate = 1f;
        public float FlatDamage;

        public const int KeySkillRate = 1;
        public const int KeyFlatDamage = 2;
        public const int KeyLastDamage = 3;
        public const int KeyLastShieldCost = 4;
        public const int KeyLastHpDamage = 5;

        public override void Execute(GameplayEffectSpec spec)
        {
            if (spec == null)
                return;

            var source = ResolveRuntime(spec, spec.SourceEntityId, spec.Source);
            var target = ResolveRuntime(spec, spec.TargetEntityId, spec.Target);

            if (source == null || target == null)
                return;

            var sourceAttr = source.AttributeOwner;
            var targetAttr = target.AttributeOwner;

            if (sourceAttr == null || targetAttr == null)
                return;

            float atk = sourceAttr.GetAttribute(AtkAttributeId);
            float def = targetAttr.GetAttribute(DefAttributeId);

            float skillRate = spec.GetSetByCaller(KeySkillRate, SkillRate);
            float flatDamage = spec.GetSetByCaller(KeyFlatDamage, FlatDamage);

            float damage = atk * skillRate + flatDamage - def;

            if (damage < 1)
                damage = 1;

            float shield = targetAttr.GetAttribute(ShieldAttributeId);
            float shieldCost = Mathf.Min(shield, damage);
            float hpDamage = damage - shieldCost;

            if (shieldCost > 0)
                target.ApplyAttributeBaseValue(spec, ShieldAttributeId, -shieldCost);

            if (hpDamage > 0)
                target.ApplyAttributeBaseValue(spec, HpAttributeId, -hpDamage);

            spec.SetByCaller(KeyLastDamage, damage);
            spec.SetByCaller(KeyLastShieldCost, shieldCost);
            spec.SetByCaller(KeyLastHpDamage, hpDamage);
        }

        private static GameplayEffectRuntime ResolveRuntime(
            GameplayEffectSpec spec,
            long entityId,
            GameplayEffectRuntime cachedRuntime)
        {
            var resolved = entityId != 0
                ? spec.RuntimeContext?.ResolveEntity(entityId) as GameplayEffectRuntime
                : null;

            if (resolved != null)
                return resolved;

            return cachedRuntime != null && (entityId == 0 || cachedRuntime.EntityId == entityId)
                ? cachedRuntime
                : null;
        }
    }
}
