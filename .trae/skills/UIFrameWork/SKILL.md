# UIFrameWork Skill

当问题涉及 UIFrameWork、UI 框架、UIWindow、ViewBase、UIModuleBase、UIRuntime、UIRoot、UIMaskService、UIInputBlockService、UIBlurService、UIViewConfig、UITypes、UILayer、安全区域适配、弹窗栈、遮罩、模糊、UI 层级、UI 窗口打开/关闭、ESCAPE 处理、UIBindNode、代码生成 Binder、Addressables UI 资源加载时，优先使用本 skill。

## 文档顺序

1. `README.md`
   - UIFrameWork 总览：架构图、文件结构、核心概念（层级/缓存/遮罩/生命周期）、接入步骤。
2. `UIRuntime.md`
   - 运行时管理器：Open/Close/Get/IsOpen API、弹窗栈管理、渲染排序、覆盖隐藏、Bootstrap 初始化入口、Dispose。
3. `UIWindow.md`
   - 窗口核心：状态机（None→Loading→Opening→Opened→Hiding→Hidden→Closing→Closed→Disposed）、OpenAsync 流程、CloseAsync 流程、缓存管理、覆盖显示/隐藏、异常处理。
4. `ViewBase.md`
   - 视图基类：生命周期钩子（OnOpen/OnClose/OnRefresh/OnShown/OnEsc）、安全区域适配、泛型变体（TParam/TBinder）、Open/Close API。
5. `UIModuleBase.md`
   - 模块基类：生命周期（OnStart/OnStop）、子模块管理、CancellationToken 传播、工具方法（BindClick/Delay/Every/RunTask/AddCleanup）。
6. `UIServices.md`
   - 服务集合：UIInputBlockService（引用计数拦截）、UIMaskService（遮罩跟随/深色/点击关闭）、UIBlurService（占位）。
7. `UIRoot.md`
   - 根节点管理：UICamera、层级 Canvas 注册、异形屏偏移、HiddenRoot。
8. `UITypes.md`
   - 枚举参考：UILayer、UICacheMode、UIMaskMode、UIBlurMode、UISafeAreaMode、UIWindowState。
9. `UIAssetService.md`
   - 资源服务：IUIAssetService 接口、Addressables 实现、自定义接入。

## 核心调用链

```text
UIRuntimeBootstrap.Create(configTable, rootObj, camera, hiddenRoot, ...)
  └─ UIRuntime (Instance)
       └─ Open<TView>(param)
            └─ UIWindow.OpenAsync
                 ├─ Asset.InstantiateAsync(PrefabReference, HiddenRoot)
                 ├─ ViewBase.BindView → OnStart → OnOpen
                 ├─ EnterPopupStack (入栈)
                 ├─ Mask.Show / Blur.Attach
                 ├─ PlayOpenAnimation → OnShown
                 └─ RefreshPresentation
                      ├─ RefreshRenderOrder (层级内 sibling 排序)
                      ├─ RefreshCoverState (全屏覆盖显隐)
                      └─ Mask.Refresh

Close<TView>()
  └─ UIWindow.CloseAsync
       ├─ RemoveWindow (出栈)
       ├─ PlayCloseAnimation → OnClose
       ├─ Mask.Hide / Blur.Detach
       └─ 根据 CacheMode: Destroy 或 HideForCache

Update (每帧)
  └─ UIRuntime.HandleEsc() → GetTopPopup() → 顶层弹窗.HandleEsc()
```