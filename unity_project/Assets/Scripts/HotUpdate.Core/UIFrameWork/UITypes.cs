public enum UILayer
{
    Scene = 10,
    World = 20,
    Hud = 30,
    HudTop = 40,
    Normal = 50,
    Top = 60,
    Mask = 70,
    Guide = 80,
    Tip = 90,
    Overlay = 100,
    Debug = 110,
}

public enum UICacheMode
{
    DestroyOnClose,
    HideOnClose,
    Preload,
}

public enum UIBlurMode
{
    None,
    SnapshotBlur,
    RealTimeBlur,
}

public enum UIMaskMode
{
    None,
    BlockInputOnly,
    DarkMask,
    DarkMaskClose,
}

public enum UISafeAreaMode
{
    Adapt,
    Ignore,
}

public enum UIWindowState
{
    None,
    Loading,
    Opening,
    Opened,
    Hiding,
    Hidden,
    Closing,
    Closed,
    Disposed,
}