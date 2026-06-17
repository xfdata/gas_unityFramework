using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    /// <summary>
    /// 鎴樻枟HUD鐣岄潰 鈥?鏄剧ず涓诲煄HP銆佸綋鍓嶆尝娆°€侀噾甯併€佹垬鏂楅樁娈点€?
    /// 
    /// 鏁版嵁娴侊細
    ///   BattleEvent 鈫?OnOpen娉ㄥ唽 鈫?鍥炶皟鏇存柊UI 鈫?OnClose鍙嶆敞鍐?
    /// 
    /// UI灞傜害鏉燂紙Phase 6锛夛細
    /// - 涓嶇洿鎺ユ煡璇㈡垬鏂楅€昏緫
    /// - 鎵€鏈夋暟鎹€氳繃 BattleEvent 椹卞姩鏇存柊
    /// - 閫氳繃 TDBattleUIBridge 璁㈤槄/鍙嶈闃呬簨浠?
    /// 
    /// 寤鸿 Prefab 灞傜骇缁撴瀯锛堣嚜鍔ㄧ粦瀹氱敤锛夛細
    ///   TDHudView (root)
    ///   鈹溾攢鈹€ Txt_MainCityHP (TextMeshProUGUI)
    ///   鈹溾攢鈹€ Img_HPFill (Image: fillAmount)
    ///   鈹溾攢鈹€ Txt_WaveInfo (TextMeshProUGUI)  鈥?"绗?/10娉?
    ///   鈹溾攢鈹€ Txt_Gold (TextMeshProUGUI)
    ///   鈹溾攢鈹€ Txt_PhaseLabel (TextMeshProUGUI) 鈥?"鍑嗗闃舵"/"鎴樻枟涓?/"閫夋嫨寮哄寲"
    ///   鈹斺攢鈹€ Btn_StartWave (Button) 鈥?Combat闃舵闅愯棌
    /// </summary>
    public class TDHudView : ViewBase<TDBattleUIBridge>
    {
        private TDBattleUIBridge _bridge;

        // ===== 鑷姩缁戝畾锛堝悕绉伴渶鍖归厤 Prefab 鑺傜偣锛?=====
        [UI] private TextMeshProUGUI Txt_MainCityHP;
        [UI] private Image Img_HPFill;
        [UI] private TextMeshProUGUI Txt_WaveInfo;
        [UI] private TextMeshProUGUI Txt_Gold;
        [UI] private TextMeshProUGUI Txt_PhaseLabel;
        [UI] private Button Btn_StartWave;

        // ===== 璁㈤槄寮曠敤锛堢敤浜?OnClose 鍙嶆敞鍐岋級 =====
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

            // 鍒濆鍒锋柊锛堜竴娆℃€ц鍙栧綋鍓嶅€硷級
            RefreshGold(bridge.PlayerGold);
            RefreshMainCityHP(bridge.MainCityHP, bridge.MainCityMaxHP);
            RefreshWaveInfo(bridge.CurrentWaveIndex, bridge.TotalWaveCount);
            RefreshPhase(bridge.CurrentPhase);

            // ===== 璁㈤槄 BattleEvent 椹卞姩鏇存柊 =====
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

            // 鎸夐挳缁戝畾
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

        // ===== 浜嬩欢鍥炶皟锛堜富绾跨▼瀹夊叏锛?=====

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
            // 閫氳繃 EventBus 鍙戝皠 UI 璇锋眰锛堜笉鐩存帴璋冪敤鎴樻枟閫昏緫锛?
            var bridge = GetBridge();
            if (bridge == null || !bridge.IsValid) return;

            bridge.EventBus.Emit(TDEventIds.UIRequestStartWave,
                new UIRequestStartWaveEvent());

            if (Btn_StartWave != null)
                Btn_StartWave.gameObject.SetActive(false);

            await UniTask.CompletedTask;
        }

        // ===== UI 鍒锋柊 =====

        private void RefreshGold(int gold)
        {
            if (Txt_Gold != null)
                Txt_Gold.text = $"閲戝竵: {gold}";
        }

        private void RefreshMainCityHP(float hp, float maxHp)
        {
            if (Txt_MainCityHP != null)
                Txt_MainCityHP.text = $"涓诲煄: {Mathf.CeilToInt(hp)}/{Mathf.CeilToInt(maxHp)}";

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

            // StartWave 鎸夐挳锛氫粎鍦?Prepare 闃舵鏄剧ず
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
        /// 鑾峰彇褰撳墠 Bridge锛圴iewBase 鍐呴儴閫氳繃 param 淇濆瓨闇€瑕佺敤瀛楁锛?
        /// 浣?ViewBase 妗嗘灦鍙湪 OnOpen 鏃朵紶鍏ワ紝姝ゅ閫氳繃閬嶅巻鏂瑰紡鑾峰彇銆?
        /// 鏇翠紭闆呯殑鏂瑰紡鏄湪娲剧敓绫讳腑鑷淇濆瓨寮曠敤銆?
        /// </summary>
        private TDBattleUIBridge GetBridge()
        {
            return _bridge;
        }
    }

    /// <summary>
    /// 甯?Bridge 缂撳瓨鐨?HUD 瑙嗗浘锛堟帹鑽愪娇鐢ㄦ鐗堟湰锛夈€?
    /// 
    /// 浣跨敤鏂瑰紡锛?
    ///   1. 鍦?UIViewConfigTable 涓敞鍐?TDHudViewBridged
    ///   2. Prefab 鍛藉悕鎸?[UI] 灞炴€ц嚜鍔ㄧ粦瀹?
    ///   3. 鎵撳紑锛歎IRuntime.Instance.Open<TDHudViewBridged>(bridge)
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
            if (Txt_Gold != null) Txt_Gold.text = $"閲戝竵: {gold}";
        }

        private void RefreshCityHP(float hp, float maxHp)
        {
            if (Txt_MainCityHP != null)
                Txt_MainCityHP.text = $"涓诲煄: {Mathf.CeilToInt(hp)}/{Mathf.CeilToInt(maxHp)}";
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
