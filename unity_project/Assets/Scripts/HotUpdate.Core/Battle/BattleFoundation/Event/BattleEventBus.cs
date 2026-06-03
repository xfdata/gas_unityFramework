using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleFoundation
{
    public static class BattleEventIds
    {
        public const int EntityCreated = 1001;
        public const int EntityRemoved = 1002;
        public const int EntityDied = 1003;
        public const int DamageDealt = 2001;
        public const int Healed = 2002;
        public const int AbilityActivated = 3001;
        public const int BuffApplied = 3002;
        public const int BuffRemoved = 3003;
        public const int CommandExecuted = 4001;
        public const int PhaseChanged = 5001;
        public const int RuleTriggered = 5002;
    }

    public class BattleEventBus : Disposable
    {
        private sealed class HandlerSet
        {
            public readonly Type PayloadType;
            public readonly List<Delegate> Handlers = new List<Delegate>();

            public HandlerSet(Type payloadType)
            {
                PayloadType = payloadType;
            }
        }

        private readonly Dictionary<int, HandlerSet> _handlers = new Dictionary<int, HandlerSet>();

        public void On<T>(int eventId, Action<T> handler)
        {
            if (handler == null) return;
            GetOrCreate<T>(eventId).Handlers.Add(handler);
        }

        public void Off<T>(int eventId, Action<T> handler)
        {
            if (handler == null || !_handlers.TryGetValue(eventId, out var set))
                return;

            EnsurePayloadType<T>(eventId, set);
            set.Handlers.Remove(handler);
            if (set.Handlers.Count == 0)
                _handlers.Remove(eventId);
        }

        public void Emit<T>(int eventId, T data)
        {
            if (!_handlers.TryGetValue(eventId, out var set))
                return;

            EnsurePayloadType<T>(eventId, set);
            var handlers = set.Handlers.ToArray();
            for (int i = 0; i < handlers.Length; i++)
            {
                try
                {
                    ((Action<T>)handlers[i]).Invoke(data);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[BattleEventBus] Error handling event {eventId}: {e}");
                }
            }
        }

        public void ClearAll()
        {
            _handlers.Clear();
        }

        private HandlerSet GetOrCreate<T>(int eventId)
        {
            if (!_handlers.TryGetValue(eventId, out var set))
            {
                set = new HandlerSet(typeof(T));
                _handlers.Add(eventId, set);
            }
            else
            {
                EnsurePayloadType<T>(eventId, set);
            }
            return set;
        }

        private static void EnsurePayloadType<T>(int eventId, HandlerSet set)
        {
            if (set.PayloadType != typeof(T))
            {
                throw new InvalidOperationException(
                    $"Battle event '{eventId}' uses payload '{set.PayloadType.Name}', not '{typeof(T).Name}'.");
            }
        }

        protected override void OnDispose()
        {
            ClearAll();
            base.OnDispose();
        }
    }
}
