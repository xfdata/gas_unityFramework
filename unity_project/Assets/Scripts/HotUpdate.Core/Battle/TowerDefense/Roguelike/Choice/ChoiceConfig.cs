using GAS;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 寮哄寲閫夋嫨閰嶇疆 ScriptableObject銆?
    /// 瀹氫箟鍗曚釜缃楀悏灏旈€夐」妯℃澘锛氱被鍒€佸睍绀轰俊鎭€佹秷鑰椼€佺洰鏍囪繃婊ゃ€佹柦鍔犵殑GAS鏁堟灉銆侀殢鏈烘潈閲嶃€?
    /// 
    /// 浣跨敤鏂瑰紡锛?
    /// - 鍦?Asset 鏁版嵁搴撲腑鍒涘缓 ChoiceConfig 璧勪骇锛岄厤缃弬鏁?
    /// - 鎸傝浇鍒?TowerDefenseGlobalConfig.RoguelikeChoicePool 涓?
    /// - RoguelikeChoiceSystem 鍦ㄦ瘡娉㈢粨鏉熸椂浠庢睜涓寜鏉冮噸闅忔満鎶藉彇3涓?
    /// 
    /// 鎵╁睍鏂扮被鍨嬶細鍙渶鍒涘缓鏂扮殑 ChoiceConfig 璧勪骇锛屾棤闇€淇敼浠ｇ爜銆?
    /// </summary>
    [CreateAssetMenu(fileName = "ChoiceConfig", menuName = "TowerDefense/Roguelike/Choice Config", order = 200)]
    public class ChoiceConfig : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("鍞竴鏍囪瘑锛岀敤浜庡洖鏀?鏃ュ織杩借釜")]
        public string ChoiceId;

        [Tooltip("寮哄寲绫诲埆")]
        public EChoiceCategory Category = EChoiceCategory.TowerBuff;

        [Header("Display (UI鏁版嵁灞?")]
        [Tooltip("灞曠ず鏍囬")]
        public string Title = "寮哄寲閫夐」";

        [Tooltip("灞曠ず鎻忚堪")]
        [TextArea(2, 4)]
        public string Description = "Choose a reinforcement effect";

        [Header("Economy")]
        [Tooltip("娑堣€楅噾甯侊紙0=鍏嶈垂锛?")]
        [Min(0)]
        public int Cost;

        [Header("Target Filter")]
        [Tooltip("鐩爣杩囨护绫诲瀷")]
        public EChoiceTarget TargetType = EChoiceTarget.AllTowers;

        [Tooltip("Optional target tag filter.")]
        public string TargetTag = string.Empty;

        [Header("GAS Effect")]
        [Tooltip("鏂藉姞鐨?GameplayEffectDefinition銆侱urationPolicy=Infinite 琛ㄧず姘镐箙寮哄寲锛孌urationPolicy=Duration 琛ㄧず闄愭椂寮哄寲銆?")]
        public GameplayEffectDefinition AppliedEffect;

        [Tooltip("鏁板€间慨楗帮紙濡?1.3 琛ㄧず鏀婚€?30%锛?.8 琛ㄧず鍐峰嵈-20%锛夈€傜敱绯荤粺鎸夐渶浣跨敤")]
        public float ValueModifier = 1f;

        [Header("Random")]
        [Tooltip("闅忔満鏉冮噸銆傛暟鍊艰秺澶ц鎶戒腑姒傜巼瓒婇珮銆?=姘镐笉鍑虹幇")]
        [Min(0)]
        public int Weight = 10;

        [Header("Prerequisite")]
        [Tooltip("鍓嶇疆鏉′欢鏍囩銆備粎褰撴垬鍦轰腑瀛樺湪鍖归厤鏍囩鐨勫/鐜╁鏃舵墠鍙€夈€傜┖鏁扮粍=鏃犳潯浠?")]
        public string[] RequiredTags = System.Array.Empty<string>();

        /// <summary>
        /// 鏄惁鍏嶈垂锛圕ost=0锛夈€?
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
