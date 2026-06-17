namespace TowerDefense
{
    /// <summary>
    /// 运行时选择数据结构（struct，栈分配，零GC）。
    /// 供UI层消费，包含选择配置的解析后数据。
    /// UI层通过 RoguelikeChoiceSystem.CurrentChoices 获取此数据。
    /// </summary>
    public readonly struct ChoiceData
    {
        /// <summary>来源配置引用</summary>
        public readonly ChoiceConfig SourceConfig;

        /// <summary>展示标题</summary>
        public readonly string Title;

        /// <summary>展示描述</summary>
        public readonly string Description;

        /// <summary>消耗金币</summary>
        public readonly int Cost;

        /// <summary>是否免费</summary>
        public readonly bool IsFree;

        /// <summary>预览文本（如 "攻速 +30%"、"范围 +20%"、"减速效果增强"）</summary>
        public readonly string PreviewText;

        /// <summary>强化类别</summary>
        public readonly EChoiceCategory Category;

        /// <summary>目标类型</summary>
        public readonly EChoiceTarget TargetType;

        /// <summary>数值修饰（原始值，未解析）</summary>
        public readonly float ValueModifier;

        /// <summary>唯一标识</summary>
        public readonly string ChoiceId;

        public ChoiceData(ChoiceConfig config)
        {
            SourceConfig = config;
            Title = config != null ? config.Title : string.Empty;
            Description = config != null ? config.Description : string.Empty;
            Cost = config != null ? config.Cost : 0;
            IsFree = config != null && config.IsFree;
            Category = config != null ? config.Category : EChoiceCategory.TowerBuff;
            TargetType = config != null ? config.TargetType : EChoiceTarget.AllTowers;
            ValueModifier = config != null ? config.ValueModifier : 1f;
            ChoiceId = config != null ? config.ChoiceId : string.Empty;
            PreviewText = BuildPreviewText(config);
        }

        /// <summary>
        /// 根据配置生成预览文本
        /// </summary>
        private static string BuildPreviewText(ChoiceConfig config)
        {
            if (config == null) return string.Empty;

            float delta = (config.ValueModifier - 1f) * 100f;
            string prefix = delta >= 0 ? "+" : string.Empty;

            return config.Category switch
            {
                EChoiceCategory.TowerBuff => $"{prefix}{delta:F0}% {config.TargetType}",
                EChoiceCategory.SkillBuff => $"{prefix}{delta:F0}% Skill",
                EChoiceCategory.AttributeBuff => $"{prefix}{delta:F0}% Attribute",
                _ => $"{prefix}{delta:F0}%",
            };
        }
    }
}
