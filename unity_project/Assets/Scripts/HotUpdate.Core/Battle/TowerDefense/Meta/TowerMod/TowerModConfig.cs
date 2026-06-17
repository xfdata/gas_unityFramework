using System;
using GAS;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 塔插件/Mod类型枚举
    /// </summary>
    public enum ETowerModType
    {
        /// <summary>暴击插件（增加暴击率和暴击伤害）</summary>
        Crit,
        /// <summary>冰冻附加（攻击附带减速效果）</summary>
        Freeze,
        /// <summary>溅射（AOE伤害）</summary>
        Splash,
        /// <summary>穿透（攻击穿透多个敌人）</summary>
        Pierce,
        /// <summary>生命偷取（造成伤害回复HP）</summary>
        LifeSteal,
        /// <summary>攻速提升（纯属性加成）</summary>
        AttackSpeed,
        /// <summary>范围扩展</summary>
        RangeBoost,
        /// <summary>自定义（完全通过 GameplayEffect 驱动）</summary>
        Custom,
    }

    /// <summary>
    /// 塔插件/Mod 配置 ScriptableObject。
    /// 
    /// 每个 Mod 可选两种生效方式：
    /// 1. 属性修改器（ModifierEntry[]）：直接修改 CombatAttributeComponent
    /// 2. GameplayEffect：通过 GAS 施加持续效果（支持复杂Buff机制）
    /// 
    /// Mod 叠加规则：同一类型 Mod 只能挂载一个（防滥用），不同类型可叠加。
    /// </summary>
    [CreateAssetMenu(fileName = "TowerModConfig", menuName = "TowerDefense/Tower Mod Config", order = 210)]
    public class TowerModConfig : ScriptableObject
    {
        [Header("Identity")]
        public string ModId;
        public string DisplayName;
        public string Description;
        public ETowerModType ModType;

        [Header("Restrictions")]
        [Tooltip("可挂载的塔类型（空=所有类型）")]
        public ETDTowerType[] AllowedTowerTypes = Array.Empty<ETDTowerType>();

        [Tooltip("是否唯一（同一塔只能挂载一个同类Mod）")]
        public bool IsUnique = true;

        [Tooltip("挂载消耗金币")]
        public int Cost;

        [Header("Attribute Modifiers (Direct)")]
        [Tooltip("直接属性修改器（CombatAttributeComponent）")]
        public ModifierEntry[] AttributeModifiers = Array.Empty<ModifierEntry>();

        [Header("GAS Effect (Advanced)")]
        [Tooltip("通过 GAS 施加的持续效果（Buff/技能触发等）")]
        public GameplayEffectDefinition AppliedEffect;

        /// <summary>单个属性修改项</summary>
        [Serializable]
        public class ModifierEntry
        {
            [Tooltip("属性ID (见 CombatAttributeIds)")]
            public int AttributeId;
            [Tooltip("操作类型")]
            public AttributeModifierOp Op = AttributeModifierOp.Add;
            [Tooltip("值")]
            public float Value;
        }
    }
}
