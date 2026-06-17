using System;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 鍏冲崱閰嶇疆 ScriptableObject 鈥?瀹氫箟涓€灞€娓告垙鐨勫叏閮ㄥ弬鏁般€?
    /// 
    /// 缁勫悎锛?
    /// - 鍦板浘锛圡apConfig锛?
    /// - 娉㈡锛圵aveConfig[]锛?
    /// - Boss锛圔ossConfig锛?
    /// - 闅惧害鏇茬嚎
    /// - 璧峰璧勬簮
    /// - 鍙敤闃插尽濉?
    /// 
    /// 鏁版嵁椹卞姩锛氭柊鍏冲崱 = 鏂板缓姝?ScriptableObject銆?
    /// </summary>
    [CreateAssetMenu(fileName = "LevelConfig", menuName = "TowerDefense/Level Config", order = 221)]
    public class LevelConfig : ScriptableObject
    {
        [Header("Identity")]
        public string LevelId;
        public string DisplayName;
        public string Description;

        [Header("Map")]
        [Tooltip("鏈叧鍗′娇鐢ㄧ殑鍦板浘閰嶇疆")]
        public MapConfig Map;

        [Header("Economy")]
        [Tooltip("鍒濆閲戝竵锛堣鐩?GlobalConfig.StartingGold锛?")]
        public int OverrideStartingGold = -1;

        [Tooltip("璧峰鐢熷懡鏁帮紙0=浣跨敤涓诲煄榛樿琛€閲忥級")]
        public int StartingLives;

        [Header("Available Towers")]
        [Tooltip("鏈叧鍗″厑璁稿缓閫犵殑闃插尽濉旓紙绌?鍏ㄥ眬閰嶇疆锛?")]
        public TowerConfig[] AvailableTowers = Array.Empty<TowerConfig>();

        [Header("Tower Mods")]
        [Tooltip("鏈叧鍗″彲鐢ㄧ殑濉旀彃浠讹紙绌?鍏ㄥ眬閰嶇疆锛?")]
        public TowerModConfig[] AvailableMods = Array.Empty<TowerModConfig>();

        [Header("Wave Configs")]
        [Tooltip("鏈叧鍗＄殑娉㈡閰嶇疆鍒楄〃")]
        public WaveConfig[] WaveConfigs = Array.Empty<WaveConfig>();

        [Header("Difficulty Curve")]
        [Tooltip("娉㈡鎴愰暱鍊嶇巼锛堟瘡杩囦竴娉紝鏁屼汉HP 脳 姝ゅ€硷級")]
        public float WaveHpScale = 1.1f;

        [Tooltip("娉㈡鎴愰暱閫熷害鍊嶇巼")]
        public float WaveSpeedScale = 1f;

        [Tooltip("娉㈡鍑绘潃閲戝竵鍊嶇巼")]
        public float WaveGoldScale = 1.05f;

        [Header("Boss")]
        [Tooltip("Boss閰嶇疆锛堝嚭鐜板湪鏈€缁堟尝娆℃垨鐗瑰畾娉㈡锛?")]
        public BossConfig[] BossConfigs = Array.Empty<BossConfig>();

        [Header("Meta Rewards")]
        [Tooltip("閫氬叧澶╄祴鐐瑰鍔?")]
        public int WinTalentPoints = 3;

        [Tooltip("澶辫触澶╄祴鐐规儵缃氾紙閫氬父 < WinTalentPoints锛?")]
        public int LoseTalentPoints = 1;

        // 渚挎嵎鏂规硶
        public int EffectiveStartingGold(TowerDefenseGlobalConfig globalConfig)
        {
            if (OverrideStartingGold >= 0)
                return OverrideStartingGold;
            return globalConfig?.StartingGold ?? 200;
        }
    }
}
