using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace TowerDefense
{
    /// <summary>
    /// 波次路径条目 — 描述某波次中一条路径的敌人配置。
    /// 
    /// 支持同一波次从多条路径同时进攻。
    /// </summary>
    [Serializable]
    public class WavePathEntry
    {
        /// <summary>
        /// 路径ID（用于查找 WaypointPath）
        /// </summary>
        public string PathId;

        /// <summary>
        /// 路径引用（直接在Inspector中拖拽赋值）
        /// </summary>
        public WaypointPath Path;

        /// <summary>
        /// 该路径上的敌人配置条目
        /// </summary>
        public WaveEnemyEntry[] EnemyEntries;

        /// <summary>
        /// 该路径的生成间隔（覆盖 WaveConfig 的全局间隔）
        /// <= 0 时使用 WaveConfig.SpawnInterval
        /// </summary>
        public float SpawnIntervalOverride = -1f;

        /// <summary>
        /// 获取实际的生成间隔
        /// </summary>
        public float GetSpawnInterval(float defaultInterval)
        {
            return SpawnIntervalOverride > 0f ? SpawnIntervalOverride : defaultInterval;
        }

        /// <summary>
        /// 计算该路径的总生成数
        /// </summary>
        public int GetTotalCount()
        {
            if (EnemyEntries == null || EnemyEntries.Length == 0)
                return 0;

            int total = 0;
            for (int i = 0; i < EnemyEntries.Length; i++)
            {
                if (EnemyEntries[i] != null)
                    total += EnemyEntries[i].Count;
            }
            return total;
        }
    }

    /// <summary>
    /// 波次敌人条目 — 描述某一条路径上的一个敌人配置。
    /// </summary>
    [Serializable]
    public class WaveEnemyEntry
    {
        /// <summary>
        /// 敌人配置
        /// </summary>
        [FormerlySerializedAs("config")]
        public TDEnemyConfig Config;

        /// <summary>
        /// 生成数量
        /// </summary>
        [FormerlySerializedAs("count")]
        public int Count = 1;

        /// <summary>
        /// 生成间隔覆盖（<= 0 时使用父级间隔）
        /// </summary>
        public float SpawnIntervalOverride = -1f;

        /// <summary>
        /// 该敌人的波次等级偏移（用于动态调整属性）
        /// </summary>
        public int WaveLevelOffset = 0;
    }
}
