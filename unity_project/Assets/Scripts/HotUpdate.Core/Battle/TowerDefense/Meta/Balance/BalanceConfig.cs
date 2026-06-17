using System;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 全局数值平衡配置 ScriptableObject。
    /// 
    /// 集中管理所有战斗公式参数和成长曲线引用。
    /// 所有数值统一来源，不允许散落在代码中。
    /// 
    /// 配置驱动：修改此 ScriptableObject 即可调平游戏数值。
    /// </summary>
    [CreateAssetMenu(fileName = "BalanceConfig", menuName = "TowerDefense/Balance Config", order = 240)]
    public class BalanceConfig : ScriptableObject
    {
        [Header("=== Damage Formula ===")]
        [Tooltip("伤害公式：Damage = Atk × (1 - Def/(Def + K))")]
        public float DefenseK = 100f;

        [Tooltip("最小伤害系数（保证低攻高防时仍造成伤害）")]
        [Range(0f, 1f)]
        public float MinDamageRatio = 0.1f;

        [Header("=== Critical Formula ===")]
        [Tooltip("暴击伤害倍率：CritDamage = Damage × (1 + CritDamageMul)")]
        public float CritDamageMultiplier = 1.5f;

        [Tooltip("暴击率上限")]
        [Range(0f, 1f)]
        public float MaxCritRate = 0.75f;

        [Header("=== Tower Attributes ===")]
        [Tooltip("防御塔攻击成长曲线（Attack × Level ^ Exponent）")]
        public float TowerAttackGrowthExponent = 1.3f;

        [Tooltip("防御塔攻速Growth（AttackInterval × (1 - SpeedGrowthPerLevel × Level))")]
        [Range(0f, 0.5f)]
        public float TowerSpeedGrowthPerLevel = 0.1f;

        [Header("=== Enemy Scaling ===")]
        [Tooltip("敌人 HP 成长曲线配置")]
        public EnemyCurveConfig EnemyHpCurve;

        [Tooltip("敌人速度成长曲线")]
        public EnemyCurveConfig EnemySpeedCurve;

        [Tooltip("敌人击杀金币成长曲线")]
        public EnemyCurveConfig EnemyGoldCurve;

        [Header("=== Wave Scaling ===")]
        [Tooltip("波次敌方数量增长公式：BaseCount × (1 + WaveScale × WaveIndex)")]
        public float WaveEnemyCountScale = 0.15f;

        [Tooltip("波次敌方HP增长倍率（每波）")]
        public float WaveEnemyHpMultiplier = 1.1f;

        [Header("=== Economy ===")]
        [Tooltip("击杀金币公式：BaseGold × (1 + EnemyGoldCurve)")]
        public float BaseKillGoldMultiplier = 1f;

        [Tooltip("建塔费用增长（每座同类型塔递增）：BuildCost × (1 + SameTowerCount × Tax)")]
        [Range(0f, 0.5f)]
        public float SameTowerBuildTax = 0f;

        [Header("=== Main City ===")]
        [Tooltip("主城 HP 成长公式：BaseHP × (1 + HPGrowthPerWave × WaveIndex)")]
        [Range(0f, 0.5f)]
        public float MainCityHPGrowthPerWave = 0.05f;

        [Header("=== Roguelike ===")]
        [Tooltip("强化选择选项数量")]
        public int RoguelikeChoiceCount = 3;

        [Tooltip("强化选择免费选项概率")]
        [Range(0f, 1f)]
        public float FreeChoiceProbability = 0.3f;
    }

    /// <summary>
    /// 敌人成长曲线配置 ScriptableObject。
    /// 
    /// 定义波次/等级对属性的增长曲线。
    /// 支持线性/指数/自定义曲线。
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyCurveConfig", menuName = "TowerDefense/Enemy Curve Config", order = 241)]
    public class EnemyCurveConfig : ScriptableObject
    {
        [Header("Curve Type")]
        public ECurveType CurveType = ECurveType.Linear;

        [Header("Linear Parameters")]
        [Tooltip("线性增长系数：Value = Base × (1 + Slope × Level)")]
        public float Slope = 0.1f;

        [Header("Exponential Parameters")]
        [Tooltip("指数增长系数：Value = Base × (Exponent ^ Level)")]
        public float Exponent = 1.15f;

        [Header("Custom Curve")]
        [Tooltip("自定义 AnimationCurve（X=等级, Y=倍率）")]
        public AnimationCurve CustomCurve = AnimationCurve.Linear(0, 1, 100, 10);

        /// <summary>根据等级计算倍率</summary>
        public float Evaluate(int level, float baseValue)
        {
            if (level <= 0) return baseValue;

            float multiplier = CurveType switch
            {
                ECurveType.Linear => 1f + Slope * level,
                ECurveType.Exponential => Mathf.Pow(Exponent, level),
                ECurveType.Custom => CustomCurve.Evaluate(level),
                _ => 1f,
            };

            return baseValue * Mathf.Max(0f, multiplier);
        }
    }

    /// <summary>成长曲线类型</summary>
    public enum ECurveType
    {
        Linear,
        Exponential,
        Custom,
    }
}
