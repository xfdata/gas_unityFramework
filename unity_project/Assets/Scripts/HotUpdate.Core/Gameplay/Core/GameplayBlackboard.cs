
    public sealed class GameplayBlackboard
    {
        private readonly ParameterStore _store = new ParameterStore();

        public void Set<T>(string key, T value)
        {
            _store.Set(key, value);
        }

        public bool TryGet<T>(string key, out T value)
        {
            return _store.TryGet(key, out value);
        }

        public T GetOrDefault<T>(string key, T defaultValue = default)
        {
            return _store.GetOrDefault(key, defaultValue);
        }

        public bool Remove(string key)
        {
            return _store.Remove(key);
        }

        public void Clear()
        {
            _store.Clear();
        }
    }

