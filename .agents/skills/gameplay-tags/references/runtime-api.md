# GameplayTag Runtime API

## GameplayTag

- Identity: `Domain` + hierarchical `Value` + `Mask`
- `IsValid`: Domain ≠ None and Mask ≠ 0
- `IsLegacyMissingDomain`: Domain None but Mask ≠ 0 (editor fixup)
- `Matches(parent)`: same Domain and current is in parent subtree  
  (`(Value & parent.Mask) == parent.Value`)
- Equality: Domain + Value + Mask
- `ToString()` / `GameplayTagDebug.GetPath(tag)`: catalog path e.g. `Combat/State.Dead`

Do not construct tags outside generated libraries (the `GameplayTag(domain, value, mask)` ctor is `internal` — compiling such code in business assemblies fails by design). Catalog: `GameplayTagCatalog.All` (generated).

## GameplayTagContainer

> **CRITICAL:** `Match(container, op)` returns `true` when **this** container is empty.
> This is the GAS convention: "no restriction = match all". An empty container
> never blocks anything. Populate tags before using it to filter.

| API | Behavior |
|---|---|---|
| `AddTag` | exact stack +1; first time updates hierarchy match counts |
| `RemoveTag` | exact stack -1 |
| `RemoveTagCompletely` | exact stack → 0 |
| `RemoveTag(tag, includeChildren, removeAllStacks)` | optional subtree |
| `RemoveMatching(tag)` | subtree + clear all stacks |
| `HasTag` | hierarchy presence (matched counts) |
| `HasAllTags(params)` | every listed tag present; empty → true |
| `HasAnyTag(params)` | any listed tag present; empty → false |
| `GetMatchingTags(parent)` | all exact tags under parent |
| `GetTagCount` | exact stacks only |
| `Clear` | remove all tags (listeners kept) |
| `RegisterListener` / `UnregisterListener` | exact hierarchy node 0↔1 |
| `Matches(TagQuery)` | evaluates query against this container |

Keys are Domain+Value so cross-domain same numeric value does not collide.

## TagQuery / TagQueryOp

> **CRITICAL:** An empty node list always returns `true`.
> A TagQuery with no tags is equivalent to "no condition". Ensure your
> TagQuery has nodes when you intend filtering behavior.

| Op | Empty nodes | Non-empty |
|---|---|---|
| `All` | true | every tag present |
| `Any` | true | any present |
| `None` | true | no listed tag present |

`None` is the correct op for BlockedTags (serialized value 2; formerly misnamed NotAll).

Factory methods:

| Factory | Op |
|---|---|
| `TagQuery.AllOf(tagA, tagB, ...)` | All |
| `TagQuery.AnyOf(tagA, tagB, ...)` | Any |
| `TagQuery.NoneOf(tagA, tagB, ...)` | None |

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
