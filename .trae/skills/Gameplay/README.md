# Gameplay Runtime 框架使用说明

纯 C# 状态机驱动的 Gameplay 模式管理框架。不依赖 PlayMaker / Facade / Notification，只依赖 Unity + Cysharp.Threading.Tasks。

## 目录结构

```
Gameplay/
├── Bootstrap/
│   └── GameplayRuntime.cs            # MonoBehaviour 入口，挂首场景 GameObject
├── Core/
│   ├── IGameplayMode.cs              # Mode 生命周期接口
│   ├── GameplayModeBase.cs           # Mode 抽象基类（默认空实现）
│   ├── GameplayModeId.cs             # Mode 枚举
│   ├── GameplayModeRegistry.cs       # Mode 工厂注册
│   ├── GameplayFlowMachine.cs        # 切换状态机（核心）
│   ├── GameplayContext.cs            # 全局上下文
│   ├── GameplayBlackboard.cs         # 跨 Mode 黑板
│   ├── ParameterStore.cs             # 共享参数存取（Blackboard / Request 复用）
│   ├── GameplaySwitchRequest.cs      # 切换请求（链式构建）
│   ├── GameplaySwitchResult.cs       # 切换结果
│   ├── GameplaySwitchTypes.cs        # Reason + BusyPolicy 枚举
│   ├── GameplayProgressReporter.cs   # 加载进度上报
│   └── IGameplayProgress.cs          # 进度接口
├── Events/
│   ├── GameplayEventBus.cs           # 事件总线
│   └── GameplayEvents.cs             # 内置事件定义
├── Modes/
│   ├── CityGameplayMode.cs           # City 模式
│   ├── WorldGameplayMode.cs          # World 模式
│   └── PveGameplayMode.cs            # PVE 模式
├── Services/
│   ├── GameplaySystemHub.cs          # 子系统注入中心
│   ├── IGameplaySceneSystem.cs       # 场景子系统接口
│   ├── IGameplayAudioSystem.cs       # 音频子系统接口
│   ├── IGameplayUiSystem.cs          # UI 子系统接口
│   ├── SceneMgrGameplaySceneSystem.cs    # Addressables 场景适配器（默认）
│   ├── UnitySceneGameplaySceneSystem.cs  # Unity SceneManager 适配器（备选）
│   ├── UnityAudioGameplayAudioSystem.cs  # 音频适配器示例
│   ├── UnityGameplayUiSystem.cs          # UI 适配器示例
│   └── UIFrameWorkGameplayUiSystem.cs    # UIFrameWork UI 适配器
└── Views/
    ├── CityMainView.cs               # City 主界面
    ├── WorldMainView.cs              # World 主界面
    ├── BattleMainView.cs             # PVE 战斗界面
    ├── LoadingView.cs                # 加载界面
    └── LoginView.cs                  # 登录界面
```

---

## 核心分层

```
GameplayRuntime (MonoBehaviour)
    │
    ├── GameplaySystemHub      ← 场景 / 音频 / UI 子系统注入
    ├── GameplayEventBus       ← 跨模块事件通信
    └── GameplayFlowMachine    ← 模式切换状态机
            │
            └── GameplayModeRegistry
                    │
                    └── IGameplayMode
                            ├── CityGameplayMode
                            ├── WorldGameplayMode
                            └── PveGameplayMode
```

---

## Mode 生命周期

每个 Mode 经历 3 个核心阶段（`IGameplayMode.cs`）：

```
新 Mode                      旧 Mode
─────────────────            ─────────────────
LoadAsync()                  ExitAsync()
    ↓                            ↓
EnterAsync()                 Dispose()
```

- **LoadAsync(request, token)** — 加载场景 + 资源
- **EnterAsync(request, token)** — 播放 BGM、打开 UI、启动玩法
- **ExitAsync(nextRequest, token)** — 关闭 UI、停止 BGM、卸载场景
- **Dispose()** — 释放 Mode 自身资源（由 GameplayModeBase 统一管理）

---

## 设计原则

