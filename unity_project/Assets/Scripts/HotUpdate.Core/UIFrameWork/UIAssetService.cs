using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public interface IUIAssetService
{
    UniTask<GameObject> InstantiateAsync(AssetReferenceGameObject reference, Transform parent, CancellationToken token);
    void Release(GameObject instance);
}

public sealed class AddressablesUIAssetService : IUIAssetService
{
    private readonly Queue<QueuedLoad> _queue = new();
    private readonly object _lock = new();

    private sealed class QueuedLoad
    {
        public AsyncOperationHandle<GameObject> Handle;
        public UniTaskCompletionSource<GameObject> Tcs;
        public volatile bool LoadCompleted;
        public Exception Error;
    }

    public async UniTask<GameObject> InstantiateAsync(AssetReferenceGameObject reference, Transform parent, CancellationToken token)
    {
        if (reference == null || string.IsNullOrWhiteSpace(reference.AssetGUID))
            throw new ArgumentException("[AddressablesUIAssetService] AssetReference cannot be null or empty.", nameof(reference));

        token.ThrowIfCancellationRequested();

        var tcs = new UniTaskCompletionSource<GameObject>();
        var handle = reference.InstantiateAsync(parent, false);

        var load = new QueuedLoad { Handle = handle, Tcs = tcs };

        CancellationTokenRegistration ctr = default;
        if (token.CanBeCanceled)
        {
            ctr = token.Register(() =>
            {
                load.LoadCompleted = true;
                load.Error = new OperationCanceledException(token);
                DrainQueue();
            });
        }

        lock (_lock) { _queue.Enqueue(load); }

        handle.Completed += h =>
        {
            ctr.Dispose();

            if (load.LoadCompleted)
            {
                ReleaseCompleted(h);
                return;
            }

            load.LoadCompleted = true;
            if (h.Status != AsyncOperationStatus.Succeeded || h.Result == null)
            {
                load.Error = h.OperationException ??
                    new Exception($"[AddressablesUIAssetService] Failed to instantiate UI prefab: {reference}");
                if (h.IsValid())
                    Addressables.Release(h);
            }

            DrainQueue();
        };

        return await tcs.Task;
    }

    private void DrainQueue()
    {
        lock (_lock)
        {
            while (_queue.Count > 0 && _queue.Peek().LoadCompleted)
            {
                var load = _queue.Dequeue();
                if (load.Error != null)
                    load.Tcs.TrySetException(load.Error);
                else
                    load.Tcs.TrySetResult(load.Handle.Result);
            }
        }
    }

    public void Release(GameObject instance)
    {
        if (instance != null)
            Addressables.ReleaseInstance(instance);
    }

    private static void ReleaseCompleted(AsyncOperationHandle<GameObject> handle)
    {
        if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
        {
            Addressables.ReleaseInstance(handle.Result);
            return;
        }

        if (handle.IsValid())
            Addressables.Release(handle);
    }
}
