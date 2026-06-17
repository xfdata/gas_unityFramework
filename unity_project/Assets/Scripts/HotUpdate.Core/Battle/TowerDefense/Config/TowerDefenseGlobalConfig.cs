using System;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 实体预创建配置项
    /// </summary>
    [Serializable]
    public struct EnemyPreWarmEntry
    {
        [Tooltip("敌人配置")]
        public TDEnemyConfig config;
        [Tooltip("预创建数量")]
        public int count;
    }

    /// <summary>
    /// TD全局配置ScriptableObject。
    /// 挂载到TDBattleEngine后，在OnInitialize阶段消费。
    /// </summary>
    [CreateAssetMenu(fileName = "TowerDefenseGlobalConfig", menuName = "TowerDefense/Global Config", order = 90)]
    public class TowerDefenseGlobalConfig : ScriptableObject
    {
        [Header("Random")]
        [Tooltip("随机种子，0=自动生成")]
        public int RandomSeed;

        [Tooltip("初始时间缩放")]
        public float InitialTimeScale = 1f;

        [Header("Economy")]
        [Tooltip("初始金币")]
        public int StartingGold = 200;

        [Header("Object Pool")]
        [Tooltip("敌人预创建池配置（减少运行时Instantiate）")]
        public EnemyPreWarmEntry[] EnemyPreWarmConfigs = Array.Empty<EnemyPreWarmEntry>();

        [Header("Wave")]
        [Tooltip("波次配置列表（Phase 6使用）")]
        public WaveConfig[] WaveConfigs = Array.Empty<WaveConfig>();

        [Header("Main City")]
        [Tooltip("主城配置（Phase 2使用）")]
        public MainCityConfig MainCityConfig;

        [Header("Path")]
        [Tooltip("默认路径（兼容旧版单路径波次配置）")]
        public WaypointPath DefaultPath;

        [Header("Placement")]
        [Tooltip("防御塔可建造网格大小")]
        public float PlacementGridSize = 1.5f;
        [Tooltip("防御塔建造LayerMask")]
        public LayerMask PlacementLayerMask = -1;
        [Tooltip("不可建造区域LayerMask")]
        public LayerMask BlockedLayerMask;

        [Header("Roguelike (Phase 5)")]
        [Tooltip("强化选择池：每波结束时从这些配置中随机抽取3个选项")]
        public ChoiceConfig[] RoguelikeChoicePool = Array.Empty<ChoiceConfig>();
        [Tooltip("Build流派协同：同类型塔达到阈值时触发的增益效果")]
        public SynergyConfig[] SynergyConfigs = Array.Empty<SynergyConfig>();

        [Header("UI (Phase 6)")]
        [Tooltip("可建造的防御塔配置列表（供 TowerBuildView 加载）")]
        public TowerConfig[] AvailableTowers = Array.Empty<TowerConfig>();

        [Header("Meta & Balance (Phase 7)")]
        [Tooltip("天赋树配置（局外永久成长）")]
        public TalentTreeConfig TalentTreeConfig;
        [Tooltip("数值平衡配置（伤害/暴击/成长公式）")]
        public BalanceConfig BalanceConfig;
        [Tooltip("当前关卡配置（地图+波次+Boss）")]
        public LevelConfig CurrentLevelConfig;
    }
}
