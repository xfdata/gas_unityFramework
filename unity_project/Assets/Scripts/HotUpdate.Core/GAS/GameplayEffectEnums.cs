namespace GAS
{
    public enum GameplayEffectDurationPolicy : byte
    {
        Instant,
        Duration,
        Infinite,
    }

    public enum GameplayEffectStackPolicy : byte
    {
        None,
        StackBySource,
        StackByTarget,
    }
}
