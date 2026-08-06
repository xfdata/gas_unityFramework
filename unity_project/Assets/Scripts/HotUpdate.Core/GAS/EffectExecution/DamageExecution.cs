namespace GAS
{
    /// <summary>
    /// 通用伤害执行器（GAS 层）。
    /// 拆分为 ComputeDamage（纯函数，产出 DamageResult）+ ApplyDamage（状态修改，应用 result 到属性）。
    /// Compute/Apply 分离支持回放确定性、表现层留痕、DamageResult 溯源链路。
    /// R2 阶段暴击预留：IsCrit 默认 false，由后续暴击模块在 ComputeDamage 后/ApplyDamage 前注入。
    /// </summary>
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

            // 阶段 1：纯函数计算
            var result = ComputeDamage(spec, sourceAttr, targetAttr);

            // 阶段 2：状态修改
            ApplyDamage(spec, target, targetAttr, result);

            // 向后兼容：保留旧版 SetByCaller 回写，供外部读取（过渡期保留，最终由 DamageResult 替代）
            spec.SetByCaller(KeyLastDamage, result.FinalDamage);
            spec.SetByCaller(KeyLastShieldCost, result.ShieldCost);
            spec.SetByCaller(KeyLastHpDamage, result.HpDamage);
        }

        /// <summary>
        /// 纯函数：根据 source/target 属性与 spec 参数计算伤害结果。
        /// 无副作用，不修改任何状态，确定性可回放。
        /// 暴击预留：IsCrit 在此保持 false，后续暴击接入时由子类或 hook 填充。
        /// </summary>
        public DamageResult ComputeDamage(
            GameplayEffectSpec spec,
            IGameplayAttributeOwner sourceAttr,
            IGameplayAttributeOwner targetAttr)
        {
            float atk = sourceAttr.GetAttribute(AtkAttributeId);
            float def = targetAttr.GetAttribute(DefAttributeId);

            float skillRate = spec.GetSetByCaller(KeySkillRate, SkillRate);
            float flatDamage = spec.GetSetByCaller(KeyFlatDamage, FlatDamage);

            // 原始伤害 = atk * skillRate + flatDamage
            float rawDamage = atk * skillRate + flatDamage;

            // 本执行器无增伤/减伤/防御/盾，保持与旧版语义一致：
            // damage = raw - def，最低 1
            float mitigated = GameplayMath.Max(1f, rawDamage - def);

            // 本执行器无格挡逻辑（格挡在 CombatDamageExecution 层处理）
            float blockedDamage = 0f;
            bool isBlocked = false;

            // 扣盾量 = min(shield, mitigated)
            float shield = targetAttr.GetAttribute(ShieldAttributeId);
            float shieldCost = GameplayMath.Min(GameplayMath.Max(0f, shield), mitigated);

            // 预期扣血 = mitigated - shieldCost
            float intendedHpDamage = mitigated - shieldCost;

            // 当前 HP（用于计算 Overkill）
            float currentHp = targetAttr.GetAttribute(HpAttributeId);
            float hpDamage = GameplayMath.Min(intendedHpDamage, currentHp);
            if (hpDamage < 0f) hpDamage = 0f;

            float finalDamage = shieldCost + hpDamage;
            float overkill = GameplayMath.Max(0f, intendedHpDamage - currentHp);

            // 暴击预留：R2 阶段默认 false，由后续暴击模块接入
            bool isCrit = false;

            return new DamageResult(
                rawDamage: rawDamage,
                bonusDamage: rawDamage,        // 本执行器无增伤阶段，Bonus = Raw
                mitigated: mitigated,
                blockedDamage: blockedDamage,
                shieldCost: shieldCost,
                hpDamage: hpDamage,
                finalDamage: finalDamage,
                overkill: overkill,
                isCrit: isCrit,
                isBlocked: isBlocked,
                reasonKind: DamageReasonKind.None,   // 本执行器层不区分业务原因，由子类/上层填充
                reasonParam: 0,
                sourceRuntimeEffectId: spec.SourceRuntimeEffectId,
                sourceAbilitySpecId: spec.SourceAbilitySpecId);
        }

        /// <summary>
        /// 状态修改：按 DamageResult 应用到 target 属性（扣盾 + 扣血）。
        /// 仅修改属性，不重算数值，确保 Compute 与 Apply 解耦。
        /// </summary>
        public void ApplyDamage(
            GameplayEffectSpec spec,
            GameplayEffectRuntime target,
            IGameplayAttributeOwner targetAttr,
            in DamageResult result)
        {
            if (target == null || targetAttr == null)
                return;

            if (result.ShieldCost > 0f)
                target.ApplyAttributeBaseValue(spec, ShieldAttributeId, -result.ShieldCost);

            if (result.HpDamage > 0f)
                target.ApplyAttributeBaseValue(spec, HpAttributeId, -result.HpDamage);
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
