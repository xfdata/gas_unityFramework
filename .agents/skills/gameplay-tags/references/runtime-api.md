# GameplayTag Runtime API

## GameplayTag

- Identity: `Domain` + hierarchical `Value` + `Mask`
- `IsValid`: Domain ≠ None and Mask ≠ 0
- `IsLegacyMissingDomain`: Domain None but Mask ≠ 0 (editor fixup)
- `Matches(parent)`: same Domain and current is in parent subtree  
  (`(Value & parent.Mask) == parent.Value`)
- Equality: Domain + Value + Mask
- `ToString()` / `GameplayTagDebug.GetPath(tag)`: catalog path e.g. `Combat/State.Dead`

Do not construct tags outside generated libraries. Catalog: `GameplayTagCatalog.All` (generated).

## GameplayTagContainer

| API | Behavior |
|---|---|
| `AddTag` | exact stack +1; first time updates hierarchy match counts |
| `RemoveTag` | exact stack -1 |
| `RemoveTagCompletely` | exact stack → 0 |
| `RemoveTag(tag, includeChildren, removeAllStacks)` | optional subtree |
| `RemoveMatching(tag)` | subtree + clear all stacks |
| `HasTag` | hierarchy presence (matched counts) |
| `GetTagCount` | exact stacks only |
| `Clear` | remove all tags (listeners kept) |
| `RegisterListener` / `UnregisterListener` | exact hierarchy node 0↔1 |

Keys are Domain+Value so cross-domain same numeric value does not collide.

## TagQuery / TagQueryOp

| Op | Empty nodes | Non-empty |
|---|---|---|
| `All` | true | every tag present |
| `Any` | true (current) | any present |
| `None` | true | no listed tag present |

`None` is the correct op for BlockedTags (serialized value 2; formerly misnamed NotAll).

## Domains

```csharp
public enum GameplayTagDomain : byte
{
    None = 0,
    Global = 1,
    Combat = 2,
}
```

Matching never crosses Domain. One Database per Domain (enforced in editor).
