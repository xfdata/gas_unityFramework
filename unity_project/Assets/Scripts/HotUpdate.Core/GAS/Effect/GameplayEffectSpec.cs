using System.Collections.Generic;
using UnityEngine;

namespace GAS
{
    public class GameplayEffectSpec
    {
        public GameplayEffectDefinition Asset { get; private set; }
        public GameplayEffectRuntime Source { get; }
        public GameplayEffectRuntime Target { get; set; }

        public int SpecId { get; internal set; }
        public long SourceEntityId;
        public long TargetEntityId;
        public int RuntimeEffectId { get; internal set; }

        public int Level { get; }
        public int Stack = 1;

        public float Duration;
        public float Period;

        public Vector3 Position;

        public int RandomSeed;

        public object UserData;

        internal IGameplayEffectRuntimeContext RuntimeContext;

        private Dictionary<int, float> _setByCaller = new Dictionary<int, float>();
        private readonly Dictionary<int, float> _capturedValues = new Dictionary<int, float>();

        public GameplayEffectSpec(
            GameplayEffectDefinition definition,
            GameplayEffectRuntime source,
            GameplayEffectRuntime target,
            int level)
        {
            Asset = definition;
            Source = source;
            Target = target;
            Level = level;
            Duration = definition != null ? definition.Duration : 0f;
            Period = definition != null ? definition.Period : 0f;
            SourceEntityId = source != null ? source.EntityId : 0;
            TargetEntityId = target != null ? target.EntityId : 0;
            RuntimeContext = source != null
                ? source.RuntimeContext
                : target != null
                    ? target.RuntimeContext
                    : null;
        }

        public void SetByCaller(int key, float value)
        {
            _setByCaller[key] = value;
        }

        public float GetSetByCaller(int key, float defaultValue = 0f)
        {
            return _setByCaller.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public GameplayEffectSpec CloneForTarget(GameplayEffectRuntime target)
        {
            var clone = new GameplayEffectSpec(Asset, Source, target, Level)
            {
                SpecId = SpecId,
                SourceEntityId = SourceEntityId,
                TargetEntityId = target != null ? target.EntityId : TargetEntityId,
                RuntimeEffectId = RuntimeEffectId,
                Stack = Stack,
                Duration = Duration,
                Period = Period,
                Position = Position,
                RandomSeed = RandomSeed,
                UserData = UserData,
                RuntimeContext = RuntimeContext,
            };

            if (_setByCaller != null)
            {
                clone._setByCaller = new Dictionary<int, float>(_setByCaller);
            }

            foreach (var pair in _capturedValues)
            {
                clone._capturedValues[pair.Key] = pair.Value;
            }

            return clone;
        }

        public void CopyDynamicValuesFrom(GameplayEffectSpec other, bool copyPeriod)
        {
            if (other == null)
                return;

            Position = other.Position;
            RandomSeed = other.RandomSeed;
            UserData = other.UserData;

            if (copyPeriod)
            {
                Period = other.Period;
            }

            _setByCaller.Clear();
            foreach (var pair in other._setByCaller)
            {
                _setByCaller[pair.Key] = pair.Value;
            }

            _capturedValues.Clear();
            foreach (var pair in other._capturedValues)
            {
                _capturedValues[pair.Key] = pair.Value;
            }
        }

        public void CaptureValue(int key, float value)
        {
            _capturedValues[key] = value;
        }

        public float GetCapturedValue(int key, float defaultValue = 0f)
        {
            return _capturedValues.TryGetValue(key, out var value) ? value : defaultValue;
        }
    }
}
