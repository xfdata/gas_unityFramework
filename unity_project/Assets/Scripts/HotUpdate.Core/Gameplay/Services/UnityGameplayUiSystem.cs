using UnityEngine;


    public sealed class UnityGameplayUiSystem : IGameplayUiSystem
    {
        public void ShowLoading(string title)
        {
#if UNITY_EDITOR
            Debug.Log($"[GameplayUI] ShowLoading: {title}");
#endif
        }

        public void UpdateLoading(float progress, string status)
        {
#if UNITY_EDITOR
            Debug.Log($"[GameplayUI] ShowLoading: {progress}");
#endif
        }

        public void HideLoading()
        {
#if UNITY_EDITOR
            Debug.Log("[GameplayUI] HideLoading");
#endif
        }

        public void SetBlockInput(bool block)
        {
#if UNITY_EDITOR
            Debug.Log($"[GameplayUI] BlockInput: {block}");
#endif
        }

        public void Open<TView>(object param = null) where TView : ViewBase
        {
#if UNITY_EDITOR
            Debug.Log($"[GameplayUI] Open: {typeof(TView).Name}");
#endif
        }

        public void Close<TView>(object result = null) where TView : ViewBase
        {
#if UNITY_EDITOR
            Debug.Log($"[GameplayUI] Close: {typeof(TView).Name}");
#endif
        }
    }
