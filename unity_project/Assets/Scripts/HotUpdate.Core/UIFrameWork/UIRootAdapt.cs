using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class UIRootAdapt : MonoBehaviour
{
    public enum AdaptMode
    {
        Expand,
        DarkSide,
    }

    public Action OnDestroyAction;
    public event Action LayoutChanged;

    [SerializeField] private CanvasScaler[] _scalers;
    [SerializeField] private RectTransform[] _controlls;
    [SerializeField] private Image _ImageLeft;
    [SerializeField] private Image _ImageRight;
    [SerializeField] private AdaptMode _mode;

    private AdaptMode _lastMode;
    private int _lastScreenWidth = -1;
    private int _lastScreenHeight = -1;
    private Rect _lastSafeArea;

    public AdaptMode Mode => _mode;
    public float SideVal { get; private set; }
    public float ScreenSideVal { get; private set; }
    public Rect ViewPort { get; private set; }

    private void OnEnable()
    {
        RefreshLayout(true);
    }

    private void Update()
    {
        RefreshLayout(false);
    }

    private void OnDestroy()
    {
        LayoutChanged = null;
        OnDestroyAction?.Invoke();
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<b>UIRootAdapt</b>: ");
        sb.AppendLine("<b>Mode</b>: " + _mode);
        sb.AppendLine("<b>SideVal</b>: " + SideVal);
        sb.AppendLine("<b>ScreenSideVal</b>: " + ScreenSideVal);
        return sb.ToString();
    }

    public void RefreshLayout(bool force = false)
    {
        var safeArea = Screen.safeArea;
        if (!force &&
            _lastMode == _mode &&
            _lastScreenWidth == Screen.width &&
            _lastScreenHeight == Screen.height &&
            _lastSafeArea == safeArea)
        {
            return;
        }

        _lastMode = _mode;
        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;
        _lastSafeArea = safeArea;

        Handle();
        LayoutChanged?.Invoke();
    }

    private void Handle()
    {
        var firstScaler = GetFirstScaler();
        if (firstScaler == null)
        {
            SideVal = 0f;
            ScreenSideVal = 0f;
            ViewPort = new Rect(0f, 0f, 1f, 1f);
            return;
        }

        switch (_mode)
        {
            case AdaptMode.Expand:
                ConfigureExpand();
                break;
            case AdaptMode.DarkSide:
                ConfigureDarkSide(firstScaler);
                break;
        }

        ApplyControlOffsets();
        UpdateViewport(firstScaler);
    }

    private void ConfigureExpand()
    {
        ForEachScaler(scaler =>
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        });

        _ImageLeft?.gameObject.SetActive(false);
        _ImageRight?.gameObject.SetActive(false);
        SideVal = 0f;
        ScreenSideVal = 0f;
    }

    private void ConfigureDarkSide(CanvasScaler firstScaler)
    {
        var canvas = firstScaler.GetComponent<Canvas>();
        var screenSize = canvas != null ? canvas.renderingDisplaySize : new Vector2(Screen.width, Screen.height);
        var referenceResolution = firstScaler.referenceResolution;

        if (screenSize.x <= 0f || screenSize.y <= 0f ||
            referenceResolution.x <= 0f || referenceResolution.y <= 0f)
        {
            SideVal = 0f;
            ScreenSideVal = 0f;
            return;
        }

        var fitHeight = screenSize.x / referenceResolution.x > screenSize.y / referenceResolution.y;
        ForEachScaler(scaler =>
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = fitHeight ? 1f : 0f;
        });

        _ImageLeft?.gameObject.SetActive(fitHeight);
        _ImageRight?.gameObject.SetActive(fitHeight);

        if (!fitHeight)
        {
            SideVal = 0f;
            ScreenSideVal = 0f;
            return;
        }

        var calculatedWidth = referenceResolution.y * screenSize.x / screenSize.y;
        SideVal = Mathf.Max(0f, (calculatedWidth - referenceResolution.x) * 0.5f);
        ScreenSideVal = SideVal * screenSize.x / calculatedWidth;
        var edge = calculatedWidth - SideVal;

        if (_ImageRight != null)
        {
            var offsetMin = _ImageRight.rectTransform.offsetMin;
            _ImageRight.rectTransform.offsetMin = new Vector2(edge, offsetMin.y);
        }

        if (_ImageLeft != null)
        {
            var offsetMax = _ImageLeft.rectTransform.offsetMax;
            _ImageLeft.rectTransform.offsetMax = new Vector2(-edge, offsetMax.y);
        }
    }

    private void ApplyControlOffsets()
    {
        if (_controlls == null)
            return;

        foreach (var control in _controlls)
        {
            if (control == null)
                continue;

            var offsetMin = control.offsetMin;
            control.offsetMin = new Vector2(SideVal, offsetMin.y);
            var offsetMax = control.offsetMax;
            control.offsetMax = new Vector2(-SideVal, offsetMax.y);
        }
    }

    private void UpdateViewport(CanvasScaler firstScaler)
    {
        if (Screen.width <= 0 || firstScaler.referenceResolution.y <= 0f)
        {
            ViewPort = new Rect(0f, 0f, 1f, 1f);
            return;
        }

        var screenOffsetPixels = SideVal * Screen.height / firstScaler.referenceResolution.y;
        var normalizedOffset = Mathf.Clamp01(screenOffsetPixels / Screen.width);
        ViewPort = new Rect(normalizedOffset, 0f, Mathf.Max(0f, 1f - 2f * normalizedOffset), 1f);
    }

    private CanvasScaler GetFirstScaler()
    {
        if (_scalers == null)
            return null;

        foreach (var scaler in _scalers)
        {
            if (scaler != null)
                return scaler;
        }

        return null;
    }

    private void ForEachScaler(Action<CanvasScaler> action)
    {
        if (_scalers == null || action == null)
            return;

        foreach (var scaler in _scalers)
        {
            if (scaler != null)
                action(scaler);
        }
    }
}
