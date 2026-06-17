using BattleCommon;
using BattleFoundation;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// TD鎴樻枟寮曟搸 鈥?濉旈槻妯″紡鐨勫叆鍙ｅ紩鎿庛€?
    /// 缁ф壙BattleEngine锛屽湪OnInitialize涓敞鍐孴D涓撳睘System鍜孯ule銆?
    /// 
    /// 浣跨敤鏂瑰紡锛?
    ///   var engine = new TDBattleEngine(tdGlobalConfig);
    ///   engine.Initialize();
    ///   engine.StartBattle();
    ///   // 姣忓抚锛歟ngine.UpdateFromUnity(Time.deltaTime);
    /// </summary>
    public class TDBattleEngine : BattleEngine
    {
        [SerializeField]
        private TowerDefenseGlobalConfig _tdConfig;

        /// <summary>
        /// TD鍏ㄥ眬閰嶇疆锛堟尝娆°€佽矾寰勩€佸垵濮嬮噾甯佺瓑锛?
        /// </summary>
        public TowerDefenseGlobalConfig TDConfig => _tdConfig;

        /// <summary>
        /// 鏁屼汉宸ュ巶渚挎嵎璁块棶
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

            // ===== 娉ㄥ唽TD System锛堟寜鎵ц椤哄簭锛?=====
            // 1. 璺緞璺熼殢锛氶┍鍔ㄦ墍鏈夋晫浜烘部璺緞绉诲姩
            ctx.AddSystem(new PathFollowerSystem());

            // 2. 涓诲煄绯荤粺锛氱鐞嗕富鍩庣敓鍛藉懆鏈熷拰浼ゅ鎺ユ敹
            ctx.AddSystem(new MainCitySystem());

            // 3. 鍩庡競鏀诲嚮鑰呯郴缁燂細椹卞姩鏁屼汉鎸佺画鏀诲嚮涓诲煄
            ctx.AddSystem(new CityAttackerSystem());

            // 4. 鎶曞皠鐗╃郴缁燂細椹卞姩鎵€鏈夐槻寰″/鐜╁鎶曞皠鐗╅琛屼笌鍛戒腑锛堝繀椤诲湪TowerPlacementSystem涔嬪墠锛?
            ctx.AddSystem(new CombatProjectileSystem());

            // 5. 闃插尽濉旀斁缃郴缁燂細绠＄悊濉旂殑寤洪€?鍗囩骇/鍑哄敭鍜屾敾鍑婚┍鍔?
            ctx.AddSystem(new TowerPlacementSystem());
            // 6. 鐜╁绉诲姩绯荤粺
            ctx.AddSystem(new TDPlayerMovementSystem());
            // 7. 鐜╁鎶€鑳借緭鍏ョ郴缁?
            ctx.AddSystem(new TDPlayerSkillInputSystem());
            // 8. 浜嬩欢缁熻绯荤粺
            ctx.AddSystem(new TDEventSystem());
            // 9. 缁忔祹绠＄悊绯荤粺 (Phase 5)锛氱洃鍚?EnemyKilled 璁＄畻閲戝竵濂栧姳
            ctx.AddSystem(new EconomySystem());
            // 10. Build娴佹淳鍗忓悓绯荤粺 (Phase 5)锛氬鏁伴噺闃堝€艰Е鍙戝鐩?
            ctx.AddSystem(new BuildSynergySystem());
            // 11. 缃楀悏灏斿己鍖栭€夋嫨绯荤粺 (Phase 5)锛氭尝闂存殏鍋溿€?閫?銆佸簲鐢ㄥ己鍖?
            ctx.AddSystem(new RoguelikeChoiceSystem());
            // 12. 娉㈡绠＄悊绯荤粺锛堝繀椤诲湪 RoguelikeChoiceSystem 涔嬪悗锛屼互渚块挬瀛愮敓鏁堬級
            ctx.AddSystem(new WaveManagerSystem());
            // 13. 鎴樻枟闃舵绠＄悊绯荤粺 (Phase 6)锛歅repare鈫扖ombat鈫扺aveEnd鈫扖hoice 寰幆
            ctx.AddSystem(new TDBattlePhaseSystem());
            // 14. 鑳滃埄鏉′欢妫€鏌ョ郴缁?
            ctx.AddSystem(new VictoryCheckSystem());

            // ===== 鍒濆鍖朤D鍏ㄥ眬閰嶇疆 =====
            if (_tdConfig != null)
            {
                ctx.PlayerGold = _tdConfig.StartingGold;

                // 棰勫垱寤烘晫浜哄璞℃睜
                if (_tdConfig.EnemyPreWarmConfigs != null)
                {
                    foreach (var preWarmEntry in _tdConfig.EnemyPreWarmConfigs)
                    {
                        ctx.EnemyFactory.PreWarm(preWarmEntry.config, preWarmEntry.count);
                    }
                }
            }

            // ===== 娉ㄥ唽BattleRule =====
            AddRule(new MainCityDestroyedRule());
            AddRule(new AllWavesClearedRule());
        }

        protected override void OnBattleStart()
        {
            base.OnBattleStart();

            var ctx = (TDBattleContext)Context;

            // ===== Phase 7: Meta娉ㄥ叆锛堝眬澶栧ぉ璧?鈫?灞€鍐呮暟鍊硷級 =====
            if (_tdConfig?.TalentTreeConfig != null)
            {
                // 纭繚 MetaTalentManager 宸插垵濮嬪寲
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

            // ===== 鐢熸垚涓诲煄 =====
            var mainCitySystem = ctx.GetSystem<MainCitySystem>();
            mainCitySystem?.SpawnMainCity(_tdConfig?.MainCityConfig, Vector3.zero);

            // ===== 寮€濮嬫尝娆″簭鍒?=====
            var waveManager = ctx.GetSystem<WaveManagerSystem>();
            // LevelConfig 鍙兘宸茶鐩栨尝娆￠厤缃紝妫€鏌ユ槸鍚﹀凡鏈夋尝娆?
            if (waveManager?.State == ETDWaveState.Idle && _tdConfig?.WaveConfigs != null && _tdConfig.WaveConfigs.Length > 0)
            {
                if (_tdConfig.DefaultPath != null)
                    waveManager.SetDefaultPath(_tdConfig.DefaultPath);
                waveManager.StartWaves(_tdConfig.WaveConfigs);
            }
            else if (waveManager?.State != ETDWaveState.Idle)
            {
                // LevelManager 宸插惎鍔ㄦ尝娆?
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

            // ===== Phase 7: Meta 缁熻璁板綍 =====
            bool victory = result == EBattleResult.Win;
            int waveReached = ctx?.WaveManager?.CurrentWaveIndex ?? 0;
            int totalGold = ctx?.PlayerGold ?? 0;

            LevelManager.Instance.OnBattleEnded(victory, waveReached, totalGold);

            // 娓呯悊
            ctx?.EnemyFactory?.RecycleAll();
            ctx?.ClearServices();
            Debug.Log($"[TDBattleEngine] Battle ended: {result}. Meta stats recorded.");
        }

        protected override void OnUpdate(float deltaTime)
        {
            // TD涓撳睘鐨勫叏灞€Update閫昏緫锛堝鏈夐渶瑕佸湪姝ゆ坊鍔狅級
        }

        protected override void OnDispose()
        {
            _tdConfig = null;
            base.OnDispose();
        }
    }
}
