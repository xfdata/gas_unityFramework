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
        DarkSide
    }

    public Action OnDestroyAction;

    [SerializeField]
    private CanvasScaler[] _scalers;

    [SerializeField]
    private RectTransform[] _controlls;

    [SerializeField]
    private Image _ImageLeft;

    [SerializeField]
    private Image _ImageRight;

    [SerializeField]
    private AdaptMode _mode;

    public AdaptMode Mode => _mode;
    public float SideVal { get; private set; }
    
    public float ScreenSideVal { get; private set;}
    public Rect ViewPort { get; private set; }

    private void OnEnable()
    {
        Handle();
    }

    private void OnDestroy()
    {
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

#if UNITY_EDITOR
    private AdaptMode _lastMode;
    // Update is called once per frame
    void Update()
    {
        if (!Application.isPlaying)
        {
            if (_lastMode != _mode)
            {
                _lastMode = _mode;
                Handle();
            }
        }
    }
#endif

    private void Handle()
    {
        switch (_mode)
        {
            case AdaptMode.Expand:
                foreach (var scaler in _scalers)
                {
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                }
                _ImageLeft?.gameObject?.SetActive(false);
                _ImageRight?.gameObject?.SetActive(false);
                SideVal = 0;
                break;
            case AdaptMode.DarkSide:
                var fitHeight = false;
                var canvas0 = _scalers[0].GetComponent<Canvas>();
                var screenSize = canvas0.renderingDisplaySize;
                var referenceResolution = _scalers[0].referenceResolution;
                fitHeight = screenSize.x / referenceResolution.x > screenSize.y / referenceResolution.y;

                foreach (var scaler in _scalers)
                {
                    var canvas = scaler.GetComponent<Canvas>();
                    Debug.Assert(scaler.referenceResolution == referenceResolution);
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    scaler.matchWidthOrHeight = fitHeight ? 1 : 0;
                }

                _ImageLeft?.gameObject.SetActive(fitHeight);
                _ImageRight?.gameObject.SetActive(fitHeight);
                if (fitHeight)
                {
                    var calcScreenSize = referenceResolution.y * screenSize.x / screenSize.y;
                    SideVal = (calcScreenSize - referenceResolution.x) / 2;
                    ScreenSideVal = SideVal * screenSize.x / calcScreenSize;
                    var val = calcScreenSize - SideVal;
                    if (_ImageRight != null)
                    {
                        var offsetMin = _ImageRight.rectTransform.offsetMin;
                        _ImageRight.rectTransform.offsetMin = new Vector2(val, offsetMin.y);
                    }

                    if (_ImageLeft != null)
                    {
                        var offsetMax = _ImageLeft.rectTransform.offsetMax;
                        _ImageLeft.rectTransform.offsetMax = new Vector2(-val, offsetMax.y);
                    }
                }
                else
                {
                    SideVal = 0;
                    ScreenSideVal = 0;
                }
                break;
        }

        foreach (var control in _controlls)
        {
            var offsetMin = control.offsetMin;
            control.offsetMin = new Vector2(SideVal, offsetMin.y);
            var offsetMax = control.offsetMax;
            control.offsetMax = new Vector2(-SideVal, offsetMax.y);
        }

        var screenOffsetPixels = SideVal * Screen.height / _scalers[0].referenceResolution.y;
        var viewOffsetnormalizedDis = screenOffsetPixels / Screen.width;
        ViewPort = new Rect(new Vector2(viewOffsetnormalizedDis, 0), new Vector2(1 - 2 * viewOffsetnormalizedDis, 1));
    }

}
