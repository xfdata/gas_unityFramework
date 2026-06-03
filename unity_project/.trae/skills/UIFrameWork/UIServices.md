# UIServices — 服务集合

包含三个 UI 基础设施服务：`UIInputBlockService`（输入拦截）、`UIMaskService`（深色遮罩）、`UIBlurService`（模糊效果）。

---

## UIInputBlockService — 输入拦截服务

### 设计

基于**引用计数**的输入拦截管理。窗口在 Loading→Opening 过渡期间自动添加引用，防止用户在动画/加载期间操作其他 UI。

### API

```csharp
public void AddRef(string reason)     // +1 引用计数，首次添加时激活拦截 GameObject
public void RemoveRef(string reason)  // -1 引用计数，归零时关闭拦截 GameObject
```

| 属性 | 类型 | 说明 |
|------|------|------|
| `ReferenceCount` | `int` | 当前引用计数 |

### 使用方式

**由框架自动管理**。UIWindow 在 `OpenAsync` 开始时 `AddRef("Open_{ViewName}")`，在 finally 中 `RemoveRef`。业务层通常无需直接调用。

特殊情况可手动使用：

```csharp
UIRuntime.Instance.InputBlock.AddRef("LoadingData");
try { await LoadDataAsync(); }
finally { UIRuntime.Instance.InputBlock.RemoveRef("LoadingData"); }
```

---

## UIMaskService — 遮罩服务

为窗口提供**深色/透明遮罩**，遮罩节点动态跟随最高层级窗口的位置。

### 遮罩模式

| 模式 | 行为 |
|------|------|
| `BlockInputOnly` | 透明遮罩（alpha=0），仅拦截 Raycast |
| `DarkMask` | 深色半透明遮罩 |
| `DarkMaskClose` | 深色遮罩 + 点击遮罩关闭窗口 |

### API

```csharp
public void Show(UIWindow window, UIMaskMode mode)  // 为窗口显示遮罩
public void Hide(UIWindow window)                    // 移除窗口的遮罩
public void Refresh()                                // 刷新遮罩状态（跟随窗口变化）
```

### Refresh 逻辑

1. 遍历所有注册了遮罩的窗口，找到 SortOrder 最高且状态正常的窗口
2. 将遮罩 GameObject 挂载到目标窗口同级（SiblingIndex = 目标窗口前一个）
3. 根据模式设置颜色透明度和交互
4. 若无符合条件的窗口 → 隐藏遮罩节点

### HandleClick — 遮罩点击

- 模式为 `DarkMaskClose`，或窗口 `Config.CloseByMask = true` → 关闭对应窗口
- 其他模式 → 仅拦截点击

### 前置条件

遮罩 GameObject 必须包含 **Image** 和 **Button** 组件。

---

## UIBlurService — 模糊服务

**占位实现**。目前仅记录已配置的模糊模式，实际模糊效果待后续完成。

### API

```csharp
public void Attach(UIWindow window, UIBlurMode mode)
public void Detach(UIWindow window)
```

---

## 初始化

由 `UIRuntimeBootstrap.Create` 自动完成：

```csharp
var mask = new UIMaskService(maskObject);            // maskObject 需含 Image + Button
var blur = new UIBlurService();                      // 当前为占位
var inputBlock = new UIInputBlockService(inputBlockObject);  // 全屏透明拦截层

// 通过 Runtime 访问
UIRuntime.Instance.Mask.Show(window, UIMaskMode.DarkMask);
UIRuntime.Instance.InputBlock.AddRef("reason");
```