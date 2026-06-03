using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;


    public sealed class WorldGameplayMode : GameplayModeBase
    {
        private const string SceneName = "World";
        private long _mapId;

        public override GameplayModeId Id => GameplayModeId.World;

        public WorldGameplayMode(GameplayContext context) : base(context) { }

        public override async UniTask LoadAsync(GameplaySwitchRequest request, CancellationToken token)
        {
            _mapId = request.GetOrDefault<long>("MapId", 0);
            if (_mapId <= 0)
            {
                Debug.LogWarning("[WorldGameplayMode] MapId is empty, use default map.");
                _mapId = 10001;
            }

            await Context.Systems.Scenes.LoadSceneAsync(SceneName, LoadSceneMode.Single, null, token);
            Context.Blackboard.Set("World.CurrentMapId", _mapId);
        }

        public override UniTask EnterAsync(GameplaySwitchRequest request, CancellationToken token)
        {
            Context.Systems.Audio.PlayBgm("bgm_world");
            Context.Systems.Ui.Open<WorldMainView>();
            return UniTask.CompletedTask;
        }

        public override async UniTask ExitAsync(GameplaySwitchRequest nextRequest, CancellationToken token)
        {
            Context.Systems.Ui.Close<WorldMainView>();
            Context.Systems.Audio.StopBgm();
            await Context.Systems.Scenes.UnloadSceneAsync(SceneName, token);
        }
    }