| # | 原则 | 说明 |
|---|------|------|
| 1 | 状态机只管流程 | `GameplayFlowMachine` 不写 City/World/PVE 业务 |
| 2 | Mode 只管自己 | 每个 Mode 实现自己的加载、进入、退出、卸载 |
| 3 | 子系统可替换 | 通过 `GameplaySystemHub.With*()` 替换任意子系统实现 |
| 4 | 先加载后退出 | 新 Mode 加载成功才退出旧 Mode，减少黑屏风险 |
| 5 | 切换防抖 | 切换中收到新请求，默认只保留最后一次（ReplacePending） |

---

## 接入步骤

### 1. 添加依赖

确认项目已安装 `Cysharp.Threading.Tasks`。

### 2. 挂载入口

在首场景创建一个 GameObject，挂 `GameplayRuntime` 组件。

### 3. 配置场景

确保 Build Settings / Addressables 中包含场景名：`City`、`World`、`PVE`。

### 4. 调用 API

```csharp
// 进入 City
await GameplayRuntime.Instance.EnterCityAsync("DefaultSpawn");

// 进入 World
await GameplayRuntime.Instance.EnterWorldAsync(10001);

// 进入 PVE
await GameplayRuntime.Instance.EnterPveAsync(chapterId: 1, sectionId: 101, startImmediately: true);

// 从 PVE 返回
await GameplayRuntime.Instance.ExitPveAsync();
```

---

## 替换子系统实现

### 场景加载

框架默认使用 `SceneMgrGameplaySceneSystem`（基于 Addressables）：

```csharp
// 替换为 Unity SceneManager
var systems = GameplaySystemHub.CreateDefault()
    .WithSceneSystem(new UnitySceneGameplaySceneSystem());

// 替换为 YooAsset
var systems = GameplaySystemHub.CreateDefault()
    .WithSceneSystem(new YooAssetGameplaySceneSystem());
```

### UI 框架

```csharp
// 接入 UIFrameWork
var systems = GameplaySystemHub.CreateDefault()
    .WithUiSystem(new UIFrameWorkGameplayUiSystem(uiRuntime));
```

### 音频

```csharp
// 接入 Wwise
var systems = GameplaySystemHub.CreateDefault()
    .WithAudioSystem(new WwiseGameplayAudioSystem());
```

### 完整自定义

```csharp
var systems = GameplaySystemHub.CreateDefault()
    .WithSceneSystem(new YooAssetGameplaySceneSystem())
    .WithUiSystem(new UIFrameWorkGameplayUiSystem(uiRuntime))
    .WithAudioSystem(new WwiseGameplayAudioSystem());
```

---

## 新增玩法

以新增 Rogue 为例：

### 1. 添加枚举

```csharp
// GameplayModeId.cs
public enum GameplayModeId
{
    None = 0,
    City = 1,
    World = 2,
    Pve = 3,
    Rogue = 4,   // ← 新增
}
```

### 2. 实现 Mode

```csharp
public sealed class RogueGameplayMode : GameplayModeBase
{
    private const string SceneName = "Rogue";

    public override GameplayModeId Id => GameplayModeId.Rogue;

    public RogueGameplayMode(GameplayContext context) : base(context) { }

    public override async UniTask LoadAsync(GameplaySwitchRequest request, CancellationToken token)
    {
        await Context.Systems.Scenes.LoadSceneAsync(SceneName, LoadSceneMode.Single, null, token);
    }

    public override UniTask EnterAsync(GameplaySwitchRequest request, CancellationToken token)
    {
        Context.Systems.Audio.PlayBgm("bgm_rogue");
        Context.Systems.Ui.Open<RogueMainView>();
        return UniTask.CompletedTask;
    }

    public override async UniTask ExitAsync(GameplaySwitchRequest nextRequest, CancellationToken token)
    {
        Context.Systems.Ui.Close<RogueMainView>();
        Context.Systems.Audio.StopBgm();
        await Context.Systems.Scenes.UnloadSceneAsync(SceneName, token);
    }
}
```

### 3. 注册

```csharp
// GameplayRuntime.CreateRegistry()
registry.Register(GameplayModeId.Rogue, ctx => new RogueGameplayMode(ctx));
```

---

## 切换流程详解

