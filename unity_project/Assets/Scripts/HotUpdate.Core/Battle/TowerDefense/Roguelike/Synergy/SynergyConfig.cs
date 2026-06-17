using GAS;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// Build流派协同配置 ScriptableObject。
    /// 定义协同规则：同类型塔数量达到阈值时触发增益效果。
    /// 
    /// 示例：
    /// - 3个箭塔 → 攻速+20%
    /// - 冰塔+炮塔组合 → 溅射冻结
    /// - 技能暴击流 → 技能伤害提升
    /// 
    /// 使用方式：
    /// - 创建 SynergyConfig 资产，配置 RequiredTowerType / RequiredCount / BonusEffect
    /// - 挂载到 TowerDefenseGlobalConfig.SynergyConfigs 中
    /// - BuildSynergySystem 自动检测并应用
    /// </summary>
    [CreateAssetMenu(fileName = "SynergyConfig", menuName = "TowerDefense/Roguelike/Synergy Config", order = 210)]
    public class SynergyConfig : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("唯一标识")]
        public string SynergyId;

        [Tooltip("流派名称（展示用）")]
        public string SynergyName = "协同效果";

        [Tooltip("流派描述")]
        [TextArea(2, 4)]
        public string Description = "当同类塔达到一定数量时触发增益";

        [Header("Condition")]
        [Tooltip("要求的塔类型")]
        public ETDTowerType RequiredTowerType = ETDTowerType.ArrowTower;

        [Tooltip("要求数量（达到此数量时触发）")]
        [Min(1)]
        public int RequiredCount = 3;

        [Header("Bonus")]
        [Tooltip("增益 GameplayEffect。DurationPolicy=Infinite 表示永久生效，移除塔时自动移除。")]
        public GameplayEffectDefinition BonusEffect;

        [Header("Advanced")]
        [Tooltip("额外标签条件（如 \"SlowSpecialist\"）。空=无额外条件")]
        public string RequiredTag = string.Empty;

        [Tooltip("是否堆叠（每多一个塔额外叠加一层）")]
        public bool IsStackable;

        [Tooltip("堆叠时每层的附加值")]
        public float StackValue = 0.1f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(SynergyId))
                SynergyId = name;
        }
#endif
    }
}
