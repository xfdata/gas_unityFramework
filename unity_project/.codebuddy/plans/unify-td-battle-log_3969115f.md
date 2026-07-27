---
name: unify-td-battle-log
overview: 创建统一的 BattleLog 类，将所有塔防战斗核心日志（约50条）替换为统一入口，清除 TDBattleEngine.cs 中的临时诊断日志（约25条）。不涉及 UI/Tower/Roguelike/Meta 模块。
todos:
  - id: create-battlelog
    content: 新建 BattleLog.cs 统一日志类，包含总开关、9个分类开关、各类别的 Debug/Warning/Error 方法和 FormattableString 重载
    status: completed
  - id: replace-wavemanager
    content: 替换 WaveManagerSystem.cs 中20条日志为 BattleLog.Wave/ConfigMatch/Spawn 调用
    status: completed
    dependencies:
      - create-battlelog
  - id: replace-tdbattleengine
    content: 精简并替换 TDBattleEngine.cs 日志：删除临时诊断日志（ConfigSource/SceneConfig），精简 SO/Wave/ConfigMatch 诊断，保留核心日志转 BattleLog.State/Config/Path/BattleEnd/Wave
    status: completed
    dependencies:
      - create-battlelog
  - id: replace-victorycheck-tdrules
    content: 替换 VictoryCheckSystem.cs(3条)和 TDRules.cs(1条)的 BattleEndDebug 为 BattleLog.BattleEnd
    status: completed
    dependencies:
      - create-battlelog
  - id: replace-remaining
    content: 替换 TowerDefenseSceneConfig.cs、MainCitySystem.cs、CityAttackerComponent.cs 中合计6条日志为 BattleLog 对应方法
    status: completed
    dependencies:
      - create-battlelog
  - id: verify-compilation
    content: 使用 [subagent:code-explorer] 验证所有7个文件无遗漏的 Debug.Log/Warning/Error 调用，并确认编译通过
    status: completed
    dependencies:
      - replace-wavemanager
      - replace-tdbattleengine
      - replace-victorycheck-tdrules
      - replace-remaining
---

## 产品概述

创建统一的战斗日志系统 `BattleLog`，将塔防战斗7个核心文件中散落的 `Debug.Log/Warning/Error` 替换为统一入口调用，支持12个分类 bool 开关控制，删除/精简 `TDBattleEngine.cs` 中约25条临时诊断日志。

## 核心功能

### 1. 新增 BattleLog 统一日志类

- 位置：`Assets/Scripts/HotUpdate.Core/Battle/TowerDefense/Core/BattleLog.cs`
- 类名：`BattleLog`（static class，namespace `TowerDefense`）
- 总开关 `EnableBattleLog` 控制所有战斗日志输出
- 9个分类开关：`EnableConfig`、`EnableSO`、`EnableSceneConfig`、`EnableConfigMatch`、`EnableWave`、`EnableSpawn`、`EnablePath`、`EnableState`、`EnableBattleEnd`
- 日志前缀统一：`[BattleLog][Category]`
- 内部调用 `Debug.Log/Warning/Error` 保持与现有模式一致
- 提供 `FormattableString` 重载，避免开关关闭时的字符串拼接GC

### 2. 替换7个文件的散落日志

- `WaveManagerSystem.cs`（20条）：波次、配置匹配、生成日志
- `TDBattleEngine.cs`（约50条）：引擎、配置诊断日志
- `VictoryCheckSystem.cs`（3条）：战斗结束判断日志
- `TDRules.cs`（1条）：结束触发日志
- `TowerDefenseSceneConfig.cs`（1条）：场景配置日志
- `MainCitySystem.cs`（2条）：主城状态日志
- `CityAttackerComponent.cs`（3条）：攻击状态日志

### 3. 删除/精简临时诊断日志

- 删除 `[TDConfigSourceDebug]` x3、`[TDSceneConfigDebug]` x5
- 精简 `[TDSODebug]` x6 → 1条汇总
- 精简 `[TDWaveDebug]` 详细诊断 x7 → 2条汇总
- 精简 `[TDConfigMatchDebug]` x6 → 1条汇总
- `[TDPathDebug]` x1 → 转为常驻 `BattleLog.Path()`

### 4. 不纳入范围

UI（TDTowerBuildView、TDRoguelikeChoiceView、TDHudView、TDBattlePhaseSystem）、防御塔（TowerPlacementSystem等）、Meta（MetaToRunBridge、LevelManager等）、Roguelike 模块保持原样不动。

## 技术栈

- 语言：C#
- 框架：Unity Engine
- 日志底层：`UnityEngine.Debug.Log/Warning/Error`
- 项目命名空间：`TowerDefense`
- 项目已有 `Framework.Log`（log4net），但 BattleLog 内部使用 `Debug.Log` 保持与现有塔防代码一致

## 实现方案

### 总体策略

新建 `BattleLog` 静态类作为塔防战斗日志唯一入口，所有核心战斗文件不再直接调用 `Debug.Log/Warning/Error`，改为通过 `BattleLog` 的类别方法输出。每个类别方法内部判断对应 bool 开关，关闭时跳过输出。

### BattleLog 类设计

