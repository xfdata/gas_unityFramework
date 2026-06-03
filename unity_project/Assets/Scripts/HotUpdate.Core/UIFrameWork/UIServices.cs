using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public sealed class UIInputBlockService : IDisposable
{
    private readonly GameObject _blockObject;
    private readonly Dictionary<string, int> _references = new();
    private int _referenceCount;

    public int ReferenceCount => _referenceCount;

    public UIInputBlockService(GameObject blockObject)
    {
        _blockObject = blockObject ?? throw new ArgumentNullException(nameof(blockObject));
        _blockObject.SetActive(false);
    }

    public void AddRef(string reason)
    {
        reason = string.IsNullOrEmpty(reason) ? "Unknown" : reason;
        _references.TryGetValue(reason, out var count);
        _references[reason] = count + 1;
        _referenceCount++;
        if (_referenceCount == 1 && !_blockObject.activeSelf)
            _blockObject.SetActive(true);
    }

    public void RemoveRef(string reason)
    {
        reason = string.IsNullOrEmpty(reason) ? "Unknown" : reason;
        if (!_references.TryGetValue(reason, out var count))
            return;

        if (count <= 1)
            _references.Remove(reason);
        else
            _references[reason] = count - 1;

        _referenceCount = Math.Max(0, _referenceCount - 1);
        if (_referenceCount == 0 && _blockObject.activeSelf)
            _blockObject.SetActive(false);
    }

    public void Dispose()
    {
        _references.Clear();
        _referenceCount = 0;
        if (_blockObject != null)
            _blockObject.SetActive(false);
    }
}

public sealed class UIMaskService : IDisposable
{
    private readonly GameObject _maskObject;
    private readonly Image _image;
    private readonly Button _button;
    private readonly Color _darkColor;
    private readonly Dictionary<UIWindow, UIMaskMode> _windows = new();

    private UIWindow _topWindow;
    private UIMaskMode _topMode;

    public UIMaskService(GameObject maskObject)
    {
        _maskObject = maskObject ?? throw new ArgumentNullException(nameof(maskObject));
        _image = _maskObject.GetComponent<Image>() ??
            throw new ArgumentException("[UIMaskService] Mask object must contain an Image.", nameof(maskObject));
        _button = _maskObject.GetComponent<Button>() ??
            throw new ArgumentException("[UIMaskService] Mask object must contain a Button.", nameof(maskObject));
        _darkColor = _image.color;
        _button.onClick.AddListener(HandleClick);
        _maskObject.SetActive(false);
    }

    public void Show(UIWindow window, UIMaskMode mode)
    {
        if (window == null || mode == UIMaskMode.None)
            return;

        if (_windows.TryGetValue(window, out var currentMode) && currentMode == mode)
            return;

        _windows[window] = mode;
        Refresh();
    }

    public void Hide(UIWindow window)
    {
        if (window == null)
            return;

        if (!_windows.Remove(window))
            return;

        Refresh();
    }

    public void Refresh()
    {
        _topWindow = null;
        _topMode = UIMaskMode.None;

        foreach (var entry in _windows)
        {
            var window = entry.Key;
            if (window == null ||
                (window.State != UIWindowState.Opening && !window.IsReady) ||
                window.GameObject == null)
                continue;

            if (_topWindow == null || window.SortOrder > _topWindow.SortOrder)
            {
                _topWindow = window;
                _topMode = entry.Value;
            }
        }

        if (_topWindow == null)
        {
            if (_maskObject.activeSelf)
                _maskObject.SetActive(false);
            return;
        }

        var targetTransform = _topWindow.GameObject.transform;
        var parent = targetTransform.parent;
        if (parent == null)
        {
            if (_maskObject.activeSelf)
                _maskObject.SetActive(false);
            return;
        }

        if (!_image.raycastTarget)
            _image.raycastTarget = true;

        var color = _topMode == UIMaskMode.BlockInputOnly
            ? new Color(_darkColor.r, _darkColor.g, _darkColor.b, 0f)
            : _darkColor;
        if (_image.color != color)
            _image.color = color;

        var maskTransform = _maskObject.transform;
        if (maskTransform.parent != parent)
            maskTransform.SetParent(parent, false);
        if (!_maskObject.activeSelf)
            _maskObject.SetActive(true);

        var maskIndex = maskTransform.GetSiblingIndex();
        var targetIndex = targetTransform.GetSiblingIndex();
        if (maskIndex + 1 != targetIndex)
        {
            var desiredIndex = maskIndex < targetIndex ? targetIndex - 1 : targetIndex;
            maskTransform.SetSiblingIndex(desiredIndex);
        }
    }

    private void HandleClick()
    {
        if (_topWindow == null)
            return;

        if (_topMode == UIMaskMode.DarkMaskClose || _topWindow.Config.CloseByMask)
            _topWindow.CloseAsync().Forget();
    }

    public void Dispose()
    {
        _windows.Clear();
        _topWindow = null;
        _button.onClick.RemoveListener(HandleClick);
        if (_maskObject != null && _maskObject.activeSelf)
            _maskObject.SetActive(false);
    }
}