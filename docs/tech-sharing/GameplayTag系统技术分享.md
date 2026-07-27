# GameplayTag 系统技术分享

> 面向项目内技术分享：背景、架构、关键设计、工具链、AI 协作约束与落地经验。  
> 代码主目录：`unity_project/Assets/Scripts/HotUpdate.Core/GameplayTags/`  
> Agent 规范：`.agents/skills/gameplay-tags/`

---

## 1. 分享目标

本次分享希望大家带走三件事：

1. **为什么**我们要自研一套 GameplayTag，而不是字符串 / 枚举满天飞。  
2. **这套系统怎么分层**：数据源、编码、运行时、编辑器、生成物。  
3. **协作红线**：Tag 只改 Database → Generate；禁止手写 `new GameplayTag(...)`。

---

## 2. 背景与问题

### 2.1 业务场景

项目在 GAS（Gameplay Ability System）里大量使用 Tag：

- 状态门控：`State.Dead` / `State.Invincible` / `State.Casting`
- 技能与阻挡：`Ability.*`、`BlockAbilitiesWithTag`
- Cue / 事件：`Cue.Hit`、`Trigger.OnKill`
- 玩法与 UI：`GameType.*`、`UI.Popup`

Tag 需要支持：

- **层级匹配**（有 `State.Dead` 时，查询 `State` 也应命中）
- **多系统独立**（战斗 Tag 与全局/UI Tag 互不污染）
- **可序列化**（写在 Ability/Effect 配置上）
- **可演进**（增删改时尽量不打坏已有配置）

### 2.2 演进过程中踩过的坑

| 问题 | 表现 | 后果 |
|---|---|---|
| 多库独立编码 | Global 的 `GameType` 与 Combat 的 `Ability` 可能同为 `0x01000000` | `HasTag` / `Matches` 跨库误命中 |
| Generate 按名字重排 Id | 插入/删除/重命名后 value 漂移 | SO/Prefab 里旧 value 静默错绑 |
| `TagQueryOp.NotAll` 名不副实 | 实现是「全部不命中」，名字像「并非全部」 | 配表理解成本高、易配错 |
| AI / 人工直接造 Tag | `new GameplayTag(...)` 或手改 `*Def.gen.cs` | 绕过 Database，编码不可维护 |
| 删除后 Id 立刻复用 | 旧资产仍引用旧 value | 配置「看起来对、跑起来错」 |
| 调试只能看 hex | `0x03030000/0xFFFF0000` | 日志与排查成本高 |

### 2.3 设计目标（一句话）

> **数据驱动的分层 Tag 身份系统**：稳定编码、域隔离、运行时高效、编辑器可治理、人和 AI 都难绕过。

---

## 3. 总体架构

### 3.1 数据流（唯一合法路径）

```
┌─────────────────────────────┐
│  GameplayTagDatabase.asset  │  ← 唯一可写定义源（path + siblingId）
└──────────────┬──────────────┘
               │ Generate Code
               ▼
┌─────────────────────────────┐
│  XxxTagsDef.gen.cs          │  ← 静态字段库（输出物）
│  GameplayTagCatalog.gen.cs  │  ← 调试 / Odin 扁平表（输出物）
└──────────────┬──────────────┘
               │ 业务引用
               ▼
┌─────────────────────────────┐
│  Ability / Effect / Cue /   │
│  OwnedTags / TagQuery       │
└─────────────────────────────┘
```

**红线：只改 Database → 再 Generate → 业务只引用静态字段。**

### 3.2 模块划分

| 层级 | 职责 | 代表类型 |
|---|---|---|
| 身份 | Domain + 层级编码 | `GameplayTag`, `GameplayTagDomain` |
| 容器 | 持有、引用计数、层级查询、监听 | `GameplayTagContainer` |
| 查询 | All / Any / None | `TagQuery`, `TagQueryOp` |
| 数据 | 路径树、稳定 siblingId、弃用/回收池 | `GameplayTagDatabase` |
| 生成 | 稳定编码写出、差分保护、Catalog | `GameplayTagCodeGenerator` |
| 编辑器 | 树编辑、回收、Legacy 修复、引用扫描 | Inspector / Tools 菜单 |
| 协作 | AI 强制走 Database | `.agents/skills/gameplay-tags` |

### 3.3 当前库划分

