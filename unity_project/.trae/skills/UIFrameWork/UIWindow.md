# UIWindow — 窗口核心类

UIWindow 是 UI 框架的核心，管理一个 View 实例从创建到销毁的**完整生命周期**。负责 Prefab 加载、View 创建绑定、层级挂载、动画播放、缓存管理、遮罩/模糊功能模块的创建及 ESC 处理。

---

## 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `ViewType` | `Type` | View 的 C# 具体类型 |
| `Config` | `UIViewConfig` | 视图配置（来自配置表） |
| `View` | `ViewBase` | View 实例（Loading 完成后有效） |
| `GameObject` | `GameObject` | 实例化后的根 GameObject |
| `State` | `UIWindowState` | 当前状态 |
| `WindowIndex` | `int` | 全局唯一递增窗口索引 |
| `SortOrder` | `int` | 排序值 = layer×1,000,000 + sortOffset×10,000 + windowIndex |
| `IsReady` | `bool` | 是否处于 Opened 状态 |
| `IsCached` | `bool` | 是否处于 Hidden 状态（缓存隐藏） |
| `IsOpening` | `bool` | 是否处于 Loading 或 Opening 状态 |

---

## 状态机

```
None → Loading → Opening → Opened ⇄ Hiding → Hidden
                                ↓
                          Closing → Closed → Disposed
```

| 状态 | 含义 |
|------|------|
| `None` | 初始状态 |
| `Loading` | 正在通过 Addressables 加载 Prefab |
| `Opening` | 执行 OnOpen + 播放入场动画 |
| `Opened` | 完全打开，可见且可交互 |
| `Hiding` | 被上层全屏窗口覆盖，已隐藏 |
| `Hidden` | 缓存隐藏状态（GameObject 移到 HiddenRoot 并 SetActive(false)） |
| `Closing` | 播放退场动画 + 执行 OnClose |
| `Closed` | 已关闭，短暂停留后进入 Disposed（仅缓存模式） |
| `Disposed` | 已销毁，不可再使用 |

---

## 公共 API

### OpenAsync — 打开窗口

```csharp
public UniTask OpenAsync(Type viewType, object param)
```

**状态分支**：
- 已在 Opening → 返回正在进行的 task（防重入）
- 已 Opened（非缓存） → 直接返回 CompletedTask
- IsCached → 复用已有实例，重新激活
- 否则 → 走完整 Loading→Opening→Opened 流程

**完整流程**：
1. `InputBlock.AddRef` — 添加输入拦截引用
2. `Loading` — `Asset.InstantiateAsync(Config.PrefabReference, HiddenRoot)` 加载 Prefab
3. `CreateViewModule` — 创建 View 实例 + `ViewBase.BindView` + `StartAsync`
4. `Opening` — `View.OpenInternal(param)` 执行 OnOpen
5. 创建窗口功能模块（MaskModule / BlurModule）
6. `AttachToLayer()` — 挂载到对应层级 Canvas
7. `View.AdaptRootTransform()` — 安全区域适配
8. `IUIWindowOpenBlocker.PrepareBeforeShow()` — 模块准备
9. `View.PlayOpenAnimationInternal()` — 播放入场动画
10. `Opened` — 状态设为 Opened，`View.ShownInternal()`
11. `RefreshPresentation` — 刷新渲染排序 + 覆盖状态 + 遮罩
12. `InputBlock.RemoveRef` — 移除输入拦截引用（finally 保证）

> 窗口已通过 `UIRuntime.Open` 在预制加载前加入 `_allWindows` 和 `_popupStack`。

### RefreshAsync — 刷新窗口

```csharp
public async UniTask RefreshAsync(object param)
```

调用 `View.RefreshInternal(param)`。若窗口处于被覆盖隐藏状态，先 `ReShowByCover()` 恢复显示。

### CloseAsync — 关闭窗口

```csharp
public UniTask CloseAsync(object result = null)
```

**流程**：
1. 状态设为 Closing
2. 隐藏 Mask / 移除 Blur
3. 播放退场动画 `View.PlayCloseAnimationInternal()`
4. 调用 `View.CloseInternal(result)`
5. 根据 CacheMode：
   - `DestroyOnClose` → Closed → Dispose → RemoveWindow（彻底销毁）
   - `HideOnClose` / `Preload` → `HideForCache()`（GameObject 隐藏并移到 HiddenRoot）
6. `RefreshPresentation` — 刷新渲染排序 + 覆盖状态 + 遮罩

### 覆盖显示/隐藏

| 方法 | 触发场景 |
|------|----------|
| `HideByCover()` | 被上层全屏窗口覆盖时，隐藏 GameObject |
| `ReShowByCover()` | 上层全屏窗口关闭后恢复显示 |

### HandleEsc — ESC 处理

```csharp
public bool HandleEsc()
```

1. 若 `Config.CloseByEsc` 为 false → 返回 true（消费 ESC 但不关闭）
2. 调用 `View.EscInternal()`，若返回 true（业务拦截）→ 停止处理
3. 否则执行 `CloseAsync()` 关闭窗口

---

## 内部机制

### CreateViewModule

```csharp
private ViewBase CreateViewModule(Type viewType, GameObject gameObject)
```

1. 从 GameObject 获取 `CSharpUIBindBehaviour` 组件
2. 通过 `UIViewBinderFactory.Create(viewType, bind)` 创建 Binder
3. 通过 `Activator.CreateInstance(viewType)` 反射创建 View 实例
4. 调用 `view.BindView(this, gameObject, binder)` 绑定
5. `RegisterChild(view)` 注册为子模块

### HideForCache

```csharp
private void HideForCache()
```

- `GameObject.SetActive(false)`
- Transform 移到 `HiddenRoot`
- `_hiddenForCache = true`
- State = Hidden

### 异常处理

- Opening 期间任何异常都会触发 `Dispose()` + `RemoveWindow()`，防止脏状态残留。
- `ThrowIfDisposed()` 在关键步骤检查是否已被销毁。
- `finally` 块确保 `InputBlock.RemoveRef` 一定被执行。

---

## 使用方式

通常不直接构造 UIWindow。通过 `UIRuntime.Open<TView>()` 间接使用。

直接访问窗口对象的场景（如自定义模块）：

```csharp
// ViewBase 内部
Window.CloseAsync(result);    // 关闭自身窗口
Window.Config.Layer;          // 查看层级配置
Window.IsReady;               // 检查是否完全打开
```