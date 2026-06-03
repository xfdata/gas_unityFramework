using System;
using System.Collections.Generic;

    public interface IGameplayEvent { }

    public sealed class GameplayEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>(8);

        public IDisposable Subscribe<T>(Action<T> handler) where T : IGameplayEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var list))
            {
                list = new List<Delegate>(4);
                _handlers.Add(type, list);
            }

            list.Add(handler);
            return new Subscription(() => Unsubscribe(handler));
        }

        public void Unsubscribe<T>(Action<T> handler) where T : IGameplayEvent
        {
            if (handler == null) return;
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var list)) return;
            list.Remove(handler);
            if (list.Count == 0) _handlers.Remove(type);
        }

        public bool HasSubscribers<T>() where T : IGameplayEvent
        {
            return _handlers.ContainsKey(typeof(T));
        }

        public void Publish<T>(T evt) where T : IGameplayEvent
        {
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var list)) return;

            if (list.Count == 1)
            {
                ((Action<T>)list[0]).Invoke(evt);
                return;
            }

            for (int i = list.Count - 1; i >= 0; i--)
                ((Action<T>)list[i]).Invoke(evt);
        }

        public void Clear()
        {
            _handlers.Clear();
        }

        private sealed class Subscription : IDisposable
        {
            private Action _dispose;
            public Subscription(Action dispose) { _dispose = dispose; }
            public void Dispose()
            {
                var action = _dispose;
                _dispose = null;
                action?.Invoke();
            }
        }
    }
