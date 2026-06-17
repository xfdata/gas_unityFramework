using System;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 天赋节点类型（决定效果作用域）
    /// </summary>
    public enum ETalentType
    {
        // === 战斗强化类 ===
        /// <summary>初始金币增加</summary>
        StartingGoldBonus,
        /// <summary>主城初始血量+%</summary>
        MainCityHPBonus,
        /// <summary>全局塔攻击力+%</summary>
        TowerAttackBonus,

        // === 塔系强化类 ===
        /// <summary>箭塔攻速+%</summary>
        ArrowTowerAttackSpeed,
        /// <summary>炮塔范围+%</summary>
        CannonTowerRange,
        /// <summary>冰塔减速效果+%</summary>
        IceTowerSlowBonus,

        // === 经济类 ===
        /// <summary>击杀金币+%</summary>
        KillGoldBonus,
        /// <summary>建造成本降低%</summary>
        BuildCostReduction,
    }

    /// <summary>
    /// 天赋节点的解锁状态
    /// </summary>
    public enum ETalentNodeState
    {
        /// <summary>未解锁（且不可解锁）</summary>
        Locked,
        /// <summary>可以解锁（前置条件满足，有待消耗的天赋点）</summary>
        Available,
        /// <summary>已解锁</summary>
        Unlocked,
    }

    /// <summary>
    /// 天赋树配置 ScriptableObject。
    /// 定义所有天赋节点的树结构（节点列表 + 前置依赖关系）。
    /// 
    /// 数据驱动：不在代码中写死任何天赋节点逻辑。
    /// 效果通过 TalentNode.EffectType + Value 驱动。
    /// </summary>
    [CreateAssetMenu(fileName = "TalentTreeConfig", menuName = "TowerDefense/Meta/Talent Tree Config", order = 200)]
    public class TalentTreeConfig : ScriptableObject
    {
        [Tooltip("天赋树名称")]
        public string TreeName = "Default Talent Tree";

        [Tooltip("天赋节点列表")]
        public TalentNodeDefinition[] Nodes = Array.Empty<TalentNodeDefinition>();
    }

    /// <summary>
    /// 单个天赋节点配置（ScriptableObject 序列化）
    /// </summary>
    [Serializable]
    public class TalentNodeDefinition
    {
        [Tooltip("唯一标识")]
        public string NodeId;

        [Tooltip("展示名称")]
        public string DisplayName;

        [Tooltip("描述")]
        public string Description;

        [Tooltip("天赋类型")]
        public ETalentType TalentType;

        [Tooltip("数值（如 30 表示+30% 起始金币）")]
        public float Value;

        [Tooltip("解锁消耗天赋点")]
        public int Cost = 1;

        [Tooltip("最大可投入点数（0=不可升级，1=单次解锁，N=可多次投入）")]
        public int MaxLevel = 1;

        [Tooltip("前置节点ID列表（必须全部解锁才能解锁本节点）")]
        public string[] PrerequisiteIds = Array.Empty<string>();

        [Tooltip("UI层分组（用于网格排列展示）")]
        public int Column;
        [Tooltip("UI层行")]
        public int Row;
    }

    /// <summary>
    /// 天赋节点运行时状态（存档数据的一部分）
    /// </summary>
    [Serializable]
    public class TalentNodeState
    {
        public string NodeId;
        public int CurrentLevel;     // 当前投入点数
        public bool IsUnlocked => CurrentLevel > 0;
    }

    /// <summary>
    /// 天赋节点运行时数据（可缓存的计算结果）
    /// </summary>
    public readonly struct TalentNodeRuntime
    {
        public readonly string NodeId;
        public readonly string DisplayName;
        public readonly ETalentType TalentType;
        public readonly float Value;           // 单点数值
        public readonly int CurrentLevel;
        public readonly int MaxLevel;
        public readonly ETalentNodeState State;
        public readonly bool IsMaxLevel;

        public float TotalValue => Value * CurrentLevel;

        public TalentNodeRuntime(string nodeId, string displayName, ETalentType type,
            float value, int currentLevel, int maxLevel, ETalentNodeState state)
        {
            NodeId = nodeId;
            DisplayName = displayName;
            TalentType = type;
            Value = value;
            CurrentLevel = currentLevel;
            MaxLevel = maxLevel;
            State = state;
            IsMaxLevel = currentLevel >= maxLevel;
        }
    }
}
