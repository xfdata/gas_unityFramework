namespace BattleFoundation
{
    public enum EBattlePhase
    {
        Uninitialized,
        Initializing,
        Preloading,
        Ready,
        Running,
        Paused,
        Replaying,
        Ended,
        Disposed,
    }

    public enum EBattleTickMode
    {
        RealTime,
        FrameSync,
        TurnBased,
    }
}
