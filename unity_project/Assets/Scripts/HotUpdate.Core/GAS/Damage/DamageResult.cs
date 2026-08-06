namespace GAS
{
    /// <summary>
    /// 伤害原因类型。用于 DamageResult 溯源，标记本次伤害的业务来源。
    /// </summary>
    public enum DamageReasonKind
    {
        None = 0,
        Ability,        // 技能伤害
        Attack,         // 普攻伤害
        Buff,           // Buff 持续伤害（DOT）
        Item,           // 道具伤害
        Environment,    // 环境伤害
        Reflect,        // 反伤
    }

    /// <summary>
    /// 伤害计算结果（全留痕）。由 ComputeDamage 纯函数产出，ApplyDamage 消费。
    /// 字段记录伤害从原始值到最终扣血的全过程中间量，供表现层/日志/溯源直接读取，
    /// 替代旧版通过 SetByCaller 魔法数 Key 反查的方式。
    /// </summary>
    public readonly struct DamageResult
    {
        /// <summary>原始伤害 = attack * factor（未含增伤/减伤）</summary>
        public readonly float RawDamage;

        /// <summary>增伤后伤害 = RawDamage * increases</summary>
        public readonly float BonusDamage;

        /// <summary>减伤后伤害 = BonusDamage * reduction - Defense - AbsoluteReduce（格挡前）</summary>
        public readonly float Mitigated;

        /// <summary>格挡抵消量</summary>
        public readonly float BlockedDamage;

        /// <summary>扣盾量 = min(shield, Mitigated)</summary>
        public readonly float ShieldCost;

        /// <summary>实际扣血量（已 clamp，不越界）</summary>
        public readonly float HpDamage;

        /// <summary>最终伤害 = ShieldCost + HpDamage</summary>
        public readonly float FinalDamage;

        /// <summary>溢出量 = max(0, intendedHpDamage - currentHp)。0 表示无溢出</summary>
        public readonly float Overkill;

        /// <summary>是否暴击（R2 预留，默认 false；暴击接入后由 ComputeDamage 填充）</summary>
        public readonly bool IsCrit;

        /// <summary>是否格挡</summary>
        public readonly bool IsBlocked;

        /// <summary>伤害原因类型</summary>
        public readonly DamageReasonKind ReasonKind;

        /// <summary>原因参数（如 EffectId / AbilityId，视 ReasonKind 而定）</summary>
        public readonly int ReasonParam;

        /// <summary>溯源：来源运行时效果 ID（0 = Ability 直接创建）</summary>
        public readonly int SourceRuntimeEffectId;

        /// <summary>溯源：来源能力规格 ID</summary>
        public readonly int SourceAbilitySpecId;

        public DamageResult(
            float rawDamage,
            float bonusDamage,
            float mitigated,
            float blockedDamage,
            float shieldCost,
            float hpDamage,
            float finalDamage,
            float overkill,
            bool isCrit,
            bool isBlocked,
            DamageReasonKind reasonKind,
            int reasonParam,
            int sourceRuntimeEffectId,
            int sourceAbilitySpecId)
        {
            RawDamage = rawDamage;
            BonusDamage = bonusDamage;
            Mitigated = mitigated;
            BlockedDamage = blockedDamage;
            ShieldCost = shieldCost;
            HpDamage = hpDamage;
            FinalDamage = finalDamage;
            Overkill = overkill;
            IsCrit = isCrit;
            IsBlocked = isBlocked;
            ReasonKind = reasonKind;
            ReasonParam = reasonParam;
            SourceRuntimeEffectId = sourceRuntimeEffectId;
            SourceAbilitySpecId = sourceAbilitySpecId;
        }
    }
}
