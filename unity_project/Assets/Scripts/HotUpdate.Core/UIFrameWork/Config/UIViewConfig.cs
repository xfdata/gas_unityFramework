using System;
using UnityEngine.AddressableAssets;

[Serializable]
public sealed class UIViewConfig
{
    public string ViewTypeName;
    public AssetReferenceGameObject PrefabReference;

    public UILayer Layer = UILayer.Normal;
    public UICacheMode CacheMode = UICacheMode.DestroyOnClose;

    public bool FullScreen;
    public bool EnterPopupStack = true;
    public bool PauseLowerView = true;
    public bool HideLowerView = true;

    public UIBlurMode BlurMode = UIBlurMode.None;
    public UIMaskMode MaskMode = UIMaskMode.None;

    public bool CloseByEsc = true;
    public bool CloseByMask;

    public UISafeAreaMode SafeAreaMode = UISafeAreaMode.Adapt;

    public int SortOffset;

    public bool IgnoreSafeArea => SafeAreaMode == UISafeAreaMode.Ignore;
}