---
alwaysApply: false
description: 
---
# Project Rules for Trae

这些规则用于 Trae 在本项目中回答、分析和修改代码时遵守。优先级低于用户当前明确指令，但高于临时猜测。

## 1. 优先读取项目 Skill 文档

当问题涉及 Gameplay 模式切换、GameplayFlowMachine、IGameplayMode、GameplayRuntime、场景管理、跨模式通信时，先阅读：

- `.trae/skills/Gameplay/SKILL.md`
- `.trae/skills/Gameplay/README.md`

当问题涉及 UI、Mediator、View、Canvas、遮罩、Tip、Loading 时，先阅读：

- `.trae/skills/UIManager/SKILL.md`
- `.trae/skills/UIManager/README.md`
- `.trae/skills/UIManager/UIManager.md`
- `.trae/skills/UIManager/MediatorBase.md`
- `.trae/skills/UIManager/UIModuleBase.md`

当问题涉及表现队列、Action 调度、优先级、冲突、取消、手动激活时，先阅读：

- `.trae/skills/QueueAction/SKILL.md`
- `.trae/skills/QueueAction/README.md`
- `.trae/skills/QueueAction/QueuedActionBase.md`

当问题同时涉及 UI 弹窗和 Action 队列时，优先阅读：

- `.trae/skills/QueueAction/UIManagerRelation.md`

## 2. UI 分层规则

UI 代码按以下职责拆分：

- `UIManager`：全局 UI 创建、关闭、Canvas、遮罩、Tip、Loading。
- `MediatorBase`：单个 UI 的生命周期、状态机、显示/关闭、业务控制。
- `ViewBase`：View 节点、控件引用、轻量表现封装。
- `UIModuleBase`：模块生命周期、子模块、通知、红点、Timer、资源回收。

修改 UI 时优先保持这个边界：

- 业务流程写在 Mediator。
- 控件节点操作封装在 View。
- 全局 UI 创建/关闭走 UIManager。
- 模块化能力走 UIModuleBase。

## 3. 不要手动修改生成文件

不要直接修改以下类型文件：

- `*.gen.cs`
- `*.binding.cs`

例如：

- `Assets/Scripts/HotUpdate.Game/View/Login/LoginView/LoginView.gen.cs`
- `Assets/Scripts/HotUpdate.Game/View/Login/LoginView/LoginView.binding.cs`

这些文件通常由工具生成，手改会被覆盖。

正确做法：

1. 如果需要新增控件引用，先维护 prefab/reference/binding 配置并重新生成。
2. 业务逻辑写在同名手写 partial 文件中，例如 `LoginView.cs`。
3. Mediator 调用 View 暴露的语义化 public 方法，而不是散落操作生成节点。

推荐：

```csharp
// LoginView.cs
public void SetGetServerTipsVisible(bool visible)
{
    nodeGetServerTip.SetActive(visible);
}

// LoginViewMediator.cs
View.SetGetServerTipsVisible(true);
```

## 4. Mediator 和 View 访问规则

业务 Mediator 通常继承：

```csharp
MediatorBase<TView>
```

在 Mediator 子类中：

- 优先使用 `protected T View` 访问强类型 View。
- 不要直接替换 `ViewComponent`。
- 不要把大量节点操作写在 Mediator 里，优先在 View 中封装方法。

在 View 中：

- 只封装节点操作、显示状态、按钮事件转发、轻量表现。
- 不接管 Mediator 生命周期。
- 不直接调用 UIManager 关闭自己，关闭流程由 Mediator 控制。

## 5. UI 弹窗和 Action 队列规则

如果 `mediator.IsPopupWin == true`，`UIManager` 创建它时会投递：

```text
ActionOpenPopupUI -> GamePlayMgr.PresentationScheduler
```

这会产生一个 running `GameplayActionType.OpenedUI`，直到 Mediator 释放结束。

分析“Action 为什么没执行”时，需要检查：

- 是否有 running `OpenedUI`。
- `GamePlayActionConflictRules` 是否允许当前 Action 和 `OpenedUI` 并行。
- Action 是否被 global condition、tag、condition 或 conflict rule 阻止。

## 6. QueueAction 编写规则

新增 Action 时：

1. 优先继承 `QueuedActionBase`。
2. 构造函数中设置明确的 `ActionType`。
3. 设置合理的 `Priority` 和 `SubPriority`。
4. 表现结束必须调用 `Finish()`。
5. 取消路径调用 `Cancel()`。
6. 事件解绑和资源清理写在 `OnDispose()`。
7. 需要条件时使用 `Conditions`、`RequireTags`、`BlockTags`。
8. 需要外部触发时使用手动激活机制。
9. 新 ActionType 如果要和其他 running Action 并行，检查并补充 `GamePlayActionConflictRules`。

注意：

- 默认 `ActionType` 是 `GameplayActionType.ActionStuck`，不要忘记改。
- 没有匹配冲突规则时默认 `Reject`。
- `ExecuteImmediate` 会忽略 `Reject`，但仍处理 `Interrupt`。
- `TryActivateQueuedAction` 和 `TryActivateQueuedActionType` 检查条件不同。

## 7. 修改代码前的习惯

修改前先读相关源码和 skill 文档，不只凭文件名猜。

优先使用现有模式：

- 找同目录同类写法。
- 复用已有 helper、manager、module。
- 不引入新的框架风格。
- 不重构无关代码。

如果文件是生成文件或资源文件，除非用户明确要求，否则不要改。

## 8. 回答风格

回答时优先给结论和调用链。

解释复杂系统时，使用这种结构：

```text
入口
  -> 中间关键对象
  -> 状态变化
  -> 结束/释放
```

如果修改代码，说明：

- 改了哪些文件。
- 为什么这样改。
- 是否运行了验证。
- 有哪些剩余风险。