| Domain | Database | 生成类 | 典型根节点 |
|---|---|---|---|
| `Global` | `GameplayTags.asset` | `GameplayTags` | `GameType`, `UI`, `Guide` |
| `Combat` | `CombatGameplayTags.asset` | `CombatGameplayTags` | `Ability`, `State`, `Cue`, `Trigger` |

原则：**会进同一个 `OwnedTags`、需要互相 `HasTag` 的，必须同 Domain。**

---

## 4. 核心设计详解

### 4.1 GameplayTag 身份

```csharp
struct GameplayTag
{
    GameplayTagDomain Domain; // 库隔离
    uint Value;               // 层级编码
    uint Mask;                // 有效深度掩码
}
```

- `IsValid`：`Domain != None && Mask != 0`
- `IsLegacyMissingDomain`：旧资产 `domain=0` 但 mask 有值（需 Fixup）
- `Matches(parent)`：同 Domain，且 `(Value & parent.Mask) == parent.Value`
- `ToString()` / `GameplayTagDebug.GetPath`：可读路径，如 `Combat/State.Dead`

### 4.2 层级编码（4 层 × 8 bit）

```
Value:  [ L1:8 ][ L2:8 ][ L3:8 ][ L4:8 ]
Mask:   按深度置位，例如 2 层 = 0xFFFF0000
```

示例（示意）：

| Path | 大致编码 |
|---|---|
| `State` | `0x03000000 / FF000000` |
| `State.Dead` | `0x03030000 / FFFF0000` |

能力：

- 子查父：`State.Dead.Matches(State) == true`
- 同级最多 255 个节点（Id 从 1 起，0 保留）

限制：

- 深度最多 4  
- 同级 255：靠拆树 + 回收解决，不靠 Generate 重排

### 4.3 Domain：为什么不只靠路径前缀

多玩法库可能出现**路径不同但编码相同**（各自从 1 编码）。  
若只有 `value/mask`，混进同一 Container 会误匹配。

Domain 规则：

1. 运行时 `Matches` / `Equals` / Container key **先比 Domain**  
2. **一个 Domain 只对应一个 Database**（编辑器校验）  
3. 新库 = 新 Domain 枚举值 + 新 Database

### 4.4 稳定 siblingId（配置寿命的核心）

旧方案：Generate 时按字典序 `1..N` 重排 → **插入一个兄弟，后面全部漂移**。

新方案：

```
entries:       { path, siblingId }   // 每个节点固定 id
parentCursors: { parentPath, nextId } // 水位线，只前进
retiredIds:    删除后的 id（默认不复用）
recycledPool:  人工批准后才可复用
```

| 操作 | siblingId |
|---|---|
| 新增 | 分配 nextId，水位 +1 |
| 重命名 | **不变**（序列化 value 稳定） |
| 删除 | 进 retired，水位不回退 |
| 回收 | 人工确认后进入 free pool，下次 Add 优先用 |

Generate **只读 siblingId 拼 value**，不再重排。

### 4.5 TagQuery 语义

| Op | 含义 | 典型用途 |
|---|---|---|
| `All` | 全部命中 | RequiredTags |
| `Any` | 任一命中 | Cancel / Block 能力 |
| `None` | 全部不命中 | BlockedTags |

说明：历史上误命名为 `NotAll`，实现一直是「None」。现已改为 `None`，序列化值仍为 `2`，旧资产兼容。

### 4.6 GameplayTagContainer（运行时）

职责：

- 精确 Tag 的**引用计数**（GrantedTags 叠加）
- **层级匹配计数**（`HasTag(State)` 在子 Tag 存在时为 true）
- 0↔1 变化时通知 Listener

关键 API：

| API | 语义 |
|---|---|
| `AddTag` / `RemoveTag` | 精确 tag 栈 ±1 |
| `RemoveTagCompletely` | 精确 tag 清零 |
| `RemoveMatching` | 子树 + 全部 stack |
| `HasTag` | 层级是否存在 |
| `GetTagCount` | 精确 stack 数 |

性能要点：

- 计数 key = `Domain << 32 | Value`（跨域不撞）  
- 序列化列表旁挂 `serializedIndex`，O(1) 删除  
- 通知不做 List 快照；用 `notifyDepth` + 延迟 compact  
- 子树删除复用 `scratchTags`  
- `RebuildRuntime` 预分配 Dictionary 容量、单次去重

