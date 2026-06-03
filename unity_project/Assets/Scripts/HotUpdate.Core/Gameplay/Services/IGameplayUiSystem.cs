
    public interface IGameplayUiSystem
    {
        void ShowLoading(string title);
        void UpdateLoading(float progress, string status);
        void HideLoading();
        void SetBlockInput(bool block);
        void Open<TView>(object param = null) where TView : ViewBase;
        void Close<TView>(object result = null) where TView : ViewBase;
    }
