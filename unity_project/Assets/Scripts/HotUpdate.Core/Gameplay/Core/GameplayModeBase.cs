using System;
using System.Threading;
using Cysharp.Threading.Tasks;

    public abstract class GameplayModeBase : IGameplayMode
    {
        protected readonly GameplayContext Context;
        private bool _disposed;

        public abstract GameplayModeId Id { get; }

        protected GameplayModeBase(GameplayContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public virtual UniTask LoadAsync(GameplaySwitchRequest request, CancellationToken token) => UniTask.CompletedTask;
        public virtual UniTask EnterAsync(GameplaySwitchRequest request, CancellationToken token) => UniTask.CompletedTask;
        public virtual UniTask ExitAsync(GameplaySwitchRequest nextRequest, CancellationToken token) => UniTask.CompletedTask;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            OnDispose();
        }

        protected virtual void OnDispose() { }
    }