```text
GameplayFlowMachine.SwitchToAsync(request)
  │
  ├── 忙且 DropIfBusy → 返回 Dropped
  ├── 忙且 ReplacePending → 覆盖 _pendingRequest，返回 Pending
  └── 空闲 → RunSwitchLoopAsync(firstRequest)
              │
              └── 循环处理 _pendingRequest 直到队列空
                   │
                   └── ExecuteSwitchAsync(request)
                         │
                         ├── 相同 Mode 且非 Force → 返回 Skipped
                         ├── 发布 GameplaySwitchStartedEvent
                         ├── [非首次切换] 旧 Mode.ExitAsync → Dispose
                         │     → SwitchToEmptyScene → GC
                         ├── 新 Mode.LoadAsync (含 loading UI)
                         ├── 新 Mode.EnterAsync
                         ├── CommitModeSwitch
                         └── 发布 GameplaySwitchCompletedEvent
```

---

## 事件系统

内置事件（`GameplayEvents.cs`）：

| 事件 | 触发时机 |
|------|---------|
| `GameplaySwitchStartedEvent` | 切换开始 |
| `GameplaySwitchPendingEvent` | 切换排队 |
| `GameplaySwitchSkippedEvent` | 同 Mode 跳过 |
| `GameplayModeLoadStartedEvent` | 场景加载开始 |
| `GameplayModeLoadCompletedEvent` | 场景加载完成 |
| `GameplaySwitchCompletedEvent` | 切换完成 |
| `GameplaySwitchFailedEvent` | 切换失败 |
| `GameplayTickEvent` | 每帧（仅在有订阅者时发布） |
| `GameplayLoadingProgressEvent` | 加载进度更新 |

订阅示例：

```csharp
_events.Subscribe<GameplaySwitchCompletedEvent>(e =>
{
    Debug.Log($"Switched: {e.From} → {e.To}");
});
```

### 性能注意事项

- `Subscribe` 返回 `IDisposable`，用于取消订阅
- Mode 退出时调用 `Clear()` 清理订阅防泄漏
- `GameplayTickEvent` 只在有订阅者时才发布 struct，无订阅者零开销

---

## 切换请求链式 API

```csharp
GameplaySwitchRequest
    .To(GameplayModeId.Pve, GameplaySwitchReason.UserAction)
    .Set("ChapterId", 1)
    .Set("SectionId", 101)
    .Set("StartImmediately", true)
    .SetForce()
    .SetBusyPolicy(GameplaySwitchBusyPolicy.DropIfBusy)
    .SetLoadingPolicy(GameplayLoadingPolicy.Default)
    .SetDebugName("Enter PVE 1-101");
```

参数访问（Mode 内部）：

```csharp
var chapterId = request.GetOrDefault("ChapterId", 0);
if (request.TryGet<int>("SectionId", out var sectionId)) { }
```

---

## 黑板（跨 Mode 通信）

```csharp
// 写入（上一个 Mode）
Context.Blackboard.Set("City.LastSpawnPoint", "NorthGate");

// 读取（下一个 Mode）
var spawn = Context.Blackboard.GetOrDefault("City.LastSpawnPoint", "Default");
```

---

## 切换结果

```csharp
public enum GameplaySwitchResultType
{
    Success = 0,
    SkippedSameMode = 1,
    DroppedBecauseBusy = 2,
    AcceptedAsPending = 3,
    Canceled = 4,
    Failed = 5,
}
```

`IsSuccess` 属性对 `Success` / `SkippedSameMode` / `AcceptedAsPending` 返回 true。

---

## 与 UIFrameWork 的关系

```
Game（顶层入口）
├── GameplayRuntime   ← 模式切换 + 场景管理
│   └── IGameplayUiSystem (接口)
│       └── UIFrameWorkGameplayUiSystem (适配器)  ← 桥接点
└── UIRuntime         ← UI 窗口管理 + 弹窗栈
```

- Gameplay 不直接依赖 UIFrameWork，仅依赖 `IGameplayUiSystem` 接口
- UIFrameWork 完全不知道 Gameplay 的存在
- 通过适配器实现单向依赖：Gameplay → Adapter → UIFrameWork

---

## 推荐后续扩展

- **GameplayModeStack** — 支持 PVE → World → City 返回链
- **TransitionPipeline** — 把 Loading / 黑屏 / 镜头过渡 / 资源预热拆成可配置步骤
- **GameplayErrorPolicy** — 加载失败后自动回退到 City / Login
- **GameplayReplayHook** — PVE Mode 中记录输入、随机种子、关键状态