using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleFoundation
{
    public readonly struct BattlePhaseChangedEvent
    {
        public readonly EBattlePhase Previous;
        public readonly EBattlePhase Current;

        public BattlePhaseChangedEvent(EBattlePhase previous, EBattlePhase current)
        {
            Previous = previous;
            Current = current;
        }
    }

    public abstract class BattleEngine : Disposable
    {
        [SerializeField]
        protected BattleFoundationConfig _config;

        public EBattlePhase Phase { get; protected set; } = EBattlePhase.Uninitialized;
        public BattleContext Context { get; protected set; }
        public BattleRuntimeSettings Settings { get; private set; }

        public float ElapsedTime { get; protected set; }
        public float DeltaTime { get; protected set; }
        public int FrameIndex { get; protected set; }
        public bool IsPaused { get; protected set; }
        public int RandomSeed => Settings?.RandomSeed ?? 0;

        public BattleRecorder Recorder { get; private set; }
        public BattlePlayback Playback { get; private set; }
        public IBattleReplayAdapter ReplayAdapter { get; private set; }

        public event Action<EBattlePhase, EBattlePhase> OnPhaseChanged;
        public event Action<BattleEngine> OnBattleEnded;

        private float _frameSyncAccumulator;
        private readonly CommandQueue _pendingCommands = new CommandQueue();
        private readonly List<BattleRuleBase> _rules = new List<BattleRuleBase>();

        protected EBattleTickMode TickMode { get; private set; }
        public float TimeScale { get; set; } = 1f;

        protected virtual BattleContext CreateContext() => new BattleContext();

        protected virtual BattleRuntimeSettings CreateRuntimeSettings()
        {
            return _config != null ? _config.CreateRuntimeSettings() : new BattleRuntimeSettings();
        }

        public virtual void Initialize()
        {
            InitializeRuntime(CreateRuntimeSettings());
        }

        protected void InitializeRuntime(BattleRuntimeSettings settings)
        {
            if (Phase != EBattlePhase.Uninitialized)
                return;

            Settings = ResolveSettings(settings);
            TickMode = Settings.TickMode;
            TimeScale = Settings.InitialTimeScale;

            ChangePhase(EBattlePhase.Initializing);
            Context = CreateContext();
            Context.Initialize(this, Settings);

            if (Settings.EnableReplay)
            {
                Recorder = new BattleRecorder();
                Recorder.Initialize(this);
            }

            OnInitialize();
            ChangePhase(EBattlePhase.Ready);
        }

        protected void SetReplayAdapter(IBattleReplayAdapter adapter)
        {
            ReplayAdapter = adapter;
        }

        protected virtual void OnInitialize() { }
        protected virtual void OnBeforeBattleStart() { }

        public virtual void StartBattle()
        {
            if (Phase != EBattlePhase.Ready) return;

            ElapsedTime = 0f;
            DeltaTime = 0f;
            FrameIndex = 0;
            IsPaused = false;
            _frameSyncAccumulator = 0f;

            OnBeforeBattleStart();
            ChangePhase(EBattlePhase.Running);
            Context.Start();
            Recorder?.StartRecording();
            OnBattleStart();
            Recorder?.RecordFrame(FrameRecordData.Create(FrameIndex, ElapsedTime, Context, ReplayAdapter));
        }

        protected virtual void OnBattleStart() { }

        public void UpdateFromUnity(float unityDeltaTime)
        {
            float scaledDeltaTime = Mathf.Max(0f, unityDeltaTime) * Mathf.Max(0f, TimeScale);

            if (Phase == EBattlePhase.Replaying)
            {
                Playback?.Update(scaledDeltaTime);
                return;
            }

            if (Phase != EBattlePhase.Running || IsPaused)
                return;

            switch (TickMode)
            {
                case EBattleTickMode.FrameSync:
                    UpdateFrameSync(scaledDeltaTime);
                    break;
                case EBattleTickMode.TurnBased:
                    break;
                case EBattleTickMode.RealTime:
                default:
                    TickSimulation(scaledDeltaTime);
                    break;
            }
        }

        private void UpdateFrameSync(float elapsedDeltaTime)
        {
            _frameSyncAccumulator += elapsedDeltaTime;

            while (_frameSyncAccumulator >= Settings.FrameSyncStep && Phase == EBattlePhase.Running)
            {
                _frameSyncAccumulator -= Settings.FrameSyncStep;
                TickSimulation(Settings.FrameSyncStep);
            }
        }

        public void TickFixed(float fixedDeltaTime)
        {
            if (Phase != EBattlePhase.Running || IsPaused)
                return;

            TickSimulation(Mathf.Max(0f, fixedDeltaTime));
        }

        public void TickTurn()
        {
            if (TickMode != EBattleTickMode.TurnBased || Phase != EBattlePhase.Running || IsPaused)
                return;

            TickSimulation(0f);
        }

        private void TickSimulation(float deltaTime)
        {
            DeltaTime = deltaTime;
            ElapsedTime += deltaTime;
            FrameIndex++;

            ExecutePendingCommands();
            Context.Update(deltaTime);
            OnUpdate(deltaTime);
            Context.LateUpdate(deltaTime);
            OnLateUpdate(deltaTime);

            if (Phase == EBattlePhase.Running)
                CheckEndConditions();

            Recorder?.RecordFrame(FrameRecordData.Create(FrameIndex, ElapsedTime, Context, ReplayAdapter));
        }

        protected virtual void OnUpdate(float deltaTime) { }
        protected virtual void OnLateUpdate(float deltaTime) { }

        public void EnqueueCommand(BattleCommand command)
        {
            if (command != null)
                _pendingCommands.Enqueue(command);
        }

        private void ExecutePendingCommands()
        {
            while (_pendingCommands.TryDequeue(out var command))
            {
                try
                {
                    command.Execute(this);
                    Context.EventBus.Emit(BattleEventIds.CommandExecuted, command);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[BattleEngine] Command execution failed: {e}");
                }
            }
        }

        public void AddRule(BattleRuleBase rule)
        {
            if (rule == null) return;
            rule.Initialize(this);
            _rules.Add(rule);
        }

        public T GetRule<T>() where T : BattleRuleBase
        {
            for (int i = 0; i < _rules.Count; i++)
            {
                if (_rules[i] is T result)
                    return result;
            }
            return null;
        }

        protected virtual void CheckEndConditions()
        {
            for (int i = 0; i < _rules.Count; i++)
            {
                var rule = _rules[i];
                if (rule == null) continue;

                rule.Update(DeltaTime);
                if (rule.IsTriggered)
                {
                    EndBattle(rule.GetBattleResult());
                    return;
                }
            }
        }

        public virtual void EndBattle(EBattleResult result)
        {
            if (Phase != EBattlePhase.Running && Phase != EBattlePhase.Paused && Phase != EBattlePhase.Replaying)
                return;

            if (Recorder?.IsRecording == true)
                Recorder.RecordFrame(FrameRecordData.Create(FrameIndex, ElapsedTime, Context, ReplayAdapter));
            Recorder?.StopRecording(result);
            Playback?.Stop();
            ChangePhase(EBattlePhase.Ended);
            OnBattleEnd(result);
            OnBattleEnded?.Invoke(this);
        }

        protected virtual void OnBattleEnd(EBattleResult result) { }

        public virtual void Pause()
        {
            if (Phase != EBattlePhase.Running) return;

            IsPaused = true;
            DeltaTime = 0f;
            ChangePhase(EBattlePhase.Paused);
            OnPause();
        }

        protected virtual void OnPause() { }

        public virtual void Resume()
        {
            if (Phase != EBattlePhase.Paused) return;

            IsPaused = false;
            ChangePhase(EBattlePhase.Running);
            OnResume();
        }

        protected virtual void OnResume() { }

        public virtual bool StartReplay(BattleRecord replayData)
        {
            if (replayData?.Frames == null || replayData.Frames.Count == 0 || Context == null)
                return false;
            if (Phase != EBattlePhase.Ready &&
                Phase != EBattlePhase.Running &&
                Phase != EBattlePhase.Paused &&
                Phase != EBattlePhase.Ended)
                return false;

            Recorder?.StopRecording(EBattleResult.None);
            Playback?.Dispose();
            Playback = new BattlePlayback();
            Playback.Initialize(replayData, Context, ReplayAdapter, result => EndBattle(result));
            ElapsedTime = 0f;
            DeltaTime = 0f;
            FrameIndex = 0;
            IsPaused = false;
            ChangePhase(EBattlePhase.Replaying);
            Playback.Start();
            return true;
        }

        public BattleRecord GetRecord() => Recorder?.GetRecord();

        protected void ChangePhase(EBattlePhase nextPhase)
        {
            if (Phase == nextPhase) return;
            if (!CanTransition(Phase, nextPhase))
                throw new InvalidOperationException($"Invalid battle phase transition: {Phase} -> {nextPhase}.");

            var previous = Phase;
            Phase = nextPhase;
            OnPhaseChanged?.Invoke(previous, nextPhase);
            Context?.EventBus?.Emit(BattleEventIds.PhaseChanged, new BattlePhaseChangedEvent(previous, nextPhase));
        }

        private static bool CanTransition(EBattlePhase current, EBattlePhase next)
        {
            if (next == EBattlePhase.Disposed)
                return current != EBattlePhase.Disposed;

            switch (current)
            {
                case EBattlePhase.Uninitialized:
                    return next == EBattlePhase.Initializing;
                case EBattlePhase.Initializing:
                    return next == EBattlePhase.Ready;
                case EBattlePhase.Preloading:
                    return next == EBattlePhase.Ready;
                case EBattlePhase.Ready:
                    return next == EBattlePhase.Preloading ||
                           next == EBattlePhase.Running ||
                           next == EBattlePhase.Replaying;
                case EBattlePhase.Running:
                    return next == EBattlePhase.Paused ||
                           next == EBattlePhase.Replaying ||
                           next == EBattlePhase.Ended;
                case EBattlePhase.Paused:
                    return next == EBattlePhase.Running ||
                           next == EBattlePhase.Replaying ||
                           next == EBattlePhase.Ended;
                case EBattlePhase.Replaying:
                    return next == EBattlePhase.Ended;
                case EBattlePhase.Ended:
                    return next == EBattlePhase.Replaying;
                default:
                    return false;
            }
        }

        private static BattleRuntimeSettings ResolveSettings(BattleRuntimeSettings settings)
        {
            var resolved = settings?.Clone() ?? new BattleRuntimeSettings();
            resolved.FrameSyncStep = Mathf.Max(0.0001f, resolved.FrameSyncStep);
            resolved.InitialTimeScale = Mathf.Max(0f, resolved.InitialTimeScale);
            if (resolved.RandomSeed == 0)
            {
                resolved.RandomSeed = Environment.TickCount;
                if (resolved.RandomSeed == 0)
                    resolved.RandomSeed = 1;
            }
            return resolved;
        }

        protected override void OnDispose()
        {
            for (int i = _rules.Count - 1; i >= 0; i--)
                _rules[i]?.Dispose();
            _rules.Clear();

            Playback?.Dispose();
            Playback = null;
            Recorder?.Dispose();
            Recorder = null;
            Context?.Dispose();
            Context = null;
            ReplayAdapter = null;
            _pendingCommands.Clear();
            ChangePhase(EBattlePhase.Disposed);
            base.OnDispose();
        }
    }
}
