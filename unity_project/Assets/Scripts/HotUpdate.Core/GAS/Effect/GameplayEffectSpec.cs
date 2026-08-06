using System.Collections.Generic;

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

        // 溯源字段：触发该 spec 的源（与 spec 自身的 RuntimeEffectId 区分）
        // - SourceAbilitySpecId：触发该 spec 的源 AbilitySpec 的 SpecId（技能/普攻/投射物路径均填充）
        // - SourceRuntimeEffectId：触发该 spec 的源 ActiveGameplayEffect 的 RuntimeEffectId
        //   （仅在由另一个 Effect 触发时填充；技能/普攻/投射物路径填 0）
        // 用于 DamageResult 等回溯链路定位"谁打出了这一击"。
        public int SourceAbilitySpecId;
        public int SourceRuntimeEffectId;

        public int Level { get; }
        public int Stack = 1;

        public float Duration;
        public float Period;

        public int RandomSeed;

        public object UserData;
        // Opaque integration-owned data propagated by GAS without interpretation.
        public object ContextData;

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
                // 溯源字段同步透传（CloneForTarget 用于 ApplySpecToTarget 跨 target 复制，源不变）
                SourceAbilitySpecId = SourceAbilitySpecId,
                SourceRuntimeEffectId = SourceRuntimeEffectId,
                Stack = Stack,
                Duration = Duration,
                Period = Period,
                RandomSeed = RandomSeed,
                UserData = UserData,
                ContextData = ContextData,
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

            RandomSeed = other.RandomSeed;
            UserData = other.UserData;

            ContextData = other.ContextData;
            // 溯源字段同步：CopyDynamicValuesFrom 在 Stack 合并时调用，
            // incoming spec 的溯源信息应覆盖 existing spec（以最新触发源为准）
            SourceAbilitySpecId = other.SourceAbilitySpecId;
            SourceRuntimeEffectId = other.SourceRuntimeEffectId;

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
