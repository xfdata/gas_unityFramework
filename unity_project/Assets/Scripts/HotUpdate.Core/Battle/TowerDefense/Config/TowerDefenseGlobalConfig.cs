using System;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 瀹炰綋棰勫垱寤洪厤缃」
    /// </summary>
    [Serializable]
    public struct EnemyPreWarmEntry
    {
        [Tooltip("鏁屼汉閰嶇疆")]
        public TDEnemyConfig config;
        [Tooltip("棰勫垱寤烘暟閲?")]
        public int count;
    }

    /// <summary>
    /// TD鍏ㄥ眬閰嶇疆ScriptableObject銆?
    /// 鎸傝浇鍒癟DBattleEngine鍚庯紝鍦∣nInitialize闃舵娑堣垂銆?
    /// </summary>
    [CreateAssetMenu(fileName = "TowerDefenseGlobalConfig", menuName = "TowerDefense/Global Config", order = 90)]
    public class TowerDefenseGlobalConfig : ScriptableObject
    {
        [Header("Random")]
        [Tooltip("闅忔満绉嶅瓙锛?=鑷姩鐢熸垚")]
        public int RandomSeed;

        [Tooltip("鍒濆鏃堕棿缂╂斁")]
        public float InitialTimeScale = 1f;

        [Header("Economy")]
        [Tooltip("鍒濆閲戝竵")]
        public int StartingGold = 200;

        [Header("Object Pool")]
        [Tooltip("鏁屼汉棰勫垱寤烘睜閰嶇疆锛堝噺灏戣繍琛屾椂Instantiate锛?")]
        public EnemyPreWarmEntry[] EnemyPreWarmConfigs = Array.Empty<EnemyPreWarmEntry>();

        [Header("Wave")]
        [Tooltip("娉㈡閰嶇疆鍒楄〃锛圥hase 6浣跨敤锛?")]
        public WaveConfig[] WaveConfigs = Array.Empty<WaveConfig>();

        [Header("Main City")]
        [Tooltip("涓诲煄閰嶇疆锛圥hase 2浣跨敤锛?")]
        public MainCityConfig MainCityConfig;

    [Header("Path")]
    [Tooltip("榛樿璺緞锛堝吋瀹规棫鐗堝崟璺緞娉㈡閰嶇疆锛?")]
    public WaypointPath DefaultPath;

    [Header("Placement")]
    [Tooltip("闃插尽濉斿彲寤洪€犵綉鏍煎ぇ灏?")]
    public float PlacementGridSize = 1.5f;
    [Tooltip("闃插尽濉斿缓閫燣ayerMask")]
    public LayerMask PlacementLayerMask = -1;
    [Tooltip("涓嶅彲寤洪€犲尯鍩烲ayerMask")]
    public LayerMask BlockedLayerMask;

    [Header("Roguelike (Phase 5)")]
    [Tooltip("寮哄寲閫夋嫨姹狅細姣忔尝缁撴潫鏃朵粠杩欎簺閰嶇疆涓殢鏈烘娊鍙?涓€夐」")]
    public ChoiceConfig[] RoguelikeChoicePool = Array.Empty<ChoiceConfig>();
    [Tooltip("Build娴佹淳鍗忓悓锛氬悓绫诲瀷濉旇揪鍒伴槇鍊兼椂瑙﹀彂鐨勫鐩婃晥鏋?")]
    public SynergyConfig[] SynergyConfigs = Array.Empty<SynergyConfig>();

    [Header("UI (Phase 6)")]
    [Tooltip("鍙缓閫犵殑闃插尽濉旈厤缃垪琛紙渚?TowerBuildView 鍔犺浇锛?")]
    public TowerConfig[] AvailableTowers = Array.Empty<TowerConfig>();

    [Header("Meta & Balance (Phase 7)")]
    [Tooltip("澶╄祴鏍戦厤缃紙灞€澶栨案涔呮垚闀匡級")]
    public TalentTreeConfig TalentTreeConfig;
    [Tooltip("鏁板€煎钩琛￠厤缃紙浼ゅ/鏆村嚮/鎴愰暱鍏紡锛?")]
    public BalanceConfig BalanceConfig;
    [Tooltip("褰撳墠鍏冲崱閰嶇疆锛堝湴鍥?娉㈡+Boss锛?")]
    public LevelConfig CurrentLevelConfig;
}
}
