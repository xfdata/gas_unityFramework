using System.Collections.Generic;

namespace GAS
{
    /// <summary>
    /// 属性注册表（全局静态）。提供 Id↔Name↔Def 的只读查询与统一 clamp 服务。
    /// 注册时机由上层决定（R2 不强制全量注册，未注册属性走默认不 clamp 路径）。
    /// </summary>
    public static class AttributeRegistry
    {
        private static readonly Dictionary<int, AttributeDef> _defs = new Dictionary<int, AttributeDef>();
        private static readonly Dictionary<string, int> _nameToId = new Dictionary<string, int>();

        /// <summary>注册一个属性定义。重复注册同 Id 将覆盖。</summary>
        public static void Register(in AttributeDef def)
        {
            _defs[def.Id] = def;
            if (!string.IsNullOrEmpty(def.Name))
                _nameToId[def.Name] = def.Id;
        }

        /// <summary>按 Id 查询属性定义。</summary>
        public static bool TryGetDef(int id, out AttributeDef def) => _defs.TryGetValue(id, out def);

        /// <summary>按名称查询属性 Id。</summary>
        public static bool TryGetId(string name, out int id) => _nameToId.TryGetValue(name, out id);

        /// <summary>
        /// 按 Id 对值执行 clamp。未注册的属性直接返回原值（默认不 clamp）。
        /// 由 AttributeSet.ClampAttributeValue 调用，统一归一化数值边界。
        /// </summary>
        public static float ClampValue(int id, float value)
        {
            if (!_defs.TryGetValue(id, out var def))
                return value;
            if (value < def.MinValue)
                return def.MinValue;
            if (def.MaxValue != float.MaxValue && value > def.MaxValue)
                return def.MaxValue;
            return value;
        }

        /// <summary>清空注册表（测试/重载用）。</summary>
        public static void Clear()
        {
            _defs.Clear();
            _nameToId.Clear();
        }
    }
}
