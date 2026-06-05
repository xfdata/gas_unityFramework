using System.Collections.Generic;

namespace GAS
{
    public class ActiveGameplayEffect
    {
        private readonly List<AttributeModifierHandle> _modifierHandles = new List<AttributeModifierHandle>();

        public int RuntimeEffectId;

        public GameplayEffectSpec Spec { get; }

        public float TimeLeft;
        public float PeriodLeft;
        public int Stack;

        public readonly bool HasAnyCue;
        public readonly bool HasWhileActiveCue;
        public readonly bool HasDuration;
        public readonly bool HasPeriod;
        public readonly bool IsInfinite;

        public IReadOnlyList<AttributeModifierHandle> ModifierHandles => _modifierHandles;

        public GameplayEffectDefinition Definition => Spec.Asset;

        public bool IsExpired => HasDuration && TimeLeft <= 0f;

        internal void AddModifierHandle(AttributeModifierHandle handle)
        {
            _modifierHandles.Add(handle);
        }

        internal void ClearModifierHandles()
        {
            _modifierHandles.Clear();
        }

        internal AttributeModifierHandle GetModifierHandleAt(int index)
        {
            return _modifierHandles[index];
        }

        internal int ModifierHandleCount => _modifierHandles.Count;

        public ActiveGameplayEffectState CaptureState()
        {
            return new ActiveGameplayEffectState
            {
                RuntimeEffectId = RuntimeEffectId,
                EffectId = Definition != null ? Definition.EffectId : 0,
                SpecId = Spec != null ? Spec.SpecId : 0,
                SourceEntityId = Spec != null ? Spec.SourceEntityId : 0,
                TargetEntityId = Spec != null ? Spec.TargetEntityId : 0,
                Level = Spec != null ? Spec.Level : 0,
                Stack = Stack,
                DurationPolicy = Definition != null
                    ? Definition.DurationPolicy
                    : GameplayEffectDurationPolicy.Instant,
                Duration = Spec != null ? Spec.Duration : 0f,
                TimeLeft = TimeLeft,
                Period = Spec != null ? Spec.Period : 0f,
                PeriodLeft = PeriodLeft,
            };
        }

        public ActiveGameplayEffect(
            int runtimeEffectId,
            GameplayEffectSpec spec,
            float timeLeft,
            float periodLeft)
        {
            RuntimeEffectId = runtimeEffectId;
            Spec = spec;
            TimeLeft = timeLeft;
            PeriodLeft = periodLeft;
            Stack = spec != null ? spec.Stack : 1;

            var asset = spec?.Asset;
            var durationPolicy = asset != null ? asset.DurationPolicy : GameplayEffectDurationPolicy.Instant;
            HasDuration = durationPolicy == GameplayEffectDurationPolicy.Duration;
            IsInfinite = durationPolicy == GameplayEffectDurationPolicy.Infinite;
            HasPeriod = spec != null && spec.Period > 0f;

            var cues = asset?.Cues;
            if (cues != null && cues.Count > 0)
            {
                HasAnyCue = true;
                for (int i = 0; i < cues.Count; i++)
                {
                    var cue = cues[i];
                    if (cue != null && cue.Policy == GameplayCuePolicy.Active)
                    {
                        HasWhileActiveCue = true;
                        break;
                    }
                }
            }

            if (Spec != null)
            {
                Spec.RuntimeEffectId = runtimeEffectId;
            }
        }
    }
}
