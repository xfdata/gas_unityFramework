
    public enum GameplaySwitchResultType
    {
        Success = 0,
        SkippedSameMode = 1,
        DroppedBecauseBusy = 2,
        AcceptedAsPending = 3,
        Canceled = 4,
        Failed = 5,
    }

    public readonly struct GameplaySwitchResult
    {
        public readonly GameplaySwitchResultType Type;
        public readonly GameplayModeId Target;
        public readonly string Error;

        public bool IsSuccess => Type == GameplaySwitchResultType.Success ||
                                 Type == GameplaySwitchResultType.SkippedSameMode ||
                                 Type == GameplaySwitchResultType.AcceptedAsPending;

        private GameplaySwitchResult(GameplaySwitchResultType type, GameplayModeId target, string error)
        {
            Type = type;
            Target = target;
            Error = error;
        }

        public static GameplaySwitchResult Success(GameplayModeId target)
        {
            return new GameplaySwitchResult(GameplaySwitchResultType.Success, target, null);
        }

        public static GameplaySwitchResult Skipped(GameplayModeId target)
        {
            return new GameplaySwitchResult(GameplaySwitchResultType.SkippedSameMode, target, null);
        }

        public static GameplaySwitchResult Dropped(GameplayModeId target)
        {
            return new GameplaySwitchResult(GameplaySwitchResultType.DroppedBecauseBusy, target, null);
        }

        public static GameplaySwitchResult Pending(GameplayModeId target)
        {
            return new GameplaySwitchResult(GameplaySwitchResultType.AcceptedAsPending, target, null);
        }

        public static GameplaySwitchResult Canceled(GameplayModeId target)
        {
            return new GameplaySwitchResult(GameplaySwitchResultType.Canceled, target, null);
        }

        public static GameplaySwitchResult Failed(GameplayModeId target, string error)
        {
            return new GameplaySwitchResult(GameplaySwitchResultType.Failed, target, error);
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Error) ? $"{Type}: {Target}" : $"{Type}: {Target}, {Error}";
        }
    }

