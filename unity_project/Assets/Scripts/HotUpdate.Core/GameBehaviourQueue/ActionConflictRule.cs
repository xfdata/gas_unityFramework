using System;

[Serializable]
public class ActionConflictRule
{
    public const int AnyActionType = int.MinValue;

    public int[] Incoming = { AnyActionType };
    public int[] Running = { AnyActionType };
    public ActionConflictResult Result;
}

public enum ActionConflictResult
{
    Allow,
    Interrupt,
    Reject,
}

public enum ActionCancelResult
{
    None,
    CanceledRunning,
    CanceledQueued,
    NotFound,
    Failed
}
