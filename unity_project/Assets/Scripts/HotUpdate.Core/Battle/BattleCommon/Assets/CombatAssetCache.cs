using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace BattleCommon
{

public class AssetCacheEntry
{
    public GameObject Prefab;
    public AsyncOperationHandle<GameObject> Handle;
    public bool IsPinned;
    public int RefCount;

    internal string Path;
    internal AssetCacheEntry Prev;
    internal AssetCacheEntry Next;

    public void ReleaseHandle()
    {
        if (Handle.IsValid())
        {
            Addressables.Release(Handle);
        }
    }
}

public class CombatAssetCache : Disposable
{
    private const string LogPrefix = "CombatAssetCache";

    private Dictionary<string, AssetCacheEntry> _modelEntries = new Dictionary<string, AssetCacheEntry>();
    private Dictionary<string, AssetCacheEntry> _particleEntries = new Dictionary<string, AssetCacheEntry>();

    private AssetCacheEntry _modelHead;
    private AssetCacheEntry _modelTail;

    private AssetCacheEntry _particleHead;
    private AssetCacheEntry _particleTail;

    private List<UniTask> _preloadTasks = new List<UniTask>();
    private HashSet<string> _pendingAssets = new HashSet<string>();
    private readonly CancellationTokenSource _disposeCts = new CancellationTokenSource();

    private int _modelCapacity;
    private int _particleCapacity;
    private int _modelLRUCount;
    private int _particleLRUCount;

    public int ModelCapacity
    {
        get => _modelCapacity;
        set
        {
            _modelCapacity = Mathf.Max(1, value);
            EvictModelLRUIfNeeded();
        }
    }

    public int ParticleCapacity
    {
        get => _particleCapacity;
        set
        {
            _particleCapacity = Mathf.Max(1, value);
            EvictParticleLRUIfNeeded();
        }
    }

    public int ModelCount => _modelEntries.Count;
    public int ParticleCount => _particleEntries.Count;
    public int ModelPinnedCount { get; private set; }
    public int ParticlePinnedCount { get; private set; }

    public CombatAssetCache(int modelCapacity = 20, int particleCapacity = 10)
    {
        _modelCapacity = Mathf.Max(1, modelCapacity);
        _particleCapacity = Mathf.Max(1, particleCapacity);

        _modelHead = new AssetCacheEntry();
        _modelTail = new AssetCacheEntry();
        _modelHead.Next = _modelTail;
        _modelTail.Prev = _modelHead;

        _particleHead = new AssetCacheEntry();
        _particleTail = new AssetCacheEntry();
        _particleHead.Next = _particleTail;
        _particleTail.Prev = _particleHead;
    }

    public GameObject GetLoadedAsset(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        if (_modelEntries.TryGetValue(path, out var entry) && entry.Prefab != null)
        {
            TouchModelLRU(entry);
            return entry.Prefab;
        }
        return null;
    }

    public GameObject GetLoadedParticle(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        if (_particleEntries.TryGetValue(path, out var entry) && entry.Prefab != null)
        {
            TouchParticleLRU(entry);
            return entry.Prefab;
        }
        return null;
    }

    public bool IsAssetLoaded(string path)
    {
        return !string.IsNullOrEmpty(path)
            && _modelEntries.TryGetValue(path, out var entry)
            && entry.Prefab != null;
    }

    public bool IsParticleLoaded(string path)
    {
        return !string.IsNullOrEmpty(path)
            && _particleEntries.TryGetValue(path, out var entry)
            && entry.Prefab != null;
    }

    public bool IsAssetCached(string path)
    {
        return !string.IsNullOrEmpty(path)
            && (_modelEntries.ContainsKey(path) || _particleEntries.ContainsKey(path));
    }

    public void RegisterPreloadAsset(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        if (_modelEntries.ContainsKey(path)) return;

        _pendingAssets.Add(path);
    }

    public void RegisterPreloadAssets(IEnumerable<string> paths)
    {
        if (paths == null) return;
        foreach (var path in paths)
        {
            RegisterPreloadAsset(path);
        }
    }

    public async UniTask StartPreloadAsync(IProgress<float> progress = null)
    {
        if (IsDisposed) return;

        _preloadTasks.Clear();
        progress?.Report(0f);

        if (_pendingAssets.Count == 0)
        {
            progress?.Report(1f);
            return;
        }

        foreach (var path in _pendingAssets)
        {
            _preloadTasks.Add(LoadAssetPinnedAsync(path));
        }
        _pendingAssets.Clear();

        await UniTask.WhenAll(_preloadTasks);
        if (IsDisposed) return;

        progress?.Report(1f);
    }

    public async UniTask<GameObject> LoadAssetLazyAsync(string path)
    {
        if (string.IsNullOrEmpty(path) || IsDisposed) return null;

        if (_modelEntries.TryGetValue(path, out var existing))
        {
            var prefab = await WaitForModelEntryAsync(existing);
            if (prefab != null)
            {
                TouchModelLRU(existing);
            }
            return prefab;
        }

        var entry = new AssetCacheEntry { Path = path };
        _modelEntries[path] = entry;

        try
        {
            var handle = Addressables.LoadAssetAsync<GameObject>(path);
            entry.Handle = handle;
            await handle.Task;

            if (!CanUseLoadedEntry(entry, _modelEntries))
                return null;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                entry.Prefab = handle.Result;
                AddToLRU(entry, ref _modelHead, ref _modelTail, ref _modelLRUCount);
                EvictModelLRUIfNeeded();
                return entry.Prefab;
            }

            RemoveModelEntry(entry);
            Log.Error($"[{LogPrefix}] Failed to lazy-load {path}");
            return null;
        }
        catch (Exception e)
        {
            RemoveModelEntry(entry);
            Log.Error($"[{LogPrefix}] Exception lazy-loading {path}: {e.Message}");
            return null;
        }
    }

    public async UniTask<GameObject> GetOrLoadAssetAsync(string path)
    {
        var existing = GetLoadedAsset(path);
        if (existing != null) return existing;
        return await LoadAssetLazyAsync(path);
    }

    public async UniTask<GameObject> LoadParticleLazyAsync(string path)
    {
        if (string.IsNullOrEmpty(path) || IsDisposed) return null;

        if (_particleEntries.TryGetValue(path, out var existing))
        {
            var prefab = await WaitForParticleEntryAsync(existing);
            if (prefab != null)
            {
                TouchParticleLRU(existing);
            }
            return prefab;
        }

        var entry = new AssetCacheEntry { Path = path };
        _particleEntries[path] = entry;

        try
        {
            var handle = Addressables.LoadAssetAsync<GameObject>(path);
            entry.Handle = handle;
            await handle.Task;

            if (!CanUseLoadedEntry(entry, _particleEntries))
                return null;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                entry.Prefab = handle.Result;
                AddToLRU(entry, ref _particleHead, ref _particleTail, ref _particleLRUCount);
                EvictParticleLRUIfNeeded();
                return entry.Prefab;
            }

            RemoveParticleEntry(entry);
            Log.Error($"[{LogPrefix}] Failed to lazy-load particle {path}");
            return null;
        }
        catch (Exception e)
        {
            RemoveParticleEntry(entry);
            Log.Error($"[{LogPrefix}] Exception lazy-loading particle {path}: {e.Message}");
            return null;
        }
    }

    public GameObject InstantiateModel(string path, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        var prefab = GetLoadedAsset(path);
        if (prefab == null)
        {
            Log.Error($"[{LogPrefix}] Model not loaded: {path}");
            return null;
        }

        var go = parent != null
            ? UnityEngine.Object.Instantiate(prefab, position, rotation, parent)
            : UnityEngine.Object.Instantiate(prefab, position, rotation);

        if (_modelEntries.TryGetValue(path, out var entry))
        {
            entry.RefCount++;
        }
        return go;
    }

    public GameObject InstantiateParticle(string path, Vector3 position, Quaternion rotation)
    {
        var prefab = GetLoadedParticle(path);
        if (prefab == null)
        {
            Log.Error($"[{LogPrefix}] Particle not loaded: {path}");
            return null;
        }

        var go = UnityEngine.Object.Instantiate(prefab, position, rotation);
        if (_particleEntries.TryGetValue(path, out var entry))
        {
            entry.RefCount++;
        }
        return go;
    }

    public void ReleaseInstance(string path, GameObject instance)
    {
        if (instance != null)
        {
            UnityEngine.Object.Destroy(instance);
        }
        if (_modelEntries.TryGetValue(path, out var entry) && entry.RefCount > 0)
        {
            entry.RefCount--;
        }
    }

    public void ReleaseParticleInstance(string path, GameObject instance)
    {
        if (instance != null)
        {
            UnityEngine.Object.Destroy(instance);
        }
        if (_particleEntries.TryGetValue(path, out var entry) && entry.RefCount > 0)
        {
            entry.RefCount--;
        }
    }

    public void UnloadUnusedAssets()
    {
        EvictModelLRUUnused();
        EvictParticleLRUUnused();
    }

    public void UnloadAllLRU()
    {
        EvictAllModelLRU();
        EvictAllParticleLRU();
    }

    public void PinAsset(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        if (!_modelEntries.TryGetValue(path, out var entry)) return;

        SetModelPinned(entry);
    }

    public void UnpinAsset(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        if (!_modelEntries.TryGetValue(path, out var entry)) return;
        if (!entry.IsPinned) return;

        entry.IsPinned = false;
        ModelPinnedCount = Mathf.Max(0, ModelPinnedCount - 1);
        AddToLRU(entry, ref _modelHead, ref _modelTail, ref _modelLRUCount);
        EvictModelLRUIfNeeded();
    }

    public float GetPreloadProgress()
    {
        if (_preloadTasks.Count == 0) return 1f;
        int completed = 0;
        foreach (var task in _preloadTasks)
        {
            if (task.Status == UniTaskStatus.Succeeded) completed++;
        }
        return (float)completed / _preloadTasks.Count;
    }

    public async UniTask LoadParticleAsync(string path)
    {
        if (string.IsNullOrEmpty(path) || IsDisposed) return;

        if (_particleEntries.TryGetValue(path, out var existing))
        {
            if (await WaitForParticleEntryAsync(existing) != null)
            {
                SetParticlePinned(existing);
            }
            return;
        }

        var entry = new AssetCacheEntry { Path = path, IsPinned = true };
        _particleEntries[path] = entry;
        ParticlePinnedCount++;

        try
        {
            var handle = Addressables.LoadAssetAsync<GameObject>(path);
            entry.Handle = handle;
            await handle.Task;

            if (!CanUseLoadedEntry(entry, _particleEntries))
                return;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                entry.Prefab = handle.Result;
                return;
            }

            RemoveParticleEntry(entry);
            Log.Error($"[{LogPrefix}] Failed to load particle {path}");
        }
        catch (Exception e)
        {
            RemoveParticleEntry(entry);
            Log.Error($"[{LogPrefix}] Exception loading particle {path}: {e.Message}");
        }
    }

    private async UniTask LoadAssetPinnedAsync(string path)
    {
        if (string.IsNullOrEmpty(path) || IsDisposed) return;

        if (_modelEntries.TryGetValue(path, out var existing))
        {
            if (await WaitForModelEntryAsync(existing) != null)
            {
                SetModelPinned(existing);
            }
            return;
        }

        var entry = new AssetCacheEntry { Path = path, IsPinned = true };
        _modelEntries[path] = entry;
        ModelPinnedCount++;

        try
        {
            var handle = Addressables.LoadAssetAsync<GameObject>(path);
            entry.Handle = handle;
            await handle.Task;

            if (!CanUseLoadedEntry(entry, _modelEntries))
                return;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                entry.Prefab = handle.Result;
                return;
            }

            RemoveModelEntry(entry);
            Log.Error($"[{LogPrefix}] Failed to load {path}");
        }
        catch (Exception e)
        {
            RemoveModelEntry(entry);
            Log.Error($"[{LogPrefix}] Exception loading {path}: {e.Message}");
        }
    }

    private async UniTask<GameObject> WaitForModelEntryAsync(AssetCacheEntry entry)
    {
        return await WaitForEntryAsync(entry, RemoveModelEntry);
    }

    private async UniTask<GameObject> WaitForParticleEntryAsync(AssetCacheEntry entry)
    {
        return await WaitForEntryAsync(entry, RemoveParticleEntry);
    }

    private async UniTask<GameObject> WaitForEntryAsync(AssetCacheEntry entry, Action<AssetCacheEntry> removeEntry)
    {
        if (entry == null) return null;
        if (entry.Prefab != null) return entry.Prefab;
        if (!entry.Handle.IsValid()) return null;

        try
        {
            await entry.Handle.Task;

            if (IsDisposed)
            {
                entry.ReleaseHandle();
                return null;
            }

            if (entry.Handle.Status == AsyncOperationStatus.Succeeded)
            {
                entry.Prefab = entry.Handle.Result;
                return entry.Prefab;
            }
        }
        catch (Exception e)
        {
            Log.Error($"[{LogPrefix}] Exception waiting asset {entry.Path}: {e.Message}");
        }

        removeEntry?.Invoke(entry);
        return null;
    }

    private void SetModelPinned(AssetCacheEntry entry)
    {
        if (entry == null || entry.IsPinned) return;

        entry.IsPinned = true;
        ModelPinnedCount++;
        RemoveFromLRU(entry, ref _modelLRUCount);
    }

    private void SetParticlePinned(AssetCacheEntry entry)
    {
        if (entry == null || entry.IsPinned) return;

        entry.IsPinned = true;
        ParticlePinnedCount++;
        RemoveFromLRU(entry, ref _particleLRUCount);
    }

    private void AddToLRU(AssetCacheEntry entry, ref AssetCacheEntry head, ref AssetCacheEntry tail, ref int count)
    {
        if (entry.IsPinned) return;
        if (entry.Prev != null || entry.Next != null) return;

        entry.Prev = tail.Prev;
        entry.Next = tail;
        tail.Prev.Next = entry;
        tail.Prev = entry;
        count++;
    }

    private void RemoveFromLRU(AssetCacheEntry entry, ref int count)
    {
        if (entry.Prev == null || entry.Next == null) return;

        entry.Prev.Next = entry.Next;
        entry.Next.Prev = entry.Prev;
        entry.Prev = null;
        entry.Next = null;
        count = Mathf.Max(0, count - 1);
    }

    private void TouchModelLRU(AssetCacheEntry entry)
    {
        if (entry.IsPinned) return;
        RemoveFromLRU(entry, ref _modelLRUCount);
        AddToLRU(entry, ref _modelHead, ref _modelTail, ref _modelLRUCount);
    }

    private void TouchParticleLRU(AssetCacheEntry entry)
    {
        if (entry.IsPinned) return;
        RemoveFromLRU(entry, ref _particleLRUCount);
        AddToLRU(entry, ref _particleHead, ref _particleTail, ref _particleLRUCount);
    }

    private void EvictModelLRUIfNeeded()
    {
        while (_modelLRUCount > _modelCapacity)
        {
            if (!TryEvictOldestUnused(_modelHead, _modelTail, _modelEntries, ref _modelLRUCount))
                break;
        }
    }

    private void EvictParticleLRUIfNeeded()
    {
        while (_particleLRUCount > _particleCapacity)
        {
            if (!TryEvictOldestUnused(_particleHead, _particleTail, _particleEntries, ref _particleLRUCount))
                break;
        }
    }

    private void EvictAllModelLRU()
    {
        EvictAllLRU(_modelHead, _modelTail, _modelEntries, ref _modelLRUCount);
    }

    private void EvictAllParticleLRU()
    {
        EvictAllLRU(_particleHead, _particleTail, _particleEntries, ref _particleLRUCount);
    }

    private void EvictModelLRUUnused()
    {
        EvictLRUUnused(_modelHead, _modelTail, _modelEntries, ref _modelLRUCount);
    }

    private void EvictParticleLRUUnused()
    {
        EvictLRUUnused(_particleHead, _particleTail, _particleEntries, ref _particleLRUCount);
    }

    private void EvictAllLRU(AssetCacheEntry head, AssetCacheEntry tail, Dictionary<string, AssetCacheEntry> entries, ref int count)
    {
        var current = head.Next;
        while (current != tail)
        {
            var next = current.Next;
            if (!current.IsPinned)
            {
                EvictEntry(current, entries, ref count);
            }
            current = next;
        }
    }

    private void EvictLRUUnused(AssetCacheEntry head, AssetCacheEntry tail, Dictionary<string, AssetCacheEntry> entries, ref int count)
    {
        var current = head.Next;
        while (current != tail)
        {
            var next = current.Next;
            if (!current.IsPinned && current.RefCount <= 0)
            {
                EvictEntry(current, entries, ref count);
            }
            current = next;
        }
    }

    private void EvictEntry(AssetCacheEntry entry, Dictionary<string, AssetCacheEntry> entries, ref int count)
    {
        if (entry == null) return;
        RemoveFromLRU(entry, ref count);
        if (!string.IsNullOrEmpty(entry.Path) &&
            entries.TryGetValue(entry.Path, out var current) &&
            ReferenceEquals(current, entry))
        {
            entries.Remove(entry.Path);
        }
        entry.ReleaseHandle();
    }

    private bool TryEvictOldestUnused(AssetCacheEntry head, AssetCacheEntry tail, Dictionary<string, AssetCacheEntry> entries, ref int count)
    {
        var current = head.Next;
        while (current != tail)
        {
            var next = current.Next;
            if (!current.IsPinned && current.RefCount <= 0)
            {
                EvictEntry(current, entries, ref count);
                return true;
            }
            current = next;
        }
        return false;
    }

    private void RemoveModelEntry(AssetCacheEntry entry)
    {
        if (entry == null) return;

        bool isCurrent = !string.IsNullOrEmpty(entry.Path) &&
                         _modelEntries.TryGetValue(entry.Path, out var current) &&
                         ReferenceEquals(current, entry);
        bool isLinked = entry.Prev != null && entry.Next != null;
        if (!isCurrent && !isLinked)
        {
            entry.ReleaseHandle();
            return;
        }

        if (!entry.IsPinned)
        {
            RemoveFromLRU(entry, ref _modelLRUCount);
        }
        else if (isCurrent)
        {
            ModelPinnedCount = Mathf.Max(0, ModelPinnedCount - 1);
        }
        if (isCurrent)
        {
            _modelEntries.Remove(entry.Path);
        }
        entry.ReleaseHandle();
    }

    private void RemoveParticleEntry(AssetCacheEntry entry)
    {
        if (entry == null) return;

        bool isCurrent = !string.IsNullOrEmpty(entry.Path) &&
                         _particleEntries.TryGetValue(entry.Path, out var current) &&
                         ReferenceEquals(current, entry);
        bool isLinked = entry.Prev != null && entry.Next != null;
        if (!isCurrent && !isLinked)
        {
            entry.ReleaseHandle();
            return;
        }

        if (!entry.IsPinned)
        {
            RemoveFromLRU(entry, ref _particleLRUCount);
        }
        else if (isCurrent)
        {
            ParticlePinnedCount = Mathf.Max(0, ParticlePinnedCount - 1);
        }
        if (isCurrent)
        {
            _particleEntries.Remove(entry.Path);
        }
        entry.ReleaseHandle();
    }

    private bool CanUseLoadedEntry(AssetCacheEntry entry, Dictionary<string, AssetCacheEntry> entries)
    {
        if (entry == null)
            return false;

        if (IsDisposed || _disposeCts.IsCancellationRequested)
        {
            entry.ReleaseHandle();
            return false;
        }

        if (string.IsNullOrEmpty(entry.Path) ||
            !entries.TryGetValue(entry.Path, out var current) ||
            !ReferenceEquals(current, entry))
        {
            entry.ReleaseHandle();
            return false;
        }

        return true;
    }

    protected override void OnDispose()
    {
        _disposeCts.Cancel();

        foreach (var entry in _modelEntries.Values)
        {
            entry.ReleaseHandle();
        }
        _modelEntries.Clear();

        foreach (var entry in _particleEntries.Values)
        {
            entry.ReleaseHandle();
        }
        _particleEntries.Clear();

        _preloadTasks.Clear();
        _pendingAssets.Clear();
        _modelLRUCount = 0;
        _particleLRUCount = 0;
        ModelPinnedCount = 0;
        ParticlePinnedCount = 0;
        _disposeCts.Dispose();
    }
}

}
