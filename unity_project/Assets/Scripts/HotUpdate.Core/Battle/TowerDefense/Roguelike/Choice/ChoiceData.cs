namespace TowerDefense
{
    /// <summary>
    /// 杩愯鏃堕€夋嫨鏁版嵁缁撴瀯锛坰truct锛屾爤鍒嗛厤锛岄浂GC锛夈€?
    /// 渚沀I灞傛秷璐癸紝鍖呭惈閫夋嫨閰嶇疆鐨勮В鏋愬悗鏁版嵁銆?
    /// UI灞傞€氳繃 RoguelikeChoiceSystem.GetCurrentChoices() 鑾峰彇姝ゆ暟鎹€?
    /// </summary>
    public readonly struct ChoiceData
    {
        /// <summary>鏉ユ簮閰嶇疆寮曠敤</summary>
        public readonly ChoiceConfig SourceConfig;

        /// <summary>灞曠ず鏍囬</summary>
        public readonly string Title;

        /// <summary>灞曠ず鎻忚堪</summary>
        public readonly string Description;

        /// <summary>娑堣€楅噾甯?/summary>
        public readonly int Cost;

        /// <summary>鏄惁鍏嶈垂</summary>
        public readonly bool IsFree;

        /// <summary>棰勮鏂囨湰锛堝 "鏀婚€?+30%"銆?鑼冨洿 +20%"銆?鍑忛€熸晥鏋滃寮?锛?/summary>
        public readonly string PreviewText;

        /// <summary>寮哄寲绫诲埆</summary>
        public readonly EChoiceCategory Category;

        /// <summary>鐩爣绫诲瀷</summary>
        public readonly EChoiceTarget TargetType;

        /// <summary>鏁板€间慨楗帮紙鍘熷鍊硷紝鏈В鏋愶級</summary>
        public readonly float ValueModifier;

        /// <summary>鍞竴鏍囪瘑</summary>
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
        /// 鏍规嵁閰嶇疆鐢熸垚棰勮鏂囨湰
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
