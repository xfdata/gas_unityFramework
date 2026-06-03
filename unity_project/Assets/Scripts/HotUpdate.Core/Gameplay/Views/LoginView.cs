using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoginView : ViewBase
{
    private const string PrefKeyAccount = "Login_Account";

    [UI] private TextMeshProUGUI txt_BundleVersion;
    [UI] private TMP_InputField input_Account;
    [UI] private Button btn_Login;

    private bool _isLoggingIn;

    protected override UniTask OnOpen(object param)
    {
        txt_BundleVersion.text = $"v{Application.version}";

        var savedAccount = PlayerPrefs.GetString(PrefKeyAccount, string.Empty);
        if (!string.IsNullOrEmpty(savedAccount))
            input_Account.text = savedAccount;

        var existingAccounts = ScoreStorage.GetAllAccounts();
        if (existingAccounts.Count > 0)
        {
            input_Account.placeholder.GetComponent<TextMeshProUGUI>().text =
                $"已有账号: {string.Join(", ", existingAccounts)}";
        }

        btn_Login.onClick.AddListener(OnLoginClicked);

        return UniTask.CompletedTask;
    }

    protected override UniTask OnClose(object result)
    {
        btn_Login.onClick.RemoveListener(OnLoginClicked);

        return UniTask.CompletedTask;
    }

    private void OnLoginClicked()
    {
        if (_isLoggingIn)
            return;

        var account = input_Account.text.Trim();

        if (string.IsNullOrEmpty(account))
        {
            Debug.LogWarning("[LoginView] 账号为空");
            return;
        }

        _isLoggingIn = true;
        SetInteractable(false);

        var isNewAccount = !ScoreStorage.AccountExists(account);
        Debug.Log($"[LoginView] 登录 — 账号: {account}, {(isNewAccount ? "新账号" : "已有账号")}");

        PlayerPrefs.SetString(PrefKeyAccount, account);
        PlayerPrefs.Save();

        ScoreStorage.CurrentAccount = account;

        if (isNewAccount)
        {
            ScoreStorage.SeedTestData();
            Debug.Log($"[LoginView] 新账号「{account}」已创建测试数据");
        }

        Close();
    }

    private void SetInteractable(bool interactable)
    {
        if (input_Account != null)
            input_Account.interactable = interactable;

        if (btn_Login != null)
            btn_Login.interactable = interactable;
    }
}