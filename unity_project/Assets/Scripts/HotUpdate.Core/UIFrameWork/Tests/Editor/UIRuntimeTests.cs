#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class UIRuntimeTests
{
    private readonly List<GameObject> _ownedObjects = new();
    private UIRuntime _runtime;
    private FakeUIAssetService _assetService;
    private UIViewConfigTable _configTable;

    [TearDown]
    public void TearDown()
    {
        _runtime?.Dispose();
        _runtime = null;

        if (_configTable != null)
            Object.DestroyImmediate(_configTable);
        _configTable = null;

        foreach (var gameObject in _ownedObjects)
        {
            if (gameObject != null)
                Object.DestroyImmediate(gameObject);
        }
        _ownedObjects.Clear();
    }

    [UnityTest]
    public IEnumerator CachedView_ReusesInstance_AndOpenScopeRemovesListeners()
    {
        return UniTask.ToCoroutine(async () =>
        {
            CachedTestView.ClickCount = 0;
            CreateRuntime<CachedTestView>(UICacheMode.HideOnClose);

            var first = await _runtime.Open<CachedTestView>();
            await first.Window.CloseAsync();
            var second = await _runtime.Open<CachedTestView>();

            Assert.That(second, Is.SameAs(first));
            Assert.That(_assetService.InstantiateCount, Is.EqualTo(1));

            second.GameObject.GetComponentInChildren<Button>(true).onClick.Invoke();
            Assert.That(CachedTestView.ClickCount, Is.EqualTo(1));
        });
    }

    [UnityTest]
    public IEnumerator CloseFailure_StillDisposesAndUnregistersWindow()
    {
        return UniTask.ToCoroutine(async () =>
        {
            CreateRuntime<ThrowingCloseTestView>(UICacheMode.DestroyOnClose);

            var view = await _runtime.Open<ThrowingCloseTestView>();
            InvalidOperationException closeError = null;
            try
            {
                await view.Window.CloseAsync();
            }
            catch (InvalidOperationException error)
            {
                closeError = error;
            }

            Assert.That(closeError, Is.Not.Null);
            Assert.That(_runtime.AllWindows, Is.Empty);
            Assert.That(_assetService.ReleaseCount, Is.EqualTo(1));
        });
    }

    private void CreateRuntime<TView>(UICacheMode cacheMode) where TView : ViewBase
    {
        var rootObject = Own(new GameObject("UIRoot", typeof(RectTransform)));
        var hiddenObject = Own(new GameObject("Hidden", typeof(RectTransform)));
        hiddenObject.transform.SetParent(rootObject.transform, false);
        var normalLayer = Own(new GameObject("Normal", typeof(RectTransform)));
        normalLayer.transform.SetParent(rootObject.transform, false);

        var prefab = Own(new GameObject(typeof(TView).Name, typeof(RectTransform), typeof(Canvas)));
        var buttonObject = Own(new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button)));
        buttonObject.transform.SetParent(prefab.transform, false);

        var inputBlock = Own(new GameObject("InputBlock", typeof(RectTransform)));
        var mask = Own(new GameObject("Mask", typeof(RectTransform), typeof(Image), typeof(Button)));

        var config = new UIViewConfig
        {
            ViewTypeName = typeof(TView).AssemblyQualifiedName,
            PrefabReference = new AssetReferenceGameObject("test-prefab"),
            Layer = UILayer.Normal,
            CacheMode = cacheMode,
            EnterPopupStack = true,
            CloseWhenSceneChange = false,
        };
        _configTable = ScriptableObject.CreateInstance<UIViewConfigTable>();
        _configTable.Views.Add(config);
        UIViewRegistry.Initialize(_configTable);

        _assetService = new FakeUIAssetService(prefab);
        var root = new UIRoot(rootObject, null, hiddenObject.transform);
        root.RegisterLayer(UILayer.Normal, normalLayer.transform);
        _runtime = new UIRuntime(
            root,
            _assetService,
            new UIMaskService(mask),
            new UIBlurService(),
            new UIInputBlockService(inputBlock));
    }

    private GameObject Own(GameObject gameObject)
    {
        _ownedObjects.Add(gameObject);
        return gameObject;
    }

    private sealed class FakeUIAssetService : IUIAssetService
    {
        private readonly GameObject _prefab;

        public int InstantiateCount { get; private set; }
        public int ReleaseCount { get; private set; }

        public FakeUIAssetService(GameObject prefab)
        {
            _prefab = prefab;
        }

        public UniTask<GameObject> InstantiateAsync(
            AssetReferenceGameObject reference,
            Transform parent,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            InstantiateCount++;
            return UniTask.FromResult(Object.Instantiate(_prefab, parent, false));
        }

        public void Release(GameObject instance)
        {
            ReleaseCount++;
            if (instance != null)
                Object.DestroyImmediate(instance);
        }
    }
}

public sealed class CachedTestView : ViewBase
{
    public static int ClickCount;

    protected override UniTask OnOpen(object param)
    {
        BindClick(GameObject.GetComponentInChildren<Button>(true), () => ClickCount++);
        return UniTask.CompletedTask;
    }
}

public sealed class ThrowingCloseTestView : ViewBase
{
    protected override UniTask OnClose(object result)
    {
        return UniTask.FromException(new InvalidOperationException("Expected close failure."));
    }
}
#endif
