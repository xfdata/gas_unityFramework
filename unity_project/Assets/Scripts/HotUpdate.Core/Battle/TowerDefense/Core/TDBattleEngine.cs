using BattleCommon;
using BattleFoundation;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// TD战斗引擎 — 塔防模式的入口引擎。
    /// 继承BattleEngine，在OnInitialize中注册TD专属System和Rule。
    /// 
    /// 使用方式：
    ///   var engine = new TDBattleEngine(tdGlobalConfig);
    ///   engine.Initialize();
    ///   engine.StartBattle();
    ///   // 每帧：engine.UpdateFromUnity(Time.deltaTime);
    /// </summary>
    public class TDBattleEngine : BattleEngine
    {
        [SerializeField]
        private TowerDefenseGlobalConfig _tdConfig;

        /// <summary>
        /// TD全局配置（波次、路径、初始金币等）
        /// </summary>
        public TowerDefenseGlobalConfig TDConfig => _tdConfig;

        /// <summary>
        /// 敌人工厂便捷访问
        /// </summary>
        public EnemyFactory EnemyFactory => ((TDBattleContext)Context)?.EnemyFactory;

        public TDBattleEngine() { }

        public TDBattleEngine(TowerDefenseGlobalConfig tdConfig)
        {
            _tdConfig = tdConfig;
        }

        public void SetTDConfig(TowerDefenseGlobalConfig config)
        {
            _tdConfig = config;
        }

        protected override BattleContext CreateContext()
        {
            return new TDBattleContext();
        }

        protected override BattleRuntimeSettings CreateRuntimeSettings()
        {
            var settings = base.CreateRuntimeSettings();
            if (_tdConfig != null)
            {
                settings.RandomSeed = _tdConfig.RandomSeed;
                settings.InitialTimeScale = _tdConfig.InitialTimeScale;
            }
            return settings;
        }

        protected override void OnInitialize()
        {
            var ctx = (TDBattleContext)Context;

            // ===== 注册TD System（按执行顺序） =====
            // 1. 路径跟随：驱动所有敌人沿路径移动
            ctx.AddSystem(new PathFollowerSystem());

            // 2. 主城系统：管理主城生命周期和伤害接收
            ctx.AddSystem(new MainCitySystem());

            // 3. 城市攻击者系统：驱动敌人持续攻击主城
            ctx.AddSystem(new CityAttackerSystem());

            // 4. 投射物系统：驱动所有防御塔/玩家投射物飞行与命中（必须在TowerPlacementSystem之前）
            ctx.AddSystem(new CombatProjectileSystem());

            // 5. 防御塔放置系统：管理塔的建造/升级/出售和攻击驱动
            ctx.AddSystem(new TowerPlacementSystem());
            // 6. 玩家移动系统
            ctx.AddSystem(new TDPlayerMovementSystem());
            // 7. 玩家技能输入系统
            ctx.AddSystem(new TDPlayerSkillInputSystem());
            // 8. 事件统计系统
            ctx.AddSystem(new TDEventSystem());
            // 9. 经济管理系统 (Phase 5)：监听 EnemyKilled 计算金币奖励
            ctx.AddSystem(new EconomySystem());
            // 10. Build流派协同系统 (Phase 5)：塔数量阈值触发增益
            ctx.AddSystem(new BuildSynergySystem());
            // 11. 罗吉尔强化选择系统 (Phase 5)：波间暂停、3选1、应用强化
            ctx.AddSystem(new RoguelikeChoiceSystem());
            // 12. 波次管理系统（必须在 RoguelikeChoiceSystem 之后，以便钩子生效）
            ctx.AddSystem(new WaveManagerSystem());
            // 13. 战斗阶段管理系统 (Phase 6)：Prepare→Combat→WaveEnd→Choice 循环
            ctx.AddSystem(new TDBattlePhaseSystem());
            // 14. 胜利条件检查系统
            ctx.AddSystem(new VictoryCheckSystem());

            // ===== 初始化TD全局配置 =====
            if (_tdConfig != null)
            {
                ctx.PlayerGold = _tdConfig.StartingGold;

                // 预创建敌人对象池
                if (_tdConfig.EnemyPreWarmConfigs != null)
                {
                    foreach (var preWarmEntry in _tdConfig.EnemyPreWarmConfigs)
                    {
                        ctx.EnemyFactory.PreWarm(preWarmEntry.config, preWarmEntry.count);
                    }
                }
            }

            // ===== 注册BattleRule =====
            AddRule(new MainCityDestroyedRule());
            AddRule(new AllWavesClearedRule());
        }

        protected override void OnBattleStart()
        {
            base.OnBattleStart();

            var ctx = (TDBattleContext)Context;

            // ===== Phase 7: Meta注入（局外天赋 → 局内数值） =====
            if (_tdConfig?.TalentTreeConfig != null)
            {
                // 确保 MetaTalentManager 已初始化
                if (MetaTalentManager.Instance.Config == null)
                    MetaTalentManager.Instance.Initialize(_tdConfig.TalentTreeConfig);
                MetaToRunBridge.ApplyToBattleContext(ctx, _tdConfig);
            }

            // ===== Phase 7: BattleFormula =====
            if (_tdConfig?.BalanceConfig != null)
            {
                BattleFormula.SetConfig(_tdConfig.BalanceConfig);
                Debug.Log($"[TDBattleEngine] BalanceConfig loaded. DefenseK={_tdConfig.BalanceConfig.DefenseK}");
            }

            // ===== Phase 7: LevelManager =====
            if (_tdConfig?.CurrentLevelConfig != null)
            {
                LevelManager.Instance.LoadLevel(_tdConfig.CurrentLevelConfig);
                LevelManager.Instance.ApplyToBattleEngine(this);
            }

            // ===== 生成主城 =====
            var mainCitySystem = ctx.GetSystem<MainCitySystem>();
            mainCitySystem?.SpawnMainCity(_tdConfig?.MainCityConfig, Vector3.zero);

            // ===== 开始波次序列 =====
            var waveManager = ctx.GetSystem<WaveManagerSystem>();
            // LevelConfig 可能已覆盖波次配置，检查是否已有波次
            if (waveManager?.State == ETDWaveState.Idle && _tdConfig?.WaveConfigs != null && _tdConfig.WaveConfigs.Length > 0)
            {
                if (_tdConfig.DefaultPath != null)
                    waveManager.SetDefaultPath(_tdConfig.DefaultPath);
                waveManager.StartWaves(_tdConfig.WaveConfigs);
            }
            else if (waveManager?.State != ETDWaveState.Idle)
            {
                // LevelManager 已启动波次
                Debug.Log("[TDBattleEngine] Waves already started by LevelManager.");
            }
            else
            {
                Debug.LogWarning("[TDBattleEngine] No wave configs defined!");
            }

            Debug.Log($"[TDBattleEngine] Battle started. Gold={ctx.PlayerGold}");
        }

        protected override void OnBattleEnd(EBattleResult result)
        {
            base.OnBattleEnd(result);

            var ctx = (TDBattleContext)Context;

            // ===== Phase 7: Meta 统计记录 =====
            bool victory = result == EBattleResult.Win;
            int waveReached = ctx?.WaveManager?.CurrentWaveIndex ?? 0;
            int totalGold = ctx?.PlayerGold ?? 0;

            LevelManager.Instance.OnBattleEnded(victory, waveReached, totalGold);

            // 清理
            ctx?.EnemyFactory?.RecycleAll();
            ctx?.ClearServices();
            Debug.Log($"[TDBattleEngine] Battle ended: {result}. Meta stats recorded.");
        }

        protected override void OnUpdate(float deltaTime)
        {
            // TD专属的全局Update逻辑（如有需要在此添加）
        }

        protected override void OnDispose()
        {
            _tdConfig = null;
            base.OnDispose();
        }
    }
}
