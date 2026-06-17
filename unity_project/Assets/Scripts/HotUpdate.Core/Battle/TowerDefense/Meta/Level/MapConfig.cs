using System;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 鍦板浘閰嶇疆 ScriptableObject 鈥?瀹氫箟涓€寮犲湴鍥剧殑甯冨眬銆?
    /// 
    /// 鍖呭惈锛?
    /// - 璺緞锛堟晫浜烘部姝ょЩ鍔級
    /// - 鍙缓閫犲尯鍩?
    /// - 鎬墿鍑虹敓鐐?
    /// - 涓诲煄浣嶇疆
    /// 
    /// 鏁版嵁椹卞姩锛氭棤浠ｇ爜纭紪鐮佸湴鍥鹃€昏緫銆?
    /// </summary>
    [CreateAssetMenu(fileName = "MapConfig", menuName = "TowerDefense/Map Config", order = 220)]
    public class MapConfig : ScriptableObject
    {
        [Header("Identity")]
        public string MapId;
        public string DisplayName;
        public string Description;

        [Header("Path")]
        [Tooltip("鏁屼汉绉诲姩璺緞锛圵aypointPath寮曠敤鐨勮矾寰勯厤缃級")]
        public WaypointPath DefaultPath;

        [Header("Spawn")]
        [Tooltip("鎬墿鍑虹敓鍖哄煙锛堝璺緞妯″紡鏃舵瘡鏉¤矾寰勫彲鏈夌嫭绔嬭捣濮嬬偣锛?")]
        public SpawnPoint[] SpawnPoints = Array.Empty<SpawnPoint>();

        [Header("Build Area")]
        [Tooltip("鍙缓閫犲尯鍩燂紙鐢ㄤ笘鐣屽潗鏍囩殑鐭╁舰鍖哄煙缃戞牸鏍囪锛?")]
        public BuildArea[] BuildAreas = Array.Empty<BuildArea>();

        [Header("Main City")]
        [Tooltip("涓诲煄涓栫晫鍧愭爣浣嶇疆")]
        public Vector3 MainCityPosition = Vector3.zero;

        [Header("Visual")]
        [Tooltip("鍦板浘鑳屾櫙/鍦板舰 Prefab")]
        public GameObject MapPrefab;

        /// <summary>鎬墿鍑虹敓鐐?/summary>
        [Serializable]
        public struct SpawnPoint
        {
            public Vector3 Position;
            [Tooltip("鍏宠仈鐨勮矾寰勭储寮曪紙澶氳矾寰勭敤锛?=榛樿璺緞锛?")]
            public int PathIndex;
        }

        /// <summary>鍙缓閫犲尯鍩燂紙杞村榻愮煩褰級</summary>
        [Serializable]
        public struct BuildArea
        {
            public Vector3 Center;
            public Vector2 Size;
        }
    }
}
