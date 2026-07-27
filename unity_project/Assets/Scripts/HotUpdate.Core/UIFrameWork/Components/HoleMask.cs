using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

public class HoleMask : MonoBehaviour, ICanvasRaycastFilter
{
    private const int MaxHoleCount = 4;

    private static readonly int[] HoleRectIds = CreatePropertyIds("_HoleRect");
    private static readonly int[] HoleTextureIds = CreatePropertyIds("_HoleTex");
    private static readonly int[] HoleUvIds = CreatePropertyIds("_HoleUv");

    [SerializeField] private Material _material;

    private readonly Vector3[] _imageCorners = new Vector3[4];
    private readonly Vector3[] _maskCorners = new Vector3[4];
    private readonly List<Image> _holeImages = new();
    private RectTransform _rectTransform;
    private Material _runtimeMaterial;
    private Image _maskImage;

    public RectTransform rectTransform => _rectTransform ??= transform as RectTransform;
    public bool BlockRaycast;

    private void OnEnable()
    {
        _maskImage ??= GetComponent<Image>();
        CreateRuntimeMaterial();
    }

    private void OnDisable()
    {
        if (_maskImage != null)
            _maskImage.material = null;

        DestroyRuntimeMaterial();
    }

    private void OnDestroy()
    {
        DestroyRuntimeMaterial();
    }

    public void SetHoles(List<Image> holes)
    {
        _holeImages.Clear();
        if (holes == null)
            return;

        foreach (var hole in holes)
        {
            if (hole != null)
                _holeImages.Add(hole);
        }
    }

    public void RefreshHole()
    {
        if (_runtimeMaterial == null)
            return;

        rectTransform.GetWorldCorners(_maskCorners);
        var maskRect = ToRect(_maskCorners);

        for (var i = 0; i < MaxHoleCount; i++)
        {
            if (i >= _holeImages.Count || maskRect.width <= 0f || maskRect.height <= 0f)
            {
                _runtimeMaterial.SetVector(HoleRectIds[i], Vector4.zero);
                _runtimeMaterial.SetTexture(HoleTextureIds[i], null);
                _runtimeMaterial.SetVector(HoleUvIds[i], Vector4.zero);
                continue;
            }

            var image = _holeImages[i];
            image.rectTransform.GetWorldCorners(_imageCorners);
            var imageRect = ToRect(_imageCorners);
            var x = (imageRect.xMin - maskRect.xMin) / maskRect.width;
            var y = (imageRect.yMin - maskRect.yMin) / maskRect.height;
            var targetX = x + imageRect.width / maskRect.width;
            var targetY = y + imageRect.height / maskRect.height;

            _runtimeMaterial.SetTexture(HoleTextureIds[i], image.mainTexture);
            _runtimeMaterial.SetVector(HoleRectIds[i], new Vector4(x, y, targetX, targetY));
            _runtimeMaterial.SetVector(
                HoleUvIds[i],
                image.sprite != null ? DataUtility.GetInnerUV(image.sprite) : Vector4.zero);
        }
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!isActiveAndEnabled || BlockRaycast || _holeImages.Count == 0)
            return true;

        foreach (var image in _holeImages)
        {
            if (image != null &&
                image.isActiveAndEnabled &&
                RectTransformUtility.RectangleContainsScreenPoint(image.rectTransform, screenPoint, eventCamera))
            {
                return false;
            }
        }

        return true;
    }

    private void CreateRuntimeMaterial()
    {
        DestroyRuntimeMaterial();
        if (_material == null || _maskImage == null)
            return;

        _runtimeMaterial = new Material(_material)
        {
            name = _material.name + " (HoleMask Instance)",
        };
        _maskImage.material = _runtimeMaterial;
    }

    private void DestroyRuntimeMaterial()
    {
        if (_runtimeMaterial == null)
            return;

        if (Application.isPlaying)
            Destroy(_runtimeMaterial);
        else
            DestroyImmediate(_runtimeMaterial);

        _runtimeMaterial = null;
    }

    private static Rect ToRect(Vector3[] corners)
    {
        return new Rect(
            corners[0].x,
            corners[0].y,
            corners[2].x - corners[0].x,
            corners[2].y - corners[0].y);
    }

    private static int[] CreatePropertyIds(string prefix)
    {
        var ids = new int[MaxHoleCount];
        for (var i = 0; i < ids.Length; i++)
            ids[i] = Shader.PropertyToID(prefix + (i + 1));
        return ids;
    }

#if UNITY_EDITOR
    public bool updatehole;

    private void Update()
    {
        if (!updatehole)
            return;

        updatehole = false;
        RefreshHole();
    }
#endif
}
