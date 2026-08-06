using System.Collections.Generic;
using Framework;
using GAS;

/// <summary>
/// Application-layer bridge that keeps GAS independent from Framework while
/// preserving the existing Framework.AutoProfiler instrumentation.
/// </summary>
internal static class FrameworkGASProfilerInstaller
{
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        GASProfiler.SetBackend(new FrameworkGASProfilerAdapter());
    }
}

/// <summary>
/// Bridges GAS profile scopes to Framework.AutoProfiler without boxing its struct.
/// GAS samples are expected to begin and end on the Unity main thread.
/// </summary>
internal sealed class FrameworkGASProfilerAdapter : IGASProfiler
{
    private readonly Stack<AutoProfiler> _scopes = new Stack<AutoProfiler>(16);

    public void BeginSample(string name)
    {
        _scopes.Push(new AutoProfiler(name));
    }

    public void EndSample()
    {
        if (_scopes.Count == 0)
            return;

        _scopes.Pop().Dispose();
    }
}
