# UIRuntime — 运行时管理器

UIRuntime 是 UIFrameWork 的**唯一入口和中央调度器**，管理所有 UIWindow 实例的完整生命周期，提供 Open/Close/Get/IsOpen 等公共 API，同时负责渲染排序、弹窗栈、全屏覆盖隐藏等核心能力。`UIRuntimeBootstrap` 是框架初始化的工厂入口。

---

## 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Instance` | `UIRuntime` (static) | 全局单例引用 |
| `Root` | `UIRoot` | UI 根节点管理器 |
| `Asset` | `IUIAssetService` | 资源加载服务 |
| `Mask` | `UIMaskService` | 遮罩服务 |
| `Blur` | `UIBlurService` | 模糊服务 |
| `InputBlock` | `UIInputBlockService` | 输入拦截服务 |
| `PopupStack` | `IReadOnlyList<UIWindow>` | 只读弹窗栈（ESC 处理用） |
| `AllWindows` | `IReadOnlyList<UIWindow>` | 所有窗口列表 |

---

## 公共 API

### Open — 打开视图

```csharp
public async UniTask<TView> Open<TView>(object param = null) where TView : ViewBase
```

流程：
1. 从 `UIViewRegistry` 获取配置
2. 创建 `UIWindow` 实例，加入 `_allWindows`
3. 若 `config.EnterPopupStack` → 插入 `_popupStack`（按 SortOrder 二分排序）
4. 调用 `window.OpenAsync(viewType, param)` 走完整生命周期
5. 返回 `TView` 实例

> 所有窗口均支持多实例。

### Close — 关闭视图

```csharp
public void Close<TView>(object result = null) where TView : ViewBase
```

查找顶层同类型窗口并调用 `CloseAsync(result).Forget()`（fire-and-forget 模式）。

### Get — 获取视图实例

```csharp
public TView Get<TView>() where TView : ViewBase
```

返回顶层同类型窗口的 View 实例，若不存在返回 null。

### IsOpen — 判断是否打开

```csharp
public bool IsOpen<TView>() where TView : ViewBase
```

返回顶层同类型窗口是否处于 Ready（Opened/Hiding/Hidden）状态。

### HandleEsc — ESC 处理

```csharp
public void HandleEsc()
```

从 `_popupStack` 找到栈顶 Ready 窗口，调用其 `HandleEsc()`。每帧在 Input Update 中调用。

### GetTopPopup — 获取栈顶弹窗

```csharp
public UIWindow GetTopPopup()
```

从 `_popupStack` 逆序查找第一个 Ready 状态的窗口。

---

## 内部机制

### RefreshPresentation

```csharp
internal void RefreshPresentation()
```

窗口变更后的统一刷新入口，执行：
1. `RefreshRenderOrder()` — 按层级分组排序，刷新 Canvas sibling 次序
2. `RefreshCoverState()` — 全屏窗口覆盖隐藏逻辑
3. `Mask.Refresh()` — 刷新遮罩状态

### RefreshRenderOrder

```csharp
private void RefreshRenderOrder()
```

1. 将 `_allWindows` 按 `UILayer` 分组
2. 每组内按 `SortOrder` 升序排列
3. 设置 `SetSiblingIndex(i)` 确保渲染次序正确

### RefreshCoverState

```csharp
private void RefreshCoverState()
```

1. 在 `_popupStack` 中找到最高层级的全屏窗口 `topFullScreen`
2. 若不存在全屏窗口 → 恢复所有窗口显示
3. 若存在：
   - `topFullScreen.PauseLowerView = true` → 隐藏下层窗口
   - `topFullScreen.PauseLowerView = false` → 仅隐藏 GameObject（通过 `HideLowerView` 配置），不改变状态
   - 排序值高于 `topFullScreen` 的窗口不受影响

### RemoveWindow

```csharp
internal void RemoveWindow(UIWindow window)
```

从 `_allWindows` 和 `_popupStack` 中移除窗口。

### FindTopWindow

```csharp
private UIWindow FindTopWindow(Type viewType)
```

从 `_allWindows` 逆序查找最新同类型且未关闭/销毁的窗口。

---

## UIRuntimeBootstrap — 初始化入口

```csharp
public static class UIRuntimeBootstrap
{
    // 默认使用 AddressablesUIAssetService
    public static UIRuntime Create(
        UIViewConfigTable configTable,
        GameObject uiRootObject,
        Camera uiCamera,
        Transform hiddenRoot,
        GameObject inputBlockObject,
        GameObject maskObject);

    // 自定义资源服务
    public static UIRuntime Create(
        UIViewConfigTable configTable,
        GameObject uiRootObject,
        Camera uiCamera,
        Transform hiddenRoot,
        GameObject inputBlockObject,
        GameObject maskObject,
        IUIAssetService assetService);
}
```

**初始化流程**：
1. `UIViewRegistry.Initialize(configTable)` — 初始化配置注册表
2. 创建 `UIRoot(rootObject, uiCamera, hiddenRoot)` — 根节点管理器
3. `RegisterDefaultLayers` — 注册 11 个 UILayer 到 Canvas_XXX 的映射
4. 创建 `UIMaskService`、`UIBlurService`、`UIInputBlockService`
5. 创建 `UIRuntime` 实例，设为 `Instance`

## Dispose

逆序销毁所有窗口 → 清空列表 → 释放所有服务 → 清除 `Instance` 静态引用。

---

## 使用示例

```csharp
// 初始化（游戏启动时）
var runtime = UIRuntimeBootstrap.Create(configTable, rootObj, camera, hiddenRoot, inputBlock, mask);

// 打开窗口
var view = await UIRuntime.Instance.Open<MyView>(new MyParam { Id = 1 });

// 关闭窗口
UIRuntime.Instance.Close<MyView>();

// 查询窗口
if (UIRuntime.Instance.IsOpen<MyView>()) { }
var v = UIRuntime.Instance.Get<MyView>();

// ESC 处理（每帧 Input Update）
UIRuntime.Instance.HandleEsc();
```

---

## 设计要点

- **单一入口**：所有窗口操作必须通过 UIRuntime，禁止直接 new UIWindow。
- **多实例**：所有窗口均支持多实例，同一类型可打开多个副本。
- **fire-and-forget Close**：Close 是同步方法（内部 `.Forget()`），调用方不需要 await。若需感知关闭完成，使用 `UIWindow.CloseAsync` 或 View 的 Close 回调。
- **弹窗栈管理**：`_popupStack` 按 SortOrder 维护，ESC 从栈顶获取，支持全屏覆盖逻辑。