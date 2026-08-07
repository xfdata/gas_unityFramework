using System;
using System.Collections.Generic;
using GAS;
using BattleFoundation;

namespace BattleCommon
{
    /// <summary>
    /// R3-S9: 默认表现层 Sink 桥接实现。
    ///
    /// 职责：
    /// - 订阅 BattleEventBus（ActorSpawned/ActorDied）。
    /// - 接收 CombatAbilityComponent 与 CombatDamageExecution 的转换后事件并转发。
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
        private readonly List<IBattlePresentationSink> _listeners = new List<IBattlePresentationSink>();
        private readonly List<IBattlePresentationSink> _pendingListenerRegistrations = new List<IBattlePresentationSink>();
        private readonly List<IBattlePresentationSink> _pendingListenerRemovals = new List<IBattlePresentationSink>();
        private int _listenerDispatchDepth;
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

        public void RegisterListener(IBattlePresentationSink listener)
        {
            if (listener == null || listener == this)
                return;

            if (_listenerDispatchDepth > 0)
            {
                _pendingListenerRemovals.Remove(listener);
                if (!_listeners.Contains(listener) && !_pendingListenerRegistrations.Contains(listener))
                    _pendingListenerRegistrations.Add(listener);
                return;
            }

            if (!_listeners.Contains(listener))
                _listeners.Add(listener);
        }

        public void UnregisterListener(IBattlePresentationSink listener)
        {
            if (listener == null)
                return;

            if (_listenerDispatchDepth > 0)
            {
                _pendingListenerRegistrations.Remove(listener);
                if (_listeners.Contains(listener) && !_pendingListenerRemovals.Contains(listener))
                    _pendingListenerRemovals.Add(listener);
                return;
            }

            _listeners.Remove(listener);
        }

        // ===== IBattlePresentationSink 转发 =====

        public void OnActorSpawned(in ActorSpawnedEvent evt)
        {
            _sink?.OnActorSpawned(evt);
            BeginListenerDispatch();
            try { for (int i = 0; i < _listeners.Count; i++) _listeners[i].OnActorSpawned(evt); }
            finally { EndListenerDispatch(); }
        }

        public void OnActorDied(in ActorDiedEvent evt)
        {
            _sink?.OnActorDied(evt);
            BeginListenerDispatch();
            try { for (int i = 0; i < _listeners.Count; i++) _listeners[i].OnActorDied(evt); }
            finally { EndListenerDispatch(); }
        }

        public void OnDamageDealt(in DamageDealtPresentation evt)
        {
            _sink?.OnDamageDealt(evt);
            BeginListenerDispatch();
            try { for (int i = 0; i < _listeners.Count; i++) _listeners[i].OnDamageDealt(evt); }
            finally { EndListenerDispatch(); }
        }

        public void OnAttributeChanged(in AttributeChangedPresentation evt)
        {
            _sink?.OnAttributeChanged(evt);
            BeginListenerDispatch();
            try { for (int i = 0; i < _listeners.Count; i++) _listeners[i].OnAttributeChanged(evt); }
            finally { EndListenerDispatch(); }
        }

        public void OnAbilityActivated(in AbilityPresentation evt)
        {
            _sink?.OnAbilityActivated(evt);
            BeginListenerDispatch();
            try { for (int i = 0; i < _listeners.Count; i++) _listeners[i].OnAbilityActivated(evt); }
            finally { EndListenerDispatch(); }
        }

        public void OnAbilityEnded(in AbilityPresentation evt)
        {
            _sink?.OnAbilityEnded(evt);
            BeginListenerDispatch();
            try { for (int i = 0; i < _listeners.Count; i++) _listeners[i].OnAbilityEnded(evt); }
            finally { EndListenerDispatch(); }
        }

        public void OnCueTriggered(in CuePresentation evt)
        {
            _sink?.OnCueTriggered(evt);
            BeginListenerDispatch();
            try { for (int i = 0; i < _listeners.Count; i++) _listeners[i].OnCueTriggered(evt); }
            finally { EndListenerDispatch(); }
        }

        public void OnGameplayTagChanged(in GameplayTagChangedPresentation evt)
        {
            _sink?.OnGameplayTagChanged(evt);
            BeginListenerDispatch();
            try { for (int i = 0; i < _listeners.Count; i++) _listeners[i].OnGameplayTagChanged(evt); }
            finally { EndListenerDispatch(); }
        }

        // ===== GAS 事件转发辅助方法 =====
        // 由 CombatAbilityComponent 在 GAS 事件触发时调用，
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
                    OnAbilityActivated(new AbilityPresentation(
                        (int)evt.SourceEntityId, evt.AbilityId, evt.AbilitySpecId));
                    break;

                case GameplayEffectEventType.AbilityEnded:
                    OnAbilityEnded(new AbilityPresentation(
                        (int)evt.SourceEntityId, evt.AbilityId, evt.AbilitySpecId));
                    break;

                case GameplayEffectEventType.AttributeChanged:
                    OnAttributeChanged(new AttributeChangedPresentation(
                        (int)evt.TargetEntityId, evt.AttributeId, evt.OldValue, evt.NewValue, evt.Delta));
                    break;

                case GameplayEffectEventType.CueTriggered:
                {
                    Float3 cuePosition = evt.ContextData is CombatEffectPresentationContext context
                        ? context.Position
                        : default;
                    OnCueTriggered(new CuePresentation(
                        (int)evt.TargetEntityId,
                        (int)evt.SourceEntityId,
                        evt.CueTag,
                        evt.CueEventType,
                        evt.RuntimeEffectId,
                        evt.Magnitude,
                        cuePosition));
                    break;
                }

                case GameplayEffectEventType.TagAdded:
                    OnGameplayTagChanged(new GameplayTagChangedPresentation(
                        (int)evt.TargetEntityId, evt.GameplayTag, true));
                    break;

                case GameplayEffectEventType.TagRemoved:
                    OnGameplayTagChanged(new GameplayTagChangedPresentation(
                        (int)evt.TargetEntityId, evt.GameplayTag, false));
                    break;
            }
        }

        public void Dispose()
        {
            Unbind();
            _sink = null;
            _listeners.Clear();
            _pendingListenerRegistrations.Clear();
            _pendingListenerRemovals.Clear();
            _listenerDispatchDepth = 0;
        }

        private void BeginListenerDispatch()
        {
            _listenerDispatchDepth++;
        }

        private void EndListenerDispatch()
        {
            _listenerDispatchDepth--;
            if (_listenerDispatchDepth != 0)
                return;

            for (int i = 0; i < _pendingListenerRemovals.Count; i++)
                _listeners.Remove(_pendingListenerRemovals[i]);
            _pendingListenerRemovals.Clear();

            for (int i = 0; i < _pendingListenerRegistrations.Count; i++)
            {
                var listener = _pendingListenerRegistrations[i];
                if (!_listeners.Contains(listener))
                    _listeners.Add(listener);
            }
            _pendingListenerRegistrations.Clear();
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
        public void OnGameplayTagChanged(in GameplayTagChangedPresentation evt) { }
    }
}
