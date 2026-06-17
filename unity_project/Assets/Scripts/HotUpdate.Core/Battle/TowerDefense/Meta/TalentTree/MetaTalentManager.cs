using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 局外天赋树管理器（Singleton，独立于 BattleEngine）。
    /// 
    /// 职责：
    /// - 加载 TalentTreeConfig 配置
    /// - 管理天赋点获取/消耗
    /// - 解锁/升级天赋节点
    /// - 计算天赋效果汇总
    /// - 读写 MetaSaveData
    /// 
    /// Meta vs Run 分离：
    /// - 本类在局外运行，不依赖 BattleEngine
    /// - 通过 MetaToRunBridge 将效果传入局内
    /// </summary>
    public class MetaTalentManager
    {
        private static MetaTalentManager _instance;

        public static MetaTalentManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new MetaTalentManager();
                return _instance;
            }
        }

        private TalentTreeConfig _config;
        private MetaSaveData _saveData;

        /// <summary>全量天赋节点列表（运行时）</summary>
        private readonly List<TalentNodeRuntime> _allNodes = new();

        /// <summary>节点ID → 索引 映射</summary>
        private readonly Dictionary<string, int> _nodeIndexMap = new();

        /// <summary>按天赋类型分组的效果汇总缓存</summary>
        private readonly Dictionary<ETalentType, float> _effectCache = new();

        public TalentTreeConfig Config => _config;
        public IReadOnlyList<TalentNodeRuntime> AllNodes => _allNodes;
        public int AvailableTalentPoints => _saveData?.AvailableTalentPoints ?? 0;

        // ===== 初始化 =====

        public void Initialize(TalentTreeConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _saveData = MetaSaveService.Load();

            BuildNodeCache();
            Debug.Log($"[MetaTalentManager] Initialized. Nodes: {_allNodes.Count}, Points: {_saveData.AvailableTalentPoints}");
        }

        private void BuildNodeCache()
        {
            _allNodes.Clear();
            _nodeIndexMap.Clear();
            _effectCache.Clear();

            if (_config == null) return;

            for (int i = 0; i < _config.Nodes.Length; i++)
            {
                var def = _config.Nodes[i];
                if (string.IsNullOrEmpty(def.NodeId)) continue;

                _saveData.EnsureNodeExists(def.NodeId);
                var state = _saveData.GetNodeState(def.NodeId);
                var nodeState = ResolveNodeState(def, state);

                _nodeIndexMap[def.NodeId] = _allNodes.Count;
                _allNodes.Add(new TalentNodeRuntime(
                    def.NodeId, def.DisplayName, def.TalentType,
                    def.Value, state?.CurrentLevel ?? 0, def.MaxLevel, nodeState));
            }

            RebuildEffectCache();
        }

        private ETalentNodeState ResolveNodeState(TalentNodeDefinition def, TalentNodeState state)
        {
            if (state == null) return ETalentNodeState.Locked;

            // 已满级
            if (state.CurrentLevel >= def.MaxLevel && def.MaxLevel > 0)
                return ETalentNodeState.Unlocked;

            // 已解锁但可继续升级
            if (state.CurrentLevel > 0)
                return ETalentNodeState.Unlocked;

            // 检查前置条件
            if (ArePrerequisitesMet(def))
            {
                // 检查是否有足够天赋点
                if (_saveData.AvailableTalentPoints >= def.Cost)
                    return ETalentNodeState.Available;
            }

            return ETalentNodeState.Locked;
        }

        private bool ArePrerequisitesMet(TalentNodeDefinition def)
        {
            if (def.PrerequisiteIds == null || def.PrerequisiteIds.Length == 0)
                return true;

            foreach (var prereqId in def.PrerequisiteIds)
            {
                var prereqState = _saveData.GetNodeState(prereqId);
                if (prereqState == null || prereqState.CurrentLevel <= 0)
                    return false;
            }
            return true;
        }

        // ===== 操作 =====

        /// <summary>尝试解锁/升级天赋节点</summary>
        public bool TryUnlockNode(string nodeId)
        {
            if (_config == null) return false;

            var state = _saveData.GetNodeState(nodeId);
            if (state == null) return false;

            // 查找节点定义
            TalentNodeDefinition def = null;
            for (int i = 0; i < _config.Nodes.Length; i++)
            {
                if (_config.Nodes[i].NodeId == nodeId)
                {
                    def = _config.Nodes[i];
                    break;
                }
            }
            if (def == null) return false;

            // 检查是否已达到最大等级
            if (state.CurrentLevel >= def.MaxLevel && def.MaxLevel > 0)
                return false;

            // 检查前置条件
            if (state.CurrentLevel == 0 && !ArePrerequisitesMet(def))
                return false;

            // 检查天赋点
            if (_saveData.AvailableTalentPoints < def.Cost)
                return false;

            // 消耗天赋点
            _saveData.AvailableTalentPoints -= def.Cost;
            _saveData.SpentTalentPoints += def.Cost;
            state.CurrentLevel++;

            // 重建缓存
            RebuildNodeCacheSingle(def, state);
            RebuildEffectCache();

            MetaSaveService.Save(_saveData);
            Debug.Log($"[MetaTalentManager] Unlocked node '{nodeId}' Lv.{state.CurrentLevel}. Points left: {_saveData.AvailableTalentPoints}");

            return true;
        }

        /// <summary>添加天赋点（局后奖励）</summary>
        public void AddTalentPoints(int amount)
        {
            if (_saveData == null || amount <= 0) return;
            _saveData.AvailableTalentPoints += amount;
            MetaSaveService.Save(_saveData);
            RebuildNodeStateCache();
        }

        /// <summary>记录Run结束（统计更新 + 天赋点奖励）</summary>
        public void OnRunCompleted(bool victory, int waveReached, int totalGoldEarned)
        {
            if (_saveData == null) return;

            _saveData.TotalRuns++;
            if (victory) _saveData.TotalWins++;
            if (waveReached > _saveData.BestWave) _saveData.BestWave = waveReached;
            _saveData.TotalGoldEarned += totalGoldEarned;

            // 天赋点奖励：每局 +1，胜利额外 +2
            int reward = 1;
            if (victory) reward += 2;
            AddTalentPoints(reward);
        }

        // ===== 效果查询 =====

        /// <summary>获取某天赋类型的总效果值</summary>
        public float GetEffectValue(ETalentType type)
        {
            _effectCache.TryGetValue(type, out float value);
            return value;
        }

        /// <summary>获取所有天赋效果汇总（供 MetaToRunBridge 使用）</summary>
        public Dictionary<ETalentType, float> GetAllEffects()
        {
            return new Dictionary<ETalentType, float>(_effectCache);
        }

        /// <summary>获取天赋节点运行时数据</summary>
        public TalentNodeRuntime GetNode(string nodeId)
        {
            if (_nodeIndexMap.TryGetValue(nodeId, out int idx) && idx < _allNodes.Count)
                return _allNodes[idx];
            return default;
        }

        // ===== 缓存管理 =====

        private void RebuildNodeCacheSingle(TalentNodeDefinition def, TalentNodeState state)
        {
            if (!_nodeIndexMap.TryGetValue(def.NodeId, out int idx) || def.MaxLevel <= 0)
                return;

            var nodeState = ResolveNodeState(def, state);
            _allNodes[idx] = new TalentNodeRuntime(
                def.NodeId, def.DisplayName, def.TalentType,
                def.Value, state.CurrentLevel, def.MaxLevel, nodeState);
        }

        private void RebuildNodeStateCache()
        {
            if (_config == null) return;
            for (int i = 0; i < _config.Nodes.Length; i++)
            {
                var def = _config.Nodes[i];
                if (string.IsNullOrEmpty(def.NodeId)) continue;
                var state = _saveData.GetNodeState(def.NodeId);
                RebuildNodeCacheSingle(def, state);
            }
        }

        private void RebuildEffectCache()
        {
            _effectCache.Clear();
            foreach (var node in _allNodes)
            {
                if (node.CurrentLevel <= 0) continue;
                if (!_effectCache.ContainsKey(node.TalentType))
                    _effectCache[node.TalentType] = 0f;
                _effectCache[node.TalentType] += node.TotalValue;
            }
        }
    }
}
