namespace GAS
{
    /// <summary>
    /// 属性分类。用于 AttributeDef 元数据标记，驱动 clamp 规则与调试展示。
    /// </summary>
    public enum AttributeCategory
    {
        Resource,   // HP/MP 等资源属性（有上下限）
        Combat,     // Attack/Defense 等战斗属性
        Derived,    // 派生属性（由其他属性计算得出）
    }

    /// <summary>
    /// 属性定义元数据（只读）。描述一个属性的 Id/名称/默认值/分类/clamp 边界。
    /// 由 AttributeRegistry 统一注册管理，运行时只读。
    /// 替代散落的 int 常量（如 CombatAttributeIds），支持 "HP(1001)" 式调试展示。
    /// </summary>
    public readonly struct AttributeDef
    {
        /// <summary>属性 ID（与 AttributeSet.GetAttribute 使用的 id 一致）</summary>
        public readonly int Id;

        /// <summary>属性名称（调试/序列化用，唯一）</summary>
        public readonly string Name;

        /// <summary>默认初始值</summary>
        public readonly float DefaultValue;

        /// <summary>属性分类</summary>
        public readonly AttributeCategory Category;

        /// <summary>clamp 下限（HP=0；float.MinValue 表示不限）</summary>
        public readonly float MinValue;

        /// <summary>clamp 上限（HP=MaxHP；float.MaxValue 表示不限）</summary>
        public readonly float MaxValue;

        public AttributeDef(
            int id,
            string name,
            float defaultValue,
            AttributeCategory category = AttributeCategory.Combat,
            float minValue = float.MinValue,
            float maxValue = float.MaxValue)
        {
            Id = id;
            Name = name;
            DefaultValue = defaultValue;
            Category = category;
            MinValue = minValue;
            MaxValue = maxValue;
        }
    }
}
