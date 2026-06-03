using System;

namespace GAS
{
    [Serializable]
    public struct ActiveGameplayEffectState
    {
        public int RuntimeEffectId;
        public int EffectId;
        public int SpecId;

        public long SourceEntityId;
        public long TargetEntityId;

        public int Level;
        public int Stack;

        public GameplayEffectDurationPolicy DurationPolicy;
        public float Duration;
        public float TimeLeft;
        public float Period;
        public float PeriodLeft;
    }

    [Serializable]
    public struct GameplayAbilitySpecState
    {
        public int AbilitySpecId;
        public int AbilityId;
        public long SourceEntityId;
        public long TargetEntityId;
        public int Level;
        public bool IsActive;
        public bool IsEnded;
        public GameplayAbilityEndReason EndReason;
        public GameplayAbilityTaskState[] ActiveTasks;
    }

    [Serializable]
    public struct GameplayAbilityTaskState
    {
        public int AbilityTaskId;
        public GameplayAbilityTaskKind Kind;
        public int DefinitionIndex;
        public float TimeLeft;
    }

    [Serializable]
    public struct GameplayAbilitySystemState
    {
        public int Frame;
        public long EntityId;
        public GameplayTag[] OwnedTags;
        public AttributeSetState AttributeSet;
        public int[] GrantedAbilityIds;
        public GameplayAbilitySpecState[] ActiveAbilities;
        public ActiveGameplayEffectState[] ActiveEffects;
    }
}
