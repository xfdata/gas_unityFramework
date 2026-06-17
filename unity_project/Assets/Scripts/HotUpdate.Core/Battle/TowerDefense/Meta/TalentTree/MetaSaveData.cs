using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// Meta 永续存档数据（局外，跨Run保留）。
    /// 
    /// 存储介质：JSON → PlayerPrefs（轻量），可扩展为文件/云端。
    /// 
    /// 数据内容：
    /// - 天赋节点解锁状态
    /// - 总可用天赋点
    /// - 总游玩局数
    /// - 累计胜利数
    /// </summary>
    [Serializable]
    public class MetaSaveData
    {
        public int Version = 1;

        // ===== 天赋系统 =====
        public int AvailableTalentPoints;                      // 可用天赋点
        public int SpentTalentPoints;                          // 已消耗天赋点
        public List<TalentNodeState> TalentNodes = new();      // 所有天赋节点状态

        // ===== 统计 =====
        public int TotalRuns;           // 总游玩局数
        public int TotalWins;           // 总胜利局数
        public int BestWave;            // 最佳波次到达

        // ===== 货币（未来扩展） =====
        public int TotalGoldEarned;     // 累计获得金币（跨局）

        // 辅助方法
        public TalentNodeState GetNodeState(string nodeId)
        {
            for (int i = 0; i < TalentNodes.Count; i++)
            {
                if (TalentNodes[i].NodeId == nodeId)
                    return TalentNodes[i];
            }
            return null;
        }

        public void EnsureNodeExists(string nodeId)
        {
            if (GetNodeState(nodeId) != null) return;
            TalentNodes.Add(new TalentNodeState { NodeId = nodeId, CurrentLevel = 0 });
        }
    }

    /// <summary>
    /// 局外存档服务：负责 MetaSaveData 的持久化读写。
    /// 
    /// 局内（Run）不依赖此服务。
    /// </summary>
    public static class MetaSaveService
    {
        private const string SAVE_KEY = "TD_MetaSaveData";
        private static MetaSaveData _cached;

        /// <summary>加载存档（内存缓存）</summary>
        public static MetaSaveData Load()
        {
            if (_cached != null) return _cached;

            try
            {
                var json = PlayerPrefs.GetString(SAVE_KEY, string.Empty);
                if (!string.IsNullOrEmpty(json))
                {
                    _cached = JsonUtility.FromJson<MetaSaveData>(json);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MetaSaveService] Failed to load save data: {e.Message}");
                _cached = null;
            }

            if (_cached == null)
            {
                _cached = new MetaSaveData();
            }

            return _cached;
        }

        /// <summary>保存存档到磁盘</summary>
        public static void Save(MetaSaveData data = null)
        {
            if (data != null) _cached = data;
            if (_cached == null) return;

            try
            {
                var json = JsonUtility.ToJson(_cached, true);
                PlayerPrefs.SetString(SAVE_KEY, json);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogError($"[MetaSaveService] Failed to save: {e.Message}");
            }
        }

        /// <summary>清空存档</summary>
        public static void Delete()
        {
            _cached = null;
            PlayerPrefs.DeleteKey(SAVE_KEY);
            PlayerPrefs.Save();
        }

        /// <summary>强制重载存档</summary>
        public static MetaSaveData Reload()
        {
            _cached = null;
            return Load();
        }
    }
}
