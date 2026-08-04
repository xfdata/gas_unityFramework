---
name: unity-ui-generator
description: Use this skill when creating, modifying, generating, validating, or reviewing Unity UGUI screens, UI Views, UI prefabs, UIViewSchema assets, binding code, or Editor generators in this project. Trigger for new UI screens/popups, layout changes, Button/TMP/Image/Input/Scroll bindings, ViewBase wiring, UI cache/mask/safe-area behavior, AI-assisted UI generation, or UI framework changes. Do not use for unrelated battle logic, backend services, non-UI gameplay, or asset imports that do not affect UGUI.
---

# Unity UI Generator

Use the project `UIRuntime` framework and the schema-first Editor pipeline.

## Required First Response

Before editing UI files, output:

- View class, script path, prefab path, `UIViewSchema` path, config table, and Addressables group.
- `UILayer`, cache, mask, blur, full-screen, popup-stack, scene-close, and safe-area settings.
- Root-to-leaf hierarchy with component types on every exported node.
- Binding Key, StableId strategy, relative path, required component types, and access style.
- Reused common prefabs, especially `Assets/Prefabs/UI/Common/Popup_Node.prefab`.

Do not start by writing only View logic.

## Read References

- Read `references/ui-rules.md` for runtime lifecycle, cache, sorting, config, and performance rules.
- Read `references/ui-hierarchy-template.md` before creating a prefab or prefab Editor generator.
- Read `references/ui-binding-rules.md` before changing bind nodes, binders, partial fields, or events.
- Read `references/ui-schema-rules.md` before AI-assisted generation or creating/updating a `UIViewSchema`.

## Project Shape

- Runtime: `unity_project/Assets/Scripts/HotUpdate.Core/UIFrameWork/`
- Schema/compiler: `UIFrameWork/Config/UIViewSchema.cs` and `Config/Editor/UIViewSchemaCompiler.cs`
- Binding generators: `UIFrameWork/Bind/`
- UI config: `unity_project/Assets/Prefabs/UI/Common/UIConfigs.asset`
- View prefabs: `unity_project/Assets/Prefabs/UI/Popup/`
- Common prefabs: `unity_project/Assets/Prefabs/UI/Common/`
- Addressables group: `Prefabs_UI` unless the nearby feature establishes another group.

## Hard Rules

- Reuse `UIRuntime`, `UIWindow`, `ViewBase`, `UIModuleBase`, `UIViewConfigTable`, masks, blur services, and `UIViewBindingAdapters`.
- For new or AI-assisted UI, use `UIViewSchema` as the binding/config contract.
- Use an Editor script and `PrefabUtility.SaveAsPrefabAsset` to create visual hierarchy.
- Never edit prefab, config-table, Addressables, or schema YAML by hand.
- Never let AI-generated C# become the source from which bindings are inferred. Do not use the legacy regex import path for new UI.
- Never create a parallel window manager, root Canvas manager, mask system, or asset loader.
- Never construct an entire View hierarchy at runtime.
- Never make `UIWindow` or `ViewBase` depend directly on a new binding implementation. Add an `IUIViewBindingAdapter`; use a resolver for the primary source and an enhancer only for additive behavior.
- Keep generated `.g.cs` files generator-owned; keep business behavior in handwritten View files.
- Do not use runtime `GameObject.Find`, repeated `Transform.Find`, or uncached `GetComponentsInChildren`.

## Creation Workflow

1. Inspect nearby View, prefab, schema, and common-prefab patterns.
2. Print the required hierarchy and binding plan.
3. Create or update the handwritten `XxxView.cs`. Declare it `partial` when partial bindings are enabled.
4. Create/update an Editor prefab generator. Add root `Canvas`, `GraphicRaycaster`, `UIViewBindingAdapterSelector`, and `CSharpUIBindBehaviour`; add `UIBindNode` only to exported nodes; save with `PrefabUtility.SaveAsPrefabAsset`.
5. Create/update `XxxViewSchema.asset` through Unity Editor APIs. Populate explicit binding Keys, StableIds, relative paths, required component types, and the primary `BindingAdapterId`.
6. Call `UIViewSchemaCompiler.Compile(schema)`. It synchronizes binder settings, config, Addressables, stable IDs, and generated code.
7. Run `UIViewFrameworkValidator.Validate(schema)` or `Tools/UI Schema/Validate All`.
8. Compile both runtime and Editor assemblies. For lifecycle changes, run focused UI EditMode tests.

## Runtime Rules

- Register listeners and open-scoped tasks in `OnOpen`; `BindClick`, `RunTask`, `Delay`, and `Every` are cleaned/canceled at close, including cached Views.
- Use `OnClose` for business subscriptions and state teardown. Use `AddCleanup` only for lifetime resources created in `OnStart`.
- Use `HideOnClose` only for Views intended to retain instances; use `Preload` with explicit `UIRuntime.Preload<TView>()`.
- Treat `CloseWhenSceneChange` as deliberate configuration.
- Use `BindClick` instead of wrapper `OnClick` helpers or raw listeners where possible.
- Update streaming/dynamic text at a bounded cadence and cancel work through the open scope when a View closes.

## Naming

- View/script/prefab: `XxxView`, `XxxView.cs`, `XxxView.prefab`.
- Schema: `XxxViewSchema.asset`.
- Binder: `XxxViewBinder.g.cs`.
- Use one local binding-key style consistently. Keys must be explicit, stable, ASCII identifiers; node display names may change without changing Keys or StableIds.
