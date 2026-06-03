using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScoreItemView : UIModuleBase
{
    private UIObjectRef _toggleGroup;
    private UIObjectRef _countGroup;
    private Toggle _checkToggle;
    private UITextRef _txtTitle;
    private UITextRef _txtScore;
    private UIButtonRef _btnMinus;
    private UIButtonRef _btnPlus;
    private UITextRef _txtCount;

    private ScoreConfigItem _config;
    private ScoreState _state;
    private Action _onChanged;

    public string ItemId => _config?.id;

    public void Setup(UIViewBinder binder, ScoreConfigItem config, ScoreState state, Action onChanged)
    {
        B = binder;
        _config = config;
        _state = state;
        _onChanged = onChanged;

        Cache(ref _toggleGroup, "ToggleGroup");
        Cache(ref _countGroup, "CountGroup");
        Cache(ref _checkToggle, "CheckToggle");
        Cache(ref _txtTitle, "TxtTitle");
        Cache(ref _txtScore, "TxtScore");
        Cache(ref _btnMinus, "BtnMinus");
        Cache(ref _btnPlus, "BtnPlus");
        Cache(ref _txtCount, "TxtCount");

        bool isCountMode = config.maxCount > 1;
        _toggleGroup.GameObject.SetActive(!isCountMode);
        _countGroup.GameObject.SetActive(isCountMode);
        _txtTitle.TMPText.text = config.title;

        if (isCountMode)
        {
            _btnMinus.Button.onClick.AddListener(OnMinusClicked);
            _btnPlus.Button.onClick.AddListener(OnPlusClicked);
        }
        else
        {
            _checkToggle.onValueChanged.AddListener(OnToggleChanged);
        }

        RefreshUI();
    }

    public void RefreshUI()
    {
        if (_config == null || B == null) return;

        int count = _state?.count ?? 0;
        bool isCountMode = _config.maxCount > 1;

        if (isCountMode)
        {
            _btnMinus.Button.interactable = count > 0;
            _btnPlus.Button.interactable = count < _config.maxCount;
            _txtCount.TMPText.text = $"{count} / {_config.maxCount}";
            _txtScore.TMPText.text = $"当前：{count * _config.scorePerCount}分";
        }
        else
        {
            bool isChecked = count >= 1;
            _checkToggle.SetIsOnWithoutNotify(isChecked);
            _txtScore.TMPText.text = isChecked ? $"已得{_config.scorePerCount}分" : $"+{_config.scorePerCount}分";
        }
    }

    private void OnToggleChanged(bool isOn)
    {
        if (_state == null || _config == null) return;
        _state.count = isOn ? _config.maxCount : 0;
        RefreshUI();
        _onChanged?.Invoke();
    }

    private void OnMinusClicked()
    {
        if (_state == null || _config == null) return;
        _state.count = Mathf.Max(0, _state.count - 1);
        RefreshUI();
        _onChanged?.Invoke();
    }

    private void OnPlusClicked()
    {
        if (_state == null || _config == null) return;
        _state.count = Mathf.Min(_config.maxCount, _state.count + 1);
        RefreshUI();
        _onChanged?.Invoke();
    }

    protected override void OnStop()
    {
        if (B == null) return;
        _checkToggle?.onValueChanged.RemoveListener(OnToggleChanged);
        _btnMinus?.Button?.onClick.RemoveListener(OnMinusClicked);
        _btnPlus?.Button?.onClick.RemoveListener(OnPlusClicked);

        if (B.Source != null)
            GameObject.Destroy(B.Source.gameObject);
    }
}