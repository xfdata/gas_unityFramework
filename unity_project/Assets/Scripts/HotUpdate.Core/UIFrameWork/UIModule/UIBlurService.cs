using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Manages UI background blur nodes and the temporary render textures used by snapshot blur.
/// </summary>
public sealed class UIBlurService : Disposable
{
    private const int BlurRendererIndex = 1;
    private const float RtTextureScale = 0.125f;

    private readonly Dictionary<UIWindow, BlurEntry> _windowEntries = new();
    private readonly HashSet<UIWindow> _realTimeWindows = new();

    private bool _isCapturing;
    private bool _hasRealTimeRendererOverride;
    private int _originRendererIndex;

    public async UniTask Attach(UIWindow contextWindow, UIBlurMode configBlurMode, CancellationToken token = default)
    {
        if (contextWindow == null || configBlurMode == UIBlurMode.None || contextWindow.View == null)
            return;

        Detach(contextWindow);

        switch (configBlurMode)
        {
            case UIBlurMode.SnapshotBlur:
                await AttachSnapshotBlur(contextWindow, token);
                break;
            case UIBlurMode.RealTimeBlur:
                AttachRealTimeBlur(contextWindow);
                break;
        }
    }

    public void Detach(UIWindow contextWindow)
    {
        if (contextWindow == null)
            return;

        if (_windowEntries.Remove(contextWindow, out var entry))
            ReleaseEntry(entry);

        if (_realTimeWindows.Remove(contextWindow) && _realTimeWindows.Count == 0)
            DisableRealTimeBlur();
    }

    protected override void OnDispose()
    {
        foreach (var entry in _windowEntries.Values)
            ReleaseEntry(entry);
        _windowEntries.Clear();

        _realTimeWindows.Clear();
        DisableRealTimeBlur();
        base.OnDispose();
    }

    private async UniTask AttachSnapshotBlur(UIWindow window, CancellationToken token)
    {
        var rt = await CaptureBlurTexture(token);
        if (rt == null || token.IsCancellationRequested || window.View == null || window.GameObject == null)
        {
            ReleaseTexture(rt);
            return;
        }

        var blurObject = CreateSnapshotBlurObject(rt);
        window.View.AttachBlurTransform(blurObject.transform);
        _windowEntries[window] = new BlurEntry(UIBlurMode.SnapshotBlur, blurObject, rt);
    }

    private void AttachRealTimeBlur(UIWindow window)
    {
        EnableRealTimeBlur();
        _realTimeWindows.Add(window);
        _windowEntries[window] = new BlurEntry(UIBlurMode.RealTimeBlur, null, null);
    }

    private async UniTask<RenderTexture> CaptureBlurTexture(CancellationToken token)
    {
        var camera = UIRuntime.Instance?.Root?.UICamera;
        var cameraData = camera != null ? camera.GetComponent<UniversalAdditionalCameraData>() : null;
        var feature = BlurUIRenderPassFeature.Instance;

        if (cameraData == null || feature == null)
        {
            Debug.LogWarning("[UIBlurService] Snapshot blur needs a UI camera with UniversalAdditionalCameraData and BlurUIRenderPassFeature.");
            return null;
        }

        RenderTexture rt = null;
        var completed = false;

        try
        {
            await UniTask.WaitUntil(() => !_isCapturing, cancellationToken: token);
            _isCapturing = true;

            var rtWidth = Mathf.Max(1, Mathf.FloorToInt(Screen.width * RtTextureScale));
            var rtHeight = Mathf.Max(1, Mathf.FloorToInt(Screen.height * RtTextureScale));
            rt = RenderTexture.GetTemporary(rtWidth, rtHeight, 0, RenderTextureFormat.Default);
            rt.filterMode = FilterMode.Bilinear;
            rt.wrapMode = TextureWrapMode.Clamp;

            var lastRendererIndex = cameraData.RendererIndex;
            var completionSource = new UniTaskCompletionSource();
            Action onBlurCompleted = () =>
            {
                completed = true;
                completionSource.TrySetResult();
            };

            try
            {
                feature.BlurUIEvent = onBlurCompleted;
                cameraData.SetRenderer(BlurRendererIndex);
                feature.DoingBlur(rt);
                await completionSource.Task.AttachExternalCancellation(token);
                return rt;
            }
            finally
            {
                if (feature.BlurUIEvent == onBlurCompleted)
                    feature.BlurUIEvent = null;
                cameraData.SetRenderer(lastRendererIndex);
            }
        }
        catch (OperationCanceledException)
        {
            if (!completed)
                await UniTask.DelayFrame(1);

            ReleaseTexture(rt);
            return null;
        }
        finally
        {
            _isCapturing = false;
        }
    }

    private void EnableRealTimeBlur()
    {
        var camera = UIRuntime.Instance?.Root?.UICamera;
        var cameraData = camera != null ? camera.GetComponent<UniversalAdditionalCameraData>() : null;
        if (cameraData == null)
        {
            Debug.LogWarning("[UIBlurService] Real-time blur needs a UI camera with UniversalAdditionalCameraData.");
            return;
        }

        if (!_hasRealTimeRendererOverride)
        {
            _originRendererIndex = cameraData.RendererIndex;
            _hasRealTimeRendererOverride = true;
        }

        RealTimeBlurRenderPassFeature.Instance?.CreateRT();
        cameraData.SetRenderer(BlurRendererIndex);
    }

    private void DisableRealTimeBlur()
    {
        if (!_hasRealTimeRendererOverride)
            return;

        var camera = UIRuntime.Instance?.Root?.UICamera;
        var cameraData = camera != null ? camera.GetComponent<UniversalAdditionalCameraData>() : null;
        if (cameraData != null)
            cameraData.SetRenderer(_originRendererIndex);

        RealTimeBlurRenderPassFeature.Instance?.ReleaseRT();
        _hasRealTimeRendererOverride = false;
    }

    private static GameObject CreateSnapshotBlurObject(RenderTexture rt)
    {
        var blurObject = new GameObject("UIBlurSnapshot", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        var rectTransform = (RectTransform)blurObject.transform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        var rawImage = blurObject.GetComponent<RawImage>();
        rawImage.texture = rt;
        rawImage.color = Color.white;
        rawImage.raycastTarget = false;
        return blurObject;
    }

    private static void ReleaseEntry(BlurEntry entry)
    {
        if (entry == null)
            return;

        if (entry.BlurObject != null)
            UnityEngine.Object.Destroy(entry.BlurObject);

        ReleaseTexture(entry.RenderTexture);
    }

    private static void ReleaseTexture(RenderTexture rt)
    {
        if (rt != null)
            RenderTexture.ReleaseTemporary(rt);
    }

    private sealed class BlurEntry
    {
        public UIBlurMode Mode { get; }
        public GameObject BlurObject { get; }
        public RenderTexture RenderTexture { get; }

        public BlurEntry(UIBlurMode mode, GameObject blurObject, RenderTexture renderTexture)
        {
            Mode = mode;
            BlurObject = blurObject;
            RenderTexture = renderTexture;
        }
    }
}
