using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

    public sealed class UnitySceneGameplaySceneSystem : IGameplaySceneSystem
    {
        public async UniTask LoadSceneAsync(string sceneName, LoadSceneMode mode, IProgress<float> progress, CancellationToken token)
        {
            if (string.IsNullOrEmpty(sceneName)) return;

            var op = SceneManager.LoadSceneAsync(sceneName, mode);
            if (op == null)
            {
                Debug.LogError($"LoadSceneAsync failed: {sceneName}");
                return;
            }

            while (!op.isDone)
            {
                token.ThrowIfCancellationRequested();
                progress?.Report(op.progress);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            progress?.Report(1f);
        }

        public async UniTask UnloadSceneAsync(string sceneName, CancellationToken token)
        {
            if (string.IsNullOrEmpty(sceneName)) return;

            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded) return;

            var op = SceneManager.UnloadSceneAsync(scene);
            if (op == null) return;

            while (!op.isDone)
            {
                token.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
    }
