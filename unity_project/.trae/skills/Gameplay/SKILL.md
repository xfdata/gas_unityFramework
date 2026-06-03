# Gameplay Skill

当问题涉及 Gameplay 模式切换、GameplayFlowMachine、GameplayMode、GameplayContext、场景管理、跨模式通信、IGameplayMode 生命周期，或 GameplayRuntime 入口时，优先使用本 skill。

## 文档顺序

1. `README.md`
   - 使用说明：如何接入、配置、新增 Mode、替换子系统实现。
2. `SKILL.md`
   - 当前入口和系统地图。

## 核心模型

```text
GameplayRuntime (MonoBehaviour)
    │
    ├── GameplaySystemHub      ← 场景 / 音频 / UI 子系统注入
    ├── GameplayEventBus       ← 跨模块事件通信
    └── GameplayFlowMachine    ← 模式切换状态机
            │
            └── GameplayModeRegistry (工厂注册)
                    │
                    └── IGameplayMode
                            ├── CityGameplayMode
                            ├── WorldGameplayMode
                            └── PveGameplayMode
```

## Mode 生命周期

```
新 Mode                      旧 Mode
─────────────────            ─────────────────
LoadAsync()                  ExitAsync()
    ↓                            ↓
EnterAsync()                 Dispose()
```

## 切换流程

```text
GameplayFlowMachine.SwitchToAsync(request)
  -> 忙则排队 / 丢弃 (BusyPolicy)
  -> RunSwitchLoopAsync
  -> ExecuteSwitchAsync
       -> 旧 Mode.ExitAsync + Dispose
       -> 切空场景 + GC
       -> 新 Mode.LoadAsync
       -> 新 Mode.EnterAsync
       -> CommitModeSwitch
```

## 关键词

- `GameplayRuntime` — MonoBehaviour 入口
- `GameplayFlowMachine` — 切换状态机核心
- `IGameplayMode` — Mode 生命周期接口
- `GameplayModeBase` — Mode 抽象基类
- `GameplayContext` — 全局上下文
- `GameplayBlackboard` — 跨 Mode 黑板
- `GameplayEventBus` — 事件总线
- `GameplaySystemHub` — 子系统注入中心
- `GameplaySwitchRequest` — 链式构建切换请求
- `GameplaySwitchResult` — 切换结果
- `GameplayModeRegistry` — Mode 工厂注册
- `ParameterStore` — 参数存取
- `GameplayProgressReporter` — 加载进度上报
- `IGameplaySceneSystem` / `IGameplayAudioSystem` / `IGameplayUiSystem` — 子系统接口

## 相关源码

- `Assets/Scripts/HotUpdate.Core/Gameplay/Bootstrap/GameplayRuntime.cs`
- `Assets/Scripts/HotUpdate.Core/Gameplay/Core/GameplayFlowMachine.cs`
- `Assets/Scripts/HotUpdate.Core/Gameplay/Core/IGameplayMode.cs`
- `Assets/Scripts/HotUpdate.Core/Gameplay/Core/GameplayModeBase.cs`
- `Assets/Scripts/HotUpdate.Core/Gameplay/Core/GameplayContext.cs`
- `Assets/Scripts/HotUpdate.Core/Gameplay/Core/GameplayBlackboard.cs`
- `Assets/Scripts/HotUpdate.Core/Gameplay/Core/GameplayModeRegistry.cs`
- `Assets/Scripts/HotUpdate.Core/Gameplay/Core/GameplaySwitchRequest.cs`
- `Assets/Scripts/HotUpdate.Core/Gameplay/Core/GameplaySwitchResult.cs`
- `Assets/Scripts/HotUpdate.Core/Gameplay/Core/GameplaySwitchTypes.cs`
- `Assets/Scripts/HotUpdate.Core/Gameplay/Core/GameplayModeId.cs`
- `Assets/Scripts/HotUpdate.Core/Gameplay/Core/ParameterStore.cs`
- `Assets/Scripts/HotUpdate.Core/Gameplay/Core/GameplayProgressReporter.cs`
- `Assets/Scripts/HotUpdate.Core/Gameplay/Events/GameplayEventBus.cs`
- `Assets/Scripts/HotUpdate.Core/Gameplay/Events/GameplayEvents.cs`
- `Assets/Scripts/HotUpdate.Core/Gameplay/Services/GameplaySystemHub.cs`
- `Assets/Scripts/HotUpdate.Core/Gameplay/Services/IGameplaySceneSystem.cs`
- `Assets/Scripts/HotUpdate.Core/Gameplay/Services/IGameplayAudioSystem.cs`
- `Assets/Scripts/HotUpdate.Core/Gameplay/Services/IGameplayUiSystem.cs`
- `Assets/Scripts/HotUpdate.Core/Gameplay/Modes/CityGameplayMode.cs`
- `Assets/Scripts/HotUpdate.Core/Gameplay/Modes/WorldGameplayMode.cs`
- `Assets/Scripts/HotUpdate.Core/Gameplay/Modes/PveGameplayMode.cs`

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