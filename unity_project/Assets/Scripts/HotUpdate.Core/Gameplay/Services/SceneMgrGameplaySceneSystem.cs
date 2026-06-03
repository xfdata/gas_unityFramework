using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.SceneSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

    public sealed class SceneMgrGameplaySceneSystem : IGameplaySceneSystem, IDisposable
    {
        private readonly SceneMgr _sceneMgr;
        private readonly bool _ownsSceneMgr;
        private readonly Dictionary<string, int> _sceneIdMap = new();
        private bool _disposed;

        public SceneMgrGameplaySceneSystem(SceneMgr sceneMgr = null)
        {
            if (sceneMgr != null)
            {
                _sceneMgr = sceneMgr;
                _ownsSceneMgr = false;
            }
            else if (SceneMgr.Instance != null)
            {
                _sceneMgr = SceneMgr.Instance;
                _ownsSceneMgr = false;
            }
            else
            {
                _sceneMgr = new SceneMgr();
                _ownsSceneMgr = true;
            }
        }

        public async UniTask LoadSceneAsync(string sceneName, LoadSceneMode mode, IProgress<float> progress, CancellationToken token)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(sceneName))
                return;

            if (mode == LoadSceneMode.Single)
            {
                var sceneType = ResolveSceneType(sceneName);
                var sceneId = await _sceneMgr.LoadSceneAsync(sceneType, sceneName, token: token);
                _sceneIdMap.Clear();
                _sceneIdMap[sceneName] = sceneId;
            }
            else
            {
                var sceneId = await _sceneMgr.LoadSceneAdditiveAsync(sceneName, activeScene: true, token: token);
                _sceneIdMap[sceneName] = sceneId;
            }
        }

        public async UniTask UnloadSceneAsync(string sceneName, CancellationToken token)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(sceneName))
                return;

            if (!_sceneIdMap.Remove(sceneName, out var sceneId))
            {
                Debug.LogWarning($"[SceneMgrGameplaySceneSystem] Scene not tracked: {sceneName}");
                return;
            }

            await _sceneMgr.UnloadSceneAsync(sceneId, token);
        }

        private static SceneType ResolveSceneType(string sceneName)
        {
            return sceneName switch
            {
                "City" => SceneType.City,
                "World" => SceneType.WorldMap,
                "PVE" => SceneType.LevelBattle,
                _ => SceneType.None,
            };
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SceneMgrGameplaySceneSystem));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _sceneIdMap.Clear();

            if (_ownsSceneMgr)
                _sceneMgr?.Dispose();
        }
    }
