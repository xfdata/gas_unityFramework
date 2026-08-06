using System;

namespace GAS
{
    /// <summary>
    /// Optional profiling backend for GAS runtime code.
    /// The GAS assembly owns this contract and defaults to a no-op backend.
    /// </summary>
    public interface IGASProfiler
    {
        void BeginSample(string name);
        void EndSample();
    }

    /// <summary>
    /// Process-wide GAS profiling facade. Configure it from an integration layer
    /// that is allowed to reference the concrete profiler implementation.
    /// </summary>
    public static class GASProfiler
    {
        private static readonly IGASProfiler NullProfiler = new NullGASProfiler();
        private static IGASProfiler _backend = NullProfiler;

        public static void SetBackend(IGASProfiler backend)
        {
            _backend = backend ?? NullProfiler;
        }

        public static GASProfilerScope Sample(string name)
        {
            var backend = _backend;
            backend.BeginSample(name);
            return new GASProfilerScope(backend);
        }

        private sealed class NullGASProfiler : IGASProfiler
        {
            public void BeginSample(string name) { }
            public void EndSample() { }
        }
    }

    /// <summary>
    /// Value-type scope that preserves the backend selected when the sample began.
    /// </summary>
    public struct GASProfilerScope : IDisposable
    {
        private IGASProfiler _backend;
        private bool _isDisposed;

        internal GASProfilerScope(IGASProfiler backend)
        {
            _backend = backend;
            _isDisposed = false;
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _backend?.EndSample();
            _backend = null;
        }
    }
}
