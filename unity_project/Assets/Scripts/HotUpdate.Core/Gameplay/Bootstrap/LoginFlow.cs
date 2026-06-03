using Cysharp.Threading.Tasks;
using UnityEngine;

public class LoginFlow
{
    private readonly UIRuntime _uiRuntime;

    public LoginFlow()
    {
        _uiRuntime = UIRuntime.Instance;
    }

    public async UniTask RunAsync(bool skipLogin = false)
    {
        if (skipLogin)
        {
            Debug.Log("[LoginFlow] 跳过登录界面，直接进入城市");
            await EnterCityAsync();
            return;
        }

        Debug.Log("[LoginFlow] 显示登录界面");
        await _uiRuntime.Open<LoginView>();

        await UniTask.WaitUntil(() => !_uiRuntime.IsOpen<LoginView>());

        Debug.Log("[LoginFlow] 登录界面关闭，进入城市");
        await EnterCityAsync();
    }

    private async UniTask EnterCityAsync()
    {
        var runtime = GameplayRuntime.Instance;
        if (runtime == null)
        {
            Debug.LogError("[LoginFlow] GameplayRuntime.Instance 为空，无法进入城市");
            return;
        }

        var result = await runtime.EnterCityAsync();

        if (!result.IsSuccess)
            Debug.LogError($"[LoginFlow] 进入城市失败: {result.Error}");
    }
}