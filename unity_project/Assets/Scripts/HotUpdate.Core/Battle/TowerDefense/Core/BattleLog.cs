using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 塔防战斗统一日志类 — 所有战斗核心日志的唯一入口。
    /// 
    /// 使用方式：
    ///   BattleLog.Wave("波次开始");
    ///   BattleLog.Spawn($"生成敌人 {name}");
    ///   BattleLog.ConfigMatchError("配置非法");
    /// 
    /// 开关控制：
    ///   总开关 EnableBattleLog 控制所有输出
    ///   分类开关 EnableXxx 控制对应类别
    ///   Error 方法不受分类开关控制（但受总开关控制）
    /// </summary>
    public static class BattleLog
    {
        // ===================== 开关 =====================

        /// <summary>总开关，关闭后所有战斗日志不输出</summary>
        public static bool EnableBattleLog = true;

        /// <summary>配置读取（SO/Level加载）</summary>
        public static bool EnableConfig = true;

        /// <summary>ScriptableObject 诊断</summary>
        public static bool EnableSO = true;

        /// <summary>场景配置相关</summary>
        public static bool EnableSceneConfig = true;

        /// <summary>配置校验与匹配检查</summary>
        public static bool EnableConfigMatch = true;

        /// <summary>波次状态与推进</summary>
        public static bool EnableWave = true;

        /// <summary>怪物生成流程</summary>
        public static bool EnableSpawn = true;

        /// <summary>路径相关</summary>
        public static bool EnablePath = true;

        /// <summary>战斗状态（引擎/主城/阶段切换等）</summary>
        public static bool EnableState = true;

        /// <summary>战斗结束判断</summary>
        public static bool EnableBattleEnd = true;

        /// <summary>移动相关（MoveTo/StopMove/Motor）</summary>
        public static bool EnableMove = true;

        /// <summary>寻路（NavMesh/AStar 路径计算）</summary>
        public static bool EnablePathfinding = true;

        /// <summary>AI 决策（行为切换/目标选择）</summary>
        public static bool EnableAI = true;

        /// <summary>目标选择与切换</summary>
        public static bool EnableTarget = true;

        /// <summary>攻击流程（释放/命中/伤害）</summary>
        public static bool EnableAttack = true;

        // ===================== 内部方法 =====================

        const string PREFIX = "[BattleLog]";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool Enabled(bool categorySwitch) => EnableBattleLog && categorySwitch;

        // ===================== Config =====================

        public static void Config(string msg) { if (Enabled(EnableConfig)) Debug.Log($"{PREFIX}[Config] {msg}"); }
        public static void Config(FormattableString msg) { if (Enabled(EnableConfig)) Debug.Log($"{PREFIX}[Config] {msg}"); }
        public static void ConfigWarning(string msg) { if (Enabled(EnableConfig)) Debug.LogWarning($"{PREFIX}[Config] {msg}"); }
        public static void ConfigError(string msg) { if (EnableBattleLog) Debug.LogError($"{PREFIX}[Config] {msg}"); }

        // ===================== SO =====================

        public static void SO(string msg) { if (Enabled(EnableSO)) Debug.Log($"{PREFIX}[SO] {msg}"); }
        public static void SO(FormattableString msg) { if (Enabled(EnableSO)) Debug.Log($"{PREFIX}[SO] {msg}"); }
        public static void SOWarning(string msg) { if (Enabled(EnableSO)) Debug.LogWarning($"{PREFIX}[SO] {msg}"); }
        public static void SOError(string msg) { if (EnableBattleLog) Debug.LogError($"{PREFIX}[SO] {msg}"); }

        // ===================== SceneConfig =====================

        public static void SceneConfig(string msg) { if (Enabled(EnableSceneConfig)) Debug.Log($"{PREFIX}[SceneConfig] {msg}"); }
        public static void SceneConfig(FormattableString msg) { if (Enabled(EnableSceneConfig)) Debug.Log($"{PREFIX}[SceneConfig] {msg}"); }
        public static void SceneConfigWarning(string msg) { if (Enabled(EnableSceneConfig)) Debug.LogWarning($"{PREFIX}[SceneConfig] {msg}"); }
        public static void SceneConfigError(string msg) { if (EnableBattleLog) Debug.LogError($"{PREFIX}[SceneConfig] {msg}"); }

        // ===================== ConfigMatch =====================

        public static void ConfigMatch(string msg) { if (Enabled(EnableConfigMatch)) Debug.Log($"{PREFIX}[ConfigMatch] {msg}"); }
        public static void ConfigMatch(FormattableString msg) { if (Enabled(EnableConfigMatch)) Debug.Log($"{PREFIX}[ConfigMatch] {msg}"); }
        public static void ConfigMatchWarning(string msg) { if (Enabled(EnableConfigMatch)) Debug.LogWarning($"{PREFIX}[ConfigMatch] {msg}"); }
        /// <summary>配置匹配错误（严重问题，不受 EnableConfigMatch 控制）</summary>
        public static void ConfigMatchError(string msg) { if (EnableBattleLog) Debug.LogError($"{PREFIX}[ConfigMatch] {msg}"); }

        // ===================== Wave =====================

        public static void Wave(string msg) { if (Enabled(EnableWave)) Debug.Log($"{PREFIX}[Wave] {msg}"); }
        public static void Wave(FormattableString msg) { if (Enabled(EnableWave)) Debug.Log($"{PREFIX}[Wave] {msg}"); }
        public static void WaveWarning(string msg) { if (Enabled(EnableWave)) Debug.LogWarning($"{PREFIX}[Wave] {msg}"); }
        public static void WaveError(string msg) { if (EnableBattleLog) Debug.LogError($"{PREFIX}[Wave] {msg}"); }

        // ===================== Spawn =====================

        public static void Spawn(string msg) { if (Enabled(EnableSpawn)) Debug.Log($"{PREFIX}[Spawn] {msg}"); }
        public static void Spawn(FormattableString msg) { if (Enabled(EnableSpawn)) Debug.Log($"{PREFIX}[Spawn] {msg}"); }
        public static void SpawnWarning(string msg) { if (Enabled(EnableSpawn)) Debug.LogWarning($"{PREFIX}[Spawn] {msg}"); }
        public static void SpawnError(string msg) { if (EnableBattleLog) Debug.LogError($"{PREFIX}[Spawn] {msg}"); }

        // ===================== Path =====================

        public static void Path(string msg) { if (Enabled(EnablePath)) Debug.Log($"{PREFIX}[Path] {msg}"); }
        public static void Path(FormattableString msg) { if (Enabled(EnablePath)) Debug.Log($"{PREFIX}[Path] {msg}"); }
        public static void PathWarning(string msg) { if (Enabled(EnablePath)) Debug.LogWarning($"{PREFIX}[Path] {msg}"); }
        public static void PathError(string msg) { if (EnableBattleLog) Debug.LogError($"{PREFIX}[Path] {msg}"); }

        // ===================== State =====================

        public static void State(string msg) { if (Enabled(EnableState)) Debug.Log($"{PREFIX}[State] {msg}"); }
        public static void State(FormattableString msg) { if (Enabled(EnableState)) Debug.Log($"{PREFIX}[State] {msg}"); }
        public static void StateWarning(string msg) { if (Enabled(EnableState)) Debug.LogWarning($"{PREFIX}[State] {msg}"); }
        public static void StateError(string msg) { if (EnableBattleLog) Debug.LogError($"{PREFIX}[State] {msg}"); }

        // ===================== BattleEnd =====================

        public static void BattleEnd(string msg) { if (Enabled(EnableBattleEnd)) Debug.Log($"{PREFIX}[BattleEnd] {msg}"); }
        public static void BattleEnd(FormattableString msg) { if (Enabled(EnableBattleEnd)) Debug.Log($"{PREFIX}[BattleEnd] {msg}"); }
        public static void BattleEndWarning(string msg) { if (Enabled(EnableBattleEnd)) Debug.LogWarning($"{PREFIX}[BattleEnd] {msg}"); }
        public static void BattleEndError(string msg) { if (EnableBattleLog) Debug.LogError($"{PREFIX}[BattleEnd] {msg}"); }

        // ===================== Move =====================

        public static void Move(string msg) { if (Enabled(EnableMove)) Debug.Log($"{PREFIX}[Move] {msg}"); }
        public static void Move(FormattableString msg) { if (Enabled(EnableMove)) Debug.Log($"{PREFIX}[Move] {msg}"); }
        public static void MoveWarning(string msg) { if (Enabled(EnableMove)) Debug.LogWarning($"{PREFIX}[Move] {msg}"); }
        public static void MoveError(string msg) { if (EnableBattleLog) Debug.LogError($"{PREFIX}[Move] {msg}"); }

        // ===================== Pathfinding =====================

        public static void Pathfinding(string msg) { if (Enabled(EnablePathfinding)) Debug.Log($"{PREFIX}[Pathfinding] {msg}"); }
        public static void Pathfinding(FormattableString msg) { if (Enabled(EnablePathfinding)) Debug.Log($"{PREFIX}[Pathfinding] {msg}"); }
        public static void PathfindingWarning(string msg) { if (Enabled(EnablePathfinding)) Debug.LogWarning($"{PREFIX}[Pathfinding] {msg}"); }
        public static void PathfindingError(string msg) { if (EnableBattleLog) Debug.LogError($"{PREFIX}[Pathfinding] {msg}"); }

        // ===================== AI =====================

        public static void AI(string msg) { if (Enabled(EnableAI)) Debug.Log($"{PREFIX}[AI] {msg}"); }
        public static void AI(FormattableString msg) { if (Enabled(EnableAI)) Debug.Log($"{PREFIX}[AI] {msg}"); }
        public static void AIWarning(string msg) { if (Enabled(EnableAI)) Debug.LogWarning($"{PREFIX}[AI] {msg}"); }
        public static void AIError(string msg) { if (EnableBattleLog) Debug.LogError($"{PREFIX}[AI] {msg}"); }

        // ===================== Target =====================

        public static void Target(string msg) { if (Enabled(EnableTarget)) Debug.Log($"{PREFIX}[Target] {msg}"); }
        public static void Target(FormattableString msg) { if (Enabled(EnableTarget)) Debug.Log($"{PREFIX}[Target] {msg}"); }
        public static void TargetWarning(string msg) { if (Enabled(EnableTarget)) Debug.LogWarning($"{PREFIX}[Target] {msg}"); }
        public static void TargetError(string msg) { if (EnableBattleLog) Debug.LogError($"{PREFIX}[Target] {msg}"); }

        // ===================== Attack =====================

        public static void Attack(string msg) { if (Enabled(EnableAttack)) Debug.Log($"{PREFIX}[Attack] {msg}"); }
        public static void Attack(FormattableString msg) { if (Enabled(EnableAttack)) Debug.Log($"{PREFIX}[Attack] {msg}"); }
        public static void AttackWarning(string msg) { if (Enabled(EnableAttack)) Debug.LogWarning($"{PREFIX}[Attack] {msg}"); }
        public static void AttackError(string msg) { if (EnableBattleLog) Debug.LogError($"{PREFIX}[Attack] {msg}"); }

        // ===================== 通用 Warning/Error（无分类） =====================

        /// <summary>通用战斗警告（不受分类开关控制，仅受总开关控制）</summary>
        public static void Warning(string msg) { if (EnableBattleLog) Debug.LogWarning($"{PREFIX} {msg}"); }
        /// <summary>通用战斗错误（不受分类开关控制，仅受总开关控制）</summary>
        public static void Error(string msg) { if (EnableBattleLog) Debug.LogError($"{PREFIX} {msg}"); }
    }
}
