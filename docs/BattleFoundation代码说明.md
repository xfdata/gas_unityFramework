# BattleFoundation 代码说明

> 代码目录：`unity_project/Assets/Scripts/HotUpdate.Core/Battle/BattleFoundation`  
> 定位：L0 战斗运行时骨架。本文面向技术审查，说明框架组成、流程、核心类和使用边界。

## 1. 整体框架

BattleFoundation 不实现具体的攻击、技能、怪物或 UI，而是提供所有战斗模式共享的确定性运行环境。

```text
应用/玩法层：关卡、PVE、输入、UI、MonoBehaviour
                         |
                         v
BattleCommon：CombatActor、AI、属性、攻击、投射物、表现桥接
                    |                         |
                    v                         v
BattleFoundation：引擎、帧、实体、系统、命令、事件、规则、回放    GAS：技能、Effect、Tag、属性运行时
```

BattleFoundation 包含以下部分：

| 部分 | 负责内容 |
| --- | --- |
| 核心运行时 | 战斗阶段、帧推进、时间缩放、系统驱动、战斗结束。 |
| 上下文与系统 | 每场战斗独立的实体管理、事件总线、随机数、日志和系统注册。 |
| 实体模型 | `BattleEntity`、`EntityComponent` 及实体索引。 |
| 命令 | 按目标帧和序列执行、可记录、可回放的输入命令。 |
| 规则 | 超时、全灭、平局等胜负条件。 |
| 回放 | 帧快照、命令记录、动态实体创建/销毁的扩展接口。 |
| 基础工具 | 无 Unity 依赖的数值类型、数学函数和确定性随机数。 |

依赖约束：BattleFoundation 不能依赖 BattleCommon、GAS、PVE、具体单位或 Unity 表现。BattleCommon 可以依赖 BattleFoundation。

## 2. 核心运行流程

### 2.1 战斗生命周期

```text
Initialize
  -> 创建 BattleContext
  -> 创建 EntityManager、EventBus、SystemManager、BattleRandom
  -> 注册系统和规则
  -> Ready

StartBattle
  -> Context.Start
  -> 启动已注册系统和已生成实体
  -> Running

每个仿真帧
  -> FrameIndex++、更新时间
  -> 执行到期命令
  -> System.Update
  -> 引擎 OnUpdate 扩展点
  -> System.LateUpdate
  -> 引擎 OnLateUpdate 扩展点
  -> 胜负规则检查
  -> 记录回放帧

EndBattle / Dispose
  -> 停止录制或回放
  -> 清理规则、系统、实体、事件和命令
```

`BattleEngine` 支持实时、帧同步、回合制三种推进方式。权威战斗逻辑只使用 Context 提供的帧时间和 `BattleRandom`，不能使用 `UnityEngine.Random`、墙钟时间或隐式 Transform 状态。

### 2.2 命令和回放流程

```text
业务输入
  -> BattleEngine.EnqueueCommand
  -> CommandQueue 按 CommandFrame + CommandSequence 排序
  -> Tick 开始时执行所有到期命令
  -> CommandExecuted / CommandFailed 事件
  -> BattleRecorder 记录命令和当前帧快照

回放
  -> BattlePlayback 读取帧和命令
  -> 命令工厂恢复并执行命令
  -> IBattleReplayAdapter 创建不存在的动态实体
  -> 恢复基础状态与业务扩展状态
  -> Adapter 回收当前帧已不存在的实体
```

## 3. 目录和核心类

### 3.1 `Core/`：引擎、上下文和系统

| 类/文件 | 作用 | 主要使用方式 |
| --- | --- | --- |
| `BattleEngine` (`Core/BattleEngine.cs`) | 战斗阶段机和唯一 Tick 入口；管理时间、命令、规则、录制和回放。 | 战斗模式继承它，在 `OnInitialize` 注册系统，在 `OnBeforeBattleStart` 生成初始对象；Unity 层调用 `UpdateFromUnity`。 |
| `BattleContext` (`Core/BattleContext.cs`) | 每场战斗的服务容器，持有实体、事件、系统、随机数和日志。 | 通过 `Context.AddSystem<T>` 注册系统，使用 `GetSystem<T>` 获取已注册系统。 |
| `IBattleSystem` / `BattleCore` | 系统生命周期契约：`Initialize`、`Start`、`Update`、`LateUpdate`、`Dispose`。 | 所有帧驱动战斗系统实现该接口，禁止另建并行 Update。 |
| `BattleSystemManager` | 系统的有序注册、延迟增删和生命周期转发。 | 更新期间移除或新增系统会延后处理，避免遍历集合时越界或跳过系统。 |
| `EBattlePhase` | 战斗阶段、结果和 Tick 模式枚举。 | 业务根据阶段决定是否允许输入、暂停、回放或结算。 |
| `IBattleLog` | 可注入的日志契约。 | 由启动层注入 `BattleEngine.SetLog`，命令失败和事件订阅异常会写入同一日志。 |
| `Disposable` | 幂等释放基类。 | 持有事件、实体或资源的对象在 `OnDispose` 释放，不能依赖 GC。 |
| `BattleRuntimeSettings` | 不依赖 Unity 的运行时设置。 | 上层把 ScriptableObject 配置转换后传给引擎，L0 不直接引用 Unity 配置。 |

### 3.2 `Entity/`：实体和组件

