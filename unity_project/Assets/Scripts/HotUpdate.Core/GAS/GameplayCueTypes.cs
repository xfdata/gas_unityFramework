namespace GAS
{
    public enum GameplayCueEventType : byte
    {
        Execute,
        OnActive,
        WhileActive,
        Removed,
    }

    public enum GameplayCuePolicy : byte
    {
        Static,
        Active,
    }
}
