using System;
using UnityEngine;
using UnityEngine.UI;

public class ImageScrollBar : MonoBehaviour
{
    [SerializeField]
    public Image img_Fill;
    [SerializeField]
    public Transform nodeFollow;
    
    [SerializeField] 
    public Transform curveNode;

    private float startX;
    private float imgWidth;
    public float value => img_Fill.fillAmount;

    private void Awake()
    {
        imgWidth = img_Fill.rectTransform.rect.width;
        startX = img_Fill.transform.localPosition.x - imgWidth / 2;
    }

    public void SetProgress(float progress)
    {
        img_Fill.fillAmount = progress;
        var curX = startX + imgWidth * progress;
        if (nodeFollow == null) return;
        nodeFollow.localPosition = new Vector3(curX, nodeFollow.localPosition.y, nodeFollow.localPosition.z);
        curveNode?.gameObject.SetActive(progress is > 0.01f and < 0.99f);
    }
}