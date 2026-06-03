# UIFrameWork 框架总览

基于 Unity (UGUI) + Cysharp UniTask + Addressables 的异步 UI 框架。采用 **View-Config-Runtime** 分层架构，支持多实例窗口、缓存复用、遮罩/模糊、安全区域适配、ESCAPE 处理和代码生成绑定系统。

---

## 架构图

```
                    ┌──────────────────────────────────────┐
                    │             UIRuntime                │
                    │  Open / Close / Get / IsOpen         │
                    │  HandleEsc / Dispose                 │
                    │  RefreshRenderOrder / RefreshCover   │
                    │  PopupStack / AllWindows             │
                    └──────────────┬───────────────────────┘
                                   │
          ┌────────────────────────┼────────────────────────┐
          │                        │                        │
   ┌──────▼──────┐          ┌──────▼──────┐          ┌──────▼──────────┐
   │   UIWindow  │          │   UIRoot    │          │  UIMaskService  │
   │  状态机管理  │          │  Canvas 层级 │          │  遮罩跟随/点击   │
   │  加载/卸载   │          │  UICamera   │          └─────────────────┘
   └──────┬──────┘          └─────────────┘          ┌──────────────────┐
          │                                           │ UIInputBlockService│
   ┌──────▼──────┐     ┌─────────────────┐          │  引用计数输入拦截  │
   │  ViewBase   │     │  UIBlurService  │          └──────────────────┘
   │  OnOpen等钩子│     │  模糊效果(占位)  │
   └──────┬──────┘     └─────────────────┘
          │
   ┌──────▼────────┐
   │ UIModuleBase  │
   │ 子模块+定时器  │
   └───────────────┘
```

---

## 文件清单

| 文件 | 角色 | 详见 |
|------|------|------|
| `UIRuntime.cs` | 入口 + 中央调度器 + 排序 + 覆盖管理 | [UIRuntime.md](UIRuntime.md) |
| `UIWindow.cs` | 窗口核心状态机 | [UIWindow.md](UIWindow.md) |
| `ViewBase.cs` | 视图基类（钩子 + 适配） | [ViewBase.md](ViewBase.md) |
| `UIModuleBase.cs` | 模块基类（子模块 + 工具） | [UIModuleBase.md](UIModuleBase.md) |
| `UIServices.cs` | 输入拦截 + 遮罩 | [UIServices.md](UIServices.md) |
| `UIRoot.cs` | 根节点 / Canvas 注册 | [UIRoot.md](UIRoot.md) |
| `UITypes.cs` | 全部枚举定义 | [UITypes.md](UITypes.md) |
| `UIAssetService.cs` | Addressables 资源加载 | [UIAssetService.md](UIAssetService.md) |
| `Config/UIViewConfig.cs` | 视图配置数据类 | — |
| `Config/UIViewConfigTable.cs` | ScriptableObject 配置表 | — |
| `Bind/UIBindNode.cs` | 自动绑定 + 代码生成 | — |
| `Components/HoleMask.cs` | 挖洞遮罩（引导高亮） | — |

---

## 核心概念

### 层级系统 (UILayer)

11 个层级，值越大渲染越靠上，每个对应一个 `Canvas_XXX` 节点：

```
Scene(10) → World(20) → Hud(30) → HudTop(40) → Normal(50)
→ Top(60) → Mask(70) → Guide(80) → Tip(90) → Overlay(100) → Debug(110)
```

排序公式: `SortOrder = layer × 1000000 + sortOffset × 10000 + windowIndex`

### 缓存模式 (UICacheMode)

| 模式 | 行为 |
|------|------|
| `DestroyOnClose` | 关闭时销毁 GameObject |
| `HideOnClose` | 关闭时隐藏到 HiddenRoot，保留实例 |
| `Preload` | 类似 HideOnClose，用于预加载 |

### 遮罩模式 (UIMaskMode)

| 模式 | 行为 |
|------|------|
| `None` | 无遮罩 |
| `BlockInputOnly` | 透明遮罩，仅拦截输入 |
| `DarkMask` | 深色半透明遮罩 |
| `DarkMaskClose` | 深色遮罩 + 点击关闭窗口 |

### 窗口状态机 (UIWindowState)

```
None → Loading → Opening → Opened ⇄ Hiding → Hidden
                                ↓
                          Closing → Closed → Disposed
```

---

## 接入步骤

```csharp
// 1. 初始化（首场景 Awake）
var runtime = UIRuntimeBootstrap.Create(
    configTable,       // UIViewConfigTable ScriptableObject
    uiRootObject,      // UI 根 GameObject
    uiCamera,          // UI 专用 Camera
    hiddenRoot,        // 隐藏/缓存窗口父节点
    inputBlockObject,  // 输入拦截遮罩 GameObject
    maskObject         // 深色遮罩 GameObject (需含 Image + Button)
);

// 2. 打开窗口
var view = await UIRuntime.Instance.Open<MyView>(new MyParam { Id = 1 });

// 3. 关闭窗口
UIRuntime.Instance.Close<MyView>();

// 4. 查询
if (UIRuntime.Instance.IsOpen<MyView>()) { }
var v = UIRuntime.Instance.Get<MyView>();

// 5. ESC 处理 (每帧)
UIRuntime.Instance.HandleEsc();
```

---

## 与 Gameplay 框架的关系

```
Game (顶层入口)
├── GameplayRuntime   ← 模式切换 + 场景管理
│   └── IGameplayUiService (接口)
│       └── UIFrameWorkGameplayUiService (适配器)  ← 桥接点
└── UIRuntime         ← UI 窗口管理 + 弹窗栈
```

- Gameplay 通过 `IGameplayUiService` 接口间接操作 UI
- UIFrameWork 完全不知道 Gameplay 的存在
- 适配器单向依赖：Gameplay → Adapter → UIFrameWork