---

## 5. 编辑器与工具链

### 5.1 Database Inspector

- Domain / 生成路径  
- Tag 树：增、删、改、搜  
- 行尾显示 `id N`  
- **Generate Code** / **Force Generate**  
- **Recycle IDs**、**Scan/Fix Legacy**

### 5.2 Generate 保护

1. Domain 不能为 `None`  
2. 全局 Domain 唯一  
3. 与旧 `*Def.gen.cs` 对比：同 path 的 value/mask 变了 → **拒绝**（除非 Force）  
4. 成功后刷新 Catalog、清 Odin/Debug 缓存

### 5.3 回收与引用扫描

删除 ≠ 可复用。流程：

```
Delete → retired（弃用列表）
用户打开 Recycle 窗口
  → 扫描项目 SO/Prefab 是否仍序列化该 value
  → 有引用：默认拦截，可强制
  → 无引用：确认后进入 free pool
下次 AddTag 优先从 free pool 取最小 id
```

### 5.4 Legacy Domain Fixup

Domain 引入前的序列化 Tag：`domain=0, mask!=0`。

- `Tools/GAS/GameplayTags/Scan Legacy Tags (Dry Run)`  
- `Tools/GAS/GameplayTags/Fix Legacy Tags`  
- Inspector 橙色 + 可一键 Fix（唯一匹配时）

### 5.5 其它工具菜单

| 菜单 | 作用 |
|---|---|
| Generate Selected / All Databases | 生成 |
| Validate Domain Uniqueness | Domain 冲突检查 |
| Validate No Hand-Written `new GameplayTag()` | 扫业务代码防绕过 |
| Scan / Fix Legacy Tags | 旧资产 Domain 修复 |

### 5.6 调试与编辑器性能

| 能力 | 实现 |
|---|---|
| 可读路径 | `GameplayTagCatalog` + `GameplayTagDebug.GetPath` |
| Odin 下拉 | 读 Catalog，不再全程序集反射；按 Domain 缓存 |
| 手写 Tag 检测 | 扫 `.cs`，忽略 gen 文件与基础设施 |

---

## 6. 与 GAS 的接入关系

Ability / Effect 上常见字段：

- `Source/Target/Activation` 的 Required / Blocked（`TagQuery`）  
- `ActivationOwnedTags`、`GrantedTags`（`GameplayTagContainer`）  
- `CancelAbilitiesWithTag` / `BlockAbilitiesWithTag`  
- CueTag、AbilityTag、EffectTag

推荐：

```csharp
// 状态
owned.AddTag(CombatGameplayTags.State_Poisoned);

// 查询
if (owned.HasTag(CombatGameplayTags.State_Dead)) { }

// 字段约束
[GameplayTagDomain(GameplayTagDomain.Combat)]
public GameplayTag StateTag;
```

禁止：

```csharp
new GameplayTag(GameplayTagDomain.Combat, 0x03030000u, 0xFFFF0000u);
// 以及手改 *Def.gen.cs
```

---

## 7. AI / 人协作规范

问题：AI 容易「直接写 Tag 常量」，绕过 Database。

解决：

1. **Skill**：`.agents/skills/gameplay-tags`  
   - 强制：只改 Database → Generate  
   - battle GAS 文档交叉引用  
2. **技术闸**：Generate 差分、Domain 唯一、手写检测菜单  
3. **流程话术**：缺 Tag 时先报 Database + 路径，不造临时 Tag  

Agent 正确流程：

1. 搜现有 `CombatGameplayTags.*` / `GameplayTags.*`  
2. 没有 → 改对应 `.asset`（或提示策划/程序在 Inspector 加）  
3. `BuildGameplayTags` / 菜单 Generate  
4. 业务代码引用新静态字段  

---

## 8. 日常操作速查

### 加 Tag

1. 打开正确 Database（Combat / Global）  
2. 添加路径，如 `State.Stunned`（最多 4 层）  
3. **Generate Code**（不要轻易 Force）  
4. 代码：`CombatGameplayTags.State_Stunned`  

### 改名

Database 树重命名（Id 不变）→ Generate → 改 C# 字段名引用  

### 删除

Database 删除 → Id 进弃用 → Generate → 确认无引用后再 Recycle  

