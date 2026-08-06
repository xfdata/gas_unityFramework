using System;
using GAS;
using NUnit.Framework;
using UnityEngine;

namespace BattleCommon.Tests
{
    /// <summary>
    /// R2-S11 数值对拍验证：验证 CombatDamageExecution.ComputeDamage 的数学正确性
    /// 和 DamageResult 字段填充。
    /// 纯逻辑测试，不依赖 Unity 运行时（不创建 ScriptableObject 实例，
    /// 直接调用 ComputeDamage 方法，传入构造好的 AttributeSet）。
    /// </summary>
    [TestFixture]
    public class CombatDamageExecutionTests
    {
        [Test]
        public void ComputeDamage_BasicAttack_NoBuff_NoShield_ProducesExpectedResult()
        {
            // === Arrange ===
            // source: Attack = 100
            var sourceAttrSet = new AttributeSet();
            sourceAttrSet.SetBaseValue(CombatAttributeIds.Attack, 100f);

            // target: HP = 200, Defense = 10, 无减伤/格挡/盾
            var targetAttrSet = new AttributeSet();
            targetAttrSet.SetBaseValue(CombatAttributeIds.HP, 200f);
            targetAttrSet.SetBaseValue(CombatAttributeIds.Defense, 10f);

            var execution = ScriptableObject.CreateInstance<CombatDamageExecution>();
            var spec = CreateMinimalSpec();

            // === Act ===
            var result = execution.ComputeDamage(spec, sourceAttrSet, targetAttrSet, currentHp: 200f);

            // === Assert ===
            // rawDamage = attack * factor(1.0) = 100
            // bonusDamage = rawDamage * (1 + 0 + 0) = 100
            // mitigated = max(1, 100 * 1 - 10 - 0) = 90
            // blockedDamage = 0 (无格挡)
            // shieldCost = 0 (无盾)
            // hpDamage = min(90, 200) = 90
            // finalDamage = 0 + 90 = 90
            // overkill = max(0, 90 - 200) = 0
            Assert.AreEqual(100f, result.RawDamage, 0.001f, "RawDamage");
            Assert.AreEqual(100f, result.BonusDamage, 0.001f, "BonusDamage");
            Assert.AreEqual(90f, result.Mitigated, 0.001f, "Mitigated");
            Assert.AreEqual(0f, result.BlockedDamage, 0.001f, "BlockedDamage");
            Assert.AreEqual(0f, result.ShieldCost, 0.001f, "ShieldCost");
            Assert.AreEqual(90f, result.HpDamage, 0.001f, "HpDamage");
            Assert.AreEqual(90f, result.FinalDamage, 0.001f, "FinalDamage");
            Assert.AreEqual(0f, result.Overkill, 0.001f, "Overkill");
            Assert.IsFalse(result.IsCrit, "IsCrit 预留应为 false");
            Assert.IsFalse(result.IsBlocked, "IsBlocked 无格挡应为 false");
            Assert.AreEqual(DamageReasonKind.None, result.ReasonKind, "ReasonKind 默认 None");
        }

        [Test]
        public void ComputeDamage_Overkill_CalculatedCorrectly()
        {
            // === Arrange ===
            // source: Attack = 1000
            var sourceAttrSet = new AttributeSet();
            sourceAttrSet.SetBaseValue(CombatAttributeIds.Attack, 1000f);

            // target: HP = 50, Defense = 0
            var targetAttrSet = new AttributeSet();
            targetAttrSet.SetBaseValue(CombatAttributeIds.HP, 50f);

            var execution = ScriptableObject.CreateInstance<CombatDamageExecution>();
            var spec = CreateMinimalSpec();

            // === Act ===
            var result = execution.ComputeDamage(spec, sourceAttrSet, targetAttrSet, currentHp: 50f);

            // === Assert ===
            // rawDamage = 1000
            // mitigated = max(1, 1000 - 0) = 1000
            // hpDamage = min(1000, 50) = 50
            // overkill = max(0, 1000 - 50) = 950
            Assert.AreEqual(1000f, result.Mitigated, 0.001f, "Mitigated");
            Assert.AreEqual(50f, result.HpDamage, 0.001f, "HpDamage clamp 到当前 HP");
            Assert.AreEqual(950f, result.Overkill, 0.001f, "Overkill = 溢出量");
            Assert.AreEqual(50f, result.FinalDamage, 0.001f, "FinalDamage = 实际扣血");
        }

        [Test]
        public void ComputeDamage_DamageUp_IncreasesBonusDamage()
        {
            // === Arrange ===
            var sourceAttrSet = new AttributeSet();
            sourceAttrSet.SetBaseValue(CombatAttributeIds.Attack, 100f);
            sourceAttrSet.SetBaseValue(CombatAttributeIds.DamageUp1, 0.5f); // +50% 增伤

            var targetAttrSet = new AttributeSet();
            targetAttrSet.SetBaseValue(CombatAttributeIds.HP, 1000f);

            var execution = ScriptableObject.CreateInstance<CombatDamageExecution>();
            var spec = CreateMinimalSpec();

            // === Act ===
            var result = execution.ComputeDamage(spec, sourceAttrSet, targetAttrSet, currentHp: 1000f);

            // === Assert ===
            // rawDamage = 100
            // bonusDamage = 100 * (1 + 0.5 + 0) = 150
            Assert.AreEqual(100f, result.RawDamage, 0.001f, "RawDamage 不含增伤");
            Assert.AreEqual(150f, result.BonusDamage, 0.001f, "BonusDamage 含增伤");
        }

