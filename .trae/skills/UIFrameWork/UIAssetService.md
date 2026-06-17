# UIAssetService — 资源服务

定义 UI 框架的资源加载抽象接口 `IUIAssetService`，并提供基于 Unity Addressables 的默认实现 `AddressablesUIAssetService`。所有 UI Prefab 的加载和释放均通过此服务，支持替换为自定义加载方式。

---

## IUIAssetService 接口

```csharp
public interface IUIAssetService
{
    UniTask<GameObject> InstantiateAsync(string address, Transform parent, CancellationToken token);
    void Release(GameObject instance);
}
```

| 方法 | 调用方 | 说明 |
|------|--------|------|
| `InstantiateAsync` | UIWindow.OpenAsync | 异步实例化 Prefab，支持取消令牌 |
| `Release` | UIWindow.Dispose / CloseAsync | 释放实例（回收引用计数，由 Addressables 决定是否真正销毁） |

---

## AddressablesUIAssetService — 默认实现

### InstantiateAsync

```csharp
public async UniTask<GameObject> InstantiateAsync(string address, Transform parent, CancellationToken token)
```

1. 调用 `Addressables.InstantiateAsync(address, parent, false, true)`
2. 通过 `UniTask.WaitUntil` 等待 AsyncOperationHandle 完成
3. 成功 → 返回 `handle.Result`
4. 失败 → 释放 handle 并抛出 Exception
5. 取消：
   - handle 已完成 → 立即 `Addressables.ReleaseInstance(result)`
   - handle 未完成 → 注册 Completed 回调，完成时释放

### Release

```csharp
public void Release(GameObject instance)
```

调用 `Addressables.ReleaseInstance(instance)`，由 Addressables 管理引用计数。

---

## 自定义资源服务

实现 `IUIAssetService` 即可替换默认的 Addressables 加载方式：

```csharp
public class MyAssetService : IUIAssetService
{
    public async UniTask<GameObject> InstantiateAsync(string address, Transform parent, CancellationToken token)
    {
        // 自定义加载逻辑（如 Resources.Load、AssetBundle、自行管理的对象池）
        var prefab = await LoadFromMySystem(address, token);
        var instance = Object.Instantiate(prefab, parent);
        return instance;
    }

    public void Release(GameObject instance)
    {
        // 自定义释放逻辑（如回收到对象池）
        MyPool.Release(instance);
    }
}

// 初始化时传入自定义服务
var runtime = UIRuntimeBootstrap.Create(
    configTable, rootObj, camera, hiddenRoot,
    inputBlock, mask,
    new MyAssetService()
);
```

---

## 错误处理

| 情况 | 行为 |
|------|------|
| Address 为空 | 抛出 `ArgumentException` |
| 加载失败 | 抛出含详细错误信息的 Exception |
| 取消操作 | 确保不泄露 Addressables handle |

---

## 依赖

- 默认实现依赖 `Unity.Addressables` 包
- `UIViewConfig.PrefabPath` 必须为有效的 Addressables address key
- Prefab 的 Addressables Group 需正确配置