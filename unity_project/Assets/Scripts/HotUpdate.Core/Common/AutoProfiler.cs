using System;
using System.Runtime.CompilerServices;
using UnityEngine.Profiling;
#if UWA
using UWAShared;
#endif

namespace Framework
{
#if ENABLE_PROFILER        
    public struct AutoProfiler : IDisposable
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AutoProfiler(string name)
        {
            Profiler.BeginSample(name);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            Profiler.EndSample();
        }
    }
#else
    public struct AutoProfiler : IDisposable
    {
        public AutoProfiler(string name) { }
        public void Dispose() { }
    }
#endif
}