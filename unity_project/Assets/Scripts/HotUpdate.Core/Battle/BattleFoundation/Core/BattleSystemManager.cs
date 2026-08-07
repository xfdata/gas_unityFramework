using System;
using System.Collections.Generic;
using Framework;

namespace BattleFoundation
{
    public class BattleSystemManager : Disposable
    {
        private readonly Dictionary<Type, IBattleSystem> _systemMap = new Dictionary<Type, IBattleSystem>();
        private readonly List<IBattleSystem> _orderedSystems = new List<IBattleSystem>();
        private readonly List<IBattleSystem> _pendingRegistrations = new List<IBattleSystem>();
        private readonly List<IBattleSystem> _pendingRemovals = new List<IBattleSystem>();
        private bool _started;
        private bool _isDispatching;

        public IReadOnlyList<IBattleSystem> Systems => _orderedSystems;

        public void Register<T>(T system) where T : IBattleSystem
        {
            if (system == null) return;

            EnsureCanRegister(system);
            if (_isDispatching)
            {
                _pendingRegistrations.Add(system);
                return;
            }

            RegisterNow(system);
        }

        public void EnsureCanRegister(IBattleSystem system)
        {
            if (system == null) return;

            var type = system.GetType();
            if (_systemMap.ContainsKey(type) || HasPendingRegistration(type))
                throw new InvalidOperationException($"Battle system '{type.Name}' is already registered.");
        }

        public T Get<T>() where T : class, IBattleSystem
        {
            var type = typeof(T);
            if (_systemMap.TryGetValue(type, out var system))
                return system as T;

            for (int i = 0; i < _orderedSystems.Count; i++)
            {
                if (_orderedSystems[i] is T result)
                    return result;
            }
            return null;
        }

        public bool Has<T>() where T : IBattleSystem
        {
            return GetAssignable(typeof(T)) != null;
        }

        public void Remove<T>(T system) where T : IBattleSystem
        {
            if (system == null) return;

            if (_isDispatching)
            {
                if (!_pendingRemovals.Contains(system))
                    _pendingRemovals.Add(system);
                return;
            }

            RemoveNow(system);
        }

        public void Start()
        {
            if (_started) return;

            _started = true;
            Dispatch(system => system.Start());
        }

        public void Update(float deltaTime)
        {
            using (new AutoProfiler("BattleFoundation.BattleSystemManager.Update"))
            {
                Dispatch(system => system.Update(deltaTime));
            }
        }

        public void LateUpdate(float deltaTime)
        {
            using (new AutoProfiler("BattleFoundation.BattleSystemManager.LateUpdate"))
            {
                Dispatch(system => system.LateUpdate(deltaTime));
            }
        }

        private void Dispatch(Action<IBattleSystem> invoke)
        {
            FlushPendingOperations();
            _isDispatching = true;
            try
            {
                int count = _orderedSystems.Count;
                for (int i = 0; i < count; i++)
                {
                    var system = _orderedSystems[i];
                    if (system != null && !_pendingRemovals.Contains(system))
                        invoke(system);
                }
            }
            finally
            {
                _isDispatching = false;
                FlushPendingOperations();
            }
        }

        private void RegisterNow(IBattleSystem system)
        {
            var type = system.GetType();
            _systemMap.Add(type, system);
            _orderedSystems.Add(system);
            if (_started)
                system.Start();
        }

        private void RemoveNow(IBattleSystem system)
        {
            if (_systemMap.Remove(system.GetType()) && _orderedSystems.Remove(system))
                system.Dispose();
        }

        private void FlushPendingOperations()
        {
            if (_isDispatching)
                return;

            for (int i = 0; i < _pendingRemovals.Count; i++)
            {
                var system = _pendingRemovals[i];
                if (!RemovePendingRegistration(system))
                    RemoveNow(system);
            }
            _pendingRemovals.Clear();

            for (int i = 0; i < _pendingRegistrations.Count; i++)
                RegisterNow(_pendingRegistrations[i]);
            _pendingRegistrations.Clear();
        }

        private bool RemovePendingRegistration(IBattleSystem system)
        {
            for (int i = _pendingRegistrations.Count - 1; i >= 0; i--)
            {
                if (_pendingRegistrations[i] != system)
                    continue;

                _pendingRegistrations.RemoveAt(i);
                system.Dispose();
                return true;
            }
            return false;
        }

        private bool HasPendingRegistration(Type type)
        {
            for (int i = 0; i < _pendingRegistrations.Count; i++)
            {
                if (_pendingRegistrations[i]?.GetType() == type)
                    return true;
            }
            return false;
        }

        private IBattleSystem GetAssignable(Type type)
        {
            for (int i = 0; i < _orderedSystems.Count; i++)
            {
                if (type.IsInstanceOfType(_orderedSystems[i]))
                    return _orderedSystems[i];
            }
            return null;
        }

        protected override void OnDispose()
        {
            _isDispatching = false;
            for (int i = _pendingRegistrations.Count - 1; i >= 0; i--)
                _pendingRegistrations[i]?.Dispose();
            _pendingRegistrations.Clear();
            _pendingRemovals.Clear();
            for (int i = _orderedSystems.Count - 1; i >= 0; i--)
                _orderedSystems[i]?.Dispose();
            _systemMap.Clear();
            _orderedSystems.Clear();
            _started = false;
            base.OnDispose();
        }
    }
}
