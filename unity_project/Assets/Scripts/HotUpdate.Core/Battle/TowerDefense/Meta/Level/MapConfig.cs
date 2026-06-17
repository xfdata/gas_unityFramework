using System;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 地图配置 ScriptableObject — 定义一张地图的布局。
    /// 
    /// 包含：
    /// - 路径（敌人沿此移动）
    /// - 可建造区域
    /// - 怪物出生点
    /// - 主城位置
    /// 
    /// 数据驱动：无代码硬编码地图逻辑。
    /// </summary>
    [CreateAssetMenu(fileName = "MapConfig", menuName = "TowerDefense/Map Config", order = 220)]
    public class MapConfig : ScriptableObject
    {
        [Header("Identity")]
        public string MapId;
        public string DisplayName;
        public string Description;

        [Header("Path")]
        [Tooltip("敌人移动路径（WaypointPath引用的路径配置）")]
        public WaypointPath DefaultPath;

        [Header("Spawn")]
        [Tooltip("怪物出生区域（多路径模式时每条路径可有独立起始点）")]
        public SpawnPoint[] SpawnPoints = Array.Empty<SpawnPoint>();

        [Header("Build Area")]
        [Tooltip("可建造区域（用世界坐标的矩形区域网格标记）")]
        public BuildArea[] BuildAreas = Array.Empty<BuildArea>();

        [Header("Main City")]
        [Tooltip("主城世界坐标位置")]
        public Vector3 MainCityPosition = Vector3.zero;

        [Header("Visual")]
        [Tooltip("地图背景/地形 Prefab")]
        public GameObject MapPrefab;

        /// <summary>怪物出生点</summary>
        [Serializable]
        public struct SpawnPoint
        {
            public Vector3 Position;
            [Tooltip("关联的路径索引（多路径用，0=默认路径）")]
            public int PathIndex;
        }

        /// <summary>可建造区域（轴对齐矩形）</summary>
        [Serializable]
        public struct BuildArea
        {
            public Vector3 Center;
            public Vector2 Size;
        }
    }
}
