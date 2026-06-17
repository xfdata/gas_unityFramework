# UIRoot — UI 根节点管理

管理 UI 根节点体系：根 GameObject、UI Camera、层级 Canvas 注册表、隐藏缓存根节点、异形屏侧偏移量。

---

## 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `RootObject` | `GameObject` | UI 根 GameObject |
| `UICamera` | `Camera` | UI 专用摄像机 |
| `HiddenRoot` | `Transform` | 隐藏/缓存窗口的父节点 |
| `SideOffset` | `float` | 异形屏侧边偏移量（用于安全区域适配） |

---

## 方法

### SetSideOffset

```csharp
public void SetSideOffset(float sideOffset)
```

设置异形屏侧边偏移量。在 `ViewBase.AdaptRootTransform()` 中使用：水平方向减去 `SideOffset × 2`，确保内容不被曲面屏边缘裁切。

### RegisterLayer

```csharp
public void RegisterLayer(UILayer layer, Transform root)
```

将 UILayer 枚举值映射到具体的 Transform 节点（Canvas 子节点）。在 `UIRuntimeBootstrap.Create` 时自动注册。

默认命名规则：

```
UILayer.Scene   → Canvas_Scene
UILayer.World   → Canvas_World
UILayer.Hud     → Canvas_Hud
UILayer.HudTop  → Canvas_Hud_Top
UILayer.Normal  → Canvas_Normal
UILayer.Top     → Canvas_Top
UILayer.Mask    → Canvas_Mask
UILayer.Guide   → Canvas_Guide
UILayer.Tip     → Canvas_Tip
UILayer.Overlay → Canvas_Overlay
UILayer.Debug   → Canvas_Debug
```

### GetLayerRoot

```csharp
public Transform GetLayerRoot(UILayer layer)
```

获取指定层级对应的 Transform。若层级未注册，抛出 `InvalidOperationException`。在 `UIWindow.AttachToLayer()` 中使用，将窗口挂载到正确的 Canvas 层级。

---

## 使用方式

UIRoot 由 `UIRuntimeBootstrap.Create` 自动创建，业务代码通过 Context 访问：

```csharp
// ViewBase 内部
Context.Root.UICamera
Context.Root.SideOffset
Context.Root.GetLayerRoot(UILayer.Normal)
```

---

## 与安全区域适配的关系

```
Screen.safeArea → AdaptRootTransform()
    ├── 水平方向: sizeDelta.x -= Root.SideOffset × 2
    ├── 垂直方向: 根据 safeArea.y 和 safeArea.height 计算裁剪
    └── 若 Config.IgnoreSafeArea → 跳过所有适配
```

SideOffset 通常由平台初始化代码设置，根据设备型号决定是否启用异形屏边距。常见值为 0（无曲面屏）或 30-60（曲面屏两侧留白）。