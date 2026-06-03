using System;
using UnityEngine;

public sealed class GameplayProgressReporter : IGameplayProgress
{
    private readonly GameplayContext _context;
    private float _current;
    private float _phaseStart;
    private float _phaseEnd;
    private bool _inPhase;

    private PhaseScope _pooledScope;

    public float Current => _current;

    public GameplayProgressReporter(GameplayContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public IDisposable BeginPhase(float start, float end)
    {
        _phaseStart = Mathf.Clamp01(start);
        _phaseEnd = Mathf.Clamp01(end);
        _inPhase = true;
        _current = _phaseStart;

        if (_pooledScope == null)
            _pooledScope = new PhaseScope(this);
        else
            _pooledScope.ResetFor(this);

        return _pooledScope;
    }

    public void Report(float localProgress, string status)
    {
        if (!_inPhase) return;

        var clampedLocal = Mathf.Clamp01(localProgress);
        _current = _phaseStart + clampedLocal * (_phaseEnd - _phaseStart);

        _context.Events.Publish(new GameplayLoadingProgressEvent(_current, status));
        _context.Systems.Ui?.UpdateLoading(_current, status);
    }

    internal void CompletePhase()
    {
        if (!_inPhase) return;
        _inPhase = false;
        _current = _phaseEnd;
    }

    private sealed class PhaseScope : IDisposable
    {
        private GameplayProgressReporter _owner;

        public PhaseScope(GameplayProgressReporter owner)
        {
            _owner = owner;
        }

        public void ResetFor(GameplayProgressReporter owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            _owner?.CompletePhase();
        }
    }
}