| 类/文件 | 作用 | 主要使用方式 |
| --- | --- | --- |
| `BattleEntity` | 通用战斗实体；保存 Id、阵营、类型、位置、旋转和组件集合。 | 派生类重写必要生命周期；`Start` 只会在 `Initialize` 后执行一次。 |
| `EntityComponent` | 实体组件基类，持有 Owner 和激活状态。 | 组件实现自身初始化、更新、池化回收和释放；事件订阅必须在回收/释放时取消。 |
| `EntityManager` | 按 Id、阵营维护实体索引和全量集合。 | 只由拥有系统注册/移除实体；迭代 `All` 时不能直接改变集合。 |

### 3.3 `Event/`：战斗事件

| 类/文件 | 作用 | 主要使用方式 |
| --- | --- | --- |
| `BattleEventBus` | 按整数事件 Id 和载荷类型分发事件。 | `On<T>` 订阅、`Off<T>` 反订阅、`Emit<T>` 发送；单个订阅者异常会记录日志而不阻断其他订阅者。 |
| `BattleEventIds` | Foundation 事件编号。 | 包含实体创建/移除、命令成功/失败、阶段改变和规则触发等事件。 |

### 3.4 `Command/`：可调度命令

| 类/文件 | 作用 | 主要使用方式 |
| --- | --- | --- |
| `BattleCommand` | 抽象命令，保存来源、目标、命令类型、目标帧和序列号。 | 子类实现 `GetCommandTypeId`、`OnExecute`，有状态命令重写序列化和反序列化。 |
| `BattleCommandRecord` | 命令回放记录。 | 保存稳定的类型、帧、序列、实体 Id 和载荷。 |
| `IBattleCommandFactory` | 由战斗模式提供的命令反序列化工厂。 | 回放开始前注入 Engine；Factory 不属于 Foundation，避免 L0 依赖玩法命令。 |
| `CommandQueue` | 按帧和序列排序的待执行命令队列。 | 引擎在每个 Tick 开始时只消费到期命令。 |

### 3.5 `Rule/`：胜负规则

| 类/文件 | 作用 | 主要使用方式 |
| --- | --- | --- |
| `BattleRuleBase` | 规则基类，维护触发状态、结果和已运行时间。 | 规则通过 `Engine.AddRule` 注册；触发时发送 `RuleTriggered`。 |
| `WinLoseConditionBase` | 胜负条件抽象基类。 | 用于扩展需要计算条件明细的玩法规则。 |
| `TimeoutRule` | 达到限制时间后返回 Timeout。 | 创建时传入时间上限。 |
| `AllEnemiesDeadRule` | 检查敌我存活数：双方归零为 Draw，单方归零为 Win/Lose。 | 通过固定间隔检查，避免每帧重复统计。 |

### 3.6 `Replay/`：录制与回放

| 类/文件 | 作用 | 主要使用方式 |
| --- | --- | --- |
| `BattleRecorder` | 录制战斗元数据、命令和每帧快照。 | 由 Engine 在开启回放设置后创建，并在开始/结束战斗时控制。 |
| `BattlePlayback` | 按时间播放快照和录制命令。 | 由 Engine 进入 Replaying 阶段后驱动。 |
| `BattleRecord` / `FrameRecordData` | 完整回放数据和单帧数据。 | 记录随机种子、Tick 设置、帧、命令和最终结果。 |
| `EntitySnapshot` | 实体的基础快照：身份、阵营、位置、旋转、生存状态及扩展数据。 | Foundation 恢复基础字段；业务字段通过 Adapter 扩展。 |
| `IBattleReplayAdapter` | 将动态实体和业务状态适配到回放的接口。 | BattleCommon 实现它，以创建/销毁 CombatActor 并恢复 GAS 属性，而非让 Foundation 引用 GAS。 |

### 3.7 `BattleMath/`、`Utils/`：基础数值与随机数

| 类/文件 | 作用 | 主要使用方式 |
| --- | --- | --- |
| `Float2`、`Float3`、`Float4` | 无 Unity 依赖的二维、三维、四维数值类型。 | Foundation 中的位置、方向和旋转权威数据使用这些类型。 |
| `BattleMathF` | 通用浮点数学操作。 | 替代战斗权威层对 Unity 数学 API 的依赖。 |
| `IRandom` | 随机数抽象。 | 由 Context 暴露给系统、AI 或适配器。 |
| `BattleRandom` | 基于 xorshift128 的确定性随机数实现。 | 相同种子得到相同序列，是帧同步和回放可复现性的基础。 |

## 4. 使用和审查约束

- 新系统必须用 `BattleContext.AddSystem` 注册。
- 新实体必须经其所属系统管理，不直接在实体遍历期间修改 `EntityManager.All`。
- 未指定 `CommandFrame` 的命令会在下一仿真帧执行，不能影响已开始的当前帧。
- 新增长生命周期业务状态时，应判断是否需要写入 `EntitySnapshot` 和 Adapter 恢复逻辑。
- 回放中动态实体必须由 `IBattleReplayAdapter` 创建和回收，不能只从 EntityManager 删除。
- 每个 EventBus、属性或 Unity 回调订阅都必须有明确的反订阅路径。

## 5. 当前结论

BattleFoundation 作为 L0 已形成清晰边界：它负责“如何稳定推进一场战斗”，但不决定“战斗中具体发生什么”。实体生命周期、命令帧调度、胜负规则和动态实体回放均已具备明确的拥有者和扩展点，适合供多个战斗模式复用。
