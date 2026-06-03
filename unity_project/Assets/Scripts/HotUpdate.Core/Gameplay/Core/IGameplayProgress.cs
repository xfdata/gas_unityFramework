using System;


    public interface IGameplayProgress
    {
        float Current { get; }

        IDisposable BeginPhase(float start, float end);
        void Report(float localProgress, string status);
    }
