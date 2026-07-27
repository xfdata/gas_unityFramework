using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TowerDefense;
using UnityEngine;
using UnityEngine.SceneManagement;


    public sealed class PveGameplayMode : GameplayModeBase
    {
        private const string SceneName = "Assets/Scenes/towerdefense.unity";

        private int _chapterId;
        private int _sectionId;
        private bool _startImmediately;
        private TowerDefenseGlobalConfig _tdConfig;
        private TDBattleEngine _tdEngine;
        private TDBattleUIBridge _tdBridge;
        private IDisposable _tickSubscription;

        public override GameplayModeId Id => GameplayModeId.Pve;

        public PveGameplayMode(GameplayContext context) : base(context) { }

        public override async UniTask LoadAsync(GameplaySwitchRequest request, CancellationToken token)
        {
            if (!request.TryGet<int>("ChapterId", out _chapterId))
                _chapterId = 0;

            if (!request.TryGet<int>("SectionId", out _sectionId))
                _sectionId = 0;

            _startImmediately = request.GetOrDefault("StartImmediately", true);

            if (_sectionId <= 0)
                Debug.LogWarning("[PveGameplayMode] SectionId not set or invalid, using default.");

            await Context.Systems.Scenes.LoadSceneAsync(SceneName, LoadSceneMode.Single, null, token);
        }

        public override UniTask EnterAsync(GameplaySwitchRequest request, CancellationToken token)
        {
            Context.Systems.Audio.PlayBgm("bgm_pve");
            // Context.Systems.Ui.Open<BattleMainView>();

            Context.Blackboard.Set("Pve.ChapterId", _chapterId);
            Context.Blackboard.Set("Pve.SectionId", _sectionId);

            if (_startImmediately)
                StartTowerDefenseBattle();

            return UniTask.CompletedTask;
        }

        public override async UniTask ExitAsync(GameplaySwitchRequest nextRequest, CancellationToken token)
        {
            StopTowerDefenseBattle();
            // Context.Systems.Ui.Close<BattleMainView>();
            Context.Systems.Audio.StopBgm();
            await Context.Systems.Scenes.UnloadSceneAsync(SceneName, token);
        }

        private void StartTowerDefenseBattle()
        {
            if (_tdEngine != null)
                return;

            _tdConfig = TowerDefenseSceneConfig.Current?.GlobalConfig;
            if (_tdConfig == null)
            {
                Debug.LogWarning("[PveGameplayMode] TowerDefenseSceneConfig.GlobalConfig not set, skip TowerDefense battle startup.");
                return;
            }

            _tdEngine = new TDBattleEngine(_tdConfig);
            _tdEngine.Initialize();
            _tdEngine.StartBattle();
            
            var player = new TDPlayerActor();
            player.InitPlayer(100, 10, 0, 5, _tdConfig.PlayerSpawnPos, _tdConfig.PlayerPrefab);
            _tdEngine.Context.EntityManager.AddEntity(player);

            _tdEngine.Context.GetSystem<TDPlayerMovementSystem>().Player = player;
            _tdEngine.Context.GetSystem<TDPlayerSkillInputSystem>().Player = player;
            
            _tdBridge = new TDBattleUIBridge(_tdEngine.Context);
            Context.Blackboard.Set("Pve.TDBattleEngine", _tdEngine);
            Context.Blackboard.Set("Pve.TDBattleBridge", _tdBridge);

            _tickSubscription = Context.Events.Subscribe<GameplayTickEvent>(OnGameplayTick);
            Debug.Log("[PveGameplayMode] TowerDefense battle started.");
        }

        private void OnGameplayTick(GameplayTickEvent evt)
        {
            _tdEngine?.UpdateFromUnity(evt.DeltaTime);
        }

        private void StopTowerDefenseBattle()
        {
            _tickSubscription?.Dispose();
            _tickSubscription = null;

            Context.Blackboard.Remove("Pve.TDBattleBridge");
            Context.Blackboard.Remove("Pve.TDBattleEngine");

            _tdBridge = null;
            _tdEngine?.Dispose();
            _tdEngine = null;
        }

        protected override void OnDispose()
        {
            StopTowerDefenseBattle();
            _tdConfig = null;
            base.OnDispose();
        }
    }
