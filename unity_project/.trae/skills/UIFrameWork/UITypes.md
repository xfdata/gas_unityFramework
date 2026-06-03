# UITypes — 枚举定义参考

UIFrameWork 所有枚举类型的完整定义和取值说明。

---

## UILayer — UI 层级

值越大渲染越靠前，每个层级在 UIRoot 中对应一个 `Canvas_XXX` 子节点。

| 值 | 名称 | 典型用途 |
|----|------|----------|
| 10 | `Scene` | 场景层（3D 场景上方） |
| 20 | `World` | 世界空间 UI |
| 30 | `Hud` | HUD 层（血条、名字） |
| 40 | `HudTop` | HUD 上层（头顶对话框） |
| 50 | `Normal` | 普通 UI 层（**默认**，主界面/商店/背包） |
| 60 | `Top` | 顶层（半屏弹窗） |
| 70 | `Mask` | 遮罩层 |
| 80 | `Guide` | 引导层（新手引导高亮） |
| 90 | `Tip` | 提示层（浮动 Tips / Toast） |
| 100 | `Overlay` | 覆盖层（Loading/重连/系统级弹窗） |
| 110 | `Debug` | 调试层（开发工具面板） |

排序公式：`SortOrder = (int)Layer × 1,000,000 + SortOffset × 10,000 + WindowIndex`

---

## UICacheMode — 缓存模式

| 值 | 行为 |
|----|------|
| `DestroyOnClose` | 关闭时销毁 GameObject，再次打开需重新加载 Prefab |
| `HideOnClose` | 关闭时隐藏到 HiddenRoot，GameObject 保留，再次打开直接激活 |
| `Preload` | 类似 HideOnClose，用于启动时预加载后立即隐藏的窗口 |

> 频繁打开/关闭的窗口（如背包、商店）使用 HideOnClose 提升响应速度。偶尔打开的提示框使用 DestroyOnClose 节省内存。

---

## UIMaskMode — 遮罩模式

| 值 | 视觉效果 | 点击行为 |
|----|----------|----------|
| `None` | 无遮罩 | — |
| `BlockInputOnly` | 透明 | 拦截输入，透传点击 |
| `DarkMask` | 深色半透明 | 拦截输入 |
| `DarkMaskClose` | 深色半透明 | 拦截输入 + 点击关闭窗口 |

---

## UIBlurMode — 模糊模式

| 值 | 说明 |
|----|------|
| `None` | 无模糊 |
| `SnapshotBlur` | 快照模糊（截取当前画面做模糊） |
| `RealTimeBlur` | 实时模糊 |

> 当前为占位实现，UIBlurService 尚未完成模糊渲染管线。

---

## UISafeAreaMode — 安全区域模式

| 值 | 行为 |
|----|------|
| `Adapt` | 自动适配异形屏安全区域（刘海/曲面屏） |
| `Ignore` | 忽略安全区域，全屏显示 |

---

## UIWindowState — 窗口状态

```
None → Loading → Opening → Opened ⇄ Hiding → Hidden
                                ↓
                          Closing → Closed → Disposed
```

| 值 | 含义 |
|----|------|
| `None` | 初始状态 |
| `Loading` | 正在通过 Addressables 加载 Prefab |
| `Opening` | 执行 OnOpen + 播放入场动画 |
| `Opened` | 完全打开，可见可交互 |
| `Hiding` | 被上层全屏窗口覆盖，正在隐藏 |
| `Hidden` | 已隐藏（被覆盖或缓存隐藏） |
| `Closing` | 正在执行退场动画和 OnClose |
| `Closed` | 已关闭（短暂停留后进入 Disposed） |
| `Disposed` | 已销毁，不可再使用 |