using Framework;
using GAS;
using BattleFoundation;
using UnityEngine;

namespace BattleCommon
{
    public static class CombatDamageKeys
    {
        public const int AttackFactor = 1;
        public const int Attack = 2;
        public const int DamageUp1 = 3;
        public const int DamageUp2 = 4;
        public const int HP = 5;
        public const int MaxHP = 6;
        public const int BlockedDamage = 7;
        public const int FinalDamage = 8;
    }

    /// <summary>
    /// 战斗伤害执行器（BattleCommon 层）。
    /// 对齐 DamageResult：拆分为 ComputeDamage（纯函数）+ ApplyDamage（状态修改）。
    /// ApplyDamage 改走 ApplyAttributeBaseValue（而非 SetBaseValue），让 HP 变更走 GAS 事件链路，
    /// 确保 AttributeChanged / OnAttributeChanged 事件正确触发（CombatHealthComponent 依赖此事件监听死亡）。
    /// R2 阶段暴击预留：IsCrit 默认 false，由后续暴击模块接入。
    /// </summary>
    [CreateAssetMenu(menuName = "BattleCommon/Abilities/Execution/Damage")]
    public class CombatDamageExecution : GameplayEffectExecution
    {
        public override void Execute(GameplayEffectSpec spec)
        {
            if (spec == null) return;

            using (new AutoProfiler("BattleCommon.CombatDamageExecution.Execute"))
            {
                var targetAttrSet = (spec.Target?.AttributeOwner as IGameplayAttributeSetProvider)?.AttributeSet;
                var sourceAttrSet = (spec.Source?.AttributeOwner as IGameplayAttributeSetProvider)?.AttributeSet;
                if (targetAttrSet == null) return;

                // 前置校验：target 能否受击、source 能否行动
                if (spec.Target?.AttributeOwner is CombatActor targetActor &&
                    targetActor.Get<CombatStateComponent>() is { } tgtState &&
                    !tgtState.CanTakeDamage) return;
                if (spec.Source?.AttributeOwner is CombatActor sourceActor &&
                    sourceActor.Get<CombatStateComponent>() is { } srcState &&
                    !srcState.CanAct) return;

                // HP <= 0 不再计算伤害
                float currentHp = targetAttrSet.GetAttribute(CombatAttributeIds.HP);
                if (currentHp <= 0f) return;

                // 阶段 1：纯函数计算
                var result = ComputeDamage(spec, sourceAttrSet, targetAttrSet, currentHp);

                // 向后兼容：保留旧版 SetByCaller 回写（过渡期，最终由 DamageResult 替代）
                spec.SetByCaller(CombatDamageKeys.FinalDamage, result.FinalDamage);
                if (result.BlockedDamage > 0f)
                    spec.SetByCaller(CombatDamageKeys.BlockedDamage, result.BlockedDamage);

                // 最终伤害 <= 0 则不应用
                if (result.FinalDamage <= 0f)
                    return;

                // 阶段 2：状态修改
                ApplyDamage(spec, spec.Target, result);
            }
        }

