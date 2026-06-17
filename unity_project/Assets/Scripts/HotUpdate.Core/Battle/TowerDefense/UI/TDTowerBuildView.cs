using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    /// <summary>
    /// 防御塔建造UI — Prepare阶段显示可建造塔列表，点击后发射 UIRequestBuildTower 事件。
    /// 
    /// 数据流：
    ///   BattlePhaseChanged(Prepare) → 显示本View
    ///   用户点击塔按钮 → Emit UIRequestBuildTower → GameFlowSystem → TowerPlacementSystem
    ///   BattlePhaseChanged(Combat) → 隐藏本View
    /// 
    /// UI层约束（Phase 6）：
    /// - 只负责发事件
    /// - 不允许直接调用 TowerPlacementSystem 内部逻辑
    /// 
    /// 建议 Prefab 层级结构（自动绑定用）：
    ///   TDTowerBuildView (root)
    ///   ├── Transform_TowerList (空节点，用于动态创建塔按钮)
    ///   └── Btn_Close (Button)
    /// 
    /// 塔按钮模板 TDTowerBuildButton（独立 Prefab）：
    ///   TowerBuildButton
    ///   ├── Img_TowerIcon (Image)
    ///   ├── Txt_TowerName (TextMeshProUGUI)
    ///   ├── Txt_TowerCost (TextMeshProUGUI)
    ///   └── Button (Button 组件，在根节点)
    /// </summary>
    public class TDTowerBuildView : ViewBase<TDBattleUIBridge>
    {
        private TDBattleUIBridge _bridge;

        [UI] private Transform Transform_TowerList;
        [UI] private Button Btn_Close;

        /// <summary>可建造的塔配置列表（从全局配置或单独注入）</summary>
        private List<TowerConfig> _availableTowers = new();

        /// <summary>阶段订阅</summary>
        private IDisposable _phaseSub;

        protected override UniTask OnOpen(TDBattleUIBridge bridge)
        {
            _bridge = bridge;
            if (_bridge == null || !_bridge.IsValid)
            {
                Debug.LogError("[TDTowerBuildView] Invalid battle bridge!");
                return UniTask.CompletedTask;
            }

            // 加载可建造塔配置（优先从 TowerPlacementSystem 的配置获取）
            LoadAvailableTowers();

            // 创建塔按钮
            BuildTowerButtons();

            // 监听阶段变化（非 Prepare 阶段自动关闭）
            _phaseSub = _bridge.Subscribe<BattlePhaseChangedEvent>(
                TDEventIds.BattlePhaseChanged, OnPhaseChanged);

            // 关闭按钮
            BindClick(Btn_Close, CloseClicked);

            return UniTask.CompletedTask;
        }

        protected override UniTask OnClose(object result)
        {
            _phaseSub?.Dispose();
            _phaseSub = null;

            _availableTowers.Clear();
            _bridge = null;
            return UniTask.CompletedTask;
        }

        // ===== 加载数据 =====

        private void LoadAvailableTowers()
        {
            // 从 TDBattleEngine 的 TowerDefenseGlobalConfig 获取 TowerConfig 列表
            // 注意：当前 GlobalConfig 没有直接持有 TowerConfig[]，需要通过其他方式加载。
            // 方案1：在 GlobalConfig 中添加 AvailableTowers 字段（见 Todo 7）
            // 方案2：通过 Resources/Addressables 加载所有 TowerConfig ScriptableObject

            // 这里使用 Bridge 的 TDContext 访问 TowerPlacementSystem 的配置
            // TowerPlacementSystem 通过 TowerBuilderComponent 持有 TDConfig。
            // 临时方案：从 Bridge 上下文获取 Engine 再取 Config。

            var engine = _bridge.Context?.Engine as TDBattleEngine;
            var config = engine?.TDConfig;
            if (config != null && config.AvailableTowers != null)
            {
                _availableTowers.AddRange(config.AvailableTowers);
            }

            Debug.Log($"[TDTowerBuildView] Loaded {_availableTowers.Count} tower configs.");
        }

        private void BuildTowerButtons()
        {
            if (Transform_TowerList == null)
            {
                Debug.LogWarning("[TDTowerBuildView] Transform_TowerList not bound!");
                return;
            }

            // 清除旧按钮
            for (int i = Transform_TowerList.childCount - 1; i >= 0; i--)
            {
                var child = Transform_TowerList.GetChild(i);
                if (child != null)
                    UnityEngine.Object.Destroy(child.gameObject);
            }

            // 创建新按钮
            foreach (var towerConfig in _availableTowers)
            {
                CreateTowerButton(towerConfig);
            }
        }

        private void CreateTowerButton(TowerConfig config)
        {
            if (config == null || Transform_TowerList == null) return;

            var go = new GameObject($"BtnTower_{config.TowerName}", typeof(RectTransform));
            go.transform.SetParent(Transform_TowerList, false);

            var btn = go.AddComponent<Button>();
            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.3f, 1f);

            // 文本（名称 + 费用）
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = $"{config.TowerName}\n费用: {config.BuildCost}金币\n范围: {config.AttackRange}m";
            text.fontSize = 14;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            // RectTransform 设置
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(180, 80);

            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;

            // 点击事件
            var capturedConfig = config;
            btn.onClick.AddListener(() => OnTowerButtonClicked(capturedConfig));
        }

        // ===== 事件处理 =====

        private void OnTowerButtonClicked(TowerConfig config)
        {
            if (_bridge == null || !_bridge.IsValid) return;

            // 只发射事件，不直接调用 TowerPlacementSystem
            _bridge.EventBus.Emit(TDEventIds.UIRequestBuildTower,
                new UIRequestBuildTowerEvent(config.TowerType));

            Debug.Log($"[TDTowerBuildView] Requested build: {config.TowerName} ({config.TowerType})");
        }

        private void OnPhaseChanged(BattlePhaseChangedEvent evt)
        {
            if (evt.CurrentPhase != EBattlePhase.Prepare)
            {
                // 非准备阶段关闭建造界面
                Close();
            }
        }

        private async UniTask CloseClicked()
        {
            Close();
            await UniTask.CompletedTask;
        }
    }
}
