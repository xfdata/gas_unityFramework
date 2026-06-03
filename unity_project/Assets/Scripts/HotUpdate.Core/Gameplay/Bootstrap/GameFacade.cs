using Cysharp.Threading.Tasks;
using UnityEngine;

[DefaultExecutionOrder(-200)]
public class GameFacade : MonoBehaviour
{
    public static GameFacade Instance { get; private set; }

    public UIRuntime UI { get; private set; }
    public GameplayRuntime Gameplay { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StartUp(UIRootConfig uiRootConfig)
    {

        UI = UIRuntimeBootstrap.Create(
            uiRootConfig.uiConfigs,
            uiRootConfig.gameObject,
            uiRootConfig.uiCamera,
            uiRootConfig.hiddenRootObject,
            uiRootConfig.stuckObject,
            uiRootConfig.maskObject);

        Gameplay = new GameplayRuntime(UI);
    }

    private void Update()
    {
        Gameplay?.Tick(Time.deltaTime);
    }

    private void OnDestroy()
    {
        Gameplay?.Dispose();
        Gameplay = null;

        UI?.Dispose();
        UI = null;

        if (Instance == this)
            Instance = null;
    }
}