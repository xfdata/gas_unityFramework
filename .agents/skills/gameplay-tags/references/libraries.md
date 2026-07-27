# GameplayTag Libraries

## Global (`GameplayTagDomain.Global`)

| Item | Path |
|---|---|
| Database | `unity_project/Assets/Scripts/HotUpdate.Core/GameplayTags/Editor/GameplayTags.asset` |
| Generated | `unity_project/Assets/Scripts/HotUpdate.Core/GameplayTags/GameplayTagsDef.gen.cs` |
| Class | `GameplayTags` |

Typical roots: `GameType`, `UI`, `Guide`.

## Combat (`GameplayTagDomain.Combat`)

| Item | Path |
|---|---|
| Database | `unity_project/Assets/Scripts/HotUpdate.Core/Battle/BattleCommon/GameplayTags/Editor/CombatGameplayTags.asset` |
| Generated | `unity_project/Assets/Scripts/HotUpdate.Core/Battle/BattleCommon/GameplayTags/CombatGameplayTagsDef.gen.cs` |
| Class | `CombatGameplayTags` |

Typical roots: `Ability`, `State`, `Cue`, `Trigger`.

## Generated Field Naming

Path `State.BornInvincible` → field `State_BornInvincible`:

```csharp
public static readonly GameplayTag State_BornInvincible =
    new GameplayTag(Domain, 0x..., 0x...); // @Tag:State.BornInvincible
```

Only the generator writes these lines.

## Editor Tools

| Tool | Location |
|---|---|
| Database inspector | select `*GameplayTags.asset` |
| Generate (per asset) | Inspector **Generate Code**, or menu `Tools/GAS/GameplayTags/Generate Selected Database` |
| Generate (all) | `Tools/GAS/GameplayTags/Generate All Databases` |
| API generate | `GameplayTagCodeGenerator.BuildGameplayTags(db, force: false)` |
| Catalog (auto) | `GameplayTagCatalog.gen.cs` rebuilt on each Generate |
| Debug paths | `GameplayTagDebug.GetPath` / `tag.ToString()` |
| Recycle IDs | Database toolbar; reference-scan gated |
| Domain validate | `Tools/GAS/GameplayTags/Validate Domain Uniqueness` |
| Hand-written check | `Tools/GAS/GameplayTags/Validate No Hand-Written new GameplayTag()` |
| Legacy scan/fix | `Tools/GAS/GameplayTags/Scan|Fix Legacy Tags` |

**Definition edits always go to the Database row above; gen files are produce-only.**

## Core Runtime Types

Under `unity_project/Assets/Scripts/HotUpdate.Core/GameplayTags/`:

- `GameplayTag.cs`
- `GameplayTagDomain.cs`
- `GameplayTagDomainAttribute.cs`
- `GameplayTagContainer.cs`
- `TagQuery.cs`
