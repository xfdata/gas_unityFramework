using System;


    public sealed class GameplaySystemHub
    {
        public IGameplaySceneSystem Scenes { get; private set; }
        public IGameplayAudioSystem Audio { get; private set; }
        public IGameplayUiSystem Ui { get; private set; }

        private GameplaySystemHub() { }

        public static GameplaySystemHub CreateDefault()
        {
            return new GameplaySystemHub
            {
                Scenes = new SceneMgrGameplaySceneSystem(),
                Audio = new UnityAudioGameplayAudioSystem(),
                Ui = new UIFrameWorkGameplayUiSystem(),
            };
        }

        public GameplaySystemHub WithSceneSystem(IGameplaySceneSystem system)
        {
            Scenes = system;
            return this;
        }

        public GameplaySystemHub WithAudioSystem(IGameplayAudioSystem system)
        {
            Audio = system;
            return this;
        }

        public GameplaySystemHub WithUiSystem(IGameplayUiSystem system)
        {
            Ui = system;
            return this;
        }

        internal void Dispose()
        {
            (Scenes as IDisposable)?.Dispose();
            (Audio as IDisposable)?.Dispose();
            (Ui as IDisposable)?.Dispose();
        }
    }
