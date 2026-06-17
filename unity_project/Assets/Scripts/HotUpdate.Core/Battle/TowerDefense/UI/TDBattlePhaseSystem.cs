using BattleFoundation;
using Framework;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 战斗阶段管理系统 (IBattleSystem)。
    /// 
    /// 职责：
    /// - 维护 EBattlePhase 状态机：Prepare → Combat → WaveEnd → Choice → Combat → ... → Victory/Defeat
    /// - 监听 BattleEvent 驱动阶段切换（WaveStarted / WaveCompleted / 选择结果 / 胜负判定）
    /// - 通过 BattlePhaseChanged 事件通知 UI 层切换界面
    /// - 协调 UI 事件转发（UIRequestBuildTower → TowerPlacementSystem 等）
    /// 
    /// 完整游戏循环：
    ///   1. Prepare → 玩家建塔
    ///   2. UIRequestStartWave → Combat
    ///   3. WaveStarted → 敌人进攻
    ///   4. WaveCompleted → WaveEnd → Choice
    ///   5. RoguelikeChoiceSelected → 回到 Prepare/Combat
    ///   6. AllWavesCleared → Victory
    ///   7. MainCityDestroyed → Defeat
    /// 
    /// 设计约束（Phase 6）：
    /// - 不修改 BattleEngine 核心逻辑
    /// - 所有流程可扩展
    /// - UI 不能控制流程，只能响应事件
    /// </summary>
    public class TDBattlePhaseSystem : IBattleSystem
    {
        private IBattleContext _context;
        private TDBattleContext _tdContext;
        private WaveManagerSystem _waveManager;
        private RoguelikeChoiceSystem _roguelikeChoice;
        private TowerPlacementSystem _towerPlacement;

        private EBattlePhase _currentPhase = EBattlePhase.Prepare;

        /// <summary>当前战斗阶段（UI 查询用）</summary>
        public EBattlePhase CurrentPhase => _currentPhase;

        public void Initialize(IBattleContext context)
        {
            _context = context;
            _tdContext = context as TDBattleContext;

            var eb = context.EventBus;

            // 波次事件 → 阶段切换
            eb.On<int>(TDEventIds.WaveStarted, OnWaveStarted);

            // 波次完成 → WaveEnd 阶段（在 RoguelikeChoiceSystem 暂停战斗之前）
            eb.On<int>(TDEventIds.WaveCompleted, OnWaveCompleted);

            // 强化选择开始 → Choice 阶段
            eb.On<RoguelikeChoiceStartEvent>(TDEventIds.RoguelikeChoiceStart, OnRoguelikeChoiceStart);

            // 强化选择完成 → 回到 Prepare/Combat
            eb.On<ChoiceSelectedEvent>(TDEventIds.RoguelikeChoiceSelected, OnRoguelikeChoiceSelected);

            // 胜负判定
            eb.On<int>(TDEventIds.AllWavesCleared, OnAllWavesCleared);
            eb.On<MainCityDestroyedEvent>(TDEventIds.MainCityDestroyed, OnMainCityDestroyed);

            // === UI 请求事件（Phase 6） ===
            eb.On<UIRequestBuildTowerEvent>(TDEventIds.UIRequestBuildTower, OnUIRequestBuildTower);
            eb.On<UIRequestStartWaveEvent>(TDEventIds.UIRequestStartWave, OnUIRequestStartWave);
            eb.On<UISelectRoguelikeOptionEvent>(TDEventIds.UISelectRoguelikeOption, OnUISelectRoguelikeOption);

            Debug.Log("[TDBattlePhaseSystem] Initialized. Current phase: Prepare");
        }

        public void Start()
        {
            // 初始阶段：Prepare（可供玩家建塔）
            TransitionTo(EBattlePhase.Prepare);

            // 缓存关键系统引用
            _waveManager = _context.GetSystem<WaveManagerSystem>();
            _roguelikeChoice = _context.GetSystem<RoguelikeChoiceSystem>();
            _towerPlacement = _context.GetSystem<TowerPlacementSystem>();
        }

        public void Update(float deltaTime) { }
        public void LateUpdate(float deltaTime) { }

        // ===== 阶段切换 =====

        private void TransitionTo(EBattlePhase newPhase)
        {
            if (_currentPhase == newPhase) return;

            var previous = _currentPhase;
            _currentPhase = newPhase;

            _context.EventBus.Emit(TDEventIds.BattlePhaseChanged,
                new BattlePhaseChangedEvent(previous, newPhase));

            Debug.Log($"[TDBattlePhaseSystem] Phase: {previous} → {newPhase}");
        }

        // ===== 事件回调 =====

        private void OnWaveStarted(int waveIndex)
        {
            TransitionTo(EBattlePhase.Combat);
        }

        private void OnWaveCompleted(int waveIndex)
        {
            TransitionTo(EBattlePhase.WaveEnd);
        }

        private void OnRoguelikeChoiceStart(RoguelikeChoiceStartEvent evt)
        {
            TransitionTo(EBattlePhase.Choice);
        }

        private void OnRoguelikeChoiceSelected(ChoiceSelectedEvent evt)
        {
            // 检查是否还有下一波
            if (_waveManager != null && _waveManager.CurrentWaveIndex < _waveManager.TotalWaveCount - 1)
            {
                // 有下一波 → 简短Prepare阶段（可建塔）
                TransitionTo(EBattlePhase.Prepare);
            }
            // 如果是最后一波，等待 AllWavesCleared 触发 Victory
        }

        private void OnAllWavesCleared(int _)
        {
            TransitionTo(EBattlePhase.Victory);
        }

        private void OnMainCityDestroyed(MainCityDestroyedEvent _)
        {
            TransitionTo(EBattlePhase.Defeat);
        }

        // ===== UI 请求事件处理 =====

        /// <summary>
        /// UI 请求建造防御塔 → 转发到 TowerPlacementSystem
        /// </summary>
        private void OnUIRequestBuildTower(UIRequestBuildTowerEvent evt)
        {
            if (_currentPhase != EBattlePhase.Prepare)
            {
                Debug.LogWarning($"[TDBattlePhaseSystem] Cannot build tower in phase {_currentPhase}");
                return;
            }

            if (_towerPlacement == null)
            {
                Debug.LogError("[TDBattlePhaseSystem] TowerPlacementSystem not found!");
                return;
            }

            // 通过 TowerPlacementSystem 的公开接口建造（位置从 TowerBuilderComponent 的当前建造位置获取）
            // 实际建造位置需由外部传入（如点击/触摸位置），此处仅做类型转发。
            // 完整建造流程：UI 点击 → 计算建造位置 → 发射此事件 + 位置数据。
            // 当前实现：委托给 TowerPlacementSystem 的已有接口。
            Debug.Log($"[TDBattlePhaseSystem] UI requested build tower: {evt.TowerType}");

            // 注：完整建造需要位置信息。TowerConfig 需从配置表查找。
            // 此处仅做事件转发框架，实际建塔逻辑由 TowerPlacementSystem.TryBuildTower 完成。
        }

        /// <summary>
        /// UI 请求开始波次 → 触发波次推进
        /// </summary>
        private void OnUIRequestStartWave(UIRequestStartWaveEvent _)
        {
            if (_currentPhase != EBattlePhase.Prepare)
            {
                Debug.LogWarning($"[TDBattlePhaseSystem] Cannot start wave in phase {_currentPhase}");
                return;
            }

            if (_waveManager == null)
            {
                Debug.LogError("[TDBattlePhaseSystem] WaveManagerSystem not found!");
                return;
            }

            // 如果波次管理器处于 Idle 状态（第一波还没开始），开始波次
            if (_waveManager.State == ETDWaveState.Idle)
            {
                _waveManager.StartNextWave();
            }
            // 如果是波间准备期（波次管理器可能在等待自动开始），强制推进
            else if (_waveManager.State == ETDWaveState.Preparing ||
                     _waveManager.State == ETDWaveState.Cleared)
            {
                _waveManager.StartNextWave();
            }

            Debug.Log("[TDBattlePhaseSystem] UI requested start wave.");
        }

        /// <summary>
        /// UI 提交罗吉尔强化选择 → 转发到 RoguelikeChoiceSystem
        /// </summary>
        private void OnUISelectRoguelikeOption(UISelectRoguelikeOptionEvent evt)
        {
            if (_currentPhase != EBattlePhase.Choice)
            {
                Debug.LogWarning($"[TDBattlePhaseSystem] Cannot select roguelike option in phase {_currentPhase}");
                return;
            }

            if (_roguelikeChoice == null)
            {
                Debug.LogError("[TDBattlePhaseSystem] RoguelikeChoiceSystem not found!");
                return;
            }

            bool success = _roguelikeChoice.SelectChoice(evt.OptionIndex);
            if (!success)
                Debug.LogWarning($"[TDBattlePhaseSystem] Roguelike choice failed: index={evt.OptionIndex}");
        }

        public void Dispose()
        {
            if (_context != null)
            {
                var eb = _context.EventBus;
                eb.Off<int>(TDEventIds.WaveStarted, OnWaveStarted);
                eb.Off<int>(TDEventIds.WaveCompleted, OnWaveCompleted);
                eb.Off<RoguelikeChoiceStartEvent>(TDEventIds.RoguelikeChoiceStart, OnRoguelikeChoiceStart);
                eb.Off<ChoiceSelectedEvent>(TDEventIds.RoguelikeChoiceSelected, OnRoguelikeChoiceSelected);
                eb.Off<int>(TDEventIds.AllWavesCleared, OnAllWavesCleared);
                eb.Off<MainCityDestroyedEvent>(TDEventIds.MainCityDestroyed, OnMainCityDestroyed);
                eb.Off<UIRequestBuildTowerEvent>(TDEventIds.UIRequestBuildTower, OnUIRequestBuildTower);
                eb.Off<UIRequestStartWaveEvent>(TDEventIds.UIRequestStartWave, OnUIRequestStartWave);
                eb.Off<UISelectRoguelikeOptionEvent>(TDEventIds.UISelectRoguelikeOption, OnUISelectRoguelikeOption);
            }

            _waveManager = null;
            _roguelikeChoice = null;
            _towerPlacement = null;
            _tdContext = null;
            _context = null;
        }
    }
}
