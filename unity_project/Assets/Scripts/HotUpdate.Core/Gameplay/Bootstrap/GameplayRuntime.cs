using Cysharp.Threading.Tasks;

public class GameplayRuntime : System.IDisposable
{
    public static GameplayRuntime Instance { get; private set; }

    private readonly UIRuntime _uiRuntime;
    private GameplayFlowMachine _flow;
    private GameplayEventBus _events;
    private GameplaySystemHub _systems;

    public GameplayFlowMachine Flow => _flow;
    public GameplayContext Context => _flow?.Context;

    public GameplayRuntime(UIRuntime uiRuntime)
    {
        _uiRuntime = uiRuntime;

        if (Instance != null)
        {
            UnityEngine.Debug.LogError("[GameplayRuntime] Duplicate instance created.");
            return;
        }

        Instance = this;

        _events = new GameplayEventBus();

        _systems = CreateSystems();
        var context = new GameplayContext(_systems, _events);
        var registry = CreateRegistry();

        _flow = new GameplayFlowMachine(context, registry);
        RegisterDebugEvents();
    }

    private GameplaySystemHub CreateSystems()
    {
        return GameplaySystemHub.CreateDefault()
            .WithUiSystem(new UIFrameWorkGameplayUiSystem(_uiRuntime));
    }

    private GameplayModeRegistry CreateRegistry()
    {
        var registry = new GameplayModeRegistry();
        registry.Register(GameplayModeId.City, ctx => new CityGameplayMode(ctx));
        registry.Register(GameplayModeId.World, ctx => new WorldGameplayMode(ctx));
        registry.Register(GameplayModeId.Pve, ctx => new PveGameplayMode(ctx));
        return registry;
    }

    public void Tick(float deltaTime)
    {
        _flow?.Tick(deltaTime);
    }

    public void Dispose()
    {
        if (Instance == this) Instance = null;
        _flow?.Dispose();
        _flow = null;
        _systems?.Dispose();
        _systems = null;
    }

    public UniTask<GameplaySwitchResult> EnterCityAsync(string spawnPoint = "Default")
    {
        return _flow.SwitchToAsync(
            GameplaySwitchRequest
                .To(GameplayModeId.City, GameplaySwitchReason.UserAction)
                .Set("SpawnPoint", spawnPoint)
                .SetLoadingPolicy(GameplayLoadingPolicy.Default)
                .SetDebugName("Enter City"));
    }

    public UniTask<GameplaySwitchResult> EnterWorldAsync(long mapId)
    {
        return _flow.SwitchToAsync(
            GameplaySwitchRequest
                .To(GameplayModeId.World, GameplaySwitchReason.UserAction)
                .Set("MapId", mapId)
                .SetLoadingPolicy(GameplayLoadingPolicy.None)
                .SetDebugName($"Enter World {mapId}"));
    }

    public UniTask<GameplaySwitchResult> EnterPveAsync(int chapterId, int sectionId, bool startImmediately = false)
    {
        return _flow.SwitchToAsync(
            GameplaySwitchRequest
                .To(GameplayModeId.Pve, GameplaySwitchReason.UserAction)
                .Set("ChapterId", chapterId)
                .Set("SectionId", sectionId)
                .Set("StartImmediately", startImmediately)
                .SetDebugName($"Enter PVE {chapterId}-{sectionId}"));
    }

    public UniTask<GameplaySwitchResult> ExitPveAsync(GameplayModeId returnMode = GameplayModeId.City)
    {
        return _flow.SwitchToAsync(
            GameplaySwitchRequest
                .To(returnMode, GameplaySwitchReason.UserAction)
                .SetDebugName($"Exit PVE → {returnMode}"));
    }

    private void RegisterDebugEvents()
    {
        _events.Subscribe<GameplaySwitchStartedEvent>(e =>
            UnityEngine.Debug.Log($"[Gameplay] Switch started: {e.From} → {e.To} ({e.DebugName})"));

        _events.Subscribe<GameplaySwitchPendingEvent>(e =>
            UnityEngine.Debug.Log($"[Gameplay] Switch pending: {e.Target}"));

        _events.Subscribe<GameplaySwitchSkippedEvent>(e =>
            UnityEngine.Debug.Log($"[Gameplay] Switch skipped: {e.Target} ({e.Reason})"));

        _events.Subscribe<GameplayModeLoadStartedEvent>(e =>
            UnityEngine.Debug.Log($"[Gameplay] Load started: {e.Target}"));

        _events.Subscribe<GameplayModeLoadCompletedEvent>(e =>
            UnityEngine.Debug.Log($"[Gameplay] Load completed: {e.Target}"));

        _events.Subscribe<GameplaySwitchCompletedEvent>(e =>
            UnityEngine.Debug.Log($"[Gameplay] Switch completed: {e.From} → {e.To}"));

        _events.Subscribe<GameplaySwitchFailedEvent>(e =>
            UnityEngine.Debug.LogError($"[Gameplay] Switch failed: {e.Target} ({e.Error})"));
    }
}