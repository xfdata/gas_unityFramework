
    public enum GameplaySwitchReason
    {
        Startup = 0,
        UserAction = 1,
        EnterScene = 2,
        ExitScene = 3,
        Reconnect = 4,
        Debug = 5,
    }

    public enum GameplaySwitchBusyPolicy
    {
        DropIfBusy = 0,
        ReplacePending = 1,
    }

    public enum GameplayLoadingPolicy
    {
        Default = 0,
        None = 1,
    }
