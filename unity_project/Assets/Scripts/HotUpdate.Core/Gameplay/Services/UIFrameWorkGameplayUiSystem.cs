using Cysharp.Threading.Tasks;

    public sealed class UIFrameWorkGameplayUiSystem : IGameplayUiSystem
    {
        private readonly UIRuntime _runtime;

        private const string InputBlockReason = "Gameplay";

        public UIFrameWorkGameplayUiSystem(UIRuntime runtime = null)
        {
            _runtime = runtime ?? UIRuntime.Instance;
        }

        public void ShowLoading(string title)
        {
            _runtime.Open<LoadingView>(title).Forget();
        }

        public void UpdateLoading(float progress, string status)
        {
            var view = _runtime.Get<LoadingView>();
            if (view != null)
                view.SetProgress(progress, status);
        }

        public void HideLoading()
        {
            _runtime.Close<LoadingView>();
        }

        public void SetBlockInput(bool block)
        {
            if (_runtime?.InputBlock == null)
                return;

            if (block)
                _runtime.InputBlock.AddRef(InputBlockReason);
            else
                _runtime.InputBlock.RemoveRef(InputBlockReason);
        }

        public void Open<TView>(object param = null) where TView : ViewBase
        {
            _runtime.Open<TView>(param).Forget();
        }

        public void Close<TView>(object result = null) where TView : ViewBase
        {
            _runtime.Close<TView>(result);
        }
    }
