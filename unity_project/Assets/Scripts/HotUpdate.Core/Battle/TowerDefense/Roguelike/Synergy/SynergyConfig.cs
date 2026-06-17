using GAS;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// Build娴佹淳鍗忓悓閰嶇疆 ScriptableObject銆?
    /// 瀹氫箟鍗忓悓瑙勫垯锛氬悓绫诲瀷濉旀暟閲忚揪鍒伴槇鍊兼椂瑙﹀彂澧炵泭鏁堟灉銆?
    /// 
    /// 绀轰緥锛?
    /// - 3涓濉?鈫?鏀婚€?20%
    /// - 鍐板+鐐缁勫悎 鈫?婧呭皠鍐荤粨
    /// - 鎶€鑳芥毚鍑绘祦 鈫?鎶€鑳戒激瀹虫彁鍗?
    /// 
    /// 浣跨敤鏂瑰紡锛?
    /// - 鍒涘缓 SynergyConfig 璧勪骇锛岄厤缃?RequiredTowerType / RequiredCount / BonusEffect
    /// - 鎸傝浇鍒?TowerDefenseGlobalConfig.SynergyConfigs 涓?
    /// - BuildSynergySystem 鑷姩妫€娴嬪苟搴旂敤
    /// </summary>
    [CreateAssetMenu(fileName = "SynergyConfig", menuName = "TowerDefense/Roguelike/Synergy Config", order = 210)]
    public class SynergyConfig : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("鍞竴鏍囪瘑")]
        public string SynergyId;

        [Tooltip("娴佹淳鍚嶇О锛堝睍绀虹敤锛?")]
        public string SynergyName = "鍗忓悓鏁堟灉";

        [Tooltip("娴佹淳鎻忚堪")]
        [TextArea(2, 4)]
        public string Description = "褰撳悓绫诲杈惧埌涓€瀹氭暟閲忔椂瑙﹀彂澧炵泭";

        [Header("Condition")]
        [Tooltip("闇€姹傜殑濉旂被鍨?")]
        public ETDTowerType RequiredTowerType = ETDTowerType.ArrowTower;

        [Tooltip("闇€姹傛暟閲忥紙杈惧埌姝ゆ暟閲忔椂瑙﹀彂锛?")]
        [Min(1)]
        public int RequiredCount = 3;

        [Header("Bonus")]
        [Tooltip("澧炵泭 GameplayEffect銆?DurationPolicy=Infinite 琛ㄧず姘镐箙鐢熸晥锛岀Щ闄ゅ鏃惰嚜鍔ㄧЩ闄ゃ€?")]
        public GameplayEffectDefinition BonusEffect;

        [Header("Advanced")]
        [Tooltip("棰濆鏍囩鏉′欢锛堝 \"SlowSpecialist\"锛夈€傜┖=鏃犻澶栨潯浠?")]
        public string RequiredTag = string.Empty;

        [Tooltip("鏄惁鍫嗗彔锛堟瘡澶氫竴涓棰濆鍙犲姞涓€灞傦級")]
        public bool IsStackable;

        [Tooltip("鍫嗗彔鏃舵瘡灞傜殑闄勫姞鍊?")]
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
