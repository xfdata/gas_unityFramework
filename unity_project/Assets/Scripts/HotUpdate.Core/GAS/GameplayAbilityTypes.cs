namespace GAS
{
    public enum GameplayAbilityTargetPolicy : byte
    {
        Self,
        Target,
        Source,
    }

    public enum GameplayAbilityEndReason : byte
    {
        Completed,
        Cancelled,
        Failed,
    }

    public enum GameplayAbilityTaskKind : byte
    {
        None,
        WaitDelay,
        DelayedEffect,
    }

    public struct GameplayEventData
    {
        public GameplayTag EventTag;

        public GameplayEffectRuntime Source;
        public GameplayEffectRuntime Target;
        public long SourceEntityId;
        public long TargetEntityId;

        public object UserData;
    }
}
