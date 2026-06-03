using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ChildScorePanel : UIModuleBase
{
    private UITextRef _txtName;
    private UITextRef _txtScore;
    private UIObjectRef _itemRoot;

    private ChildData _child;
    private List<ScoreConfigItem> _items;
    private List<ScoreState> _states;
    private Action _onChanged;
    private GameObject _itemPrefab;
    private readonly List<ScoreItemView> _itemViews = new();

    public string ChildId => _child?.id;

    public void Setup(UIViewBinder binder, GameObject itemPrefab,
        ChildData child, List<ScoreConfigItem> items, List<ScoreState> states,
        Action onChanged)
    {
        B = binder;
        _itemPrefab = itemPrefab;
        _child = child;
        _items = items;
        _states = states;
        _onChanged = onChanged;

        Cache(ref _txtName, "TxtName");
        Cache(ref _txtScore, "TxtScore");
        Cache(ref _itemRoot, "ItemRoot");

        _txtName.TMPText.text = child.name;
        BuildItems();
        RefreshUI();
    }

    private void BuildItems()
    {
        ClearItems();

        if (_itemPrefab == null || _items == null)
            return;

        foreach (var item in _items)
        {
            var state = _states.Find(s => s.itemId == item.id);
            if (state == null)
            {
                state = new ScoreState { itemId = item.id, count = 0 };
                _states.Add(state);
            }

            var go = GameObject.Instantiate(_itemPrefab, _itemRoot.Transform);
            var bindBehaviour = go.GetComponent<CSharpUIBindBehaviour>();
            var itemBinder = new UIViewBinder(bindBehaviour);
            var view = new ScoreItemView();
            AddModule(view);
            view.Setup(itemBinder, item, state, OnItemChanged);
            _itemViews.Add(view);
        }
    }

    private void ClearItems()
    {
        foreach (var view in _itemViews)
        {
            if (view is IDisposable d) d.Dispose();
        }
        _itemViews.Clear();
    }

    public void RefreshUI()
    {
        if (_items == null || _states == null)
            return;

        int total = ScoreUtils.CalcTotal(_states, _items);
        int maxScore = ScoreUtils.CalcMax(_items);
        _txtScore.TMPText.text = $"今日：{total} / {maxScore}分";

        foreach (var view in _itemViews)
            view?.RefreshUI();
    }

    public int GetTotalScore()
    {
        return ScoreUtils.CalcTotal(_states, _items);
    }

    private void OnItemChanged()
    {
        RefreshUI();
        _onChanged?.Invoke();
    }
}