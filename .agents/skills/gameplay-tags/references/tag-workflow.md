# GameplayTag Workflow

## Source of Truth

```
GameplayTagDatabase (.asset)     ← ONLY writable definition source
        │
        ▼  Generate Code / BuildGameplayTags / menu
*Def.gen.cs                      ← generated output only
        │
        ▼
C# / SO use static fields only
```

**Rule: modify Database only, then generate. Never author gen.cs by hand.**

## Agent change procedure

1. Locate Database (`libraries.md`).
2. **Only** change Database:
   - Inspector tree / toolbar, or
   - Editor API: `AddTag` / `RenameTag` / `RemoveTagRecursive`.
3. Generate:
   - `Tools/GAS/GameplayTags/Generate Selected Database`
   - `Tools/GAS/GameplayTags/Generate All Databases`
   - Database Inspector **Generate Code**
   - `GameplayTagCodeGenerator.BuildGameplayTags(db, force: false)`
4. Consume generated static fields in feature code.

Do not:

- Patch `*Def.gen.cs` to add tags
- Invent `new GameplayTag(...)`
- Skip Generate and pretend the field exists

## Add a Tag

1. Correct Database; Domain set and unique.
2. Add path (toolbar or Add Child). Ancestors auto-created when needed.
3. **Generate Code** (`force: false`).
4. Use field: path dots → underscores (`State.Dead` → `State_Dead`).

### Naming

- Segments: letter/`_` start; letter/digit/`_`
- Max depth **4**
- Prefer hierarchy: `State.Debuff.Poison`
- Combat: `Ability.*`, `State.*`, `Cue.*`, `Trigger.*`
- Global: `GameType.*`, `UI.*`, `Guide`

## Rename

1. Database tree rename (siblingId unchanged).
2. Generate.
3. Update C# references to the new field name.

## Delete

1. Database Delete Recursive → ids **retired** (not auto-reused).
2. Generate.
3. Recycle IDs only when parent full and references cleared.

## Generate

| Entry | Behavior |
|---|---|
| Generate Code / menu / `BuildGameplayTags(db, false)` | Domain check + value/mask drift reject |
| Force Generate / `force: true` | Skips drift; user must confirm |

Side effects: Odin cache clear, legacy lookup cache clear.

## Recycle

1. Prefer deeper hierarchy before recycle.
2. Recycle Sibling IDs window.
3. Only recycle unreferenced ids.
4. Add Tag again (uses free pool first).

## Legacy Domain Fixup

- Scan / Fix menus under `Tools/GAS/GameplayTags/`
- Does not replace Database workflow; only repairs old serialized Domain=None tags.

## Checklist

- [ ] Definition change is on Database `.asset` only
- [ ] Generate invoked from Database path (not hand-written gen)
- [ ] No `new GameplayTag(` in feature code
- [ ] No manual `*Def.gen.cs` authoring
- [ ] Business code uses static fields after Generate
