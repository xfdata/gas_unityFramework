
    public readonly struct GameplaySwitchStartedEvent : IGameplayEvent
    {
        public readonly GameplayModeId From;
        public readonly GameplayModeId To;
        public readonly string DebugName;
        public GameplaySwitchStartedEvent(GameplayModeId from, GameplayModeId to, string debugName)
        {
            From = from;
            To = to;
            DebugName = debugName;
        }
    }

    public readonly struct GameplaySwitchPendingEvent : IGameplayEvent
    {
        public readonly GameplayModeId Target;
        public readonly string DebugName;
        public GameplaySwitchPendingEvent(GameplayModeId target, string debugName)
        {
            Target = target;
            DebugName = debugName;
        }
    }

    public readonly struct GameplaySwitchSkippedEvent : IGameplayEvent
    {
        public readonly GameplayModeId Target;
        public readonly string Reason;
        public GameplaySwitchSkippedEvent(GameplayModeId target, string reason)
        {
            Target = target;
            Reason = reason;
        }
    }

    public readonly struct GameplayModeLoadStartedEvent : IGameplayEvent
    {
        public readonly GameplayModeId Target;
        public GameplayModeLoadStartedEvent(GameplayModeId target) { Target = target; }
    }

    public readonly struct GameplayModeLoadCompletedEvent : IGameplayEvent
    {
        public readonly GameplayModeId Target;
        public GameplayModeLoadCompletedEvent(GameplayModeId target) { Target = target; }
    }

    public readonly struct GameplaySwitchCompletedEvent : IGameplayEvent
    {
        public readonly GameplayModeId From;
        public readonly GameplayModeId To;
        public GameplaySwitchCompletedEvent(GameplayModeId from, GameplayModeId to)
        {
            From = from;
            To = to;
        }
    }

    public readonly struct GameplaySwitchFailedEvent : IGameplayEvent
    {
        public readonly GameplayModeId Target;
        public readonly string Error;
        public GameplaySwitchFailedEvent(GameplayModeId target, string error)
        {
            Target = target;
            Error = error;
        }
    }

    public readonly struct GameplayTickEvent : IGameplayEvent
    {
        public readonly float DeltaTime;
        public GameplayTickEvent(float deltaTime) { DeltaTime = deltaTime; }
    }

    public readonly struct GameplayLoadingProgressEvent : IGameplayEvent
    {
        public readonly float Progress;
        public readonly string Status;
        public GameplayLoadingProgressEvent(float progress, string status)
        {
            Progress = progress;
            Status = status;
        }
    }

