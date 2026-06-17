using System;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 澶╄祴鑺傜偣绫诲瀷锛堝喅瀹氭晥鏋滀綔鐢ㄥ煙锛?
    /// </summary>
    public enum ETalentType
    {
        // === 鎴樻枟寮哄寲绫?===
        /// <summary>鍒濆閲戝竵澧炲姞</summary>
        StartingGoldBonus,
        /// <summary>涓诲煄鍒濆琛€閲?%</summary>
        MainCityHPBonus,
        /// <summary>鍏ㄥ眬濉旀敾鍑诲姏+%</summary>
        TowerAttackBonus,

        // === 濉旂郴寮哄寲绫?===
        /// <summary>绠鏀婚€?%</summary>
        ArrowTowerAttackSpeed,
        /// <summary>鐐鑼冨洿+%</summary>
        CannonTowerRange,
        /// <summary>鍐板鍑忛€熸晥鏋?%</summary>
        IceTowerSlowBonus,

        // === 缁忔祹绫?===
        /// <summary>鍑绘潃閲戝竵+%</summary>
        KillGoldBonus,
        /// <summary>寤洪€犳垚鏈檷浣?</summary>
        BuildCostReduction,
    }

    /// <summary>
    /// 澶╄祴鑺傜偣鐨勮В閿佺姸鎬?
    /// </summary>
    public enum ETalentNodeState
    {
        /// <summary>鏈В閿侊紙涓斾笉鍙В閿侊級</summary>
        Locked,
        /// <summary>鍙互瑙ｉ攣锛堝墠缃潯浠舵弧瓒筹紝鏈夊緟娑堣€楃殑澶╄祴鐐癸級</summary>
        Available,
        /// <summary>宸茶В閿?/summary>
        Unlocked,
    }

    /// <summary>
    /// 澶╄祴鏍戦厤缃?ScriptableObject銆?
    /// 瀹氫箟鎵€鏈夊ぉ璧嬭妭鐐圭殑鏍戠粨鏋勶紙鑺傜偣鍒楄〃 + 鍓嶇疆渚濊禆鍏崇郴锛夈€?
    /// 
    /// 鏁版嵁椹卞姩锛氫笉鍦ㄤ唬鐮佷腑鍐欐浠讳綍澶╄祴鑺傜偣閫昏緫銆?
    /// 鏁堟灉閫氳繃 TalentNode.EffectType + Value 椹卞姩銆?
    /// </summary>
    [CreateAssetMenu(fileName = "TalentTreeConfig", menuName = "TowerDefense/Meta/Talent Tree Config", order = 200)]
    public class TalentTreeConfig : ScriptableObject
    {
        [Tooltip("澶╄祴鏍戝悕绉?")]
        public string TreeName = "Default Talent Tree";

        [Tooltip("澶╄祴鑺傜偣鍒楄〃")]
        public TalentNodeDefinition[] Nodes = Array.Empty<TalentNodeDefinition>();
    }

    /// <summary>
    /// 鍗曚釜澶╄祴鑺傜偣閰嶇疆锛圫criptableObject 搴忓垪鍖栵級
    /// </summary>
    [Serializable]
    public class TalentNodeDefinition
    {
        [Tooltip("鍞竴鏍囪瘑")]
        public string NodeId;

        [Tooltip("灞曠ず鍚嶇О")]
        public string DisplayName;

        [Tooltip("鎻忚堪")]
        public string Description;

        [Tooltip("澶╄祴绫诲瀷")]
        public ETalentType TalentType;

        [Tooltip("鏁板€硷紙濡?30 琛ㄧず+30% 璧峰閲戝竵锛?")]
        public float Value;

        [Tooltip("瑙ｉ攣娑堣€楀ぉ璧嬬偣")]
        public int Cost = 1;

        [Tooltip("鏈€澶у彲鎶曞叆鐐规暟锛?=涓嶅彲鍗囩骇锛?=鍗曟瑙ｉ攣锛孨=鍙娆℃姇鍏ワ級")]
        public int MaxLevel = 1;

        [Tooltip("鍓嶇疆鑺傜偣ID鍒楄〃锛堝繀椤诲叏閮ㄨВ閿佹墠鑳借В閿佹湰鑺傜偣锛?")]
        public string[] PrerequisiteIds = Array.Empty<string>();

        [Tooltip("UI灞傚垎缁勶紙鐢ㄤ簬缃戞牸鎺掑垪灞曠ず锛?")]
        public int Column;
        [Tooltip("UI灞傝")]
        public int Row;
    }

    /// <summary>
    /// 澶╄祴鑺傜偣杩愯鏃剁姸鎬侊紙瀛樻。鏁版嵁鐨勪竴閮ㄥ垎锛?
    /// </summary>
    [Serializable]
    public class TalentNodeState
    {
        public string NodeId;
        public int CurrentLevel;     // 褰撳墠鎶曞叆鐐规暟
        public bool IsUnlocked => CurrentLevel > 0;
    }

    /// <summary>
    /// 澶╄祴鑺傜偣杩愯鏃舵暟鎹紙鍙紦瀛樼殑璁＄畻缁撴灉锛?
    /// </summary>
    public readonly struct TalentNodeRuntime
    {
        public readonly string NodeId;
        public readonly string DisplayName;
        public readonly ETalentType TalentType;
        public readonly float Value;           // 鍗曠偣鏁板€?
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