### 同级满 255

优先拆子层级；必要时 Recycle（带引用扫描）  

---

## 9. 设计取舍与经验

### 9.1 为什么稳定 Id 比「紧凑编码」更重要

重排能省几个空洞，但会打坏全项目序列化配置。  
**配置正确性 >> 同级多空几个号。**  
空洞靠回收窗口 + 引用扫描治理即可。

### 9.2 为什么 Domain 用 enum 而不是塞进 value 高位

- API 清晰：`Matches` 先比 Domain  
- 不挤占 4×8bit 层级空间  
- 与 Database 一一对应，易校验  

### 9.3 为什么 Container 用引用计数

GrantedTags / ActivationOwnedTags 会叠加；  
`RemoveTag` = 栈 -1，清状态用 `RemoveMatching` / `RemoveTagCompletely`。

### 9.4 已知边界（诚实说明）

- 深度 4、同级 255：靠规范拆树，不是无限扩展  
- Recycle 扫描是 Editor 资产扫描，有成本；Force 仍可能误用  
- `TagQueryOp.Any` 空列表当前为 true（与历史一致）  
- 跨 Domain 匹配被禁止是特性，不是 bug  

---

## 10. 改造前后对比（分享用）

| 维度 | 改造前 | 改造后 |
|---|---|---|
| 多库 | value 可撞车 | Domain 隔离 + 唯一校验 |
| Generate | 可能重排 Id | 稳定 siblingId + 漂移拒绝 |
| 删除 | 无治理 | retired / recycle + 引用扫描 |
| 查询语义 | NotAll 易误解 | `None` |
| 调试 | hex | `Combat/State.Dead` |
| 编辑器下拉 | 全程序集反射 | Catalog 缓存 |
| AI 协作 | 易手写 Tag | Skill + 检测菜单 |
| 运行时 GC | 通知/删除有分配 | 去快照、O(1) 索引、scratch 复用 |

**自评：约 82 → 约 90（可落地的中上水平自研 Tag 系统）。**

---

## 11. 关键文件索引

```
HotUpdate.Core/GameplayTags/
  GameplayTag.cs
  GameplayTagDomain.cs
  GameplayTagDomainAttribute.cs
  GameplayTagContainer.cs
  TagQuery.cs
  GameplayTagDebug.cs
  GameplayTagCatalog.gen.cs
  GameplayTagsDef.gen.cs
  Editor/
    GameplayTagDatabase.cs
    GameplayTagCodeGenerator.cs
    GameplayTagDomainValidator.cs
    GameplayTagLegacyFixup.cs
    GameplayTagReferenceScanner.cs
    GameplayTagRecycleWindow.cs
    GameplayTagHandwrittenValidator.cs
    TagEditor/GameplayTagOdin*.cs
    GameplayTags.asset

BattleCommon/GameplayTags/
  CombatGameplayTagsDef.gen.cs
  Editor/CombatGameplayTags.asset

.agents/skills/gameplay-tags/   ← AI 规范
```

---

## 12. 分享后 Q&A 预案

**Q：为什么不用纯字符串 Tag？**  
A：匹配与序列化成本高，难做高效层级与稳定配置；运行时我们用 bit 匹配。

**Q：和 UE GameplayTag 比差在哪？**  
A：我们覆盖了 GAS 所需的核心链路；UE 还有更完整的编辑器引用、网络复制等。当前体量下性价比足够。

**Q：Force Generate 什么时候用？**  
A：仅在确认要迁移编码、并接受全量修配置时。日常禁止。

**Q：AI 又写了 `new GameplayTag` 怎么办？**  
A：跑手写检测菜单；Code Review 卡 skill；业务侧只允许引用 `*Tags` 静态字段。

**Q：同级真的会满 255 吗？**  
A：正常层级设计很难；危险的是「表主键平铺成同级 Tag」。那种应拆父节点或不要用 Tag 表达。

---

## 13. 一句话收束

> **GameplayTag = 稳定编码的身份系统 + 数据驱动的生成管线。**  
> 运行时求快、配置求稳、协作求不可绕过。  
> 记住：**Database 是源，Generate 是闸，静态字段是唯一入口。**

---

*文档版本：与当前仓库 GameplayTag 改造同步，可用于组内技术分享 / 新人 onboarding。*
