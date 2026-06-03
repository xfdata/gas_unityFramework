using System;
public sealed class GameplayContext
    {
        public GameplayModeId CurrentModeId { get; private set; } = GameplayModeId.None;
        public GameplayModeId LastModeId { get; private set; } = GameplayModeId.None;
        public bool IsSwitching { get; private set; }

        public GameplaySystemHub Systems { get; }
        public GameplayEventBus Events { get; }
        public GameplayBlackboard Blackboard { get; } = new GameplayBlackboard();
        public IGameplayProgress Progress { get; }

        public GameplayContext(GameplaySystemHub systems, GameplayEventBus events = null)
        {
            Systems = systems ?? throw new ArgumentNullException(nameof(systems));
            Events = events ?? new GameplayEventBus();
            Progress = new GameplayProgressReporter(this);
        }

        internal void SetSwitchingState(bool isSwitching)
        {
            IsSwitching = isSwitching;
        }

        internal void CommitModeSwitch(GameplayModeId newModeId)
        {
            LastModeId = CurrentModeId;
            CurrentModeId = newModeId;
        }
    }
