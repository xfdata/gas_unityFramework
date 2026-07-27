---
name: TD-battle-monster-spawn-analysis
overview: 对塔防战斗中怪物不生成且战斗立即结束的问题进行深度分析，第一阶段仅分析不动代码，输出完整的调用链、根本原因、验证方法和修复建议。
todos:
  - id: add-diag-logs-startup
    content: 在 TDBattleEngine.OnBattleStart() 末尾添加诊断日志，打印 _tdConfig.WaveConfigs 长度、CurrentLevelConfig 是否为空、LevelManager.CurrentLevel.WaveConfigs 长度、waveManager.State 和 TotalWaves
    status: completed
  - id: add-diag-logs-wavemanager
    content: 在 WaveManagerSystem.StartNextWave() 方法入口添加诊断日志，打印 TotalWaves、CurrentWaveIndex、以及 State 变更前后的值，确认是否在 TotalWaves==0 时立即置 Cleared
    status: completed
  - id: add-diag-logs-initpath
    content: 在 WaveManagerSystem.InitPathProgress() 的两个分支（PathEntries 和 EnemyEntries fallback）添加日志，打印每条路径的敌人数量，并在 _defaultPath==null 时打印 Error 级别日志
    status: completed
  - id: add-diag-logs-victorycheck
    content: 在 VictoryCheckSystem.CheckVictoryCondition() 中添加诊断日志，打印 AllWavesCleared 值、aliveEnemies 数量、以及判定结果
    status: completed
  - id: add-diag-logs-rule-end
    content: 在 AllWavesClearedRule.OnAllWavesCleared() 和 BattleEngine.EndBattle() 中添加日志，确认战斗结束的触发链
    status: completed
  - id: verify-config
    content: 验证 TowerDefenseGlobalConfig 资产文件中 WaveConfigs 数组和 CurrentLevelConfig 字段是否已赋值，检查关联的 WaveConfig ScriptableObject 是否存在且内含有效敌人条目
    status: completed
  - id: output-report
    content: 汇总所有诊断日志输出，确认根因归属于"配置未设置""路径缺失""逻辑缺陷"中的哪一类，输出完整诊断报告
    status: completed
    dependencies:
      - add-diag-logs-startup
      - add-diag-logs-wavemanager
      - add-diag-logs-initpath
      - add-diag-logs-victorycheck
      - add-diag-logs-rule-end
      - verify-config
---

## 用户需求

在塔防战斗测试中定位"怪物不生成 + 游戏进度直接变成已结束"的根因。第一阶段仅加诊断日志验证假设，不修改核心战斗逻辑。需要输出完整调用链分析、具体问题位置、最可疑原因排序及对应验证方法。

## 现象描述

- 主角创建成功
- 城堡创建成功
- 怪物没有生成
- 游戏进度直接变成"已结束"（Battle Phase 跳转为 Victory/Ended）

## 分析目标

1. 确认波次配置是否为空（TotalWaves == 0）
2. 确认当前关卡配置是否正确加载
3. 确认 DefaultPath 是否为空（路径缺失场景）
4. 定位"已结束"的精确代码触发位置
5. 找出配置/逻辑/顺序三大类问题的根因归属

## 技术分析

### 完整调用链（从进入场景到战斗结束）

```
┌─ PveGameplayMode.EnterAsync()
│   └─ StartTowerDefenseBattle()
│       ├─ _tdConfig = TowerDefenseSceneConfig.Current?.GlobalConfig
│       ├─ new TDBattleEngine(_tdConfig)
│       ├─ _tdEngine.Initialize()
│       │   └─ OnInitialize() → 注册14个System + 2个Rule
│       ├─ _tdEngine.StartBattle()
│       │   └─ ChangePhase(Running) → Context.Start() → OnBattleStart()
│       │       ├─ [1] LevelManager.LoadLevel(CurrentLevelConfig)
│       │       ├─ [2] LevelManager.ApplyToBattleEngine(this)
│       │       │       └─ 如果 CurrentLevel.WaveConfigs 非空 → waveManager.StartWaves()
│       │       ├─ [3] mainCitySystem.SpawnMainCity(...)          ← 城堡创建
│       │       └─ [4] 如果 waveManager.State==Idle 且有 WaveConfigs → StartWaves()
│       └─ [5] new TDPlayerActor().InitPlayer(...)                  ← 主角创建
│
├─ 每帧 TickSimulation(deltaTime):
│   ├─ Step 1: ExecutePendingCommands()     ← 执行 WaveSpawnerCommand 队列
│   ├─ Step 2: Context.Update(deltaTime)     ← WaveManager → VictoryCheck → ...
│   ├─ Step 3: OnUpdate(deltaTime)
│   ├─ Step 4: Context.LateUpdate(deltaTime)
│   ├─ Step 5: CheckEndConditions()          ← 遍历 Rule，检查 IsTriggered
│
└─ 结束路径（当前走的此路径）:
    WaveManager.AllWavesCleared==true
    → VictoryCheckSystem.TriggerVictory()
    → Emit(TDEventIds.AllWavesCleared)
    → AllWavesClearedRule.OnAllWavesCleared()
    → Trigger(EBattleResult.Win)
    → BattleEngine.EndBattle(Win)
    → ChangePhase(Ended)
```

