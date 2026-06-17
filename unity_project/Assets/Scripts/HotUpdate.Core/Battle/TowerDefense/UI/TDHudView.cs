using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    /// <summary>
    /// 战斗HUD界面 — 显示主城HP、当前波次、金币、战斗阶段。
    /// 
    /// 数据流：
    ///   BattleEvent → OnOpen注册 → 回调更新UI → OnClose反注册
    /// 
    /// UI层约束（Phase 6）：
    /// - 不直接查询战斗逻辑
    /// - 所有数据通过 BattleEvent 驱动更新
    /// - 通过 TDBattleUIBridge 订阅/反订阅事件
    /// 
    /// 建议 Prefab 层级结构（自动绑定用）：
    ///   TDHudView (root)
    ///   ├── Txt_MainCityHP (TextMeshProUGUI)
    ///   ├── Img_HPFill (Image: fillAmount)
    ///   ├── Txt_WaveInfo (TextMeshProUGUI)  — "第3/10波"
    ///   ├── Txt_Gold (TextMeshProUGUI)
    ///   ├── Txt_PhaseLabel (TextMeshProUGUI) — "准备阶段"/"战斗中"/"选择强化"
    ///   └── Btn_StartWave (Button) — Combat阶段隐藏
    /// </summary>
    public class TDHudView : ViewBase<TDBattleUIBridge>
    {
        private TDBattleUIBridge _bridge;

        // ===== 自动绑定（名称需匹配 Prefab 节点） =====
        [UI] private TextMeshProUGUI Txt_MainCityHP;
        [UI] private Image Img_HPFill;
        [UI] private TextMeshProUGUI Txt_WaveInfo;
        [UI] private TextMeshProUGUI Txt_Gold;
        [UI] private TextMeshProUGUI Txt_PhaseLabel;
        [UI] private Button Btn_StartWave;

        // ===== 订阅引用（用于 OnClose 反注册） =====
        private IDisposable _goldSub;
        private IDisposable _phaseSub;
        private IDisposable _cityDamageSub;
        private IDisposable _cityDestroyedSub;
        private IDisposable _waveStartedSub;

        protected override UniTask OnOpen(TDBattleUIBridge bridge)
        {
            _bridge = bridge;
            if (bridge == null || !bridge.IsValid)
            {
                Debug.LogError("[TDHudView] Invalid battle bridge!");
                return UniTask.CompletedTask;
            }

            // 初始刷新（一次性读取当前值）
            RefreshGold(bridge.PlayerGold);
            RefreshMainCityHP(bridge.MainCityHP, bridge.MainCityMaxHP);
            RefreshWaveInfo(bridge.CurrentWaveIndex, bridge.TotalWaveCount);
            RefreshPhase(bridge.CurrentPhase);

            // ===== 订阅 BattleEvent 驱动更新 =====
            _goldSub = bridge.Subscribe<PlayerGoldChangedEvent>(
                TDEventIds.PlayerGoldChanged, OnGoldChanged);

            _phaseSub = bridge.Subscribe<BattlePhaseChangedEvent>(
                TDEventIds.BattlePhaseChanged, OnPhaseChanged);

            _cityDamageSub = bridge.Subscribe<MainCityDamagedEvent>(
                TDEventIds.MainCityDamaged, OnCityDamaged);

            _cityDestroyedSub = bridge.Subscribe<MainCityDestroyedEvent>(
                TDEventIds.MainCityDestroyed, OnCityDestroyed);

            _waveStartedSub = bridge.Subscribe<int>(
                TDEventIds.WaveStarted, OnWaveStarted);

            // 按钮绑定
            BindClick(Btn_StartWave, OnStartWaveClicked);

            return UniTask.CompletedTask;
        }

        protected override UniTask OnClose(object result)
        {
            TDBattleUIBridge.UnsubscribeAll(
                _goldSub, _phaseSub, _cityDamageSub, _cityDestroyedSub, _waveStartedSub);
            _goldSub = _phaseSub = _cityDamageSub = _cityDestroyedSub = _waveStartedSub = null;
            _bridge = null;
            return UniTask.CompletedTask;
        }

        // ===== 事件回调（主线程安全） =====

        private void OnGoldChanged(PlayerGoldChangedEvent evt)
        {
            RefreshGold(evt.CurrentGold);
        }

        private void OnPhaseChanged(BattlePhaseChangedEvent evt)
        {
            RefreshPhase(evt.CurrentPhase);
        }

        private void OnCityDamaged(MainCityDamagedEvent evt)
        {
            RefreshMainCityHP(evt.RemainingHp, evt.MaxHp);
        }

        private void OnCityDestroyed(MainCityDestroyedEvent _)
        {
            RefreshMainCityHP(0f, 100f);
        }

        private void OnWaveStarted(int waveIndex)
        {
            RefreshWaveInfo(waveIndex, GetTotalWaves());
        }

        private async UniTask OnStartWaveClicked()
        {
            // 通过 EventBus 发射 UI 请求（不直接调用战斗逻辑）
            var bridge = GetBridge();
            if (bridge == null || !bridge.IsValid) return;

            bridge.EventBus.Emit(TDEventIds.UIRequestStartWave,
                new UIRequestStartWaveEvent());

            if (Btn_StartWave != null)
                Btn_StartWave.gameObject.SetActive(false);

            await UniTask.CompletedTask;
        }

        // ===== UI 刷新 =====

        private void RefreshGold(int gold)
        {
            if (Txt_Gold != null)
                Txt_Gold.text = $"金币: {gold}";
        }

        private void RefreshMainCityHP(float hp, float maxHp)
        {
            if (Txt_MainCityHP != null)
                Txt_MainCityHP.text = $"主城: {Mathf.CeilToInt(hp)}/{Mathf.CeilToInt(maxHp)}";

            if (Img_HPFill != null && maxHp > 0)
                Img_HPFill.fillAmount = Mathf.Clamp01(hp / maxHp);
        }

        private void RefreshWaveInfo(int currentIndex, int totalWaves)
        {
            if (Txt_WaveInfo != null)
                Txt_WaveInfo.text = totalWaves > 0
                    ? $"Wave {currentIndex + 1}/{totalWaves}"
                    : $"Wave {currentIndex + 1}";
        }

        private void RefreshPhase(EBattlePhase phase)
        {
            if (Txt_PhaseLabel != null)
            {
                Txt_PhaseLabel.text = phase switch
                {
                    EBattlePhase.Prepare => "Prepare",
                    EBattlePhase.Combat => "Combat",
                    EBattlePhase.WaveEnd => "Wave End",
                    EBattlePhase.Choice => "Choice",
                    EBattlePhase.Victory => "Victory",
                    EBattlePhase.Defeat => "Defeat",
                    _ => phase.ToString(),
                };
            }

            // StartWave 按钮：仅在 Prepare 阶段显示
            if (Btn_StartWave != null)
            {
                Btn_StartWave.gameObject.SetActive(phase == EBattlePhase.Prepare);
            }
        }

        private int GetTotalWaves()
        {
            var bridge = GetBridge();
            return bridge?.TotalWaveCount ?? 0;
        }

        /// <summary>
        /// 获取当前 Bridge（ViewBase 内部通过 param 保存需要用字段，但
        /// ViewBase 框架只在 OnOpen 时传入，此处通过遍历方式获取。
        /// 更优雅的方式是在派生类中自行保存引用。
        /// </summary>
        private TDBattleUIBridge GetBridge()
        {
            return _bridge;
        }
    }

    /// <summary>
    /// 带 Bridge 缓存的 HUD 视图（推荐使用此版本）。
    /// 
    /// 使用方式：
    ///   1. 在 UIViewConfigTable 中注册 TDHudViewBridged
    ///   2. Prefab 命名按 [UI] 属性自动绑定
    ///   3. 打开：UIRuntime.Instance.Open<TDHudViewBridged>(bridge)
    /// </summary>
    public class TDHudViewBridged : ViewBase<TDBattleUIBridge>
    {
        private TDBattleUIBridge _bridge;

        [UI] private TextMeshProUGUI Txt_MainCityHP;
        [UI] private Image Img_HPFill;
        [UI] private TextMeshProUGUI Txt_WaveInfo;
        [UI] private TextMeshProUGUI Txt_Gold;
        [UI] private TextMeshProUGUI Txt_PhaseLabel;
        [UI] private Button Btn_StartWave;

        private IDisposable _goldSub, _phaseSub, _cityDamageSub, _cityDestroyedSub, _waveStartedSub;

        protected override UniTask OnOpen(TDBattleUIBridge bridge)
        {
            _bridge = bridge;
            if (_bridge == null || !_bridge.IsValid)
            {
                Debug.LogError("[TDHudView] Invalid battle bridge!");
                return UniTask.CompletedTask;
            }

            RefreshAll();

            _goldSub = _bridge.Subscribe<PlayerGoldChangedEvent>(
                TDEventIds.PlayerGoldChanged, e => RefreshGold(e.CurrentGold));
            _phaseSub = _bridge.Subscribe<BattlePhaseChangedEvent>(
                TDEventIds.BattlePhaseChanged, e => RefreshPhase(e.CurrentPhase));
            _cityDamageSub = _bridge.Subscribe<MainCityDamagedEvent>(
                TDEventIds.MainCityDamaged, e => RefreshCityHP(e.RemainingHp, e.MaxHp));
            _cityDestroyedSub = _bridge.Subscribe<MainCityDestroyedEvent>(
                TDEventIds.MainCityDestroyed, _ => RefreshCityHP(0f, 100f));
            _waveStartedSub = _bridge.Subscribe<int>(
                TDEventIds.WaveStarted, i => RefreshWave(i, _bridge.TotalWaveCount));

            BindClick(Btn_StartWave, OnStartWaveClicked);
            return UniTask.CompletedTask;
        }

        protected override UniTask OnClose(object result)
        {
            TDBattleUIBridge.UnsubscribeAll(_goldSub, _phaseSub, _cityDamageSub, _cityDestroyedSub, _waveStartedSub);
            _goldSub = _phaseSub = _cityDamageSub = _cityDestroyedSub = _waveStartedSub = null;
            _bridge = null;
            return UniTask.CompletedTask;
        }

        private void RefreshAll()
        {
            RefreshGold(_bridge.PlayerGold);
            RefreshCityHP(_bridge.MainCityHP, _bridge.MainCityMaxHP);
            RefreshWave(_bridge.CurrentWaveIndex, _bridge.TotalWaveCount);
            RefreshPhase(_bridge.CurrentPhase);
        }

        private void RefreshGold(int gold)
        {
            if (Txt_Gold != null) Txt_Gold.text = $"金币: {gold}";
        }

        private void RefreshCityHP(float hp, float maxHp)
        {
            if (Txt_MainCityHP != null)
                Txt_MainCityHP.text = $"主城: {Mathf.CeilToInt(hp)}/{Mathf.CeilToInt(maxHp)}";
            if (Img_HPFill != null && maxHp > 0)
                Img_HPFill.fillAmount = Mathf.Clamp01(hp / maxHp);
        }

        private void RefreshWave(int current, int total)
        {
            if (Txt_WaveInfo != null)
                Txt_WaveInfo.text = total > 0 ? $"Wave {current + 1}/{total}" : $"Wave {current + 1}";
        }

        private void RefreshPhase(EBattlePhase phase)
        {
            if (Txt_PhaseLabel != null)
            {
                Txt_PhaseLabel.text = phase switch
                {
                    EBattlePhase.Prepare => "Prepare",
                    EBattlePhase.Combat => "Combat",
                    EBattlePhase.WaveEnd => "Wave End",
                    EBattlePhase.Choice => "Choice",
                    EBattlePhase.Victory => "Victory",
                    EBattlePhase.Defeat => "Defeat",
                    _ => phase.ToString(),
                };
            }
            if (Btn_StartWave != null)
                Btn_StartWave.gameObject.SetActive(phase == EBattlePhase.Prepare);
        }

        private async UniTask OnStartWaveClicked()
        {
            if (_bridge == null || !_bridge.IsValid) return;
            _bridge.EventBus.Emit(TDEventIds.UIRequestStartWave, new UIRequestStartWaveEvent());
            if (Btn_StartWave != null) Btn_StartWave.gameObject.SetActive(false);
            await UniTask.CompletedTask;
        }
    }
}
