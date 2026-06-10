using System;
using System.Collections.Generic;
using Framework;

namespace BattleFoundation
{
    public class BattleSystemManager : Disposable
    {
        private Dictionary<Type, IBattleSystem> _systemMap = new Dictionary<Type, IBattleSystem>();
        private List<IBattleSystem> _orderedSystems = new List<IBattleSystem>();
        private bool _started;

        public IReadOnlyList<IBattleSystem> Systems => _orderedSystems;

        public void Register<T>(T system) where T : IBattleSystem
        {
            if (system == null) return;

            EnsureCanRegister(system);
            var type = system.GetType();

            _systemMap.Add(type, system);
            _orderedSystems.Add(system);

            if (_started)
                system.Start();
        }

        public void EnsureCanRegister(IBattleSystem system)
        {
            if (system == null) return;

            var type = system.GetType();
            if (_systemMap.ContainsKey(type))
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
            if (_systemMap.Remove(system.GetType()) && _orderedSystems.Remove(system))
                system.Dispose();
        }

        public void Start()
        {
            if (_started) return;

            int count = _orderedSystems.Count;
            _started = true;
            for (int i = 0; i < count; i++)
            {
                _orderedSystems[i]?.Start();
            }
        }

        public void Update(float deltaTime)
        {
            using (new AutoProfiler("BattleFoundation.BattleSystemManager.Update"))
            {
                int count = _orderedSystems.Count;
                for (int i = 0; i < count; i++)
                {
                    _orderedSystems[i]?.Update(deltaTime);
                }
            }
        }

        public void LateUpdate(float deltaTime)
        {
            using (new AutoProfiler("BattleFoundation.BattleSystemManager.LateUpdate"))
            {
                int count = _orderedSystems.Count;
                for (int i = 0; i < count; i++)
                {
                    _orderedSystems[i]?.LateUpdate(deltaTime);
                }
            }
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
            for (int i = _orderedSystems.Count - 1; i >= 0; i--)
            {
                _orderedSystems[i]?.Dispose();
            }
            _systemMap.Clear();
            _orderedSystems.Clear();
            _started = false;
            base.OnDispose();
        }
    }
}
