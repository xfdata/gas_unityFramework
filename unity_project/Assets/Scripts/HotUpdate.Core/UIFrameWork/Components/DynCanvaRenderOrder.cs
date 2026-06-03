using UnityEngine;

public class DynCanvaRenderOrder : MonoBehaviour
{
    private Canvas _canvas;
    private Renderer _renderer;

    private void Awake()
    {
        _canvas = GetComponent<Canvas>();
        _renderer = GetComponent<Renderer>();
    }

    public void SetRenderOrder(int sortingLayerID, int order)
    {
        if (_canvas != null)
        {
            _canvas.overrideSorting = true;
            _canvas.sortingLayerID = sortingLayerID;
            _canvas.sortingOrder = order;
        }
        else if (_renderer != null)
        {
            _renderer.sortingLayerID = sortingLayerID;
            _renderer.sortingOrder = order;
        }
    }
}