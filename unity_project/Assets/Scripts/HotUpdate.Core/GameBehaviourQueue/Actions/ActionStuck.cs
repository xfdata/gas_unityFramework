using System;

public class ActionStuck : QueuedActionBase
{
    public ActionStuck()
    {
        IsImmediate = true;
    }
    
    private Action OnTriggerEnd { get; set; }

    public override void Execute()
    {
        OnTriggerEnd += Finish;
    }

    protected override void OnDispose()
    {
        OnTriggerEnd -= Finish;
    }

    public void TriggerEnd()
    {
        OnTriggerEnd?.Invoke();
    }
}