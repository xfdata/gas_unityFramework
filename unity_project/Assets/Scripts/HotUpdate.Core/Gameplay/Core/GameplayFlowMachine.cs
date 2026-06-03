using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

    public sealed class GameplayFlowMachine : IDisposable
    {
        private readonly GameplayContext _context;
        private readonly GameplayModeRegistry _registry;
        private readonly CancellationTokenSource _lifeCts = new CancellationTokenSource();

        private IGameplayMode _current;
        private GameplaySwitchRequest _pendingRequest;
        private CancellationTokenSource _switchCts;
        private bool _disposed;

        public GameplayContext Context => _context;
        public IGameplayMode CurrentMode => _current;
        public bool IsSwitching => _context.IsSwitching;

        public GameplayFlowMachine(GameplayContext context, GameplayModeRegistry registry)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public UniTask<GameplaySwitchResult> SwitchToAsync(GameplaySwitchRequest request, CancellationToken externalToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (_disposed) return UniTask.FromResult(GameplaySwitchResult.Failed(request.Target, "GameplayFlowMachine disposed."));

            if (_context.IsSwitching)
            {
                if (request.BusyPolicy == GameplaySwitchBusyPolicy.DropIfBusy)
                    return UniTask.FromResult(GameplaySwitchResult.Dropped(request.Target));

                _pendingRequest = request;
                _context.Events.Publish(new GameplaySwitchPendingEvent(request.Target, request.DebugName));
                return UniTask.FromResult(GameplaySwitchResult.Pending(request.Target));
            }

            return RunSwitchLoopAsync(request, externalToken);
        }

        public void CancelCurrentSwitch()
        {
            _pendingRequest = null;
            _switchCts?.Cancel();
        }

        public void Tick(float deltaTime)
        {
            if (_context.Events.HasSubscribers<GameplayTickEvent>())
                _context.Events.Publish(new GameplayTickEvent(deltaTime));
        }

        private async UniTask<GameplaySwitchResult> RunSwitchLoopAsync(GameplaySwitchRequest firstRequest, CancellationToken externalToken)
        {
            _context.SetSwitchingState(true);

            _switchCts = externalToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(_lifeCts.Token, externalToken)
                : CancellationTokenSource.CreateLinkedTokenSource(_lifeCts.Token);

            var token = _switchCts.Token;
            GameplaySwitchResult finalResult = default;

            try
            {
                var request = firstRequest;
                while (request != null)
                {
                    _pendingRequest = null;
                    finalResult = await ExecuteSwitchAsync(request, token);
                    request = _pendingRequest;
                }

                return finalResult;
            }
            finally
            {
                _context.SetSwitchingState(false);
                _switchCts?.Dispose();
                _switchCts = null;
            }
        }

        private async UniTask<GameplaySwitchResult> ExecuteSwitchAsync(GameplaySwitchRequest request, CancellationToken token)
        {
            if (!request.Force && _current != null && _current.Id == request.Target)
            {
                _context.Events.Publish(new GameplaySwitchSkippedEvent(request.Target, "same mode"));
                return GameplaySwitchResult.Skipped(request.Target);
            }

            _context.Events.Publish(new GameplaySwitchStartedEvent(_context.CurrentModeId, request.Target, request.DebugName));

            var useLoading = request.LoadingPolicy != GameplayLoadingPolicy.None;
            var isFirstSwitch = _current == null;
            var progress = _context.Progress;
            var remaining = 1f - progress.Current;

            IGameplayMode next = null;
            try
            {
                next = _registry.Create(request.Target, _context);

                if (useLoading)
                {
                    _context.Systems.Ui?.SetBlockInput(true);
                    _context.Systems.Ui?.ShowLoading(request.DebugName);
                }

                if (!isFirstSwitch)
                {
                    using (progress.BeginPhase(progress.Current, progress.Current + 0.15f * remaining))
                    {
                        progress.Report(0f, "退出旧模式");
                        await _current.ExitAsync(request, token);
                        _current.Dispose();
                        progress.Report(0.3f, "已退出");

                        SwitchToEmptyScene(request);
                        progress.Report(0.6f, "切换空场景");

                        await CollectGarbageAsync(token);
                        progress.Report(1f, "GC 完成");
                    }
                }

                using (progress.BeginPhase(progress.Current, progress.Current + 0.85f * remaining))
                {
                    _context.Events.Publish(new GameplayModeLoadStartedEvent(request.Target));
                    await next.LoadAsync(request, token);
                    _context.Events.Publish(new GameplayModeLoadCompletedEvent(request.Target));
                }

                using (progress.BeginPhase(progress.Current, 1f))
                {
                    progress.Report(0f, "进入新模式");
                    _context.CommitModeSwitch(next.Id);
                    _current = next;
                    await next.EnterAsync(request, token);
                }

                _context.Events.Publish(new GameplaySwitchCompletedEvent(_context.LastModeId, _context.CurrentModeId));
                return GameplaySwitchResult.Success(request.Target);
            }
            catch (OperationCanceledException)
            {
                next?.Dispose();
                _context.Events.Publish(new GameplaySwitchFailedEvent(request.Target, "canceled"));
                return GameplaySwitchResult.Canceled(request.Target);
            }
            catch (Exception ex)
            {
                next?.Dispose();
                Debug.LogException(ex);
                _context.Events.Publish(new GameplaySwitchFailedEvent(request.Target, ex.Message));
                return GameplaySwitchResult.Failed(request.Target, ex.Message);
            }
            finally
            {
                if (useLoading)
                {
                    _context.Systems.Ui?.HideLoading();
                    _context.Systems.Ui?.SetBlockInput(false);
                }
            }
        }

        private static void SwitchToEmptyScene(GameplaySwitchRequest request)
        {
            var safeSceneName = request.GetOrDefault("SafeScene", "Empty");

            var prevScene = SceneManager.GetSceneByName(safeSceneName);
            if (!prevScene.isLoaded)
                prevScene = SceneManager.CreateScene(safeSceneName);

            SceneManager.SetActiveScene(prevScene);
        }

        private static async UniTask CollectGarbageAsync(CancellationToken token)
        {
            GC.Collect();
            var gcRequest = Resources.UnloadUnusedAssets();
            while (!gcRequest.isDone)
                await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _pendingRequest = null;
            _switchCts?.Cancel();
            _switchCts?.Dispose();

            _lifeCts.Cancel();
            _lifeCts.Dispose();

            _current?.Dispose();
            _current = null;
        }
    
}