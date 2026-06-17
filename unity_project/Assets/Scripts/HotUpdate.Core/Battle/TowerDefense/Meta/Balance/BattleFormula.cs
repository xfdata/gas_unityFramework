using System;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 战斗公式库 — 集中管理所有战斗计算公式。
    /// 
    /// 职责：
    /// - 提供 Damage/Defense/Crit/Economy/BossPhase 等公式
    /// - 支持 BalanceConfig 参数驱动
    /// - 禁止公式散落在代码中
    /// 
    /// 数值流转：Config → BattleFormula.Evaluate() → GAS Execution / TowerSystem / EnemySystem
    /// </summary>
    public static class BattleFormula
    {
        private static BalanceConfig _config;

        /// <summary>设置全局BalanceConfig（在 BattleEngine.Initialize 时调用）</summary>
        public static void SetConfig(BalanceConfig config)
        {
            _config = config;
        }

        // ===== 伤害公式 =====

        /// <summary>
        /// 计算最终伤害。
        /// 公式：Damage = Atk × (1 - Def/(Def + K))，最小为 Atk × MinDamageRatio
        /// </summary>
        public static float CalculateDamage(float attackerAtk, float targetDef, float critMultiplier = 1f)
        {
            if (_config == null) return attackerAtk;

            float defK = _config.DefenseK > 0 ? _config.DefenseK : 100f;
            float defRatio = 1f - (targetDef / (targetDef + defK));
            defRatio = Mathf.Max(defRatio, _config.MinDamageRatio);

            float baseDamage = attackerAtk * defRatio;
            return baseDamage * Mathf.Max(1f, critMultiplier);
        }

        // ===== 暴击公式 =====

        /// <summary>判定是否暴击</summary>
        public static bool RollCrit(float critRate)
        {
            if (_config == null) return false;
            float clampedRate = Mathf.Clamp(critRate, 0f, _config.MaxCritRate);
            return UnityEngine.Random.value < clampedRate;
        }

        /// <summary>获取暴击伤害倍率</summary>
        public static float GetCritDamageMultiplier()
        {
            return _config?.CritDamageMultiplier ?? 1.5f;
        }

        // ===== 塔成长公式 =====

        /// <summary>计算塔升级后的攻击力</summary>
        public static float CalculateTowerAttackGrowth(float baseAttack, int level)
        {
            if (_config == null || level <= 1) return baseAttack;
            float exponent = _config.TowerAttackGrowthExponent;
            return baseAttack * Mathf.Pow(level, exponent);
        }

        /// <summary>计算塔升级后的攻速</summary>
        public static float CalculateTowerSpeedGrowth(float baseInterval, int level)
        {
            if (_config == null || level <= 1) return baseInterval;
            float speedGrowth = Mathf.Clamp01(_config.TowerSpeedGrowthPerLevel);
            return baseInterval * (1f - speedGrowth * (level - 1));
        }

        // ===== 敌人成长公式 =====

        /// <summary>计算波次敌人的HP</summary>
        public static float CalculateEnemyHP(float baseHp, int waveIndex)
        {
            if (_config?.EnemyHpCurve != null)
                return _config.EnemyHpCurve.Evaluate(waveIndex, baseHp);

            // 回退：简单线性增长
            return baseHp * Mathf.Pow(1.1f, waveIndex);
        }

        /// <summary>计算波次敌方数量</summary>
        public static int CalculateWaveEnemyCount(int baseCount, int waveIndex)
        {
            if (_config == null) return baseCount;
            float scale = 1f + _config.WaveEnemyCountScale * waveIndex;
            return Mathf.Max(1, Mathf.RoundToInt(baseCount * scale));
        }

        // ===== 经济公式 =====

        /// <summary>计算击杀金币</summary>
        public static int CalculateKillGold(int baseGold, int waveIndex)
        {
            if (_config?.EnemyGoldCurve != null)
                return Mathf.RoundToInt(_config.EnemyGoldCurve.Evaluate(waveIndex, baseGold));

            return baseGold;
        }

        /// <summary>计算建造成本（含同类塔递增税）</summary>
        public static int CalculateBuildCost(int baseCost, int sameTowerCount)
        {
            if (_config == null || sameTowerCount <= 0) return baseCost;
            float tax = Mathf.Clamp01(_config.SameTowerBuildTax);
            float multiplier = 1f + tax * sameTowerCount;
            return Mathf.RoundToInt(baseCost * multiplier);
        }

        // ===== 主城公式 =====

        /// <summary>计算主城当前波次的HP</summary>
        public static float CalculateMainCityHP(float baseHp, int waveIndex)
        {
            if (_config == null) return baseHp;
            float growthRate = Mathf.Clamp01(_config.MainCityHPGrowthPerWave);
            return baseHp * (1f + growthRate * waveIndex);
        }

        // ===== Boss 公式 =====

        /// <summary>计算Boss阶段HP阈值</summary>
        public static float GetBossPhaseHPThreshold(float maxHp, float thresholdRatio)
        {
            return maxHp * Mathf.Clamp01(thresholdRatio);
        }

        /// <summary>获取Boss实际HP（基础 × Boss倍率 × 波次倍率）</summary>
        public static float CalculateBossHP(float baseEnemyHp, float bossMultiplier, int waveIndex)
        {
            if (_config == null) return baseEnemyHp * bossMultiplier;
            float waveHp = CalculateEnemyHP(baseEnemyHp, waveIndex);
            return waveHp * bossMultiplier;
        }

        // ===== 罗吉尔选择公式 =====

        /// <summary>判定强化选择是否免费</summary>
        public static bool IsChoiceFree()
        {
            if (_config == null) return false;
            return UnityEngine.Random.value < _config.FreeChoiceProbability;
        }
    }
}
