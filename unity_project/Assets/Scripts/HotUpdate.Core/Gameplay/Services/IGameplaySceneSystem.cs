using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

    public interface IGameplaySceneSystem
    {
        UniTask LoadSceneAsync(string sceneName, LoadSceneMode mode, IProgress<float> progress, CancellationToken token);
        UniTask UnloadSceneAsync(string sceneName, CancellationToken token);
    }
