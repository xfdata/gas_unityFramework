// Addressables SceneMgr Core - Optimized Version
// -----------------------------------------------------------------------------
// 设计目标：
// 1. 主场景与叠加场景统一使用 Addressables。
// 2. 所有场景操作串行化，避免同时 Load / Unload 导致状态错乱。
// 3. 主场景使用 LoadSceneMode.Single。
// 4. 叠加场景使用 LoadSceneMode.Additive，并支持 activeScene 压栈。
// 5. 支持场景 ActivateAsync 前插入等待点，给 UIAdditiveSceneModule / Blur / Loading 流程使用。
// 6. SceneInfo 内聚当前场景的可见性、相机缓存、UI 覆盖状态。
// 7. 不依赖旧 ResourceMgr。
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

using Cinemachine;
using Framework;

#if UNIVERSAL_RP_PRESENT || UNITY_RENDER_PIPELINE_UNIVERSAL
using UnityEngine.Rendering.Universal;
#endif

namespace Game.SceneSystem
{
    public enum SceneType
    {
        None,
        LoginScene,
        City,
        LevelBattle,
        WorldMap,
        Transition,
    }

    public interface ISceneLoadProgress
    {
        void SetProgress(string stage, int value);
    }

    public readonly struct AddressableSceneKey
    {
        public readonly string Address;
        public readonly AssetReference Reference;

        public bool HasReference => Reference != null && !string.IsNullOrEmpty(Reference.AssetGUID);
        public bool HasAddress => !string.IsNullOrWhiteSpace(Address);

        public AddressableSceneKey(string address)
        {
            Address = address;
            Reference = null;
        }

        public AddressableSceneKey(AssetReference reference, string fallbackAddress = null)
        {
            Reference = reference;
            Address = fallbackAddress;
        }

        public string GetDebugName()
        {
            if (HasReference) return Reference.AssetGUID;
            return Address ?? string.Empty;
        }

        public override string ToString() => GetDebugName();
    }

    public sealed class MainSceneLoadRequest
    {
        public SceneType SceneType;
        public AddressableSceneKey SceneKey;
        public bool ForceReload;
        public ISceneLoadProgress Progress;
        public Func<UniTask> BeforeActivate;
        public Action<int, SceneMgr.SceneInfo> AfterLoaded;
    }

    public sealed class AdditiveSceneLoadRequest
    {
        public AddressableSceneKey SceneKey;
        public bool ActiveScene;
        public Func<UniTask> BeforeActivate;
        public Action<int, SceneMgr.SceneInfo> AfterLoaded;
    }

    public sealed class SceneMgr : IDisposable
    {
        public const int BaseSceneId = 0;
        public static SceneMgr Instance { get; private set; }

        private readonly SceneOperationQueue _operationQueue = new();
        private readonly Dictionary<int, SceneInfo> _sceneMap = new();
        private readonly List<int> _sceneStack = new();
        private readonly CancellationTokenSource _disposeCts = new();

        private AsyncOperationHandle<SceneInstance> _baseSceneHandle;
        private int _nextSceneId = BaseSceneId;
        private bool _disposed;

        private SceneType _currentSceneType = SceneType.None;
        private string _currentSceneKey;
        private Camera _mainCamera;

        public bool IsDisposed => _disposed;
        public SceneType CurrentSceneType => _currentSceneType;
        public string CurrentSceneKey => _currentSceneKey;
        public int CurrentActiveSceneId => GetCurActiveSceneId();
        public Camera MainCamera => _mainCamera;

        public readonly SceneHideEventInfo OnSceneHide = new();
        public readonly SceneReshowEventInfo OnSceneReShow = new();
        public readonly SceneHideEventInfo OnSceneBeCovered = new();
        public readonly SceneReshowEventInfo OnSceneUnCover = new();

        public event Action<Camera> MainCameraChanged;
        public event Action<int, SceneInfo> SceneLoaded;
        public event Action<int, SceneInfo> SceneUnloaded;

        public SceneMgr()
        {
            if (Instance != null)
                throw new InvalidOperationException("SceneMgr already created.");

            Instance = this;
        }

        // ============================================================
        // Event Register API
        // ============================================================

        public void AddSceneHideAction(string key, Func<int, List<GameObject>> action) => OnSceneHide.AddEvent(key, action);
        public void RemoveSceneHideAction(string key) => OnSceneHide.RemoveEvent(key);

