using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

public class HoleMask : MonoBehaviour, ICanvasRaycastFilter
{
    private RectTransform _rectTransform;
    public RectTransform rectTransform => _rectTransform ?? (_rectTransform = transform as RectTransform);

    [SerializeField]
    private Material _material;
    private List<Image> HoleImages = new List<Image>();

    private readonly int _maxHoleCount = 4;
    /// <summary>
    /// 是否拦截点击事件
    /// </summary>
    public bool BlockRaycast = false;

    void OnEnable()
    {
        GetComponent<Image>().material = _material;
    }

    void OnDisable()
    {
        GetComponent<Image>().material = null;
    }

    public void SetHoles(List<Image> holes)
    {
        HoleImages = holes;
    }

    public void RefreshHole()
    {
        if (HoleImages.Count == 0)
        {
            return;
        }
        
        for (int i = 0; i < _maxHoleCount; i++)
        {
            if (i >= HoleImages.Count)
            {
                _material.SetVector($"_HoleRect{i + 1}", new Vector4(0, 0, 0, 0));
                continue;
            }
            var image = HoleImages[i];
            var texture = image.mainTexture;
            Vector3[] imgCorners = new Vector3[4];
            image.rectTransform.GetWorldCorners(imgCorners);
            Rect imgRect = new Rect(imgCorners[0].x, imgCorners[0].y, imgCorners[2].x - imgCorners[0].x,
                imgCorners[2].y - imgCorners[0].y);
            Vector3[] maskCorners = new Vector3[4];

            rectTransform.GetWorldCorners(maskCorners);
            Rect maskRect = new Rect(maskCorners[0].x, maskCorners[0].y, maskCorners[2].x - maskCorners[0].x,
                maskCorners[2].y - maskCorners[0].y);
            var x = (imgRect.xMin - maskRect.xMin) / (maskRect.width);
            var y = (imgRect.yMin - maskRect.yMin) / (maskRect.height);
            var targetX = x + (imgRect.width / maskRect.width);
            var targetY = y + (imgRect.height / maskRect.height);
            _material.SetTexture($"_HoleTex{i + 1}", texture);
            var targetRect = new Vector4(x, y, targetX, targetY);
            _material.SetVector($"_HoleRect{i + 1}", targetRect);
            var sprite = image.sprite;
            var uv = (sprite != null) ? DataUtility.GetInnerUV(sprite) : Vector4.zero;
            _material.SetVector($"_HoleUv{i + 1}", uv);
        }
    }

    public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
    {
        if (!isActiveAndEnabled) return true;
        if (BlockRaycast) return true;
        if (HoleImages.Count == 0)
        {
            return true;
        }

        foreach (Image image in HoleImages)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(image.rectTransform, sp, eventCamera, out var localPoint);
            Vector3[] imgCorners = new Vector3[4];
            image.rectTransform.GetLocalCorners(imgCorners);
            Rect imgRect = new Rect(imgCorners[0].x, imgCorners[0].y, imgCorners[2].x - imgCorners[0].x,
                imgCorners[2].y - imgCorners[0].y);
            var result = !imgRect.Contains(localPoint);
            if (!result)
            {
                return false;
            }
        }

        return true;
    }
    
    #if UNITY_EDITOR
    public bool updatehole = false;
    private void Update()
    {
        if (!updatehole) return;
        updatehole = false;
        RefreshHole();
    }
    #endif
}