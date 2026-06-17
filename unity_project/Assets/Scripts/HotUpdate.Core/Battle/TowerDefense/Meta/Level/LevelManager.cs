using System;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 关卡管理器（局外 → 局内桥梁）。
    /// 
    /// 职责：
    /// - 加载 LevelConfig
    /// - 将配置注入到 TDBattleEngine
    /// - 赛后收集统计 → MetaTalentManager
    /// 
    /// 使用流程：
    ///   1. LevelManager.LoadLevel(levelConfig)
    ///   2. LevelManager.ApplyToBattleEngine(engine)  ← OnBeforeBattleStart
    ///   3. 战斗结束 → LevelManager.OnBattleEnded(victory, wave, gold)
    /// </summary>
    public class LevelManager
    {
        private static LevelManager _instance;
        public static LevelManager Instance => _instance ??= new LevelManager();

        public LevelConfig CurrentLevel { get; private set; }

        /// <summary>加载关卡配置</summary>
        public void LoadLevel(LevelConfig config)
        {
            CurrentLevel = config ?? throw new ArgumentNullException(nameof(config));
            Debug.Log($"[LevelManager] Loaded level: {config.DisplayName} (Bosses: {config.BossConfigs?.Length ?? 0}, Waves: {config.WaveConfigs?.Length ?? 0})");
        }

        /// <summary>将关卡配置应用到 BattleEngine</summary>
        public void ApplyToBattleEngine(TDBattleEngine engine)
        {
            if (CurrentLevel == null)
            {
                Debug.LogError("[LevelManager] No level loaded!");
                return;
            }

            var ctx = engine?.Context as TDBattleContext;
            if (ctx == null) return;

            var globalConfig = engine.TDConfig;

            // 1. 地图设置
            if (CurrentLevel.Map != null)
            {
                if (globalConfig != null && CurrentLevel.Map.DefaultPath != null)
                    globalConfig.GetType().GetField("DefaultPath")?.SetValue(globalConfig, CurrentLevel.Map.DefaultPath);
            }

            // 2. 经济覆盖
            int startingGold = CurrentLevel.EffectiveStartingGold(globalConfig);
            ctx.PlayerGold = startingGold;

            // 3. 可用塔覆盖
            if (CurrentLevel.AvailableTowers != null && CurrentLevel.AvailableTowers.Length > 0)
            {
                // LevelConfig 的 AvailableTowers 优先级高于 GlobalConfig
                // 通过 TowerDefenseGlobalConfig 的 AvailableTowers 字段注入
                if (globalConfig != null)
                {
                    globalConfig.AvailableTowers = CurrentLevel.AvailableTowers;
                }
            }

            // 4. 波次配置
            if (CurrentLevel.WaveConfigs != null && CurrentLevel.WaveConfigs.Length > 0)
            {
                // 如果 LevelConfig 自带波次，用 LevelConfig 的
                // 否则使用 GlobalConfig 的
                var waveManager = ctx.WaveManager;
                if (waveManager != null)
                {
                    // LevelConfig 带路径信息时设置默认路径
                    if (CurrentLevel.Map?.DefaultPath != null)
                        waveManager.SetDefaultPath(CurrentLevel.Map.DefaultPath);

                    waveManager.StartWaves(CurrentLevel.WaveConfigs);
                }
            }

            // 5. Boss 注入（通过 WaveConfig 中的 BossConfig 引用，在 SpawnEnemy 时检查）
            // BossConfig 注册到 ctx 的 Service 中供 WaveSpawner 查询

            Debug.Log($"[LevelManager] Applied level '{CurrentLevel.DisplayName}' to battle. Gold={ctx.PlayerGold}");
        }

        /// <summary>战斗结束回调 → Meta 统计更新</summary>
        public void OnBattleEnded(bool victory, int waveReached, int totalGoldEarned)
        {
            // 记录统计到 Meta
            MetaTalentManager.Instance.OnRunCompleted(victory, waveReached, totalGoldEarned);
            Debug.Log($"[LevelManager] Run completed. Victory={victory}, Waves={waveReached}, Gold={totalGoldEarned}");
        }
    }
}
