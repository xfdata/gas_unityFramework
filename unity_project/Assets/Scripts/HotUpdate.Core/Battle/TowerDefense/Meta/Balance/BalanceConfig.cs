using System;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 鍏ㄥ眬鏁板€煎钩琛￠厤缃?ScriptableObject銆?
    /// 
    /// 闆嗕腑绠＄悊鎵€鏈夋垬鏂楀叕寮忓弬鏁板拰鎴愰暱鏇茬嚎寮曠敤銆?
    /// 鎵€鏈夋暟鍊肩粺涓€鏉ユ簮锛屼笉鍏佽鏁ｈ惤鍦ㄤ唬鐮佷腑銆?
    /// 
    /// 閰嶇疆椹卞姩锛氫慨鏀规 ScriptableObject 鍗冲彲璋冨钩娓告垙鏁板€笺€?
    /// </summary>
    [CreateAssetMenu(fileName = "BalanceConfig", menuName = "TowerDefense/Balance Config", order = 240)]
    public class BalanceConfig : ScriptableObject
    {
        [Header("=== Damage Formula ===")]
        [Tooltip("浼ゅ鍏紡锛欴amage = Atk 脳 (1 - Def/(Def + K))")]
        public float DefenseK = 100f;

        [Tooltip("鏈€灏忎激瀹崇郴鏁帮紙淇濊瘉浣庢敾楂橀槻鏃朵粛閫犳垚浼ゅ锛?")]
        [Range(0f, 1f)]
        public float MinDamageRatio = 0.1f;

        [Header("=== Critical Formula ===")]
        [Tooltip("鏆村嚮浼ゅ鍊嶇巼锛欳ritDamage = Damage 脳 (1 + CritDamageMul)")]
        public float CritDamageMultiplier = 1.5f;

        [Tooltip("鏆村嚮鐜囦笂闄?")]
        [Range(0f, 1f)]
        public float MaxCritRate = 0.75f;

        [Header("=== Tower Attributes ===")]
        [Tooltip("闃插尽濉旀敾鍑绘垚闀挎洸绾匡紙Attack 脳 Level ^ Exponent锛?")]
        public float TowerAttackGrowthExponent = 1.3f;

        [Tooltip("闃插尽濉旀敾閫烥rowth锛圓ttackInterval 脳 (1 - SpeedGrowthPerLevel 脳 Level))")]
        [Range(0f, 0.5f)]
        public float TowerSpeedGrowthPerLevel = 0.1f;

        [Header("=== Enemy Scaling ===")]
        [Tooltip("鏁屼汉 HP 鎴愰暱鏇茬嚎閰嶇疆")]
        public EnemyCurveConfig EnemyHpCurve;

        [Tooltip("鏁屼汉閫熷害鎴愰暱鏇茬嚎")]
        public EnemyCurveConfig EnemySpeedCurve;

        [Tooltip("鏁屼汉鍑绘潃閲戝竵鎴愰暱鏇茬嚎")]
        public EnemyCurveConfig EnemyGoldCurve;

        [Header("=== Wave Scaling ===")]
        [Tooltip("娉㈡鏁屾柟鏁伴噺澧為暱鍏紡锛欱aseCount 脳 (1 + WaveScale 脳 WaveIndex)")]
        public float WaveEnemyCountScale = 0.15f;

        [Tooltip("娉㈡鏁屾柟HP澧為暱鍊嶇巼锛堟瘡娉級")]
        public float WaveEnemyHpMultiplier = 1.1f;

        [Header("=== Economy ===")]
        [Tooltip("鍑绘潃閲戝竵鍏紡锛欱aseGold 脳 (1 + EnemyGoldCurve)")]
        public float BaseKillGoldMultiplier = 1f;

        [Tooltip("寤哄璐圭敤澧為暱锛堟瘡搴у悓绫诲瀷濉旈€掑锛夛細BuildCost 脳 (1 + SameTowerCount 脳 Tax)")]
        [Range(0f, 0.5f)]
        public float SameTowerBuildTax = 0f;

        [Header("=== Main City ===")]
        [Tooltip("涓诲煄 HP 鎴愰暱鍏紡锛欱aseHP 脳 (1 + HPGrowthPerWave 脳 WaveIndex)")]
        [Range(0f, 0.5f)]
        public float MainCityHPGrowthPerWave = 0.05f;

        [Header("=== Roguelike ===")]
        [Tooltip("寮哄寲閫夋嫨閫夐」鏁伴噺")]
        public int RoguelikeChoiceCount = 3;

        [Tooltip("寮哄寲閫夋嫨鍏嶈垂閫夐」姒傜巼")]
        [Range(0f, 1f)]
        public float FreeChoiceProbability = 0.3f;
    }

    /// <summary>
    /// 鏁屼汉鎴愰暱鏇茬嚎閰嶇疆 ScriptableObject銆?
    /// 
    /// 瀹氫箟娉㈡/绛夌骇瀵瑰睘鎬х殑澧為暱鏇茬嚎銆?
    /// 鏀寔绾挎€?鎸囨暟/鑷畾涔夋洸绾裤€?
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyCurveConfig", menuName = "TowerDefense/Enemy Curve Config", order = 241)]
    public class EnemyCurveConfig : ScriptableObject
    {
        [Header("Curve Type")]
        public ECurveType CurveType = ECurveType.Linear;

        [Header("Linear Parameters")]
        [Tooltip("绾挎€у闀跨郴鏁帮細Value = Base 脳 (1 + Slope 脳 Level)")]
        public float Slope = 0.1f;

        [Header("Exponential Parameters")]
        [Tooltip("鎸囨暟澧為暱绯绘暟锛歏alue = Base 脳 (Exponent ^ Level)")]
        public float Exponent = 1.15f;

        [Header("Custom Curve")]
        [Tooltip("鑷畾涔?AnimationCurve锛圶=绛夌骇, Y=鍊嶇巼锛?")]
        public AnimationCurve CustomCurve = AnimationCurve.Linear(0, 1, 100, 10);

        /// <summary>鏍规嵁绛夌骇璁＄畻鍊嶇巼</summary>
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

    /// <summary>鎴愰暱鏇茬嚎绫诲瀷</summary>
    public enum ECurveType
    {
        Linear,
        Exponential,
        Custom,
    }
}
