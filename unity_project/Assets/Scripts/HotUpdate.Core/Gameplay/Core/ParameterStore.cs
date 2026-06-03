using System.Collections.Generic;
using System.Collections.ObjectModel;

    public sealed class ParameterStore
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

        public void Set<T>(string key, T value)
        {
            _values[key] = value;
        }

        public bool TryGet<T>(string key, out T value)
        {
            if (_values.TryGetValue(key, out var raw) && raw is T typed)
            {
                value = typed;
                return true;
            }

            value = default;
            return false;
        }

        public T GetOrDefault<T>(string key, T defaultValue = default)
        {
            return TryGet<T>(key, out var value) ? value : defaultValue;
        }

        public bool Remove(string key)
        {
            return _values.Remove(key);
        }

        public void Clear()
        {
            _values.Clear();
        }

        public IReadOnlyDictionary<string, object> AsReadOnly()
        {
            return new ReadOnlyDictionary<string, object>(_values);
        }
    }
