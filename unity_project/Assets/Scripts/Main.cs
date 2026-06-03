using Cysharp.Threading.Tasks;
using UnityEngine;

public class Main : MonoBehaviour
{
    private async void Start()
    {
        var uiRootObj = GameObject.Find("UI Root");
        if (uiRootObj == null)
        {
            Debug.LogError("[Main] UI Root not found in scene.");
            return;
        }

        DontDestroyOnLoad(uiRootObj);

        var uiRootConfig = uiRootObj.GetComponent<UIRootConfig>();
        if (uiRootConfig == null)
        {
            Debug.LogError("[Main] UIRootConfig not found on UI Root.");
            return;
        }

        GameFacade.Instance.StartUp(uiRootConfig);

        var loginFlow = new LoginFlow();
        await loginFlow.RunAsync();
    }
}