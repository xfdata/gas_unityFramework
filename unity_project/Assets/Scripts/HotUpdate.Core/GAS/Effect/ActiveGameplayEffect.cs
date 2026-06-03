using System.Collections.Generic;

namespace GAS
{
    public class ActiveGameplayEffect
    {
        public int RuntimeEffectId;

        public GameplayEffectSpec Spec { get; }

        public float TimeLeft;
        public float PeriodLeft;
        public int Stack;

        public readonly List<AttributeModifierHandle> ModifierHandles = new List<AttributeModifierHandle>();

        public GameplayEffectDefinition Definition => Spec.Asset;

        public int RuntimeId
        {
            get => RuntimeEffectId;
            set => RuntimeEffectId = value;
        }

        public bool IsInfinite => Definition.DurationPolicy == GameplayEffectDurationPolicy.Infinite;

        public bool IsExpired =>
            Definition.DurationPolicy == GameplayEffectDurationPolicy.Duration &&
            TimeLeft <= 0f;

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

            if (Spec != null)
            {
                Spec.RuntimeEffectId = runtimeEffectId;
            }
        }
    }
}
