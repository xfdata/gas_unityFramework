using System;

public class FuncActionCondition : IActionCondition
{
    private readonly Func<int, bool> _evaluate;

    public FuncActionCondition(Func<int, bool> evaluate)
    {
        _evaluate = evaluate;
    }

    public bool Evaluate(int param)
    {
        return _evaluate(param);
    }
}