        public void AddSceneReShowAction(string key, Action<int, List<GameObject>> action) => OnSceneReShow.AddEvent(key, action);
        public void RemoveSceneReShowAction(string key) => OnSceneReShow.RemoveEvent(key);

        public void AddSceneBeCoveredAction(string key, Func<int, List<GameObject>> action) => OnSceneBeCovered.AddEvent(key, action);
        public void RemoveSceneBeCoveredAction(string key) => OnSceneBeCovered.RemoveEvent(key);

        public void AddSceneUnCoverAction(string key, Action<int, List<GameObject>> action) => OnSceneUnCover.AddEvent(key, action);
        public void RemoveSceneUnCoverAction(string key) => OnSceneUnCover.RemoveEvent(key);

        // ============================================================
        // Query API
        // ============================================================

        public bool TryGetSceneInfo(int sceneId, out SceneInfo info) => _sceneMap.TryGetValue(sceneId, out info);

        public SceneInfo GetSceneInfo(int sceneId)
        {
            _sceneMap.TryGetValue(sceneId, out var info);
            return info;
        }

        public int GetCurActiveSceneId()
        {
            for (int i = _sceneStack.Count - 1; i >= 0; i--)
            {
                var sceneId = _sceneStack[i];
                if (_sceneMap.TryGetValue(sceneId, out var info) && info.SetActiveScene)
                    return sceneId;
            }

            return BaseSceneId;
        }

        public int GetLastActiveSceneId(int fromSceneId)
        {
            var index = _sceneStack.IndexOf(fromSceneId);
            if (index < 0)
                index = _sceneStack.Count;

            for (int i = index - 1; i >= 0; i--)
            {
                var sceneId = _sceneStack[i];
                if (_sceneMap.TryGetValue(sceneId, out var info) && info.SetActiveScene)
                    return sceneId;
            }

            return BaseSceneId;
        }

        public UniTask WaitLastLoadingDone() => _operationQueue.WaitLastDone();

        // ============================================================
        // Main Scene Loading
        // ============================================================

        public UniTask<int> LoadSceneAsync(
            SceneType sceneType,
            string address,
            ISceneLoadProgress progress = null,
            bool forceReload = false,
            CancellationToken token = default)
        {
            return LoadSceneAsync(new MainSceneLoadRequest
            {
                SceneType = sceneType,
                SceneKey = new AddressableSceneKey(address),
                Progress = progress,
                ForceReload = forceReload
            }, token);
        }

        public UniTask<int> LoadSceneAsync(
            SceneType sceneType,
            AssetReference sceneAsset,
            string fallbackAddress = null,
            ISceneLoadProgress progress = null,
            bool forceReload = false,
            CancellationToken token = default)
        {
            return LoadSceneAsync(new MainSceneLoadRequest
            {
                SceneType = sceneType,
                SceneKey = new AddressableSceneKey(sceneAsset, fallbackAddress),
                Progress = progress,
                ForceReload = forceReload
            }, token);
        }

        public async UniTask<int> LoadSceneAsync(MainSceneLoadRequest request, CancellationToken token = default)
        {
            ThrowIfDisposed();

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            using var _ = new AutoProfiler(nameof(LoadSceneAsync));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token, token);
            var ct = linkedCts.Token;

