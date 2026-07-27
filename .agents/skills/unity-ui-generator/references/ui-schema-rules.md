# UIViewSchema Rules

## Purpose

`UIViewSchema` is the structured contract accepted from AI or automation. It owns View configuration, generated output settings, Addressables group, and binding declarations. It does not replace the visual prefab hierarchy generator.

## Required Fields

- `ViewConfig.ViewTypeName`: resolvable `ViewBase` assembly-qualified name.
- `ConfigTable`: active `UIViewConfigTable` asset.
- `PrefabPath`: existing `Assets/.../XxxView.prefab` path.
- `GeneratedFolder`, `GeneratedNamespace`, `BinderClassName`, `ViewClassName`, and `ViewNamespace`.
- `AddressablesGroup`: normally `Prefabs_UI`.
- Every binding: StableId, Key, RelativePath, required component types, and sub-binder metadata.

## AI Boundary

- Let AI propose schema values and an Editor prefab generator.
- Create/update schema assets through Unity serialized/Editor APIs, never YAML text.
- Do not let AI output directly modify `UIRuntime`, config-table YAML, Addressables YAML, prefab YAML, or generated `.g.cs` files.
- Convert runtime model output to typed ViewState and allowlisted UI commands.

## Compile

Call:

```csharp
UIViewSchemaCompiler.Compile(schema);
```

The compiler:

1. validates the schema and binding targets;
2. configures root components and binder metadata;
3. applies stable binding contracts;
4. saves the prefab;
5. synchronizes `UIViewConfigTable`;
6. registers the prefab in Addressables;
7. emits changed generated files only;
8. records compiler version and schema hash;
9. validates compiled artifacts.

Use `Tools/UI Schema/Validate All` before handoff. Treat every validation error as blocking.

## Change Policy

- Preserve StableId and Key when moving or renaming a node.
- Introduce a new binding for an intentional contract replacement.
- Review schema/config/cache changes as API changes, not visual-only changes.
- Keep handwritten View behavior outside generated files.
