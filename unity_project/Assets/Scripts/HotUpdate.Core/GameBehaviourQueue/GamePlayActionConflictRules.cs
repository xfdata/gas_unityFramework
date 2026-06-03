public static class GamePlayActionConflictRules
{
    public static ActionConflictRule[] CreateDefault()
    {
        return new ActionConflictRule[]
        {
            new()
            {
                Incoming = new int[] { },
                Running = new int[] { GameplayActionType.ActionStuck },
                Result = ActionConflictResult.Reject,
            },
            new()
            {
                Incoming = new int[] { GameplayActionType.Guide },
                Running = new int[] { GameplayActionType.Guide },
                Result = ActionConflictResult.Reject,
            },
            new()
            {
                Incoming = new int[] { GameplayActionType.ProgressDisplay },
                Running = new int[] { GameplayActionType.ProgressDisplay },
                Result = ActionConflictResult.Reject,
            },
            new()
            {
                Incoming = new int[] { GameplayActionType.TaskProgressDisplay },
                Running = new int[] { GameplayActionType.TaskProgressDisplay },
                Result = ActionConflictResult.Reject,
            },
            new()
            {
                Incoming = new int[]
                {
                    GameplayActionType.ProgressDisplay,
                    GameplayActionType.TaskProgressDisplay,
                },
                Running = new int[] { },
                Result = ActionConflictResult.Allow,
            },
            new()
            {
                Incoming = new int[]
                {
                    GameplayActionType.Guide,
                    GameplayActionType.HeroViewUnlockDisplay,
                },
                Running = new int[] { GameplayActionType.OpenedUI },
                Result = ActionConflictResult.Allow,
            },
            new()
            {
                Incoming = new int[]
                {
                    GameplayActionType.MainAuthorityUnlock,
                    GameplayActionType.MainFuncUnlock,
                },
                Running = new int[] { GameplayActionType.Guide },
                Result = ActionConflictResult.Allow,
            },
        };
    }
}
