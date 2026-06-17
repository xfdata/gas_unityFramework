---
name: unity-ui-generator
description: Use this skill when creating, modifying, or generating Unity UGUI screens, UI Views, UI prefabs, UI binding code, or Unity Editor scripts that build UI prefabs in this project. Trigger for requests such as new UI screen, new popup, modify UI layout, generate UI prefab, add Button/TMP_Text/Image/InputField/ScrollRect bindings, or wire a ViewBase to a prefab. Do not use for battle logic, GAS/combat systems, backend/data services, non-UI gameplay logic, asset import unrelated to UGUI, or pure code tasks that do not touch UI.
---

# Unity UI Generator

Follow this project UI workflow exactly when building or changing UGUI.

## Required First Response

Before editing files, output the intended UI hierarchy and binding plan:

- View class name, prefab path, script path, config entry, target `UILayer`, cache/mask/safe-area settings.
- Root-to-leaf hierarchy with component types on every exported node.
- Binding keys and their runtime access style.
- Reused project components/prefabs, especially `Assets/Prefabs/UI/Common/Popup_Node.prefab` and other existing common nodes.

Do not skip this step. Do not start by writing only View logic.

## Project Shape

Read these references as needed:

- `references/ui-rules.md` for framework, prefab, config, component, and performance rules.
- `references/ui-hierarchy-template.md` before generating a new prefab or Editor script.
- `references/ui-binding-rules.md` before adding fields, `UIBindNode`, `CSharpUIBindBehaviour`, generated binders, or runtime event wiring.

Important local files:

- Runtime framework: `unity_project/Assets/Scripts/HotUpdate.Core/UIFrameWork/`
- Existing examples: `LoginView.cs`, `LoadingView.cs`, their `*Binder.g.cs`, and prefabs under `unity_project/Assets/Prefabs/UI/Popup/`
- UI config table: `unity_project/Assets/Prefabs/UI/Common/UIConfigs.asset`
- Common prefabs: `unity_project/Assets/Prefabs/UI/Common/`

## Hard Rules

- Reuse the existing `UIRuntime` / `UIWindow` / `ViewBase` / `UIModuleBase` / `UIViewConfigTable` framework.
- Never create a parallel UI framework, window manager, root canvas manager, or asset loader.
- Never bypass prefab creation by writing runtime code that constructs the whole UI on every open.
- Prefer generating a Unity Editor script that creates/updates the prefab; do not hand-author prefab YAML.
- The Editor script must save prefabs with `PrefabUtility.SaveAsPrefabAsset`.
- Generated View prefabs must be real assets referenced by `UIViewConfig.PrefabReference`.
- Do not use runtime `GameObject.Find`, repeated `Transform.Find`, or uncached `GetComponentsInChildren` to drive UI.
- Do not allocate avoidable garbage in hot UI paths; cache bind refs/components once during open/setup.

## Creation Workflow

1. Analyze nearby UI examples and confirm the target folder.
2. Print the UI hierarchy and binding plan.
3. Create/update the View script as `XxxView.cs` inheriting `ViewBase`, `ViewBase<TParam>`, or `ViewBase<TParam, TBinder>`.
4. Create a Unity Editor generator script for the prefab. The script should:
   - Build the UGUI hierarchy with Unity APIs.
   - Add `Canvas`, `GraphicRaycaster`, and root `CSharpUIBindBehaviour`.
   - Add `UIBindNode` to exported controls.
   - Set root binder fields: `GeneratedNamespace = "Game.UI.Generated"`, `GeneratedClassName = "XxxViewBinder"`, `GeneratedFolder` to the View script folder, and `GeneratedViewClassName = "XxxView"`.
   - Call `RefreshBindingsInEditor(true)` and, when appropriate, generator APIs for strong binder/partial View bindings.
   - Save via `PrefabUtility.SaveAsPrefabAsset`.
5. Register the prefab in the UI config flow:
   - Add/update `UIViewConfig` in `Assets/Prefabs/UI/Common/UIConfigs.asset`.
   - Set `ViewTypeName` to the View type assembly-qualified name.
   - Set `PrefabReference` to the saved prefab.
   - Ensure the prefab is addressable in the project UI addressables group (`Prefabs_UI` or the current project-equivalent group).
6. Verify by compiling or using Unity validation when available. At minimum, inspect generated scripts and config references.

## Naming Rules

- View class/script/prefab: `XxxView`, `XxxView.cs`, `XxxView.prefab`.
- Generated binder: `XxxViewBinder.g.cs` in the same View folder unless the nearby feature uses another folder.
- Root prefab object: exactly `XxxView`.
- Prefer existing node prefixes within the local feature:
  - HotUpdate examples commonly use `btn_Login`, `txt_BundleVersion`, `img_Fill`, `input_Account`, `Image_Logo`.
  - Binder-heavy examples may use `BtnDaily`, `TxtScore`, `ImgReward`, `DailyScorePage`.
- Do not mix naming styles inside one new View unless integrating into an existing prefab that already does.

## Runtime Coding Rules

- Bind UI once in `OnOpen`, setup methods, or generated `BindGeneratedUI`.
- Prefer `Cache(ref field, "Key")`, `Btn("Key")`, `Txt("Key")`, `Img("Key")`, `Scroll("Key")`, generated binder properties, or generated partial fields.
- Use `BindClick` when possible so listeners are cleaned by `UIModuleBase`; otherwise remove listeners in `OnClose`/`OnStop`.
- Use `TextMeshProUGUI` for labels and `TMP_InputField` for input fields.
- Set non-interactive `Image`/text `raycastTarget = false`; keep raycast only on interactive masks/buttons/inputs.
- Use `Close()` from `ViewBase` and `Open<TView>()` / `Context.Runtime.Open<TView>()`; do not instantiate UI prefabs manually at runtime except item prefabs owned by a module and bound with `CSharpUIBindBehaviour`.
