using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoadingView : ViewBase
{
    [UI] private GameObject Image_Logo;
    [UI] private Image img_Fill;
    [UI] private TextMeshProUGUI txt_ProgressNum;
    [UI] private TextMeshProUGUI txt_Strategy;

    protected override UniTask OnOpen(object param)
    {
        Image_Logo?.SetActive(true);

        return UniTask.CompletedTask;
    }

    public void SetProgress(float progress, string status)
    {
        if (img_Fill != null)
            img_Fill.fillAmount = Mathf.Clamp01(progress);
        if (txt_ProgressNum != null)
            txt_ProgressNum.text = $"{progress * 100}%";

        if (txt_Strategy != null && !string.IsNullOrEmpty(status))
            txt_Strategy.text = status;
    }

    protected override UniTask OnClose(object result)
    {
        Image_Logo?.SetActive(false);

        return UniTask.CompletedTask;
    }
}
