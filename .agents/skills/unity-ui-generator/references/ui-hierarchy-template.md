# UI Hierarchy Templates

Always print the hierarchy and schema binding contract before generating files.

## Full-Screen View

```text
XxxView [RectTransform stretch, Canvas, GraphicRaycaster, UIViewBindingAdapterSelector, CSharpUIBindBehaviour]
  img_Background [Image, raycastTarget=false]
  ContentRoot [RectTransform stretch]
    Header [RectTransform]
      txt_Title [TextMeshProUGUI, UIBindNode]
      btn_Close [Button, Image, UIBindNode]
    Body [RectTransform]
    Footer [RectTransform, optional]
```

Use framework root safe-area adaptation. Add a visual `SafeArea` grouping node only when the layout benefits from that structure.

## Popup

Reuse `Assets/Prefabs/UI/Common/Popup_Node.prefab` when possible:

```text
XxxView [RectTransform stretch, Canvas, GraphicRaycaster, UIViewBindingAdapterSelector, CSharpUIBindBehaviour]
  Popup_Node [common prefab or nested binder]
    Image_Bg [Image]
    Text_Title [TextMeshProUGUI]
    btn_Close [Button, Image, UIBindNode]
    Content [RectTransform]
```

Use `UIViewConfig.MaskMode`; do not add a duplicate framework backdrop.

## Scroll/Streaming List

```text
scroll_Items [ScrollRect, UIBindNode]
  Viewport [RectTransform, RectMask2D]
    Content [RectTransform, layout component, UIBindNode]
```

Use pooled/virtualized items for AI chat, feeds, logs, and other unbounded content. Cap retained history and coalesce streaming updates.

## Layout Rules

- Root anchors `(0,0)` to `(1,1)`, size delta `(0,0)`, pivot `(0.5,0.5)`.
- Root name equals the View class name.
- Use UI layer `5` unless a reused prefab requires otherwise.
- Bind only nodes needed by code.
- Keep StableId and Key independent from visible/localized node labels.
- Decorative Image/TMP nodes do not receive raycasts.