        /// <summary>
        /// 纯函数：根据 source/target 属性与 spec 参数计算伤害结果。
        /// 无副作用，不修改任何状态，确定性可回放。
        /// 包含：原始伤害 → 增伤 → 减伤/防御/绝对减伤 → 格挡 → 扣盾 → 扣血 → Overkill。
        /// 暴击预留：IsCrit 在此保持 false，后续暴击接入时由子类或 hook 填充。
        /// </summary>
        public DamageResult ComputeDamage(
            GameplayEffectSpec spec,
            AttributeSet sourceAttrSet,
            AttributeSet targetAttrSet,
            float currentHp)
        {
            // === 原始伤害 ===
            float attack = spec.GetSetByCaller(
                CombatDamageKeys.Attack,
                sourceAttrSet?.GetAttribute(CombatAttributeIds.Attack) ?? 0f);
            float factor = spec.GetSetByCaller(CombatDamageKeys.AttackFactor, 1f);
            float rawDamage = attack * factor;

            // === 增伤后伤害 ===
            float increases = 1f +
                spec.GetSetByCaller(CombatDamageKeys.DamageUp1, sourceAttrSet?.GetAttribute(CombatAttributeIds.DamageUp1) ?? 0f) +
                spec.GetSetByCaller(CombatDamageKeys.DamageUp2, sourceAttrSet?.GetAttribute(CombatAttributeIds.DamageUp2) ?? 0f);
            float bonusDamage = rawDamage * BattleMathF.Max(0f, increases);

            // === 减伤后伤害（格挡前）===
            float reduction = 1f -
                targetAttrSet.GetAttribute(CombatAttributeIds.DamageReduce) -
                targetAttrSet.GetAttribute(CombatAttributeIds.DamageReduce1) -
                targetAttrSet.GetAttribute(CombatAttributeIds.DamageReduce2);
            float mitigated = bonusDamage * BattleMathF.Max(0f, reduction) -
                targetAttrSet.GetAttribute(CombatAttributeIds.Defense) -
                targetAttrSet.GetAttribute(CombatAttributeIds.AbsoluteReduce);
            // 最低 1 点伤害（保持旧版语义）
            mitigated = BattleMathF.Max(1f, mitigated);

            // === 格挡 ===
            float blockedDamage = 0f;
            bool isBlocked = false;
            float afterBlock = ApplyDamageBlock(spec, mitigated);
            if (afterBlock < mitigated)
            {
                blockedDamage = mitigated - afterBlock;
                isBlocked = true;
            }

            // === 扣盾 ===
            // CombatDamageExecution 当前不处理盾属性（旧版直接扣 HP），保持语义一致：
            // ShieldCost = 0，所有伤害走 HpDamage
            float shieldCost = 0f;
            float intendedHpDamage = afterBlock;

            // === 扣血（clamp 不越界）===
            float hpDamage = BattleMathF.Min(intendedHpDamage, currentHp);
            if (hpDamage < 0f) hpDamage = 0f;

            float finalDamage = shieldCost + hpDamage;
            float overkill = BattleMathF.Max(0f, intendedHpDamage - currentHp);

            // 暴击预留：R2 阶段默认 false，由后续暴击模块接入
            bool isCrit = false;

            return new DamageResult(
                rawDamage: rawDamage,
                bonusDamage: bonusDamage,
                mitigated: mitigated,
                blockedDamage: blockedDamage,
                shieldCost: shieldCost,
                hpDamage: hpDamage,
                finalDamage: finalDamage,
                overkill: overkill,
                isCrit: isCrit,
                isBlocked: isBlocked,
                reasonKind: DamageReasonKind.None,   // 业务原因由上层（技能/普攻/buff）填充
                reasonParam: 0,
                sourceRuntimeEffectId: spec.SourceRuntimeEffectId,
                sourceAbilitySpecId: spec.SourceAbilitySpecId);
        }

        /// <summary>
        /// 状态修改：按 DamageResult 应用到 target 属性（扣血）。
        /// 改走 ApplyAttributeBaseValue（而非 SetBaseValue），让 HP 变更触发 GAS AttributeChanged 事件，
        /// 确保 CombatHealthComponent 等监听者正确收到死亡通知。
        /// </summary>
        public void ApplyDamage(
            GameplayEffectSpec spec,
            GameplayEffectRuntime target,
            in DamageResult result)
        {
            if (target == null)
                return;

            // R2-S10：写入 LastDamageResult + LastDamageSourceEntityId，
            // 供 CombatHealthComponent 死亡时通过 EntityManager 解析 killer
            var targetActor = target.AttributeOwner as CombatActor;
            var attrComp = targetActor?.Get<CombatAttributeComponent>();

            // CombatDamageExecution 当前不处理盾属性（ShieldCost 始终为 0），所有伤害走 HpDamage
            if (result.HpDamage > 0f)
            {
                // 在 ApplyAttributeBaseValue 之前写入，确保 OnAttributeChanged 回调时能读到
                if (attrComp != null)
                {
                    attrComp.LastDamageResult = result;
                    attrComp.LastDamageSourceEntityId = spec?.SourceEntityId ?? 0;
                }

                target.ApplyAttributeBaseValue(spec, CombatAttributeIds.HP, -result.HpDamage);
            }
        }

        /// <summary>
        /// 格挡判定（无副作用，仅查询 target 的格挡能力）。
        /// 返回格挡后的剩余伤害。
        /// </summary>
        private float ApplyDamageBlock(GameplayEffectSpec spec, float damage)
        {
            var targetActor = spec.Target?.AttributeOwner as CombatActor;
            var ability = targetActor?.Get<CombatAbilityComponent>();
            if (ability == null || damage <= 0f)
                return damage;

            var blockContext = new DamageBlockContext(spec, spec.Source, spec.Target, damage);
            if (!ability.TryBlockIncomingDamage(blockContext))
                return damage;

            // 格挡成功：blockContext 内部已设置 BlockedDamage，这里仅返回剩余伤害
            // 注意：旧版这里回写 SetByCaller，新版改为在 Execute 中统一从 result 回写
            return blockContext.RemainingDamage;
        }
    }
}
