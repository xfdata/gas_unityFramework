using System;
using GAS;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 濉旀彃浠?Mod绫诲瀷鏋氫妇
    /// </summary>
    public enum ETowerModType
    {
        /// <summary>鏆村嚮鎻掍欢锛堝鍔犳毚鍑荤巼鍜屾毚鍑讳激瀹筹級</summary>
        Crit,
        /// <summary>鍐板喕闄勫姞锛堟敾鍑婚檮甯﹀噺閫熸晥鏋滐級</summary>
        Freeze,
        /// <summary>婧呭皠锛圓OE浼ゅ锛?/summary>
        Splash,
        /// <summary>绌块€忥紙鏀诲嚮绌块€忓涓晫浜猴級</summary>
        Pierce,
        /// <summary>鐢熷懡鍋峰彇锛堥€犳垚浼ゅ鍥炲HP锛?/summary>
        LifeSteal,
        /// <summary>鏀婚€熸彁鍗囷紙绾睘鎬у姞鎴愶級</summary>
        AttackSpeed,
        /// <summary>鑼冨洿鎵╁睍</summary>
        RangeBoost,
        /// <summary>鑷畾涔夛紙瀹屽叏閫氳繃 GameplayEffect 椹卞姩锛?/summary>
        Custom,
    }

    /// <summary>
    /// 濉旀彃浠?Mod 閰嶇疆 ScriptableObject銆?
    /// 
    /// 姣忎釜 Mod 鍙€変袱绉嶇敓鏁堟柟寮忥細
    /// 1. 灞炴€т慨鏀瑰櫒锛圡odifierEntry[]锛夛細鐩存帴淇敼 CombatAttributeComponent
    /// 2. GameplayEffect锛氶€氳繃 GAS 鏂藉姞鎸佺画鏁堟灉锛堟敮鎸佸鏉侭uff鏈哄埗锛?
    /// 
    /// Mod 鍙犲姞瑙勫垯锛氬悓涓€绫诲瀷 Mod 鍙兘鎸傝浇涓€涓紙闃叉互鐢級锛屼笉鍚岀被鍨嬪彲鍙犲姞銆?
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
        [Tooltip("鍙寕杞界殑濉旂被鍨嬶紙绌?鎵€鏈夌被鍨嬶級")]
        public ETDTowerType[] AllowedTowerTypes = Array.Empty<ETDTowerType>();

        [Tooltip("鏄惁鍞竴锛堝悓涓€濉斿彧鑳芥寕杞戒竴涓悓绫籑od锛?")]
        public bool IsUnique = true;

        [Tooltip("鎸傝浇娑堣€楅噾甯?")]
        public int Cost;

        [Header("Attribute Modifiers (Direct)")]
        [Tooltip("鐩存帴灞炴€т慨鏀瑰櫒锛圕ombatAttributeComponent锛?")]
        public ModifierEntry[] AttributeModifiers = Array.Empty<ModifierEntry>();

        [Header("GAS Effect (Advanced)")]
        [Tooltip("閫氳繃 GAS 鏂藉姞鐨勬寔缁晥鏋滐紙Buff/鎶€鑳借Е鍙戠瓑锛?")]
        public GameplayEffectDefinition AppliedEffect;

        /// <summary>鍗曚釜灞炴€т慨鏀归」</summary>
        [Serializable]
        public class ModifierEntry
        {
            [Tooltip("灞炴€D (瑙?CombatAttributeIds)")]
            public int AttributeId;
            [Tooltip("鎿嶄綔绫诲瀷")]
            public AttributeModifierOp Op = AttributeModifierOp.Add;
            [Tooltip("鍊?")]
            public float Value;
        }
    }
}
