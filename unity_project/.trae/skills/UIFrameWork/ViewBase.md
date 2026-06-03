# ViewBase — 视图基类

所有 UI 视图的抽象基类。定义打开/关闭/刷新/动画/ESC/安全区域适配等完整生命周期钩子。提供泛型变体以支持强类型参数和 Binder。

---

## 继承链

```
UIModuleBase
  └── ViewBase (abstract)
        ├── ViewBase<TParam>        → 泛型参数，自动 object→TParam 转换
        │     └── ViewBase<TParam, TBinder>  → 泛型参数 + 强类型 Binder
        └── 业务 View（继承上述任一）
```

---

## 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `GameObject` | `GameObject` | 实例化后的根 GameObject |
| `Transform` | `Transform` | 根 Transform |
| `RectTransform` | `RectTransform` | 根 RectTransform |
| `Window` | `UIWindow` | 所属的 UIWindow 实例 |
| `Config` | `UIViewConfig` | 视图配置（快捷访问 Window.Config） |
| `B` | `UIViewBinder` (protected) | 绑定器（泛型子类可 override 为具体类型） |

---

## 生命周期钩子

### 打开流程

```
BindView → OnBind() → OnOpen(param) → AdaptRootTransform() → PlayOpenAnimation() → OnShown()
```

| 方法 | 签名 | 调用时机 |
|------|------|----------|
| `OnBind()` | `protected virtual void` | View 绑定到 Window 后立即同步调用 |
| `OnOpen(param)` | `protected virtual UniTask OnOpen(object param)` | 窗口打开，执行 UI 初始化逻辑 |
| `PlayOpenAnimation()` | `protected virtual UniTask` | 入场动画（完成前窗口不可见） |
| `OnShown()` | `protected virtual void` | 窗口完全显示后（动画结束） |

### 刷新流程

```
RefreshInternal(param) → OnRefresh(param)
```

| 方法 | 签名 | 调用时机 |
|------|------|----------|
| `OnRefresh(param)` | `protected virtual UniTask OnRefresh(object param)` | Single 模式窗口被再次 Open 时调用 |

### 关闭流程

```
PlayCloseAnimation() → OnClose(result)
```

| 方法 | 签名 | 调用时机 |
|------|------|----------|
| `PlayCloseAnimation()` | `protected virtual UniTask` | 退场动画（先播动画再关） |
| `OnClose(result)` | `protected virtual UniTask OnClose(object result)` | 窗口关闭，清理资源 |

### 其他钩子

| 方法 | 签名 | 说明 |
|------|------|------|
| `OnEsc()` | `protected virtual bool` | ESC 键处理，`return true` 阻止默认关闭行为 |
| `OnAdaptRoot()` | `protected virtual void` | 安全区域适配时的自定义逻辑 |

---

## 视图操作 API

```csharp
protected void Close(object result = null)                         // 关闭自身
protected UniTask<TView> Open<TView>(object param = null)          // 打开其他 View
    where TView : ViewBase
```

---

## 安全区域适配

`AdaptRootTransform()` 在窗口打开时自动调用：

1. 计算原始尺寸 `_originSize` = sizeDelta + parentSize × (anchorMax - anchorMin)
2. 水平方向减去 `SideOffset × 2`（异形屏侧边距）
3. 根据 `Screen.safeArea` 计算上下裁剪量
4. 调整 RectTransform 的 sizeDelta 和 localPosition
5. 若 `Config.IgnoreSafeArea` 为 true → 跳过适配

模糊 Transform 也会同步进行安全区域适配。

---

## 泛型变体

### ViewBase\<TParam\>

自动完成 `object param` → `TParam` 类型转换，子类直接使用强类型参数：

```csharp
public abstract class ViewBase<TParam> : ViewBase
{
    protected sealed override UniTask OnOpen(object param)
    protected virtual UniTask OnOpen(TParam param)      // 子类重写此方法

    protected sealed override UniTask OnRefresh(object param)
    protected virtual UniTask OnRefresh(TParam param)    // 子类重写此方法
}
```

### ViewBase\<TParam, TBinder\>

提供编译时安全的强类型 Binder 访问：

```csharp
public abstract class ViewBase<TParam, TBinder> : ViewBase<TParam>
    where TBinder : UIViewBinder
{
    protected new TBinder B => (TBinder)base.B;  // 覆盖 B 为具体 Binder 类型
}
```

---

## 使用示例

```csharp
// 基础 View：无参数、字符串绑定
public class MySimpleView : ViewBase
{
    protected override UniTask OnOpen(object param)
    {
        B.Txt("Title").Value = "Hello";
        B.Btn("Close").OnClick(() => Close());
        return UniTask.CompletedTask;
    }
}

// 强类型参数 View
public class ShopView : ViewBase<ShopParam>
{
    protected override UniTask OnOpen(ShopParam param)
    {
        B.Txt("ShopName").Value = param.ShopName;
        return UniTask.CompletedTask;
    }
}

// 强类型 Binder View（推荐）
public class ShopView : ViewBase<ShopParam, ShopViewBinder>
{
    protected override UniTask OnOpen(ShopParam param)
    {
        B.BtnClose.OnClick(() => Close());         // 编译时安全
        B.TxtTitle.Value = $"Shop: {param.Id}";    // 智能提示
        return UniTask.CompletedTask;
    }

    protected override bool OnEsc()
    {
        // 自定义 ESC：弹出确认对话框而不是直接关闭
        Open<ConfirmDialog>(new ConfirmParam { Message = "确定要关闭吗？" }).Forget();
        return true; // 阻止默认关闭
    }
}
```

---

## 设计要点

- **virtual 钩子而非 abstract**：所有生命周期方法均为 virtual，子类按需覆写，无需实现空方法。
- **强类型推荐**：优先使用 `ViewBase<TParam, TBinder>`，编译时类型安全且支持 IDE 智能提示。
- **动画非阻塞**：`PlayOpenAnimation` 为 async，动画期间窗口不对用户可见，完成后才触发 `OnShown`。
- **安全区域自动适配**：默认启用，若全屏背景图需要延伸到刘海区域，设置 `Config.IgnoreSafeArea = true`。