# UI Rules

## Framework Contract

- `UIRuntime.Open<TView>()` looks up `UIViewConfigTable`, creates a `UIWindow`, instantiates `UIViewConfig.PrefabReference` through `AddressablesUIAssetService`, then creates the `ViewBase`.
- `UIWindow` expects the instantiated prefab root to contain optional `CSharpUIBindBehaviour`. If present, it creates a `UIViewBinder`; otherwise it falls back to `[UI]` field auto binding.
- `ViewBase` owns `GameObject`, `Transform`, `RectTransform`, `Window`, `Config`, open/close/refresh hooks, `Close()`, and `Open<TView>()`.
- `UIModuleBase` owns child modules, cancellation, cleanup, binder accessors, `Cache(...)`, and `BindClick(...)`.
- Masks and blur are config-driven via `UIMaskWindowModule`, `UIBlurWindowModule`, `UIMaskService`, and `UIBlurService`; do not build separate mask/blur systems.

## Prefab And Config Paths

- Existing View prefabs live under `unity_project/Assets/Prefabs/UI/Popup/`.
- Existing common UI prefabs live under `unity_project/Assets/Prefabs/UI/Common/`, including:
  - `Popup_Node.prefab`
  - `node_ProgressBar.prefab`
  - `stuckSceneImage.prefab`
  - `UIConfigs.asset`
- `unity_project/Assets/ArtPrefabs/UI/System` exists but currently does not contain the active View prefab examples. Follow the active config and nearby existing UI path unless the user asks to migrate.
- UI config table: `unity_project/Assets/Prefabs/UI/Common/UIConfigs.asset`.
- Addressables group discovered for UI prefabs: `Prefabs_UI`. Ensure new View prefabs are addressable before relying on `AssetReferenceGameObject`.

## View Config Defaults

Use `UIViewConfig` fields instead of custom runtime flags:

- `Layer`: use `UILayer.Top` for popup/login/loading-style screens; choose a lower/higher layer only when existing usage demands it.
- `CacheMode`: default `DestroyOnClose`; use `HideOnClose` or `Preload` only when reopening cost or state retention matters.
- `FullScreen`: true for full-screen screens that cover gameplay; false for compact popups.
- `EnterPopupStack`: true for screens that should respond to Esc/top-popup behavior.
- `PauseLowerView` / `HideLowerView`: true for full-screen blocking UI; choose deliberately for overlays.
- `MaskMode`: use `DarkMask`, `DarkMaskClose`, or `BlockInputOnly` from config. Do not add a custom mask object to each prefab unless it is visual content, not framework mask behavior.
- `SafeAreaMode`: keep `Adapt` unless the screen intentionally ignores safe area.
- `SortOffset`: use only to order same-layer windows.

## Component Rules

- Use `TextMeshProUGUI` for UI text; avoid legacy `Text` for new UI.
- Use `TMP_InputField` for inputs; set placeholder as a `TextMeshProUGUI`.
- Use `Button` with an `Image` target graphic for clickable controls.
- Use `Image` for backgrounds, icons, and fill bars. Set `raycastTarget = false` for decorative/non-interactive images.
- Use `ScrollRect` with `Viewport` + `Mask`/`RectMask2D` + `Content`; bind the `ScrollRect` node or content node as needed.
- Use root `Canvas` + `GraphicRaycaster` on View prefabs so `UIWindow.ApplyRenderOrder()` can set sorting.

## Editor Generation Rules

- Generate prefab assets with a Unity Editor script; never edit prefab YAML by hand.
- Save with `PrefabUtility.SaveAsPrefabAsset(root, prefabPath)`.
- Clean up temporary scene objects with `UnityEngine.Object.DestroyImmediate(root)` after saving.
- After adding `UIBindNode`, call `CSharpUIBindBehaviour.RefreshBindingsInEditor(true)`.
- If strong binder output is desired, call the existing generator APIs or save the prefab with auto generation enabled.
- Set `EditorUtility.SetDirty` and `AssetDatabase.SaveAssets()` after config/addressables changes.

## Performance And Safety

- Runtime UI code must not use `GameObject.Find`.
- Avoid repeated `Transform.Find`, `GetComponent`, or `GetComponentsInChildren` in refresh/update loops.
- Cache binder refs/components once in `OnOpen`, `Setup`, or generated binding.
- Remove listeners in `OnClose`/`OnStop` or use `BindClick` to register cleanup.
- Do not create per-frame strings in UI refresh loops; update only when state changes.
