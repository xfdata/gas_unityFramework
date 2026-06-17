# UI Hierarchy Template

Always output the proposed hierarchy before generating files.

## Full-Screen View Template

```text
XxxView [RectTransform stretch, Canvas, GraphicRaycaster, CSharpUIBindBehaviour]
  img_Background [Image, optional UIBindNode]
  SafeArea or ContentRoot [RectTransform stretch, optional UIBindNode]
    Header [RectTransform]
      txt_Title [TextMeshProUGUI, UIBindNode]
      btn_Close [Button, Image, UIBindNode] (only when closeable)
    Body [RectTransform]
      ... feature content ...
    Footer [RectTransform] (optional)
```

Use `SafeArea` only as a content grouping node when layout needs an explicit safe-area subtree. The framework already adapts the root through `ViewBase.AdaptRootTransform()` based on `UIViewConfig.SafeAreaMode`.

## Popup Template

Prefer reusing `Assets/Prefabs/UI/Common/Popup_Node.prefab` for standard framed popups.

```text
XxxView [RectTransform stretch, Canvas, GraphicRaycaster, CSharpUIBindBehaviour]
  Popup_Node [common prefab instance or equivalent, optional nested CSharpUIBindBehaviour]
    Image_Bg [Image]
    Text_Title [TextMeshProUGUI]
    btn_Close [Button, Image]
    Content [RectTransform]
      ... popup-specific controls ...
```

Use `UIViewConfig.MaskMode` for backdrop/input blocking. Do not create a separate full-screen black mask inside the View unless it is unique visual content.

## Scroll List Template

```text
scroll_Items [ScrollRect, Image optional, UIBindNode]
  Viewport [RectTransform, Image, Mask or RectMask2D]
    Content [RectTransform, VerticalLayoutGroup/GridLayoutGroup, ContentSizeFitter optional, UIBindNode]
```

Bind `scroll_Items` when code controls scroll position. Bind `Content` when code instantiates item prefabs.

## Chat Input UI Example Hierarchy

```text
ChatInputView [RectTransform stretch, Canvas, GraphicRaycaster, CSharpUIBindBehaviour]
  img_Backdrop [Image]
  SafeArea [RectTransform stretch]
    Panel_InputBar [RectTransform, Image]
      btn_Emoji [Button, Image, UIBindNode]
      input_Message [TMP_InputField, Image, UIBindNode]
        Text Area
          Placeholder [TextMeshProUGUI]
          Text [TextMeshProUGUI]
      btn_Send [Button, Image, UIBindNode]
```

Suggested config: `Layer = UILayer.Top` or `UILayer.Overlay` depending on product behavior, `FullScreen = false`, `MaskMode = None`, `SafeAreaMode = Adapt`.

## Layout Rules

- Root `RectTransform`: anchors min `(0,0)`, max `(1,1)`, size delta `(0,0)`, pivot `(0.5,0.5)`.
- Keep root name equal to View class name.
- Set all UI objects to layer `UI` (`5`) unless the existing prefab being reused dictates otherwise.
- Use stable anchors and sizes; do not rely on runtime scripts to repair obvious prefab layout.
- Decorative text/images should not receive raycasts.
- Buttons and inputs should receive raycasts through their target graphic.
