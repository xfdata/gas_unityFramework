using GAS;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 强化选择配置 ScriptableObject。
    /// 定义单个罗吉尔选项模板：类别、展示信息、消耗、目标过滤、施加的GAS效果、随机权重。
    /// 
    /// 使用方式：
    /// - 在 Asset 数据库中创建 ChoiceConfig 资产，配置参数
    /// - 挂载到 TowerDefenseGlobalConfig.RoguelikeChoicePool 中
    /// - RoguelikeChoiceSystem 在每波结束时从池中按权重随机抽取3个
    /// 
    /// 扩展新类型：只需创建新的 ChoiceConfig 资产，无需修改代码。
    /// </summary>
    [CreateAssetMenu(fileName = "ChoiceConfig", menuName = "TowerDefense/Roguelike/Choice Config", order = 200)]
    public class ChoiceConfig : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("唯一标识，用于回溯/日志追踪")]
        public string ChoiceId;

        [Tooltip("强化类别")]
        public EChoiceCategory Category = EChoiceCategory.TowerBuff;

        [Header("Display (UI数据层)")]
        [Tooltip("展示标题")]
        public string Title = "强化选项";

        [Tooltip("展示描述")]
        [TextArea(2, 4)]
        public string Description = "选择一个强化效果";

        [Header("Economy")]
        [Tooltip("消耗金币（0=免费）")]
        [Min(0)]
        public int Cost;

        [Header("Target Filter")]
        [Tooltip("目标过滤类型")]
        public EChoiceTarget TargetType = EChoiceTarget.AllTowers;

        [Tooltip("可选的Tag过滤")]
        public string TargetTag = string.Empty;

        [Header("GAS Effect")]
        [Tooltip("施加的 GameplayEffectDefinition。DurationPolicy=Infinite 表示永久强化，DurationPolicy=Duration 表示限时强化。")]
        public GameplayEffectDefinition AppliedEffect;

        [Tooltip("数值修饰（如 1.3 表示攻速+30%，0.8 表示冷却-20%）。由系统按需使用")]
        public float ValueModifier = 1f;

        [Header("Random")]
        [Tooltip("随机权重。数值越大被抽中概率越高。0=永不出现")]
        [Min(0)]
        public int Weight = 10;

        [Header("Prerequisite")]
        [Tooltip("前置条件标签。仅当战场中存在匹配标签的塔/玩家时才可选。空数组=无条件。")]
        public string[] RequiredTags = System.Array.Empty<string>();

        /// <summary>
        /// 是否免费（Cost=0）。
        /// </summary>
        public bool IsFree => Cost <= 0;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(ChoiceId))
                ChoiceId = name;
        }
#endif
    }
}
