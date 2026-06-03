using System;
using System.Collections.Generic;
using Framework;
using UnityEngine;

public class SceneRoot : MonoBehaviour
{
    public Camera mainCamera;
    public bool isManualUpdateCamera = false;

    [field: SerializeField]
    public List<GameObject> activeGameObjects { get; private set; } = new List<GameObject>();

    [SerializeField]
    List<Transform> showAfterloading = new List<Transform>();

    Dictionary<int, int> recordObjLayers = new Dictionary<int, int>();
    HashSet<int> _activeObjectIdSet = new HashSet<int>();
    Stack<Transform> _traversalStack = new Stack<Transform>();
    GameObject[] _cachedRootObjects;

    void Awake()
    {
        BuildActiveObjectIdSet();
        CacheRootObjects();

        for (int i = 0; i < showAfterloading.Count; i++)
        {
            if (showAfterloading[i] == null) continue;
            showAfterloading[i].gameObject.SetActive(false);
        }
    }

    public void ShowAfterloaded()
    {
        for (int i = 0; i < showAfterloading.Count; i++)
        {
            if (showAfterloading[i] == null) continue;
            showAfterloading[i].gameObject.SetActive(true);
        }
    }

    public void SetOthersHidden(bool hidden, int hiddenlayer = -1, HashSet<GameObject> ignoreObjects = null)
    {
        using var _ = new AutoProfiler(nameof(SetOthersHidden));

        if (hidden)
        {
            if (hiddenlayer < 0 || hiddenlayer > 31)
            {
                hiddenlayer = LayerMask.NameToLayer("Hidden");
                if (hiddenlayer < 0) return;
            }

            var gameObjLs = GetLayerObjects(ignoreObjects);
            for (int i = 0; i < gameObjLs.Count; i++)
            {
                if (gameObjLs[i] == null) continue;
                gameObjLs[i].layer = hiddenlayer;
            }
        }
        else
        {
            var gameObjects = GetRootObjects();
            for (int i = 0; i < gameObjects.Length; i++)
            {
                if (gameObjects[i] == null) continue;
                RestoreLayersIterative(gameObjects[i]);
            }
        }
    }

    void RestoreLayersIterative(GameObject root)
    {
        using var _ = new AutoProfiler(nameof(RestoreLayersIterative));

        _traversalStack.Clear();
        _traversalStack.Push(root.transform);

        while (_traversalStack.Count > 0)
        {
            var t = _traversalStack.Pop();
            if (t == null) continue;

            var go = t.gameObject;
            if (!_activeObjectIdSet.Contains(go.GetInstanceID()) && recordObjLayers.TryGetValue(go.GetInstanceID(), out var layer))
                go.layer = layer;

            for (int i = 0; i < t.childCount; i++)
                _traversalStack.Push(t.GetChild(i));
        }
    }

    public List<GameObject> GetLayerObjects(HashSet<GameObject> ignoreObjects)
    {
        using var _ = new AutoProfiler(nameof(GetLayerObjects));

        List<GameObject> layerGameObjects = new List<GameObject>();
        recordObjLayers.Clear();
        var gameObjects = GetRootObjects();
        for (int i = 0; i < gameObjects.Length; i++)
        {
            RecordLayersIterative(gameObjects[i], ignoreObjects, layerGameObjects);
        }
        return layerGameObjects;
    }

    void RecordLayersIterative(GameObject root, HashSet<GameObject> ignoreObjects, List<GameObject> result)
    {
        using var _ = new AutoProfiler(nameof(RecordLayersIterative));

        _traversalStack.Clear();
        _traversalStack.Push(root.transform);

        while (_traversalStack.Count > 0)
        {
            var t = _traversalStack.Pop();
            if (t == null) continue;

            var go = t.gameObject;
            if (ignoreObjects != null && ignoreObjects.Contains(go))
                continue;
            if (_activeObjectIdSet.Contains(go.GetInstanceID()))
                continue;

            recordObjLayers[go.GetInstanceID()] = go.layer;
            result.Add(go);

            for (int i = 0; i < t.childCount; i++)
                _traversalStack.Push(t.GetChild(i));
        }
    }

    void BuildActiveObjectIdSet()
    {
        _activeObjectIdSet.Clear();
        foreach (var go in activeGameObjects)
        {
            if (go != null)
                _activeObjectIdSet.Add(go.GetInstanceID());
        }
    }

    void CacheRootObjects()
    {
        _cachedRootObjects = this.gameObject.scene.GetRootGameObjects();
    }

    GameObject[] GetRootObjects()
    {
        if (_cachedRootObjects == null || _cachedRootObjects.Length == 0)
            CacheRootObjects();
        return _cachedRootObjects;
    }

#if UNITY_EDITOR
    public void SetupObjectAfterLoading(GameObject[] rootObjects)
    {
        showAfterloading.Clear();
        foreach (var rootObject in rootObjects)
        {
            if (!rootObject.TryGetComponent(out SceneRoot s))
            {
                var light = rootObject.GetComponentInChildren<Light>();
                if (light != null)
                {
                    showAfterloading.Add(rootObject.transform);
                }
            }
        }
    }

    public void InitializeRealObjects(GameObject[] rootObjects)
    {
        activeGameObjects.Clear();
        foreach (var rootObject in rootObjects)
        {
            if (!rootObject.TryGetComponent(out SceneRoot _))
            {
                var lights = rootObject.GetComponentsInChildren<Light>();
                var camera = rootObject.GetComponentInChildren<Camera>();
                if (camera != null && camera.CompareTag("MainCamera"))
                {
                    mainCamera = camera;
                }
                if ((lights != null && lights.Length > 0) || camera != null)
                {
                    activeGameObjects.Add(rootObject);
                }
                else
                {
                    if (rootObject.name.Contains("CameraControl"))
                    {
                        if (!activeGameObjects.Contains(rootObject))
                            activeGameObjects.Add(rootObject);
                    }
                }
            }
        }

        BuildActiveObjectIdSet();
        CacheRootObjects();
    }
#endif
}