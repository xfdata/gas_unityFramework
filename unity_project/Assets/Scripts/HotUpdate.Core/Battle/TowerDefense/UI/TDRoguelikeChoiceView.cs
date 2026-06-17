using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 罗吉尔强化选择UI — Choice阶段显示3选1界面。
    /// 
    /// 数据流：
    ///   RoguelikeChoiceStart → 本View打开，展示 RoguelikeChoiceSystem.CurrentChoices
    ///   用户点击选项 → Emit UISelectRoguelikeOption → GameFlowSystem → RoguelikeChoiceSystem.SelectChoice()
    ///   RoguelikeChoiceSelected → 本View关闭
    /// 
    /// UI层约束（Phase 6）：
    /// - 只负责展示和发射选项索引
    /// - 数据来自 RoguelikeChoiceSystem（通过 Bridge. RoguelikeChoice）
    /// - 不支持操作战门逻辑
    /// 
    /// 建议 Prefab 层级结构（自动绑定用）：
    ///   TDRoguelikeChoiceView (root)
    ///   ├── Txt_WaveLabel (TextMeshProUGUI) — "第3波结束，选择一个强化！"
    ///   ├── Btn_Choice0 / Btn_Choice1 / Btn_Choice2 (3个选择按钮)
    ///   │   ├── Txt_ChoiceTitle (TextMeshProUGUI)
    ///   │   ├── Txt_ChoiceDesc (TextMeshProUGUI)
    ///   │   └── Txt_ChoiceCost (TextMeshProUGUI)
    ///   └── GameObject_Panel (半透明背景遮罩)
    /// 
    /// 更简单的方式：使用 choiceContainer 预制件引用 3 个相同的控件组：
    ///   ├── P0_Title / P0_Desc / P0_Cost / P0_Button
    ///   ├── P1_Title / P1_Desc / P1_Cost / P1_Button
    ///   └── P2_Title / P2_Desc / P2_Cost / P2_Button
    /// </summary>
    public class TDRoguelikeChoiceView : ViewBase<TDBattleUIBridge>
    {
        private TDBattleUIBridge _bridge;

        // ===== 3个选项的UI控件组 =====
        // 选项 0
        [UI] private TextMeshProUGUI P0_Title;
        [UI] private TextMeshProUGUI P0_Desc;
        [UI] private TextMeshProUGUI P0_Cost;
        [UI] private UnityEngine.UI.Button P0_Button;

        // 选项 1
        [UI] private TextMeshProUGUI P1_Title;
        [UI] private TextMeshProUGUI P1_Desc;
        [UI] private TextMeshProUGUI P1_Cost;
        [UI] private UnityEngine.UI.Button P1_Button;

        // 选项 2
        [UI] private TextMeshProUGUI P2_Title;
        [UI] private TextMeshProUGUI P2_Desc;
        [UI] private TextMeshProUGUI P2_Cost;
        [UI] private UnityEngine.UI.Button P2_Button;

        // 顶部标签
        [UI] private TextMeshProUGUI Txt_WaveLabel;

        // 事件订阅
        private System.IDisposable _choiceStartSub;
        private System.IDisposable _choiceSelectedSub;

        protected override UniTask OnOpen(TDBattleUIBridge bridge)
        {
            _bridge = bridge;
            if (_bridge == null || !_bridge.IsValid)
            {
                Debug.LogError("[TDRoguelikeChoiceView] Invalid battle bridge!");
                return UniTask.CompletedTask;
            }

            // 订阅罗吉尔选择事件
            _choiceStartSub = _bridge.Subscribe<RoguelikeChoiceStartEvent>(
                TDEventIds.RoguelikeChoiceStart, OnChoiceStart);
            _choiceSelectedSub = _bridge.Subscribe<ChoiceSelectedEvent>(
                TDEventIds.RoguelikeChoiceSelected, OnChoiceSelected);

            // 绑定按钮
            BindClick(P0_Button, () => OnOptionClicked(0));
            BindClick(P1_Button, () => OnOptionClicked(1));
            BindClick(P2_Button, () => OnOptionClicked(2));

            // 默认隐藏所有选项（等待 RoguelikeChoiceStart 时显示）
            SetAllOptionsVisible(false);

            return UniTask.CompletedTask;
        }

        protected override UniTask OnClose(object result)
        {
            TDBattleUIBridge.UnsubscribeAll(_choiceStartSub, _choiceSelectedSub);
            _choiceStartSub = _choiceSelectedSub = null;
            _bridge = null;
            return UniTask.CompletedTask;
        }

        // ===== 事件回调 =====

        /// <summary>
        /// 罗吉尔选择面板打开 → 填充3个选项数据
        /// </summary>
        private void OnChoiceStart(RoguelikeChoiceStartEvent evt)
        {
            if (_bridge == null || !_bridge.IsValid) return;

            var choiceSystem = _bridge.RoguelikeChoice;
            if (choiceSystem == null || choiceSystem.CurrentChoices == null)
            {
                Debug.LogError("[TDRoguelikeChoiceView] RoguelikeChoiceSystem or CurrentChoices is null!");
                return;
            }

            var choices = choiceSystem.CurrentChoices;
            int count = choices.Count;

            // 填充标题
            if (Txt_WaveLabel != null)
                Txt_WaveLabel.text = $"第{evt.WaveIndex + 1}波结束，选择一个强化！";

            // 创建 (Title, Desc, Cost, Button) 数组
            var titles = new[] { P0_Title, P1_Title, P2_Title };
            var descs = new[] { P0_Desc, P1_Desc, P2_Desc };
            var costs = new[] { P0_Cost, P1_Cost, P2_Cost };
            var buttons = new[] { P0_Button, P1_Button, P2_Button };

            for (int i = 0; i < 3; i++)
            {
                bool hasData = i < count;

                if (titles[i] != null) titles[i].text = hasData ? choices[i].Title : string.Empty;
                if (descs[i] != null) descs[i].text = hasData ? choices[i].Description : string.Empty;
                if (costs[i] != null)
                {
                    costs[i].text = hasData
                        ? (choices[i].IsFree ? "免费" : $"消耗: {choices[i].Cost}金币")
                        : string.Empty;
                }
                if (buttons[i] != null) buttons[i].gameObject.SetActive(hasData);
            }

            SetAllOptionsVisible(true);
        }

        /// <summary>
        /// 选择完成 → 隐藏所有选项
        /// </summary>
        private void OnChoiceSelected(ChoiceSelectedEvent evt)
        {
            SetAllOptionsVisible(false);
        }

        // ===== 用户交互 =====

        private async UniTask OnOptionClicked(int index)
        {
            if (_bridge == null || !_bridge.IsValid) return;

            // 只发射事件，不直接调用 RoguelikeChoiceSystem.SelectChoice()
            _bridge.EventBus.Emit(TDEventIds.UISelectRoguelikeOption,
                new UISelectRoguelikeOptionEvent(index));

            Debug.Log($"[TDRoguelikeChoiceView] Selected option {index}");

            await UniTask.CompletedTask;
        }

        // ===== 辅助 =====

        private void SetAllOptionsVisible(bool visible)
        {
            var buttons = new[] { P0_Button, P1_Button, P2_Button };
            foreach (var btn in buttons)
            {
                if (btn != null)
                    btn.gameObject.SetActive(visible);
            }
        }
    }
}
