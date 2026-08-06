using System;
using GAS;
using BattleFoundation;

namespace BattleCommon
{
    /// <summary>
    /// R3-S9: 默认表现层 Sink 桥接实现。
    ///
    /// 职责：
    /// - 订阅 BattleEventBus（ActorSpawned/ActorDied）和 GAS RuntimeContext 事件，
    ///   转换为 IBattlePresentationSink 载荷并转发。
    /// - DamageDealt 由 CombatDamageExecution 直接调用 Sink.OnDamageDealt（R2 已建立）。
    ///
    /// 使用方式：
    /// - 上层（Samples/工厂）创建 BattlePresentationSink，传入实际表现实现（如 Unity 表现层）。
    /// - BattleEngine 初始化时订阅事件总线，销毁时反订阅。
    ///
    /// 设计原则：
    /// - 桥接层不做表现决策，仅做事件格式转换与转发。
    /// - 实际表现实现（如 UnityParticleSink / UnityAnimancerSink）注入 _sink 字段。
    /// - NullPresentationSink 作为默认空实现，保证无表现层时逻辑层正常运行。
    /// </summary>
    public class BattlePresentationSink : IBattlePresentationSink, IDisposable
    {
        private IBattlePresentationSink _sink;
        private BattleContext _context;
        private Action<ActorSpawnedEvent> _onActorSpawned;
        private Action<ActorDiedEvent> _onActorDied;

        public BattlePresentationSink(IBattlePresentationSink sink = null)
        {
            _sink = sink ?? NullPresentationSink.Instance;
        }

        /// <summary>
        /// 绑定到 BattleContext，订阅事件总线。
        /// 由上层（BattleEngine/Samples）在初始化时调用。
        /// </summary>
        public void Bind(BattleContext context)
        {
            if (_context != null)
                Unbind();

            _context = context;
            if (_context?.EventBus == null)
                return;

            _onActorSpawned = evt => OnActorSpawned(evt);
            _onActorDied = evt => OnActorDied(evt);
            _context.EventBus.On(CombatActorEventIds.ActorSpawned, _onActorSpawned);
            _context.EventBus.On(CombatActorEventIds.ActorDied, _onActorDied);
        }

        /// <summary>解绑事件总线。</summary>
        public void Unbind()
        {
            if (_context?.EventBus != null)
            {
                if (_onActorSpawned != null)
                    _context.EventBus.Off(CombatActorEventIds.ActorSpawned, _onActorSpawned);
                if (_onActorDied != null)
                    _context.EventBus.Off(CombatActorEventIds.ActorDied, _onActorDied);
            }
            _onActorSpawned = null;
            _onActorDied = null;
            _context = null;
        }

        // ===== IBattlePresentationSink 转发 =====

        public void OnActorSpawned(in ActorSpawnedEvent evt) => _sink.OnActorSpawned(evt);
        public void OnActorDied(in ActorDiedEvent evt) => _sink.OnActorDied(evt);
        public void OnDamageDealt(in DamageDealtPresentation evt) => _sink.OnDamageDealt(evt);
        public void OnAttributeChanged(in AttributeChangedPresentation evt) => _sink.OnAttributeChanged(evt);
        public void OnAbilityActivated(in AbilityPresentation evt) => _sink.OnAbilityActivated(evt);
        public void OnAbilityEnded(in AbilityPresentation evt) => _sink.OnAbilityEnded(evt);
        public void OnCueTriggered(in CuePresentation evt) => _sink.OnCueTriggered(evt);

        // ===== GAS 事件转发辅助方法 =====
        // 由 CombatAbilityComponent / CombatDamageExecution 在 GAS 事件触发时调用，
        // 将 GameplayEffectEvent 转换为表现载荷并转发。

        /// <summary>
        /// 转发 GAS GameplayEffectEvent 到表现层。
        /// 由 CombatAbilityComponent 在 GAS 事件回调中调用。
        /// </summary>
        public void ForwardGASEvent(in GameplayEffectEvent evt)
        {
            switch (evt.Type)
            {
                case GameplayEffectEventType.AbilityActivated:
                    _sink.OnAbilityActivated(new AbilityPresentation(
                        (int)evt.SourceEntityId, evt.AbilityId, evt.AbilitySpecId));
                    break;

                case GameplayEffectEventType.AbilityEnded:
                    _sink.OnAbilityEnded(new AbilityPresentation(
                        (int)evt.SourceEntityId, evt.AbilityId, evt.AbilitySpecId));
                    break;

                case GameplayEffectEventType.AttributeChanged:
                    _sink.OnAttributeChanged(new AttributeChangedPresentation(
                        (int)evt.TargetEntityId, evt.AttributeId, evt.OldValue, evt.NewValue, evt.Delta));
                    break;

                case GameplayEffectEventType.CueTriggered:
                {
                    Float3 cuePosition = evt.ContextData is CombatEffectPresentationContext context
                        ? context.Position
                        : default;
                    _sink.OnCueTriggered(new CuePresentation(
                        (int)evt.TargetEntityId, (int)evt.SourceEntityId, evt.CueTag, evt.Magnitude, cuePosition));
                    break;
                }
            }
        }

        public void Dispose()
        {
            Unbind();
            _sink = null;
        }
    }

    /// <summary>
    /// R3-S9: 空表现层 Sink 实现。
    /// 作为默认值，保证无表现层时逻辑层正常运行。
    /// </summary>
    public sealed class NullPresentationSink : IBattlePresentationSink
    {
        public static readonly NullPresentationSink Instance = new NullPresentationSink();

        public void OnActorSpawned(in ActorSpawnedEvent evt) { }
        public void OnActorDied(in ActorDiedEvent evt) { }
        public void OnDamageDealt(in DamageDealtPresentation evt) { }
        public void OnAttributeChanged(in AttributeChangedPresentation evt) { }
        public void OnAbilityActivated(in AbilityPresentation evt) { }
        public void OnAbilityEnded(in AbilityPresentation evt) { }
        public void OnCueTriggered(in CuePresentation evt) { }
    }
}