### 根因分析

**Bug A — WaveManagerSystem.StartNextWave()（行71-80）**

```
CurrentWaveIndex++;          // -1 → 0
if (CurrentWaveIndex >= TotalWaves)  // 当 TotalWaves==0 时，0>=0 为 true
{
    State = ETDWaveState.Cleared;    // 立即置为 Cleared
    Debug.Log("[WaveManager] All waves completed!");
    return;                          // 没有任何怪物生成就结束
}
```

**Bug B — WaveManagerSystem.AllWavesCleared 属性（行31）**

```
public bool AllWavesCleared => 
    CurrentWaveIndex >= TotalWaves - 1 && State == ETDWaveState.Cleared;
// 当 TotalWaves==0: TotalWaves-1=-1, CurrentWaveIndex=0, 0>=-1为true
// State==Cleared → 返回 true
```

**串联触发链（Frame 0）：**

1. OnBattleStart中 `StartWaves(emptyArray)` → `StartNextWave()` → TotalWaves==0 → State=Cleared
2. 首帧Tick: Context.Update → VictoryCheckSystem.Update() → CheckVictoryCondition()

- AllWavesCleared = true（Bug B）
- aliveEnemies = 0（从未生成过敌人）
- → TriggerVictory() → Emit AllWavesCleared事件

3. CheckEndConditions → AllWavesClearedRule.OnUpdate()订阅事件后，若事件在本次帧已触发则IsTriggered=true → EndBattle(Win)

### TotalWaves==0 的两个可能原因

**原因1：配置未设置（最可能）**

- `TowerDefenseGlobalConfig.WaveConfigs` 数组为空
- `TowerDefenseGlobalConfig.CurrentLevelConfig` 为null或其`WaveConfigs`为空

**原因2：路径缺失导致波次跳过**

- WaveConfigs非空但全部使用旧版`EnemyEntries`
- `DefaultPath`未设置
- InitPathProgress中走到fallback分支但`_defaultPath==null` → 警告日志但路径列表为空
- UpdateSpawning中allSpawned立即为true → 波次秒过

### 关键文件清单

| 文件 | 路径 | 角色 |
| --- | --- | --- |
| PveGameplayMode.cs | `Assets/Scripts/HotUpdate.Core/Gameplay/Modes/` | 战斗入口 |
| TDBattleEngine.cs | `Assets/Scripts/HotUpdate.Core/Battle/TowerDefense/Core/` | TD引擎 |
| WaveManagerSystem.cs | `Assets/Scripts/HotUpdate.Core/Battle/TowerDefense/Wave/` | 波次管理 |
| VictoryCheckSystem.cs | `Assets/Scripts/HotUpdate.Core/Battle/TowerDefense/System/` | 胜利检查 |
| TDRules.cs | `Assets/Scripts/HotUpdate.Core/Battle/TowerDefense/Core/` | 结束规则 |
| BattleEngine.cs | `Assets/Scripts/HotUpdate.Core/Battle/BattleFoundation/Core/` | 引擎基类 |
| TowerDefenseGlobalConfig.cs | `Assets/Scripts/HotUpdate.Core/Battle/TowerDefense/Config/` | 全局配置 |
| LevelManager.cs | `Assets/Scripts/HotUpdate.Core/Battle/TowerDefense/Meta/Level/` | 关卡注入 |
| WaveConfig.cs | `Assets/Scripts/HotUpdate.Core/Battle/TowerDefense/Wave/` | 波次配表 |
| TDBattlePhaseSystem.cs | `Assets/Scripts/HotUpdate.Core/Battle/TowerDefense/UI/` | 阶段管理 |