using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class UIRoot
{
    private readonly Dictionary<UILayer, Transform> _layerRoots = new();

    public GameObject RootObject { get; }
    public Camera UICamera { get; }
    public Transform HiddenRoot { get; }
    public float SideOffset { get; private set; }

    public UIRoot(GameObject rootObject, Camera uiCamera, Transform hiddenRoot)
    {
        RootObject = rootObject ?? throw new ArgumentNullException(nameof(rootObject));
        UICamera = uiCamera;
        HiddenRoot = hiddenRoot ?? throw new ArgumentNullException(nameof(hiddenRoot));
    }

    public void SetSideOffset(float sideOffset)
    {
        SideOffset = sideOffset;
    }

    public void RegisterLayer(UILayer layer, Transform root)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));

        _layerRoots[layer] = root;
    }

    public Transform GetLayerRoot(UILayer layer)
    {
        if (_layerRoots.TryGetValue(layer, out var root))
            return root;

        throw new InvalidOperationException(
            $"[UIRoot] Layer root not registered: {layer}. Add the matching Canvas child or register the layer before opening a view.");
    }
}