            return await _operationQueue.Enqueue(async () =>
            {
                ThrowIfDisposed();

                var sceneKey = request.SceneKey.GetDebugName();
                if (!request.ForceReload && _currentSceneType == request.SceneType && _currentSceneKey == sceneKey)
                    return BaseSceneId;

                request.Progress?.SetProgress("Begin", 0);

                await UnloadAllAdditiveScenesInternal(ct);

                var oldBaseHandle = _baseSceneHandle;
                var newHandle = LoadSceneHandle(request.SceneKey, LoadSceneMode.Single, activateOnLoad: false);

                try
                {
                    await WaitSceneLoadHandle(newHandle, request.Progress, "MainScene", ct);
                    EnsureSceneHandleSucceeded(newHandle, sceneKey);

                    if (request.BeforeActivate != null)
                        await request.BeforeActivate();

                    request.Progress?.SetProgress("Activate", 95);
                    await newHandle.Result.ActivateAsync().ToUniTask(cancellationToken: ct);
                    request.Progress?.SetProgress("Activate", 100);

                    ReleaseHandleSafe(oldBaseHandle);

                    var sceneInfo = CreateSceneInfo(
                        sceneId: BaseSceneId,
                        sceneType: request.SceneType,
                        address: sceneKey,
                        handle: newHandle,
                        isBaseScene: true,
                        setActiveScene: true
                    );

                    ClearSceneRecordsBeforeBaseReplace();

                    _baseSceneHandle = newHandle;
                    _currentSceneType = request.SceneType;
                    _currentSceneKey = sceneKey;
                    _nextSceneId = BaseSceneId;

                    RegisterSceneInfo(sceneInfo);
                    SetMainCamera(sceneInfo.MainCamera);

                    if (sceneInfo.Scene.IsValid())
                        SceneManager.SetActiveScene(sceneInfo.Scene);

                    SceneLoaded?.Invoke(BaseSceneId, sceneInfo);
                    request.AfterLoaded?.Invoke(BaseSceneId, sceneInfo);

                    return BaseSceneId;
                }
                catch
                {
                    await TryUnloadSceneHandle(newHandle);
                    throw;
                }
            });
        }

        // ============================================================
        // Additive Scene Loading
        // ============================================================

        public UniTask<int> LoadSceneAdditiveAsync(
            string address,
            bool activeScene,
            Func<UniTask> beforeActivate = null,
            Action<int, SceneInfo> afterLoaded = null,
            CancellationToken token = default)
        {
            return LoadSceneAdditiveAsync(new AdditiveSceneLoadRequest
            {
                SceneKey = new AddressableSceneKey(address),
                ActiveScene = activeScene,
                BeforeActivate = beforeActivate,
                AfterLoaded = afterLoaded
            }, token);
        }

        public UniTask<int> LoadSceneAdditiveAsync(
            AssetReference sceneAsset,
            string fallbackAddress,
            bool activeScene,
            Func<UniTask> beforeActivate = null,
            Action<int, SceneInfo> afterLoaded = null,
            CancellationToken token = default)
        {
            return LoadSceneAdditiveAsync(new AdditiveSceneLoadRequest
            {
                SceneKey = new AddressableSceneKey(sceneAsset, fallbackAddress),
                ActiveScene = activeScene,
                BeforeActivate = beforeActivate,
                AfterLoaded = afterLoaded
            }, token);
        }

        public async UniTask<int> LoadSceneAdditiveAsync(AdditiveSceneLoadRequest request, CancellationToken token = default)
        {
            ThrowIfDisposed();

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            using var _ = new AutoProfiler(nameof(LoadSceneAdditiveAsync));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token, token);
            var ct = linkedCts.Token;