        [Test]
        public void ComputeDamage_DamageReduce_ReducesMitigated()
        {
            // === Arrange ===
            var sourceAttrSet = new AttributeSet();
            sourceAttrSet.SetBaseValue(CombatAttributeIds.Attack, 100f);

            var targetAttrSet = new AttributeSet();
            targetAttrSet.SetBaseValue(CombatAttributeIds.HP, 1000f);
            targetAttrSet.SetBaseValue(CombatAttributeIds.DamageReduce, 0.3f); // 30% 减伤

            var execution = ScriptableObject.CreateInstance<CombatDamageExecution>();
            var spec = CreateMinimalSpec();

            // === Act ===
            var result = execution.ComputeDamage(spec, sourceAttrSet, targetAttrSet, currentHp: 1000f);

            // === Assert ===
            // rawDamage = 100
            // bonusDamage = 100
            // reduction = 1 - 0.3 = 0.7
            // mitigated = max(1, 100 * 0.7 - 0 - 0) = 70
            Assert.AreEqual(70f, result.Mitigated, 0.001f, "Mitigated 应受减伤影响");
        }

        [Test]
        public void ComputeDamage_MinOneDamage_Enforced()
        {
            // === Arrange ===
            var sourceAttrSet = new AttributeSet();
            sourceAttrSet.SetBaseValue(CombatAttributeIds.Attack, 1f);

            var targetAttrSet = new AttributeSet();
            targetAttrSet.SetBaseValue(CombatAttributeIds.HP, 1000f);
            targetAttrSet.SetBaseValue(CombatAttributeIds.Defense, 1000f); // 防御远超攻击

            var execution = ScriptableObject.CreateInstance<CombatDamageExecution>();
            var spec = CreateMinimalSpec();

            // === Act ===
            var result = execution.ComputeDamage(spec, sourceAttrSet, targetAttrSet, currentHp: 1000f);

            // === Assert ===
            // rawDamage = 1
            // mitigated = max(1, 1 - 1000) = 1 (最低 1 点伤害)
            Assert.AreEqual(1f, result.Mitigated, 0.001f, "Mitigated 最低 1 点伤害");
            Assert.AreEqual(1f, result.HpDamage, 0.001f, "HpDamage = 1");
        }

        [Test]
        public void ComputeDamage_SourceRuntimeEffectId_PropagatedFromSpec()
        {
            // === Arrange ===
            var sourceAttrSet = new AttributeSet();
            sourceAttrSet.SetBaseValue(CombatAttributeIds.Attack, 100f);

            var targetAttrSet = new AttributeSet();
            targetAttrSet.SetBaseValue(CombatAttributeIds.HP, 1000f);

            var execution = ScriptableObject.CreateInstance<CombatDamageExecution>();
            var spec = CreateMinimalSpec();
            spec.SourceAbilitySpecId = 42;
            spec.SourceRuntimeEffectId = 99;

            // === Act ===
            var result = execution.ComputeDamage(spec, sourceAttrSet, targetAttrSet, currentHp: 1000f);

            // === Assert ===
            Assert.AreEqual(42, result.SourceAbilitySpecId, "SourceAbilitySpecId 应从 spec 透传");
            Assert.AreEqual(99, result.SourceRuntimeEffectId, "SourceRuntimeEffectId 应从 spec 透传");
        }

        [Test]
        public void ComputeDamage_SameInputs_ProducesSameResult()
        {
            // === 数值对拍核心：相同输入必须产生相同输出（确定性可回放）===
            // === Arrange ===
            var sourceAttrSet = new AttributeSet();
            sourceAttrSet.SetBaseValue(CombatAttributeIds.Attack, 150f);
            sourceAttrSet.SetBaseValue(CombatAttributeIds.DamageUp1, 0.2f);

            var targetAttrSet = new AttributeSet();
            targetAttrSet.SetBaseValue(CombatAttributeIds.HP, 500f);
            targetAttrSet.SetBaseValue(CombatAttributeIds.Defense, 20f);
            targetAttrSet.SetBaseValue(CombatAttributeIds.DamageReduce, 0.1f);

            var execution = ScriptableObject.CreateInstance<CombatDamageExecution>();
            var spec = CreateMinimalSpec();

            // === Act ===
            var result1 = execution.ComputeDamage(spec, sourceAttrSet, targetAttrSet, currentHp: 500f);
            var result2 = execution.ComputeDamage(spec, sourceAttrSet, targetAttrSet, currentHp: 500f);

            // === Assert ===
            Assert.AreEqual(result1.RawDamage, result2.RawDamage, 0.0001f, "RawDamage 确定性");
            Assert.AreEqual(result1.BonusDamage, result2.BonusDamage, 0.0001f, "BonusDamage 确定性");
            Assert.AreEqual(result1.Mitigated, result2.Mitigated, 0.0001f, "Mitigated 确定性");
            Assert.AreEqual(result1.HpDamage, result2.HpDamage, 0.0001f, "HpDamage 确定性");
            Assert.AreEqual(result1.FinalDamage, result2.FinalDamage, 0.0001f, "FinalDamage 确定性");
            Assert.AreEqual(result1.Overkill, result2.Overkill, 0.0001f, "Overkill 确定性");
        }

        /// <summary>
        /// 创建最小化的 GameplayEffectSpec 用于测试。
        /// ComputeDamage 仅读取 spec.GetSetByCaller / spec.SourceAbilitySpecId / spec.SourceRuntimeEffectId，
        /// 不依赖 Source/Target runtime，因此可用 null runtime 构造。
        /// </summary>
        private static GameplayEffectSpec CreateMinimalSpec()
        {
            return new GameplayEffectSpec(
                definition: null,
                source: null,
                target: null,
                level: 1);
        }
    }
}
