#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class UIViewBindingAdaptersTests
{
    [Test]
    public void PreferredResolver_IsInvokedWithoutChangingWindowRuntime()
    {
        var root = new GameObject("BindingRoot");
        var selector = root.AddComponent<UIViewBindingAdapterSelector>();
        selector.PreferredAdapterId = "test-binding";
        var adapter = new RecordingBindingAdapter();

        UIViewBindingAdapters.Register(adapter);
        try
        {
            UIViewBindingAdapters.Bind(new BindingTestView(), root);
            Assert.That(adapter.WasInvoked, Is.True);
        }
        finally
        {
            UIViewBindingAdapters.Unregister(adapter);
            Object.DestroyImmediate(root);
        }
    }

    private sealed class BindingTestView : ViewBase
    {
    }

    private sealed class RecordingBindingAdapter : IUIViewBindingAdapter
    {
        public string AdapterId => "test-binding";
        public int Priority => 1000;
        public UIViewBindingAdapterPhase Phase => UIViewBindingAdapterPhase.Resolve;
        public bool WasInvoked { get; private set; }

        public UIViewBindingAdapterResult Bind(UIViewBindingContext context)
        {
            WasInvoked = true;
            return UIViewBindingAdapterResult.Stop;
        }
    }
}
#endif