            return await _operationQueue.Enqueue(async () =>
            {
                ThrowIfDisposed();

                var sceneId = ++_nextSceneId;
                var sceneKey = request.SceneKey.GetDebugName();
                var handle = LoadSceneHandle(request.SceneKey, LoadSceneMode.Additive, activateOnLoad: false);

                try
                {
                    await WaitSceneLoadHandle(handle, null, "AdditiveScene", ct);
                    EnsureSceneHandleSucceeded(handle, sceneKey);

                    if (request.BeforeActivate != null)
                        await request.BeforeActivate();

                    await handle.Result.ActivateAsync().ToUniTask(cancellationToken: ct);

                    var sceneInfo = CreateSceneInfo(
                        sceneId: sceneId,
                        sceneType: SceneType.None,
                        address: sceneKey,
                        handle: handle,
                        isBaseScene: false,
                        setActiveScene: request.ActiveScene
                    );

                    RegisterSceneInfo(sceneInfo);

                    if (request.ActiveScene)
                    {
                        HideLowerActiveScenes(sceneId);
                        SyncActiveSceneCamera(sceneInfo);

                        if (sceneInfo.Scene.IsValid())
                            SceneManager.SetActiveScene(sceneInfo.Scene);
                    }

                    SceneLoaded?.Invoke(sceneId, sceneInfo);
                    request.AfterLoaded?.Invoke(sceneId, sceneInfo);

                    return sceneId;
                }
                catch
                {
                    await TryUnloadSceneHandle(handle);
                    throw;
                }
            });
        }

        // ============================================================
        // Unload API
        // ============================================================

        public async UniTask UnloadSceneAsync(int sceneId, CancellationToken token = default)
        {
            ThrowIfDisposed();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token, token);
            var ct = linkedCts.Token;

            await _operationQueue.Enqueue(async () =>
            {
                if (sceneId == BaseSceneId)
                    throw new InvalidOperationException("Base scene cannot be unloaded directly. Use LoadSceneAsync to replace it.");

                if (!_sceneMap.TryGetValue(sceneId, out var info))
                    return;

                await UnloadSceneInternal(info, ct, refreshAfterUnload: true);
            });
        }

        public async UniTask UnloadTopAdditiveSceneAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token, token);
            var ct = linkedCts.Token;

            await _operationQueue.Enqueue(async () =>
            {
                for (int i = _sceneStack.Count - 1; i >= 0; i--)
                {
                    var sceneId = _sceneStack[i];
                    if (sceneId == BaseSceneId)
                        continue;

                    if (_sceneMap.TryGetValue(sceneId, out var info))
                    {
                        await UnloadSceneInternal(info, ct, refreshAfterUnload: true);
                        return;
                    }
                }
            });
        }

        private async UniTask UnloadAllAdditiveScenesInternal(CancellationToken token)
        {
            using var _ = new AutoProfiler(nameof(UnloadAllAdditiveScenesInternal));

            for (int i = _sceneStack.Count - 1; i >= 0; i--)
            {
                var sceneId = _sceneStack[i];
                if (sceneId == BaseSceneId)
                    continue;

                if (_sceneMap.TryGetValue(sceneId, out var info))
                    await UnloadSceneInternal(info, token, refreshAfterUnload: false);
            }

            RefreshTopActiveSceneAfterUnload();
        }

        private async UniTask UnloadSceneInternal(SceneInfo info, CancellationToken token, bool refreshAfterUnload)
        {
            using var _ = new AutoProfiler(nameof(UnloadSceneInternal));
            var sceneId = info.SceneId;
            var wasCurrentActiveScene = info.SetActiveScene && sceneId == GetCurActiveSceneId();

            _sceneStack.Remove(sceneId);
            _sceneMap.Remove(sceneId);

            info.OnRemove();

            if (info.Handle.IsValid())
            {
                var unloadHandle = Addressables.UnloadSceneAsync(info.Handle, autoReleaseHandle: true);
                await unloadHandle.ToUniTask(cancellationToken: token);
            }

            SceneUnloaded?.Invoke(sceneId, info);

            if (refreshAfterUnload && wasCurrentActiveScene)
                RefreshTopActiveSceneAfterUnload();
        }

        // ============================================================
        // UI Cover API
        // ============================================================

        public void SetCurrentSceneCoveredByUI(bool covered)
        {
            var sceneId = GetCurActiveSceneId();
            if (!_sceneMap.TryGetValue(sceneId, out var info))
                return;

            if (covered)
                info.SetCoverByUI(OnSceneBeCovered);
            else
                info.SetUnCoverUI(OnSceneUnCover);
        }

        // ============================================================
        // Internal Helpers
        // ============================================================

        private SceneInfo CreateSceneInfo(
            int sceneId,
            SceneType sceneType,
            string address,
            AsyncOperationHandle<SceneInstance> handle,
            bool isBaseScene,
            bool setActiveScene)
        {
            var info = new SceneInfo
            {
                SceneId = sceneId,
                SceneType = sceneType,
                Address = address,
                Handle = handle,
                Scene = handle.Result.Scene,
                IsBaseScene = isBaseScene,
                SetActiveScene = setActiveScene
            };

            info.Initialize();
            return info;
        }

        private void RegisterSceneInfo(SceneInfo info)
        {
            _sceneMap[info.SceneId] = info;
            _sceneStack.Add(info.SceneId);
        }

        private void ClearSceneRecordsBeforeBaseReplace()
        {
            foreach (var info in _sceneMap.Values)
                info.OnRemove();

            _sceneMap.Clear();
            _sceneStack.Clear();
        }

        private void HideLowerActiveScenes(int newActiveSceneId)
        {
            for (int i = _sceneStack.Count - 1; i >= 0; i--)
            {
                var sceneId = _sceneStack[i];
                if (sceneId == newActiveSceneId)
                    continue;

                if (!_sceneMap.TryGetValue(sceneId, out var info))
                    continue;

                info.HideByAdditiveScene(OnSceneHide);

                if (info.SetActiveScene)
                    break;
            }
        }

        private void RefreshTopActiveSceneAfterUnload()
        {
            var activeSceneId = GetCurActiveSceneId();
            if (!_sceneMap.TryGetValue(activeSceneId, out var info))
                return;

            info.ReShowFromAdditiveScene(OnSceneReShow);
            info.ApplyCachedCameraTo(_mainCamera);

            if (info.Scene.IsValid())
                SceneManager.SetActiveScene(info.Scene);
        }

        private void SyncActiveSceneCamera(SceneInfo activeInfo)
        {
            if (_mainCamera == null)
            {
                SetMainCamera(activeInfo.MainCamera);
                return;
            }

            var sceneCamera = activeInfo.MainCamera;
            if (sceneCamera == null || sceneCamera == _mainCamera)
                return;

            _mainCamera.transform.SetPositionAndRotation(sceneCamera.transform.position, sceneCamera.transform.rotation);
            activeInfo.ApplyCurrentCameraTo(_mainCamera);
            DisableAdditiveSceneCamera(sceneCamera);
        }

        private void SetMainCamera(Camera camera)
        {
            if (camera == null)
                return;

            _mainCamera = camera;
            MainCameraChanged?.Invoke(_mainCamera);
        }

        private static void DisableAdditiveSceneCamera(Camera sceneCamera)
        {
            if (sceneCamera == null)
                return;

            var audio = sceneCamera.GetComponent<AudioListener>();
            if (audio != null)
                UnityEngine.Object.Destroy(audio);

            sceneCamera.enabled = false;

var brain = sceneCamera.GetComponent<CinemachineBrain>();
            if (brain != null)
                UnityEngine.Object.Destroy(brain);

#if UNIVERSAL_RP_PRESENT || UNITY_RENDER_PIPELINE_UNIVERSAL
            var urp = sceneCamera.GetComponent<UniversalAdditionalCameraData>();
            if (urp != null)
                UnityEngine.Object.Destroy(urp);
#endif
        }

        private static AsyncOperationHandle<SceneInstance> LoadSceneHandle(AddressableSceneKey key, LoadSceneMode mode, bool activateOnLoad)
        {
            if (key.HasReference)
                return key.Reference.LoadSceneAsync(mode, activateOnLoad);

            if (key.HasAddress)
                return Addressables.LoadSceneAsync(key.Address, mode, activateOnLoad);

            throw new ArgumentException("Invalid scene key. Address or AssetReference required.");
        }

        private static async UniTask WaitSceneLoadHandle(
            AsyncOperationHandle<SceneInstance> handle,
            ISceneLoadProgress progress,
            string stage,
            CancellationToken token)
        {
            using var _ = new AutoProfiler(nameof(WaitSceneLoadHandle));

            while (!handle.IsDone)
            {
                token.ThrowIfCancellationRequested();

                // activateOnLoad=false 时 PercentComplete 通常到 0.9 停住，所以这里映射到 90。
                var value = Mathf.Clamp(Mathf.RoundToInt(handle.PercentComplete * 100f), 0, 90);
                progress?.SetProgress(stage, value);

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            progress?.SetProgress(stage, 90);
        }

        private static void EnsureSceneHandleSucceeded(AsyncOperationHandle<SceneInstance> handle, string debugName)
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
                return;

            if (handle.OperationException != null)
                throw new Exception($"Load scene failed: {debugName}", handle.OperationException);

            throw new Exception($"Load scene failed: {debugName}, status={handle.Status}");
        }

        private static async UniTask TryUnloadSceneHandle(AsyncOperationHandle<SceneInstance> handle)
        {
            try
            {
                if (handle.IsValid())
                    await Addressables.UnloadSceneAsync(handle, autoReleaseHandle: true).ToUniTask();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"TryUnloadSceneHandle failed: {e}");
            }
        }

        private static void ReleaseHandleSafe(AsyncOperationHandle<SceneInstance> handle)
        {
            try
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Release scene handle failed: {e}");
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SceneMgr));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _disposeCts.Cancel();

            foreach (var info in _sceneMap.Values)
                info.OnRemove();

            _sceneMap.Clear();
            _sceneStack.Clear();

            ReleaseHandleSafe(_baseSceneHandle);

            _disposeCts.Dispose();

            if (Instance == this)
                Instance = null;
        }

        // ============================================================
        // SceneInfo
        // ============================================================

        public sealed class SceneInfo
        {
            public int SceneId;
            public SceneType SceneType;
            public string Address;
            public Scene Scene;
            public AsyncOperationHandle<SceneInstance> Handle;
            public bool IsBaseScene;
            public bool SetActiveScene;

            public SceneRoot Root { get; private set; }
            public Camera MainCamera => Root != null ? Root.mainCamera : null;

            public bool HiddenByAdditiveScene { get; private set; }
            public bool CoveredByUI { get; private set; }

            private readonly Dictionary<string, List<GameObject>> _ignoreObjectMap = new();
            private readonly List<GameObject> _rootObjects = new();
            private readonly List<GameObject> _activeObjects = new();

            private CameraClearFlags _cameraClearFlags;
            private Color _cameraBackgroundColor;
            private bool _cameraAllowHDR;
            private bool _cameraAllowMSAA;
            private bool _cameraOrthographic;
            private float _cameraOrthographicSize;
            private float _cameraFieldOfView;
            private float _cameraNearClipPlane;
            private float _cameraFarClipPlane;
            private int _cameraCullingMask;
            private bool _cameraRenderPostProcessing;
            private bool _hasCinemachineBrain;
            private CinemachineBlendDefinition _cinemachineDefaultBlend;
            private CinemachineBrain.UpdateMethod _cinemachineUpdateMethod;
            private CinemachineBrain.BrainUpdateMethod _cinemachineBlendUpdateMethod;

            public void Initialize()
            {
                CacheRootObjects();
                ResolveSceneRoot();
                CacheActiveObjects();

                if (MainCamera != null)
                    CacheCurrentCamera(MainCamera);
            }

            private void CacheRootObjects()
            {
                _rootObjects.Clear();
                _rootObjects.AddRange(Scene.GetRootGameObjects());
            }

            private void ResolveSceneRoot()
            {
                foreach (var go in _rootObjects)
                {
                    if (go == null)
                        continue;

                    var sceneRoot = go.GetComponent<SceneRoot>();
                    if (sceneRoot != null)
                    {
                        Root = sceneRoot;
                        return;
                    }
                }

                throw new Exception($"SceneRoot not found in scene: {Scene.name}");
            }

            private void CacheActiveObjects()
            {
                _activeObjects.Clear();

                if (Root != null && Root.activeGameObjects != null && Root.activeGameObjects.Count > 0)
                {
                    foreach (var go in Root.activeGameObjects)
                    {
                        if (go != null)
                            _activeObjects.Add(go);
                    }

                    return;
                }

                // Fallback: when SceneRoot.activeGameObjects is not configured,
                // include all active root GameObjects. This is safe but may affect
                // more objects than necessary. Prefer configuring activeGameObjects
                // in the scene for precise control.
                foreach (var go in _rootObjects)
                {
                    if (go != null && go.activeSelf)
                        _activeObjects.Add(go);
                }
            }

            public void HideByAdditiveScene(SceneHideEventInfo onSceneHide)
            {
                using var _ = new AutoProfiler(nameof(HideByAdditiveScene));

                if (HiddenByAdditiveScene)
                    return;

                HiddenByAdditiveScene = true;

                var ignoreObjects = InvokeHideEvents(onSceneHide);

                if (Root != null)
                {
                    Root.SetOthersHidden(true, LayerMask.NameToLayer("Hidden"), ignoreObjects);
                    return;
                }

                foreach (var go in _activeObjects)
                {
                    if (go == null)
                        continue;

                    if (ignoreObjects != null && ignoreObjects.Contains(go))
                        continue;

                    go.SetActive(false);
                }
            }

            public void ReShowFromAdditiveScene(SceneReshowEventInfo onSceneReShow)
            {
                using var _ = new AutoProfiler(nameof(ReShowFromAdditiveScene));

                if (!HiddenByAdditiveScene)
                    return;

                HiddenByAdditiveScene = false;

                if (Root != null)
                    Root.SetOthersHidden(false);
                else
                {
                    foreach (var go in _activeObjects)
                    {
                        if (go != null)
                            go.SetActive(true);
                    }
                }

                onSceneReShow?.Invoke(this);
            }

            public void SetCoverByUI(SceneHideEventInfo onSceneHide)
            {
                using var _ = new AutoProfiler(nameof(SetCoverByUI));

                if (CoveredByUI)
                    return;

                CoveredByUI = true;
                InvokeHideEvents(onSceneHide);

                if (MainCamera == null)
                    return;

                MainCamera.cullingMask = 0;

#if UNIVERSAL_RP_PRESENT || UNITY_RENDER_PIPELINE_UNIVERSAL
                var cameraData = MainCamera.GetComponent<UniversalAdditionalCameraData>();
                if (cameraData != null)
                    cameraData.renderPostProcessing = false;
#endif
            }

            public void SetUnCoverUI(SceneReshowEventInfo onSceneReShow)
            {
                using var _ = new AutoProfiler(nameof(SetUnCoverUI));

                if (!CoveredByUI)
                    return;

                CoveredByUI = false;
                onSceneReShow?.Invoke(this);

                if (MainCamera == null)
                    return;

                ApplyCachedCameraTo(MainCamera);
            }

            private HashSet<GameObject> InvokeHideEvents(SceneHideEventInfo events)
            {
                _ignoreObjectMap.Clear();
                return events?.Invoke(SceneId, _ignoreObjectMap);
            }

            public List<GameObject> GetIgnoreObjs(string key)
            {
                return _ignoreObjectMap.TryGetValue(key, out var list) ? list : null;
            }

            public void CacheCurrentCamera(Camera camera)
            {
                if (camera == null)
                    return;

                _cameraClearFlags = camera.clearFlags;
                _cameraBackgroundColor = camera.backgroundColor;
                _cameraAllowHDR = camera.allowHDR;
                _cameraAllowMSAA = camera.allowMSAA;
                _cameraOrthographic = camera.orthographic;
                _cameraOrthographicSize = camera.orthographicSize;
                _cameraFieldOfView = camera.fieldOfView;
                _cameraNearClipPlane = camera.nearClipPlane;
                _cameraFarClipPlane = camera.farClipPlane;
                _cameraCullingMask = camera.cullingMask;

var brain = camera.GetComponent<CinemachineBrain>();
                _hasCinemachineBrain = brain != null;
                if (brain != null)
                {
                    _cinemachineDefaultBlend = brain.m_DefaultBlend;
                    _cinemachineUpdateMethod = brain.m_UpdateMethod;
                    _cinemachineBlendUpdateMethod = brain.m_BlendUpdateMethod;
                }

#if UNIVERSAL_RP_PRESENT || UNITY_RENDER_PIPELINE_UNIVERSAL
                var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
                if (cameraData != null)
                    _cameraRenderPostProcessing = cameraData.renderPostProcessing;
#endif
            }

            public void ApplyCurrentCameraTo(Camera targetCamera)
            {
                if (MainCamera == null || targetCamera == null)
                    return;

                targetCamera.clearFlags = MainCamera.clearFlags;
                targetCamera.backgroundColor = MainCamera.backgroundColor;
                targetCamera.allowHDR = MainCamera.allowHDR;
                targetCamera.allowMSAA = MainCamera.allowMSAA;
                targetCamera.orthographic = MainCamera.orthographic;
                targetCamera.orthographicSize = MainCamera.orthographicSize;
                targetCamera.fieldOfView = MainCamera.fieldOfView;
                targetCamera.nearClipPlane = MainCamera.nearClipPlane;
                targetCamera.farClipPlane = MainCamera.farClipPlane;
                targetCamera.cullingMask = MainCamera.cullingMask;

                var sourceBrain = MainCamera.GetComponent<CinemachineBrain>();
                var targetBrain = targetCamera.GetComponent<CinemachineBrain>();
                if (sourceBrain != null && targetBrain != null)
                {
                    targetBrain.m_DefaultBlend = sourceBrain.m_DefaultBlend;
                    targetBrain.m_UpdateMethod = sourceBrain.m_UpdateMethod;
                    targetBrain.m_BlendUpdateMethod = sourceBrain.m_BlendUpdateMethod;
                }
            }

            public void ApplyCachedCameraTo(Camera targetCamera)
            {
                if (targetCamera == null)
                    return;

                targetCamera.clearFlags = _cameraClearFlags;
                targetCamera.backgroundColor = _cameraBackgroundColor;
                targetCamera.allowHDR = _cameraAllowHDR;
                targetCamera.allowMSAA = _cameraAllowMSAA;
                targetCamera.orthographic = _cameraOrthographic;
                targetCamera.orthographicSize = _cameraOrthographicSize;
                targetCamera.fieldOfView = _cameraFieldOfView;
                targetCamera.nearClipPlane = _cameraNearClipPlane;
                targetCamera.farClipPlane = _cameraFarClipPlane;
                targetCamera.cullingMask = _cameraCullingMask;

                var brain = targetCamera.GetComponent<CinemachineBrain>();
                if (_hasCinemachineBrain && brain != null)
                {
                    brain.m_DefaultBlend = _cinemachineDefaultBlend;
                    brain.m_UpdateMethod = _cinemachineUpdateMethod;
                    brain.m_BlendUpdateMethod = _cinemachineBlendUpdateMethod;
                }

#if UNIVERSAL_RP_PRESENT || UNITY_RENDER_PIPELINE_UNIVERSAL
                var cameraData = targetCamera.GetComponent<UniversalAdditionalCameraData>();
                if (cameraData != null)
                    cameraData.renderPostProcessing = _cameraRenderPostProcessing;
#endif
            }

            public void OnRemove()
            {
                _ignoreObjectMap.Clear();
                _activeObjects.Clear();
                _rootObjects.Clear();
                Root = null;
            }
        }

        // ============================================================
        // Hide / Reshow Events
        // ============================================================

        public sealed class SceneHideEventInfo
        {
            private readonly Dictionary<string, Func<int, List<GameObject>>> _events = new();

            public void AddEvent(string key, Func<int, List<GameObject>> func)
            {
                if (string.IsNullOrWhiteSpace(key) || func == null)
                    return;

                _events[key] = func;
            }

            public void RemoveEvent(string key)
            {
                if (string.IsNullOrWhiteSpace(key))
                    return;

                _events.Remove(key);
            }

            public HashSet<GameObject> Invoke(int sceneId, Dictionary<string, List<GameObject>> ignoreMap)
            {
                var result = new HashSet<GameObject>();

                foreach (var kv in _events)
                {
                    try
                    {
                        var list = kv.Value?.Invoke(sceneId);
                        if (list == null)
                            continue;

                        ignoreMap[kv.Key] = list;
                        result.UnionWith(list);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }

                return result;
            }
        }

        public sealed class SceneReshowEventInfo
        {
            private readonly Dictionary<string, Action<int, List<GameObject>>> _events = new();

            public void AddEvent(string key, Action<int, List<GameObject>> func)
            {
                if (string.IsNullOrWhiteSpace(key) || func == null)
                    return;

                _events[key] = func;
            }

            public void RemoveEvent(string key)
            {
                if (string.IsNullOrWhiteSpace(key))
                    return;

                _events.Remove(key);
            }

            public void Invoke(SceneInfo sceneInfo)
            {
                foreach (var kv in _events)
                {
                    try
                    {
                        kv.Value?.Invoke(sceneInfo.SceneId, sceneInfo.GetIgnoreObjs(kv.Key));
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
        }

        // ============================================================
        // SceneOperationQueue
        // ============================================================

        private sealed class SceneOperationQueue
        {
            private UniTaskCompletionSource<bool> _lastWaiter;

            public UniTask WaitLastDone()
            {
                return _lastWaiter?.Task ?? UniTask.CompletedTask;
            }

            public async UniTask<T> Enqueue<T>(Func<UniTask<T>> operation)
            {
                using var _ = new AutoProfiler("SceneOpQueue.Enqueue");

                if (operation == null)
                    throw new ArgumentNullException(nameof(operation));

                var current = new UniTaskCompletionSource<bool>();
                var previous = Interlocked.Exchange(ref _lastWaiter, current);

                if (previous != null)
                    await previous.Task;

                try
                {
                    return await operation();
                }
                finally
                {
                    current.TrySetResult(true);
                    Interlocked.CompareExchange(ref _lastWaiter, null, current);
                }
            }

            public async UniTask Enqueue(Func<UniTask> operation)
            {
                await Enqueue(async () =>
                {
                    await operation();
                    return true;
                });
            }
        }
    }
}
