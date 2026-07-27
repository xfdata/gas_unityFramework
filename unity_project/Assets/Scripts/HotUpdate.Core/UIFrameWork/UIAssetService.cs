using System;
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
    private sealed class PendingLoad
    {
        public AsyncOperationHandle<GameObject> Handle;
        public UniTaskCompletionSource<GameObject> Tcs;
        public CancellationTokenRegistration CancellationRegistration;
        public CancellationToken CancellationToken;
        public int CompletionState;
    }

    public async UniTask<GameObject> InstantiateAsync(
        AssetReferenceGameObject reference,
        Transform parent,
        CancellationToken token)
    {
        if (reference == null || string.IsNullOrWhiteSpace(reference.AssetGUID))
            throw new ArgumentException("[AddressablesUIAssetService] AssetReference cannot be null or empty.", nameof(reference));

        token.ThrowIfCancellationRequested();

        var load = new PendingLoad
        {
            Handle = reference.InstantiateAsync(parent, false),
            Tcs = new UniTaskCompletionSource<GameObject>(),
            CancellationToken = token,
        };

        load.Handle.Completed += handle => CompleteLoad(load, handle, reference);

        if (token.CanBeCanceled)
        {
            load.CancellationRegistration = token.Register(() => CancelLoad(load));
            if (Volatile.Read(ref load.CompletionState) != 0)
                load.CancellationRegistration.Dispose();
        }

        return await load.Tcs.Task;
    }

    public void Release(GameObject instance)
    {
        if (instance != null)
            Addressables.ReleaseInstance(instance);
    }

    private static void CompleteLoad(
        PendingLoad load,
        AsyncOperationHandle<GameObject> handle,
        AssetReferenceGameObject reference)
    {
        if (Interlocked.CompareExchange(ref load.CompletionState, 1, 0) != 0)
        {
            ReleaseCompleted(handle);
            return;
        }

        load.CancellationRegistration.Dispose();
        if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
        {
            if (handle.IsValid())
                Addressables.Release(handle);

            load.Tcs.TrySetException(handle.OperationException ??
                new Exception($"[AddressablesUIAssetService] Failed to instantiate UI prefab: {reference}"));
            return;
        }

        load.Tcs.TrySetResult(handle.Result);
    }

    private static void CancelLoad(PendingLoad load)
    {
        if (Interlocked.CompareExchange(ref load.CompletionState, 1, 0) == 0)
            load.Tcs.TrySetCanceled(load.CancellationToken);
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
