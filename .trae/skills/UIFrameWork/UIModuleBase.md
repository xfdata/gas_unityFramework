# UIModuleBase — 模块基类

所有 UI 框架模块的公共基类。提供生命周期管理、子模块树、CancellationToken 自动传播、资源清理，以及常用工具方法（按钮绑定、定时器、异步任务封装）。

---

## 类层次

```
UIModuleContext (上下文传递器)
  └── UIModuleBase (abstract)
        ├── ViewBase            → UI 视图基类
        ├── UIWindow            → 窗口核心类
        ├── UIMaskWindowModule  → 遮罩功能模块
        └── UIBlurWindowModule  → 模糊功能模块
```

---

## UIModuleContext — 上下文传递器

```csharp
public sealed class UIModuleContext
{
    public UIRuntime Runtime { get; }              // 运行时管理器
    public UIWindow Window { get; }                // 所属窗口（可能为 null）
    public CancellationToken DestroyToken { get; } // 销毁时触发的取消令牌
    public UIRoot Root => Runtime.Root;            // UI 根节点
    public UIModuleContext CreateChildContext();   // 创建子上下文
}
```

子上下文继承父上下文的 Runtime、Window 和 DestroyToken，形成一个取消令牌链。

---

## 生命周期

```
Attach(context) → StartAsync() → OnStart()  ...  Dispose() → OnStop()
```

| 方法 | 调用方 | 说明 |
|------|--------|------|
| `Attach(UIModuleContext)` | 框架内部 | 绑定上下文，**不可手动调用** |
| `StartAsync()` | 框架内部 | 调用 OnStart，Attach 后自动触发 |
| `OnStart()` | 子类覆写 | 模块启动（async），可执行初始化逻辑 |
| `Dispose()` | 框架/业务 | 取消 Token → 逆序 Dispose 子模块 → 执行清理回调 → OnStop |
| `OnStop()` | 子类覆写 | 同步清理资源（不要 await） |

### 状态属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `IsStarted` | `bool` | 是否已执行 OnStart |
| `IsDisposed` | `bool` | 是否已销毁 |
| `Context` | `UIModuleContext` (protected) | 模块上下文 |
| `DestroyToken` | `CancellationToken` (protected) | 销毁取消令牌 |

---

## 子模块管理

```
ParentModule
  ├── ChildModule A  (AddModule)
  ├── ChildModule B  (AddModuleAsync)
  └── ChildModule C  (RegisterChild)
```

| 方法 | 返回值 | 行为 |
|------|--------|------|
| `AddModule<T>(T module)` | `T` | 添加子模块，不 await StartAsync |
| `AddModuleAsync<T>(T module)` | `Task<T>` | 添加子模块，await StartAsync 完成再返回 |
| `RegisterChild(UIModuleBase)` | `void` | 注册已有模块为子模块（不调 StartAsync，用于自行管理的模块） |

子模块销毁规则：
- 父模块 Dispose 时**自动逆序销毁**所有子模块
- 子模块的 DestroyToken 链接到父模块
- **禁止**手动调用子模块 Dispose

---

## 清理注册

```csharp
protected void AddCleanup(Action cleanup)
```

注册清理回调，Dispose 时按注册的**逆序**执行。适用于非 UIModuleBase 的资源（如原生对象、文件句柄）。

---

## 工具方法

### 异步任务

```csharp
protected void RunTask(Func<CancellationToken, UniTask> task)
```

运行异步任务，自动绑定 DestroyToken（模块销毁时自动取消），异常自动捕获并 LogError。

### 定时器

```csharp
// 延迟 seconds 秒后执行回调
protected void Delay(float seconds, Action callback)

// 每隔 seconds 秒执行异步回调
// immediately=true 则立即先执行一次再开始计时
protected void Every(float seconds, Func<UniTask> callback, bool immediately = false)
```

> 内部使用 `UniTask.Delay` 实现，自动绑定 DestroyToken。

### 按钮绑定

```csharp
// 同步 Action 版本
protected void BindClick(Button button, Action action)

// 异步 Func<UniTask> 版本（内部 .Forget()）
protected void BindClick(Button button, Func<UniTask> asyncAction)
```

自动处理按钮的 onClick 事件订阅和取消订阅。

---

## 核心规则

1. **一切异步操作使用 DestroyToken** — 模块销毁时所有 pending 任务自动取消，防止访问已销毁对象。
2. **OnStop 中只做同步清理** — 不要 await UniTask，此时模块已进入销毁流程。
3. **子模块自动管理** — 添加到父模块后由父模块负责销毁，勿手动调用子模块的 Dispose。
4. **Dispose 不可重入** — `_disposed` 标志保护，多次调用安全。

---

## 使用示例

```csharp
public class MyFeature : UIModuleBase
{
    private int _counter;

    protected override async UniTask OnStart()
    {
        // 每 3 秒轮询
        Every(3f, async () =>
        {
            await RefreshData(DestroyToken);
        });

        // 延迟初始化
        Delay(1.5f, () => Debug.Log("Lazy init done"));

        // 运行带取消的异步任务
        RunTask(async token =>
        {
            while (!token.IsCancellationRequested)
            {
                await DoWork(token);
            }
        });

        // 注册非模块资源清理
        var handle = NativeAlloc();
        AddCleanup(() => NativeFree(handle));
    }

    protected override void OnStop()
    {
        _counter = 0;
        // 同步清理，不 await
    }
}
```