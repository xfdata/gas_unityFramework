using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

public sealed class UIAdditiveSceneModule : UIModuleBase
{
    private readonly string _sceneName;
    private bool _sceneLoaded;

    public UIAdditiveSceneModule(string sceneName)
    {
        _sceneName = sceneName;
    }

    protected override async UniTask OnStart()
    {
        await SceneManager.LoadSceneAsync(_sceneName, LoadSceneMode.Additive);
        _sceneLoaded = true;
    }

    protected override void OnStop()
    {
        if (_sceneLoaded)
        {
            SceneManager.UnloadSceneAsync(_sceneName);
            _sceneLoaded = false;
        }
    }
}