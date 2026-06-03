using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;


    public sealed class PveGameplayMode : GameplayModeBase
    {
        private const string SceneName = "PVE";

        private int _chapterId;
        private int _sectionId;
        private bool _startImmediately;

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

        public override async UniTask EnterAsync(GameplaySwitchRequest request, CancellationToken token)
        {
            Context.Systems.Audio.PlayBgm("bgm_pve");
            Context.Systems.Ui.Open<BattleMainView>();

            Context.Blackboard.Set("Pve.ChapterId", _chapterId);
            Context.Blackboard.Set("Pve.SectionId", _sectionId);
        }

        public override async UniTask ExitAsync(GameplaySwitchRequest nextRequest, CancellationToken token)
        {
            Context.Systems.Ui.Close<BattleMainView>();
            Context.Systems.Audio.StopBgm();
            await Context.Systems.Scenes.UnloadSceneAsync(SceneName, token);
        }
    }
