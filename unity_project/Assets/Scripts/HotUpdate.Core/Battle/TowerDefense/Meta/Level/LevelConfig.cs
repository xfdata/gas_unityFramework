using System;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 关卡配置 ScriptableObject — 定义一局游戏的全部参数。
    /// 
    /// 组合：
    /// - 地图（MapConfig）
    /// - 波次（WaveConfig[]）
    /// - Boss（BossConfig）
    /// - 难度曲线
    /// - 起始资源
    /// - 可用防御塔
    /// 
    /// 数据驱动：新关卡 = 新建此 ScriptableObject。
    /// </summary>
    [CreateAssetMenu(fileName = "LevelConfig", menuName = "TowerDefense/Level Config", order = 221)]
    public class LevelConfig : ScriptableObject
    {
        [Header("Identity")]
        public string LevelId;
        public string DisplayName;
        public string Description;

        [Header("Map")]
        [Tooltip("本关卡使用的地图配置")]
        public MapConfig Map;

        [Header("Economy")]
        [Tooltip("初始金币（覆盖 GlobalConfig.StartingGold）")]
        public int OverrideStartingGold = -1;

        [Tooltip("起始生命数（0=使用主城默认血量）")]
        public int StartingLives;

        [Header("Available Towers")]
        [Tooltip("本关卡允许建造的防御塔（空=全局配置）")]
        public TowerConfig[] AvailableTowers = Array.Empty<TowerConfig>();

        [Header("Tower Mods")]
        [Tooltip("本关卡可用的塔插件（空=全局配置）")]
        public TowerModConfig[] AvailableMods = Array.Empty<TowerModConfig>();

        [Header("Wave Configs")]
        [Tooltip("本关卡的波次配置列表")]
        public WaveConfig[] WaveConfigs = Array.Empty<WaveConfig>();

        [Header("Difficulty Curve")]
        [Tooltip("波次成长倍率（每过一波，敌人HP × 此值）")]
        public float WaveHpScale = 1.1f;

        [Tooltip("波次成长速度倍率")]
        public float WaveSpeedScale = 1f;

        [Tooltip("波次击杀金币倍率")]
        public float WaveGoldScale = 1.05f;

        [Header("Boss")]
        [Tooltip("Boss配置（出现在最终波次或特定波次）")]
        public BossConfig[] BossConfigs = Array.Empty<BossConfig>();

        [Header("Meta Rewards")]
        [Tooltip("通关天赋点奖励")]
        public int WinTalentPoints = 3;

        [Tooltip("失败天赋点惩罚（通常 < WinTalentPoints）")]
        public int LoseTalentPoints = 1;

        // 便捷方法
        public int EffectiveStartingGold(TowerDefenseGlobalConfig globalConfig)
        {
            if (OverrideStartingGold >= 0)
                return OverrideStartingGold;
            return globalConfig?.StartingGold ?? 200;
        }
    }
}
