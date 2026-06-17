using System;
using BattleFoundation;

namespace TowerDefense
{
    /// <summary>
    /// UI↔Battle 数据桥梁。
    /// 
    /// 职责：
    /// - 持有 BattleContext/EventBus 和关键 System 引用
    /// - 提供便捷的 BattleEvent 订阅/反订阅方法（生命周期内自动绑定）
    /// - 作为 ViewBase.OnOpen(param) 的参数传入，UI 层通过它读取战斗数据和订阅事件
    /// 
    /// 设计约束（Phase 6）：
    /// - UI 不允许直接查询战斗逻辑
    /// - UI 变化通过 BattleEvent 驱动
    /// - 此 Bridge 仅暴露"读接口"和"事件订阅"，不暴露写操作
    /// </summary>
    public class TDBattleUIBridge
    {
        private readonly IBattleContext _context;
        private TDBattleContext _tdContext;

        // ===== 系统缓存 =====
        private MainCitySystem _mainCitySystem;
        private WaveManagerSystem _waveManager;
        private TowerPlacementSystem _towerPlacement;
        private RoguelikeChoiceSystem _roguelikeChoice;
        private TDBattlePhaseSystem _phaseSystem;

        public TDBattleContext TDContext => _tdContext;
        public IBattleContext Context => _context;
        public BattleEventBus EventBus => _context?.EventBus;
        public bool IsValid => _context != null && _context.EventBus != null;

        // ===== 系统只读访问 =====
        public MainCitySystem MainCitySystem =>
            _mainCitySystem ??= _context?.GetSystem<MainCitySystem>();
        public WaveManagerSystem WaveManager =>
            _waveManager ??= _context?.GetSystem<WaveManagerSystem>();
        public TowerPlacementSystem TowerPlacement =>
            _towerPlacement ??= _context?.GetSystem<TowerPlacementSystem>();
        public RoguelikeChoiceSystem RoguelikeChoice =>
            _roguelikeChoice ??= _context?.GetSystem<RoguelikeChoiceSystem>();
        public TDBattlePhaseSystem PhaseSystem =>
            _phaseSystem ??= _context?.GetSystem<TDBattlePhaseSystem>();

        // ===== 便捷属性（纯读） =====
        public int PlayerGold => _tdContext?.PlayerGold ?? 0;
        public float MainCityHP => MainCitySystem?.MainCity?.Health?.HP ?? 0f;
        public float MainCityMaxHP => MainCitySystem?.MainCity?.Health?.MaxHP ?? 0f;
        public float MainCityHPPercent => MainCitySystem?.MainCity?.Health?.HPPercent ?? 0f;
        public int CurrentWaveIndex => WaveManager?.CurrentWaveIndex ?? 0;
        public int TotalWaveCount => WaveManager?.TotalWaveCount ?? 0;
        public EBattlePhase CurrentPhase => PhaseSystem?.CurrentPhase ?? EBattlePhase.Prepare;
        public bool IsChoosing => RoguelikeChoice?.IsChoosing ?? false;

        public TDBattleUIBridge(IBattleContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tdContext = context as TDBattleContext;
        }

        // ===== 事件订阅（生命周期绑定） =====

        /// <summary>订阅事件（会在 Dispose 时自动取消）</summary>
        public IDisposable Subscribe<T>(int eventId, Action<T> handler)
        {
            if (EventBus == null)
                return Disposable.Empty;
            EventBus.On(eventId, handler);
            return new EventUnsubscriber(() =>
            {
                if (_context != null && EventBus != null)
                    EventBus.Off<T>(eventId, handler);
            });
        }

        /// <summary>批量取消订阅</summary>
        public static void UnsubscribeAll(params IDisposable[] subscriptions)
        {
            if (subscriptions == null) return;
            foreach (var sub in subscriptions)
                sub?.Dispose();
        }

        private sealed class EventUnsubscriber : IDisposable
        {
            private Action _unsubscribe;
            public EventUnsubscriber(Action unsubscribe) => _unsubscribe = unsubscribe;
            public void Dispose()
            {
                _unsubscribe?.Invoke();
                _unsubscribe = null;
            }
        }

        private static class Disposable
        {
            public static readonly IDisposable Empty = new EmptyDisposable();
            private sealed class EmptyDisposable : IDisposable { public void Dispose() { } }
        }
    }
}
