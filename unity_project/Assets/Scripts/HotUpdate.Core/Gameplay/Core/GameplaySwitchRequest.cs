using System.Collections.Generic;


    public sealed class GameplaySwitchRequest
    {
        private readonly ParameterStore _parameters = new ParameterStore();

        public GameplayModeId Target { get; private set; }
        public GameplaySwitchReason Reason { get; private set; }
        public GameplaySwitchBusyPolicy BusyPolicy { get; private set; }
        public GameplayLoadingPolicy LoadingPolicy { get; private set; }
        public bool Force { get; private set; }
        public string DebugName { get; private set; }

        public IReadOnlyDictionary<string, object> Parameters => _parameters.AsReadOnly();

        private GameplaySwitchRequest(GameplayModeId target, GameplaySwitchReason reason)
        {
            Target = target;
            Reason = reason;
            BusyPolicy = GameplaySwitchBusyPolicy.ReplacePending;
            DebugName = target.ToString();
        }

        public static GameplaySwitchRequest To(GameplayModeId target, GameplaySwitchReason reason = GameplaySwitchReason.UserAction)
        {
            return new GameplaySwitchRequest(target, reason);
        }

        public GameplaySwitchRequest SetForce(bool force = true)
        {
            Force = force;
            return this;
        }

        public GameplaySwitchRequest SetBusyPolicy(GameplaySwitchBusyPolicy policy)
        {
            BusyPolicy = policy;
            return this;
        }

        public GameplaySwitchRequest SetLoadingPolicy(GameplayLoadingPolicy policy)
        {
            LoadingPolicy = policy;
            return this;
        }

        public GameplaySwitchRequest SetDebugName(string debugName)
        {
            DebugName = debugName;
            return this;
        }

        public GameplaySwitchRequest Set<T>(string key, T value)
        {
            _parameters.Set(key, value);
            return this;
        }

        public bool TryGet<T>(string key, out T value)
        {
            return _parameters.TryGet(key, out value);
        }

        public T GetOrDefault<T>(string key, T defaultValue = default)
        {
            return _parameters.GetOrDefault(key, defaultValue);
        }
    }

