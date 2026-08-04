---
name: gameplay-tags
description: "Use this skill when creating, adding, renaming, deleting, reviewing, or assigning GameplayTags; editing GameplayTagDatabase assets; generating *Def.gen.cs; using GameplayTag / GameplayTagContainer / TagQuery; wiring Ability/Effect/Cue tags; or when code needs a new tag path (State, Ability, Cue, UI, GameType, etc.). Trigger for 标签, GameplayTag, OwnedTags, GrantedTags, TagQuery, Domain, siblingId. Tag changes: ONLY edit Database asset then generate — never hand-edit *Def.gen.cs or new GameplayTag()."
---

# GameplayTags Skill

Tags are **Database-owned**. Agents must treat `GameplayTagDatabase` as the only writable source of tag definitions.

## Golden Rule (mandatory)

**修改 Tag 定义时：只改 Database 资产 → 再调用 Database 生成代码。**

```
1) Edit GameplayTagDatabase (.asset)   ← only place to add/rename/delete paths
2) Generate from that Database          ← only way *Def.gen.cs may change
3) Business C# uses generated fields    ← CombatGameplayTags.X / GameplayTags.Y
```

| Allowed | Forbidden |
|---|---|
| Edit `*GameplayTags.asset` (entries / paths) | Edit `*Def.gen.cs` by hand |
| Call Generate (Inspector / menu / `BuildGameplayTags`) | `new GameplayTag(domain, value, mask)` in feature code |
| Reference `CombatGameplayTags.*` / `GameplayTags.*` | Invent value/mask or siblingId in C# |
| Ask user to Generate if editor API unavailable | “先写死 Tag，以后再补 Database” |

`*Def.gen.cs` is **output only**. Any direct edit will be overwritten or fail drift checks.

## Hard Rules

1. **Never invent a tag in C#.**
2. **Never hand-edit `*Def.gen.cs`** for add/rename/delete.
3. **Never hardcode magic value/mask.**
4. **Do not add `GameplayTagDomain` values** unless the user explicitly asks.
5. **Same matching surface ⇒ same Domain** (do not mix Global/Combat in one OwnedTags for cross-match).

If a tag is missing: update the correct Database, generate, then consume the static field. No temporary fake tags.

## First Reads

| Task | Read |
|---|---|
| Add / rename / generate | this file + `references/tag-workflow.md` |
| Runtime API | `references/runtime-api.md` |
| Asset / gen paths | `references/libraries.md` |

## Libraries

| Domain | Database asset | Generated class / file |
|---|---|---|
| `Global` | `Assets/Scripts/HotUpdate.Core/GameplayTags/Editor/GameplayTags.asset` | `GameplayTags` / `GameplayTagsDef.gen.cs` |
| `Combat` | `Assets/Scripts/HotUpdate.Core/Battle/BattleCommon/GameplayTags/Editor/CombatGameplayTags.asset` | `CombatGameplayTags` / `CombatGameplayTagsDef.gen.cs` |

- Combat state / ability / cue / trigger → **Combat**
- Game mode / UI / guide / meta → **Global**

## Agent Workflow: Change Tags

### A. Prefer (when Unity Editor is available)

1. Search existing gen fields + Database entries.
2. **Only modify** the correct `GameplayTagDatabase` asset:
   - Unity Inspector: add / rename / delete on the tree, **or**
   - Load asset in Editor and call `db.AddTag` / `RenameTag` / `RemoveTagRecursive` (Editor scripts only).
3. **Generate from Database** (do not write gen file yourself):
   - Inspector: **Generate Code** on that Database
   - Menu: `Tools/GAS/GameplayTags/Generate Selected Database` (select asset first)
   - Menu: `Tools/GAS/GameplayTags/Generate All Databases`
   - Code: `GameplayTagCodeGenerator.BuildGameplayTags(db, force: false)`
4. After gen succeeds, use the new static field in business code.

### B. If agent cannot run Unity Generate

1. Still **only** propose / apply changes to the Database `.asset` (not gen.cs).
2. Tell the user: open that Database → **Generate Code**.
3. Do not invent gen.cs contents; leave consuming code with a TODO until the field exists, or wire only after user generates.

### C. Force Generate

Use only when user explicitly accepts breaking serialized values:

- Inspector **Force Generate**, or `BuildGameplayTags(db, force: true)`.

Default is always `force: false` (drift protected).

## Database edit rules (when editing .asset)

- Prefer API: `AddTag`, `RenameTag`, `RemoveTagRecursive` (allocates stable siblingId, maintains cursors/retired).
- If editing YAML by hand (discouraged): never change existing entries’ `siblingId`; only append new paths with correct next ids — **prefer API**.
- Never reassign existing siblingIds.
- Never rewrite other tags’ encodings.

## Use Tags in Code

```csharp
// Good
owned.AddTag(CombatGameplayTags.State_Poisoned);

// Bad — never (constructor is internal: this fails to compile in business code)
owned.AddTag(new GameplayTag(GameplayTagDomain.Combat, 0x03030000u, 0xFFFF0000u));
```

```csharp
[GameplayTagDomain(GameplayTagDomain.Combat)]
public GameplayTag StateTag;
```

`TagQueryOp`: `All` / `Any` / `None` (BlockedTags → `None`).

## Validation

1. Menu: `Tools/GAS/GameplayTags/Validate No Hand-Written new GameplayTag()`
2. Grep / git: no hand-authored hunks in `*Def.gen.cs` / `GameplayTagCatalog.gen.cs` except via Generate.
3. Tag definition diffs should show **Database `.asset`** (+ gen only after Generate).
4. Referenced static fields exist in gen after Generate.
5. Legacy: `Tools/GAS/GameplayTags/Scan|Fix Legacy Tags`.
6. Debug paths: `GameplayTagDebug.GetPath(tag)` / `tag.ToString()` after catalog generate.
7. Recycle: reference scan runs automatically; Force Recycle only if intentional.

## Anti-Patterns

| Anti-pattern | Correct approach |
|---|---|
| AI writes `new GameplayTag(...)` | Compile error by design (ctor is `internal`; only gen files + Editor tools build tags). Use Database + Generate + static field |
| AI edits `*Def.gen.cs` | Edit Database only → Generate |
| AI only changes C# field names | Rename on Database → Generate → update C# references |
| Skip Generate after asset edit | Always Generate before consuming new fields |

## When User Says “Add Tag X”

1. Which Database? Combat / Global  
2. Full path? e.g. `State.Stunned`  
3. Edit **only** that Database  
4. **Generate** from Database  
5. Code: `CombatGameplayTags.State_Stunned`

## References

- `references/tag-workflow.md`
- `references/runtime-api.md`
- `references/libraries.md`
