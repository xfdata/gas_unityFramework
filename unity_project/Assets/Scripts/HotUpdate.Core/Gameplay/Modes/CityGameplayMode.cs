using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

    public sealed class CityGameplayMode : GameplayModeBase
    {
        private const string SceneName = "City";

        public override GameplayModeId Id => GameplayModeId.City;

        public CityGameplayMode(GameplayContext context) : base(context) { }

        public override async UniTask LoadAsync(GameplaySwitchRequest request, CancellationToken token)
        {
            await Context.Systems.Scenes.LoadSceneAsync(SceneName, LoadSceneMode.Single, null, token);
        }

        public override UniTask EnterAsync(GameplaySwitchRequest request, CancellationToken token)
        {
            Context.Systems.Audio.PlayBgm("bgm_city");
            Context.Systems.Ui.Open<CityMainView>();

            var spawnPoint = request.GetOrDefault("SpawnPoint", "Default");
            Context.Blackboard.Set("City.LastSpawnPoint", spawnPoint);
            return UniTask.CompletedTask;
        }

        public override async UniTask ExitAsync(GameplaySwitchRequest nextRequest, CancellationToken token)
        {
            Context.Systems.Ui.Close<CityMainView>();
            Context.Systems.Audio.StopBgm();
            await Context.Systems.Scenes.UnloadSceneAsync(SceneName, token);
        }
    }
