using System.Collections.Generic;

public enum ActionEndReason
{
    None = 0,
    Completed = 1,
    Canceled = 2,
    Faulted = 3,
}

public interface IActionCondition
{
    bool Evaluate(int param);
}

public interface IQueuedAction
{
    string Name { get; }
    int ActionType { get; }
    int Priority { get; }
    int SubPriority { get; }
    float EnqueueTimeStamp { get; set; }

    GameplayTagContainer Tags { get; }
    TagQuery RequireTags { get; }
    TagQuery BlockTags { get; }
    List<IActionCondition> Conditions { get; }
    List<IActionCondition> ManualActivationConditions { get; }

    void Execute();
    void Tick(float deltaTime);
    void End(ActionEndReason reason);

    bool IsEnded { get; }
    ActionEndReason EndReason { get; }
    bool IsCompleted { get; }
    bool IsCanceled { get; }
    
    bool IsDisposed { get; }

    void Finish();
    void Cancel();
    void Dispose(); 
}

public static class GameplayActionType
{
    public const int OpenedUI = 0;
    public const int ActionStuck = -1;
    public const int Guide = 1;
    public const int MainFuncUnlock = 2;
    public const int NextDayLogin = 3;
    public const int OfflineReward = 4;
    public const int BeastTideSettle = 5;
    public const int BeastTideBattleResultEnd = 6;
    public const int BeastTideBattleStart = 7;
    public const int MainAuthorityUnlock = 8;
    public const int HeroViewUnlockDisplay = 9;
    public const int InGameOffer = 10;
    public const int CastleBeKilled = 11;
    public const int SDKShowAppReview = 12;
    public const int AlnMobilDisplay = 13;
    public const int OfficialAppointedTip = 14;
    public const int TaskProgressDisplay = 15;
    public const int AlnTerrGuide = 16;
    public const int ProgressDisplay = 17;
    public const int PaluEvolution = 18;
}
