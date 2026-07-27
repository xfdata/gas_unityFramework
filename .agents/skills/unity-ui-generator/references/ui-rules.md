# UI Runtime Rules

## Lifecycle Contract

- `UIRuntime.Open<TView>()` resolves `UIViewConfig`, reuses cached windows, or creates a `UIWindow` and instantiates its Addressable prefab.
- `UIRuntime.Preload<TView>()` is valid only for `UICacheMode.Preload`.
- `UIWindow` owns loading, open/close animation, cache transitions, finite render order, mask/blur modules, and exception-safe disposal.
- `ViewBase` owns the prefab root, config, hooks, safe-area adaptation, and nested View opens.
- `UIModuleBase` has lifetime and open scopes. `BindClick`, `RunTask`, `Delay`, and `Every` use the open scope and stop on every close, including cache close.
- Addressable loads complete independently. Do not reintroduce a global FIFO completion queue.

## Cache And Close

- `DestroyOnClose`: release the instance and unregister the window.
- `HideOnClose`: reuse one instance and rerun `OnOpen`; open-scope listeners/tasks must not survive close.
- `Preload`: explicitly call `UIRuntime.Preload<TView>()`, then open the cached instance later.
- `CloseWhenSceneChange`: set deliberately; cached windows with this flag are disposed on scene changes.
- A close animation or `OnClose` exception must not leave a window in `Closing`.

## Rendering And Layout

- Cross-layer render order uses bounded per-layer ranges. Never write logical values such as `Layer * 1_000_000` to Canvas/Renderer sorting order.
- Root prefabs require `RectTransform`, `Canvas`, and `GraphicRaycaster`.
- `UIRootAdapt` detects runtime resolution/safe-area changes and asks open Views to readapt.
- Do not recalculate safe area from an already adapted root; `ViewBase` preserves its original layout.

## Config Defaults

- Default `CacheMode`: `DestroyOnClose`.
- Popup/login/loading Views generally use `UILayer.Top`; choose by existing product behavior.
- Use config-driven `MaskMode` and `BlurMode`; do not add framework masks inside every prefab.
- Keep `SafeAreaMode.Adapt` unless content intentionally draws into unsafe regions.
- Use `SortOffset` only as a same-layer logical ordering hint.

## Performance And Safety

- Cache binding/component references once.
- Avoid runtime hierarchy searches in refresh/update paths.
- Set decorative Graphic `raycastTarget = false`.
- Coalesce streaming or high-frequency text updates; do not rebuild TMP geometry per token.
- Reuse buffers in raycast/layout hot paths and instantiate mutable Materials per owner.
- Runtime AI/model output must become typed ViewState. It must not reflect View types, invoke arbitrary `Open<T>()`, mutate GameObjects, or choose Addressable GUIDs.