```
namespace TowerDefense
{
    public static class BattleLog
    {
        // 总开关
        public static bool EnableBattleLog = true;

        // 分类开关
        public static bool EnableConfig = true;       // 配置读取
        public static bool EnableSO = true;           // ScriptableObject
        public static bool EnableSceneConfig = true;  // 场景配置
        public static bool EnableConfigMatch = true;  // 配置匹配/校验
        public static bool EnableWave = true;         // 波次
        public static bool EnableSpawn = true;        // 怪物生成
        public static bool EnablePath = true;         // 路径
        public static bool EnableState = true;        // 战斗状态
        public static bool EnableBattleEnd = true;    // 结束判断

        // 每个类别提供 4 个重载：普通 string、FormattableString、Warning、Error
        // 示例：Wave()
        public static void Wave(string msg) { if (Enabled(EnableWave)) Debug.Log($"[BattleLog][Wave] {msg}"); }
        public static void Wave(FormattableString msg) { if (Enabled(EnableWave)) Debug.Log($"[BattleLog][Wave] {msg}"); }
        public static void WaveWarning(string msg) { if (Enabled(EnableWave)) Debug.LogWarning($"[BattleLog][Wave] {msg}"); }
        public static void WaveError(string msg) { Debug.LogError($"[BattleLog][Wave] {msg}"); } // Error 不受开关控制

        // 各方法同理...
        private static bool Enabled(bool categorySwitch) => EnableBattleLog && categorySwitch;
    }
}
```

### 关键设计决策

1. **Error 级别不受开关控制**：所有 `*Error()` 方法忽略分类开关（但受总开关控制），因为 Error 代表严重问题必须输出
2. **Warning 受分类开关控制**：可通过开关关闭非关键的 Warning
3. **FormattableString 重载**：当开关关闭时，插值不会执行，避免字符串拼接开销和GC分配
4. **内部调用 `Debug.Log`**：不走 `Framework.Log`，因为 `Framework.Log` 会追加 `\r\n stacktrace`，且塔防现有代码从未使用 `Framework.Log`

### 目录结构

```
Assets/Scripts/HotUpdate.Core/Battle/TowerDefense/
├── Core/
│   ├── BattleLog.cs              # [NEW] 统一战斗日志类
│   ├── TDBattleEngine.cs         # [MODIFY] 替换25条日志，删除临时诊断
│   └── TDRules.cs                # [MODIFY] 替换1条 BattleEndDebug
├── Wave/
│   └── WaveManagerSystem.cs      # [MODIFY] 替换20条日志
├── System/
│   └── VictoryCheckSystem.cs     # [MODIFY] 替换3条日志
├── Config/
│   └── TowerDefenseSceneConfig.cs # [MODIFY] 替换1条日志
├── MainCity/
│   └── MainCitySystem.cs         # [MODIFY] 替换2条日志
└── Component/
    └── CityAttackerComponent.cs  # [MODIFY] 替换3条日志
```

### 性能考量

- 开关关闭时：`Enabled()` 是简单 bool 短路判断，O(1)
- `FormattableString` 重载：编译器在调用点生成 `FormattableString`，只有开关打开时才 `ToString()`，避免 GC
- CityAttackerComponent 的更新频率攻击日志（每帧可能触发）：通过 `EnableState` 开关控制，关闭后零开销
- VictoryCheckSystem 的 `Debug.Log` 每帧执行一次：通过 `EnableBattleEnd` 开关控制

### 日志替换映射表

| 原前缀 | BattleLog 方法 | 说明 |
| --- | --- | --- |
| `[TDWaveDebug]` | `BattleLog.Wave()` / `WaveWarning()` | 波次状态 |
| `[WaveManager]` | `BattleLog.Wave()` | 波次管理 |
| `[TDConfigMatchDebug]` | `BattleLog.ConfigMatch()` / `ConfigMatchError()` | 配置匹配 |
| `[TDSpawnDebug]` | `BattleLog.Spawn()` / `SpawnWarning()` | 怪物生成 |
| `[TDPathDebug]` | `BattleLog.Path()` | 路径 |
| `[BattleEndDebug]` | `BattleLog.BattleEnd()` / `BattleEndWarning()` | 结束判断 |
| `[TDBattleEngine]` | `BattleLog.State()` / `BattleLog.Config()` | 引擎/配置 |
| `[TDSODebug]` | `BattleLog.SO()` | SO诊断保留1条汇总 |
| `[TDConfigSourceDebug]` | 删除 | 临时诊断 |
| `[TDSceneConfigDebug]` | 删除 | 临时诊断 |
| `[MainCitySystem]` | `BattleLog.State()` | 主城状态 |
| `[CityAttacker]` | `BattleLog.State()` / `BattleLog.Warning()` | 攻击状态 |
| `[VictoryCheckSystem]` | `BattleLog.BattleEnd()` | 胜利检查 |
| `[TowerDefenseSceneConfig]` | `BattleLog.SceneConfigWarning()` | 场景配置 |


## Agent Extensions

### SubAgent

- **code-explorer**
- 用途：在替换日志前，精确确认每个文件的每条 Debug.Log/Warning/Error 行号和上下文，确保替换不遗漏、不错位
- 预期结果：获得每个目标文件的所有日志语句精确位置，作为替换操作的依据