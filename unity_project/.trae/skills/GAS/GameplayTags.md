# GameplayTags

相关源码：

```text
Assets/Scripts/HotUpdate.Core/GameplayTags/GameplayTag.cs
Assets/Scripts/HotUpdate.Core/GameplayTags/GameplayTagContainer.cs
Assets/Scripts/HotUpdate.Core/GameplayTags/TagQuery.cs
Assets/Scripts/HotUpdate.Core/GameplayTags/GameplayTagsDef.gen.cs
Assets/Scripts/HotUpdate.Core/GameplayTags/PVEGameTagsDef.gen.cs
```

## 1. GameplayTag

`GameplayTag` 是一个层级标签值（struct）。

内部包含：

- `value`（ulong/digit 编码）
- `mask`（层级掩码）

匹配规则：

```csharp
(Value & parent.Mask) == parent.Value
```

因此子标签可以匹配父标签。

示例：

```csharp
GameplayTags.GameType_City_Build.Matches(GameplayTags.GameType_City)
// 返回 true
```

关键属性：

- `GameplayTag.None` — 无效标签，不参与任何匹配。
- `IsValid` — 判断是否为有效标签。

## 2. 自动生成标签

`GameplayTagsDef.gen.cs` 和 `PVEGameTagsDef.gen.cs` 是自动生成文件：

```csharp
public static class GameplayTags
{
    public static readonly GameplayTag GameType = ...
    public static readonly GameplayTag GameType_City = ...
    public static readonly GameplayTag GameType_City_Build = ...
}
```

不要手动修改 `.gen.cs`。新增标签应通过编辑器工具/标签配置生成。

编辑器工具位于：

```text
Assets/Scripts/HotUpdate.Core/GameplayTags/Editor/
  - GameplayTagDatabase.cs
  - GameplayTagDatabaseEditor.cs
  - GameplayTagCodeGenerator.cs
  - GameplayTagTreeView.cs
```

## 3. GameplayTagContainer

`GameplayTagContainer` 是可序列化标签容器。

特点：

- 保存序列化 `List<GameplayTag>`（反序列化后重建 runtime 字典）。
- 运行时维护 `exactTagCount`（精确计数）。
- 运行时维护 `matchedTagCount`（含父级匹配计数）。
- 支持 tag listener（注册/注销回调）。

常用方法：

- `AddTag(tag)` / `RemoveTag(tag)` — 添加/移除单个 tag。
- `RemoveTag(tag, includeChild)` — 支持子标签级联移除。
- `HasTag(query)` — TagQuery 条件查询。
- `GetTagCount(tag)` — 精确计数（只看 exact count）。
- `AddTags(container)` / `RemoveTags(container)` — 批量操作。
- `Clear()` — 清空所有 tag。
- `RegisterListener(tag, callback)` / `UnregisterListener(callback)` — 监听 tag 变化。
- `Tags` — 返回序列化标签列表（IReadOnlyList）。

## 4. 层级计数

添加一个子标签时，会更新它自己和所有父级的 matched count。

例如添加：

```text
GameType.City.Build
```

则 `HasTag(GameType)`、`HasTag(GameType.City)`、`HasTag(GameType.City.Build)` 都返回 true。

`GetTagCount(tag)` 只看 exact count，即只统计精确匹配 `GameType.City.Build` 的次数。

## 5. TagQuery

`TagQuery` 用来表达标签条件。

节点操作类型：

```csharp
public enum TagQueryOp : byte
{
    All,      // 所有子节点条件都满足
    Any,      // 任一子节点条件满足
    NotAll,   // 命中任一节点即返回 false（当所有节点都不命中时返回 true）
}
```

匹配容器：

```csharp
query.Match(container)
```

匹配单个 tag：

```csharp
query.Match(tag)
```

空 query（无 nodes）视为通过。

## 6. GAS 中的使用位置

Ability 中：

- `SourceRequiredTags`（默认 All）
- `SourceBlockedTags`（默认 NotAll）
- `TargetRequiredTags`（默认 All）
- `TargetBlockedTags`（默认 NotAll）
- `ActivationRequiredTags`（默认 All）
- `ActivationBlockedTags`（默认 NotAll）
- `ActivationOwnedTags`（GameplayTagContainer）
- `CancelAbilitiesWithTag`（默认 Any）— 匹配 active ability 的 AbilityTag
- `BlockAbilitiesWithTag`（默认 Any）— Nodes 中的每个 tag 进入引用计数阻塞表
- `AbilityTriggers`（List\<GameplayTag\>）— 逐个与 eventTag.Matches() 匹配

Effect 中：

- `SourceRequiredTags`（默认 All）
- `SourceBlockedTags`（默认 NotAll）
- `TargetRequiredTags`（默认 All）
- `TargetBlockedTags`（默认 NotAll）
- `GrantedTags`（GameplayTagContainer）
- `EffectTag`
- `CueTag`

运行时：

- `GameplayEffectRuntime.OwnedTags`
- `GameplayAbilitySystem.OwnedTags`

## 7. 注意事项

- `GameplayTag.None` 不参与匹配，`IsValid` 返回 false。
- 空 `TagQuery`（无 nodes）返回 true。
- `NotAll` 语义：只要命中任意节点就返回 false，全部未命中才返回 true。
- `.gen.cs` 文件不手动修改。
- Container 的 serialized list 会在反序列化后自动重建 runtime 字典。
- Container 的 listener 机制可用于响应 tag 的增删变化。