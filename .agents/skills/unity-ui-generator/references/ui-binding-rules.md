# UI Binding Rules

## Source Of Truth

For new UI, binding data flows in one direction:

```text
UIViewSchema binding contract
  -> UIBindNode/CSharpUIBindBehaviour serialized data
  -> XxxViewBinder.g.cs and optional XxxView.g.cs
```

Never infer new bindings from AI-generated C# or the legacy regex import command.

Each exported schema binding requires:

- immutable `StableId`;
- explicit ASCII `Key`;
- prefab-root-relative path;
- required component type names;
- sub-binder metadata when applicable.

Node names may change. Keep Key and StableId unchanged unless intentionally breaking the contract.

## Adapter Boundary

`UIWindow` delegates page binding to `UIViewBindingAdapters`; it does not inspect a concrete binder component.

- Implement `IUIViewBindingAdapter` for a new binding scheme.
- A `Resolve` adapter supplies the one primary `UIViewBinder`; use `UIViewBindingAdapterSelector.PreferredAdapterId` or `UIViewSchema.BindingAdapterId` to select it.
- An `Enhance` adapter may add generated partial fields or an explicitly retained compatibility layer, but must not replace a resolved binder.
- Keep `schema-generated` as the default resolver. `legacy-attributes` is an enhancer for existing `[UI]` fields only.
- Register a project/plugin adapter during bootstrap with a stable ASCII `AdapterId`; do not edit `UIWindow` or `ViewBase` to add it.


## Preferred Access

Use generated binder/partial members or cached refs:


```csharp
private UIButtonRef _btnSend;

protected override UniTask OnOpen(ChatParam param)
{
    Cache(ref _btnSend, "btn_Send");
    BindClick(_btnSend.Button, OnSendClicked);
    return UniTask.CompletedTask;
}
```

`BindClick` is open-scoped and removes listeners on cache close. Raw `AddListener` requires explicit removal in `OnClose`.

Declare the handwritten View `partial` when `GeneratePartialViewBindings` is enabled. `UIViewPartialBindingCodeGenerator` owns the `.g.cs` partial file.

## Compatibility

`[UI]` fields remain supported for legacy small Views. Prefer explicit paths when names are ambiguous. Do not add new AI-generated Views using `[UI]` name search.

## Generator Rules

- Generated code writes only when content changes and uses atomic replacement.
- Batch generation refreshes `AssetDatabase` once.
- Do not manually edit `.g.cs` files.
- `CSharpUIBindCodeGenerator` emits strong binders.
- `UIViewPartialBindingCodeGenerator` emits optional partial View fields.
- Treat `AIGeneratedUIViewCodeGenerator` as a compatibility alias only.

## Runtime Find Ban

Forbid `GameObject.Find`, repeated `Transform.Find`, and `GetComponentsInChildren` in runtime View refresh paths. These are allowed in Editor schema compilation and prefab generation.
