using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public sealed class GameplayTagEntry
{
    public string path;
    public int siblingId;

    public GameplayTagEntry()
    {
    }

    public GameplayTagEntry(string path, int siblingId)
    {
        this.path = path;
        this.siblingId = siblingId;
    }
}

[Serializable]
public sealed class GameplayTagParentCursor
{
    public string parentPath = "";
    public int nextId = 1;
}

/// <summary>
/// Deleted sibling slot that is not reusable until the user recycles it.
/// </summary>
[Serializable]
public sealed class GameplayTagRetiredId
{
    public string parentPath = "";
    public int siblingId;
    public string lastPath = "";
    public int encodedValue;
    public int encodedMask;
}

/// <summary>
/// Explicitly recycled sibling slot that may be reused by the next AddTag under the same parent.
/// </summary>
[Serializable]
public sealed class GameplayTagRecycledId
{
    public string parentPath = "";
    public int siblingId;
}

public readonly struct GameplayTagSiblingSlotInfo
{
    public readonly string ParentPath;
    public readonly int SiblingId;
    public readonly string LastPath;
    public readonly bool IsRecycled;
    public readonly uint EncodedValue;
    public readonly uint EncodedMask;
    public bool HasEncodedTag => EncodedMask != 0;

    public GameplayTagSiblingSlotInfo(string parentPath, int siblingId, string lastPath, bool isRecycled, uint encodedValue = 0, uint encodedMask = 0)
    {
        ParentPath = parentPath ?? string.Empty;
        SiblingId = siblingId;
        LastPath = lastPath ?? string.Empty;
        IsRecycled = isRecycled;
        EncodedValue = encodedValue;
        EncodedMask = encodedMask;
    }

    public string DisplayParent => string.IsNullOrEmpty(ParentPath) ? "<root>" : ParentPath;
}

[CreateAssetMenu(menuName = "GAS/GameplayTagDatabase")]
public sealed class GameplayTagDatabase : ScriptableObject
{
    public const int MaxDepth = GameplayTagEncoding.MaxLevels;
    public const int MaxSiblingId = 255;

    [SerializeField]
    private GameplayTagDomain domain = GameplayTagDomain.Global;

    [SerializeField]
    private string generatedCodePath = "";

    // Previous field name on disk was "tags" (itself formerly "Tags").
    [FormerlySerializedAs("tags")]
    [SerializeField]
    private List<string> legacyTags = new();

    [SerializeField]
    private List<GameplayTagEntry> entries = new();

    /// <summary>
    /// Per-parent next sibling id (1..255). Only increases unless user recycles.
    /// </summary>
    [SerializeField]
    private List<GameplayTagParentCursor> parentCursors = new();

    /// <summary>
    /// Deleted ids waiting for manual recycle. Not used by allocation.
    /// </summary>
    [SerializeField]
    private List<GameplayTagRetiredId> retiredIds = new();

    /// <summary>
    /// User-approved free list. Allocation prefers these before advancing the cursor.
    /// </summary>
    [SerializeField]
    private List<GameplayTagRecycledId> recycledPool = new();

    public GameplayTagDomain Domain => domain;
    public string GeneratedCodePath => generatedCodePath;
    public IReadOnlyList<GameplayTagEntry> Entries => entries;

    /// <summary>
    /// Paths only. Prefer iterating <see cref="Entries"/> to avoid allocations.
    /// </summary>
    public IReadOnlyList<string> Tags
    {
        get
        {
            EnsureMigrated();
            // Cached view rebuilt only when entries change is ideal; for editor frequency this is OK.
            // TreeView should prefer Entries — kept for API compat.
            var list = new List<string>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
                list.Add(entries[i].path);
            return list;
        }
    }

    public bool Contains(string tag)
    {
        EnsureMigrated();
        tag = NormalizeTag(tag);
        return IndexOf(tag) >= 0;
    }

    public bool TryGetSiblingId(string tag, out int siblingId)
    {
        EnsureMigrated();
        tag = NormalizeTag(tag);
        int index = IndexOf(tag);
        if (index < 0)
        {
            siblingId = 0;
            return false;
        }

        siblingId = entries[index].siblingId;
        return true;
    }

    /// <summary>
    /// High-water usage for a parent: max(live count, cursor-1).
    /// Does not subtract recycled pool (those are already free for reuse).
    /// </summary>
    public int GetSiblingUsage(string parentPath)
    {
        EnsureMigrated();
        parentPath = NormalizeTag(parentPath ?? string.Empty);

        int live = CountLiveSiblings(parentPath);
        int next = GetNextIdCursor(parentPath);
        int highWater = Mathf.Max(0, next - 1);
        return Mathf.Max(live, highWater);
    }

    public int GetLiveSiblingCount(string parentPath)
    {
        EnsureMigrated();
        return CountLiveSiblings(NormalizeTag(parentPath ?? string.Empty));
    }

    public int GetRetiredCount(string parentPath = null)
    {
        EnsureMigrated();
        if (parentPath == null)
            return retiredIds.Count;

        parentPath = NormalizeTag(parentPath);
        int count = 0;
        for (int i = 0; i < retiredIds.Count; i++)
        {
            if (string.Equals(retiredIds[i].parentPath, parentPath, StringComparison.Ordinal))
                count++;
        }

        return count;
    }

    public int GetRecycledPoolCount(string parentPath = null)
    {
        EnsureMigrated();
        if (parentPath == null)
            return recycledPool.Count;

        parentPath = NormalizeTag(parentPath);
        int count = 0;
        for (int i = 0; i < recycledPool.Count; i++)
        {
            if (string.Equals(recycledPool[i].parentPath, parentPath, StringComparison.Ordinal))
                count++;
        }

        return count;
    }

    public bool IsParentFull(string parentPath)
    {
        EnsureMigrated();
        parentPath = NormalizeTag(parentPath ?? string.Empty);

        if (GetRecycledPoolCount(parentPath) > 0)
            return false;

        if (GetNextIdCursor(parentPath) <= MaxSiblingId)
            return false;

        // Cursor exhausted and no free pool: still may have retired ids user can recycle.
        return CountLiveSiblings(parentPath) >= MaxSiblingId
               || GetNextIdCursor(parentPath) > MaxSiblingId;
    }

    public List<GameplayTagSiblingSlotInfo> GetRetiredSlots(string parentFilter = null)
    {
        EnsureMigrated();
        parentFilter = parentFilter == null ? null : NormalizeTag(parentFilter);

        var result = new List<GameplayTagSiblingSlotInfo>(retiredIds.Count);
        for (int i = 0; i < retiredIds.Count; i++)
        {
            var item = retiredIds[i];
            if (parentFilter != null &&
                !string.Equals(item.parentPath, parentFilter, StringComparison.Ordinal))
            {
                continue;
            }

            result.Add(new GameplayTagSiblingSlotInfo(
                item.parentPath,
                item.siblingId,
                item.lastPath,
                isRecycled: false,
                encodedValue: unchecked((uint)item.encodedValue),
                encodedMask: unchecked((uint)item.encodedMask)));
        }

        result.Sort(CompareSlots);
        return result;
    }

    public List<GameplayTagSiblingSlotInfo> GetRecycledSlots(string parentFilter = null)
    {
        EnsureMigrated();
        parentFilter = parentFilter == null ? null : NormalizeTag(parentFilter);

        var result = new List<GameplayTagSiblingSlotInfo>(recycledPool.Count);
        for (int i = 0; i < recycledPool.Count; i++)
        {
            var item = recycledPool[i];
            if (parentFilter != null &&
                !string.Equals(item.parentPath, parentFilter, StringComparison.Ordinal))
            {
                continue;
            }

            result.Add(new GameplayTagSiblingSlotInfo(
                item.parentPath,
                item.siblingId,
                lastPath: string.Empty,
                isRecycled: true));
        }

        result.Sort(CompareSlots);
        return result;
    }

    public List<string> GetParentsWithRetiredIds()
    {
        EnsureMigrated();
        var set = new SortedSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < retiredIds.Count; i++)
            set.Add(retiredIds[i].parentPath ?? string.Empty);
        return new List<string>(set);
    }

    /// <summary>
    /// Move retired slots into the free pool so future AddTag can reuse them.
    /// </summary>
    public int RecycleRetiredIds(IReadOnlyList<(string parentPath, int siblingId)> slots, out string error)
    {
        EnsureMigrated();
        error = null;

        if (slots == null || slots.Count == 0)
        {
            error = "没有选择任何可回收 Id。";
            return 0;
        }

        int recycled = 0;

        for (int i = 0; i < slots.Count; i++)
        {
            string parent = NormalizeTag(slots[i].parentPath ?? string.Empty);
            int siblingId = slots[i].siblingId;

            if (siblingId < 1 || siblingId > MaxSiblingId)
                continue;

            if (IsSiblingIdTaken(parent, siblingId, ignorePath: null))
            {
                // Live again somehow — drop retired record, skip pool.
                RemoveRetired(parent, siblingId);
                continue;
            }

            if (!RemoveRetired(parent, siblingId))
            {
                // Allow recycling discovered holes that were not tracked as retired.
            }

            if (!ContainsRecycled(parent, siblingId))
            {
                recycledPool.Add(new GameplayTagRecycledId
                {
                    parentPath = parent,
                    siblingId = siblingId
                });
            }

            recycled++;
        }

        SortRecycledPool();
        return recycled;
    }

    public int RecycleAllRetiredIds(string parentFilter, out string error)
    {
        EnsureMigrated();
        var list = GetRetiredSlots(parentFilter);
        var slots = new List<(string, int)>(list.Count);
        for (int i = 0; i < list.Count; i++)
            slots.Add((list[i].ParentPath, list[i].SiblingId));
        return RecycleRetiredIds(slots, out error);
    }

    /// <summary>
    /// Scan holes below each parent high-water mark and register them as retired (not free yet).
    /// </summary>
    public int ScanAndRegisterRetiredHoles()
    {
        EnsureMigrated();
        int added = 0;

        var parents = new HashSet<string>(StringComparer.Ordinal) { string.Empty };
        for (int i = 0; i < entries.Count; i++)
        {
            parents.Add(GetParentPath(entries[i].path));
            parents.Add(entries[i].path);
        }

        for (int i = 0; i < parentCursors.Count; i++)
            parents.Add(parentCursors[i].parentPath ?? string.Empty);

        foreach (string parent in parents)
        {
            int next = GetNextIdCursor(parent);
            int highWater = Mathf.Min(MaxSiblingId, next - 1);
            if (highWater < 1)
                continue;

            for (int id = 1; id <= highWater; id++)
            {
                if (IsSiblingIdTaken(parent, id, ignorePath: null))
                    continue;

                if (ContainsRecycled(parent, id))
                    continue;

                if (ContainsRetired(parent, id))
                    continue;

                if (!TryEncodeSiblingSlot(parent, id, out uint encodedValue, out uint encodedMask))
                {
                    Debug.LogWarning($"Unable to preserve historical GameplayTag encoding for retired slot {FormatParent(parent)}/{id}.");
                    continue;
                }

                retiredIds.Add(new GameplayTagRetiredId
                {
                    parentPath = parent,
                    siblingId = id,
                    lastPath = "(hole)",
                    encodedValue = unchecked((int)encodedValue),
                    encodedMask = unchecked((int)encodedMask)
                });
                added++;
            }
        }

        SortRetired();
        return added;
    }

    public bool AddTag(string tag)
    {
        return AddTag(tag, out _);
    }

    public bool AddTag(string tag, out string error)
    {
        EnsureMigrated();
        error = null;
        tag = NormalizeTag(tag);

        if (!IsValidTagPath(tag, out error))
        {
            Debug.LogError($"非法 Tag: {tag}, reason: {error}");
            return false;
        }

        if (Contains(tag))
        {
            error = $"Tag 已存在: {tag}";
            Debug.LogError(error);
            return false;
        }

        if (!EnsurePath(tag, out error))
        {
            Debug.LogError(error);
            return false;
        }

        Sort();
        return true;
    }

    /// <summary>
    /// Restore / import helper: set or create path with an explicit sibling id (1..255).
    /// </summary>
    public bool UpsertTagWithSiblingId(string tag, int siblingId, out string error)
    {
        EnsureMigrated();
        error = null;
        tag = NormalizeTag(tag);

        if (!IsValidTagPath(tag, out error))
            return false;

        if (siblingId < 1 || siblingId > MaxSiblingId)
        {
            error = $"siblingId 必须在 1..{MaxSiblingId}: {tag}={siblingId}";
            return false;
        }

        string parent = GetParentPath(tag);
        int existingIndex = IndexOf(tag);
        if (existingIndex >= 0)
        {
            int oldId = entries[existingIndex].siblingId;
            AdvanceCursorAtLeast(parent, Mathf.Max(oldId, siblingId) + 1);
            RemoveRetired(parent, siblingId);
            RemoveRecycled(parent, siblingId);
            return true;
        }

        string ancestorWalk = "";
        var parts = tag.Split('.');
        for (int i = 0; i < parts.Length - 1; i++)
        {
            ancestorWalk = string.IsNullOrEmpty(ancestorWalk)
                ? parts[i]
                : ancestorWalk + "." + parts[i];

            if (IndexOf(ancestorWalk) < 0)
            {
                error = $"导入 Tag 前缺少祖先节点: {ancestorWalk} (while adding {tag})";
                return false;
            }
        }

        if (IsSiblingIdTaken(parent, siblingId, ignorePath: null))
        {
            error = $"同级 siblingId 冲突: parent='{FormatParent(parent)}', id={siblingId}, path={tag}";
            return false;
        }

        entries.Add(new GameplayTagEntry(tag, siblingId));
        AdvanceCursorAtLeast(parent, siblingId + 1);
        RemoveRetired(parent, siblingId);
        RemoveRecycled(parent, siblingId);
        Sort();
        return true;
    }

    public bool RemoveTagRecursive(string tag)
    {
        EnsureMigrated();
        tag = NormalizeTag(tag);

        if (string.IsNullOrEmpty(tag))
            return false;

        var removedEntries = new List<GameplayTagEntry>();
        for (int i = 0; i < entries.Count; i++)
        {
            if (IsSameOrChild(tag, entries[i].path))
                removedEntries.Add(entries[i]);
        }

        if (removedEntries.Count == 0)
            return false;


        var encodedByPath = new Dictionary<string, (uint value, uint mask)>(removedEntries.Count, StringComparer.Ordinal);
        for (int i = 0; i < removedEntries.Count; i++)
        {
            var removed = removedEntries[i];
            if (!TryEncodeExistingPath(removed.path, out uint value, out uint mask))
            {
                Debug.LogError($"Unable to preserve retired GameplayTag encoding: {removed.path}");
                return false;
            }

            encodedByPath.Add(removed.path, (value, mask));
        }

        entries.RemoveAll(e => IsSameOrChild(tag, e.path));


        for (int i = 0; i < removedEntries.Count; i++)
        {
            var removed = removedEntries[i];
            string parent = GetParentPath(removed.path);

            // Deleted ids become retired (not free) until user recycles them.
            RemoveRecycled(parent, removed.siblingId);

            if (!IsSiblingIdTaken(parent, removed.siblingId, ignorePath: null))
            {
                var encoded = encodedByPath[removed.path];
                UpsertRetired(parent, removed.siblingId, removed.path, encoded.value, encoded.mask);
            }

            // Drop retired/recycled records under deleted subtree parents that no longer exist.
            PruneSlotRecordsForMissingParents();
        }

        // Intentionally do not rewind parent cursors.
        Sort();
        return true;
    }

    public void ClearTags()
    {
        EnsureMigrated();
        entries.Clear();
        parentCursors.Clear();
        retiredIds.Clear();
        recycledPool.Clear();
    }

    public bool RenameTag(string oldPath, string newName)
    {
        EnsureMigrated();
        oldPath = NormalizeTag(oldPath);
        newName = NormalizeTag(newName);

        if (string.IsNullOrEmpty(oldPath) || string.IsNullOrEmpty(newName))
            return false;

        if (newName.Contains("."))
        {
            Debug.LogError("Rename 只允许输入当前节点名称，不允许包含 '.'");
            return false;
        }

        if (!IsValidTagSegment(newName, out var segmentError))
        {
            Debug.LogError($"非法 Tag 名称: {newName}, reason: {segmentError}");
            return false;
        }

        bool found = false;
        for (int i = 0; i < entries.Count; i++)
        {
            if (IsSameOrChild(oldPath, entries[i].path))
            {
                found = true;
                break;
            }
        }

        if (!found)
        {
            Debug.LogError("找不到要重命名的 Tag: " + oldPath);
            return false;
        }

        string parent = GetParentPath(oldPath);
        string newPath = string.IsNullOrEmpty(parent)
            ? newName
            : parent + "." + newName;

        if (oldPath == newPath)
            return false;

        if (!IsValidTagPath(newPath, out var pathError))
        {
            Debug.LogError($"非法目标路径: {newPath}, reason: {pathError}");
            return false;
        }

        var existing = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < entries.Count; i++)
            existing.Add(entries[i].path);

        var changes = new List<(int index, string newTag)>();

        for (int i = 0; i < entries.Count; i++)
        {
            string oldTag = entries[i].path;

            if (!IsSameOrChild(oldPath, oldTag))
                continue;

            string suffix = oldTag.Substring(oldPath.Length);
            string newTag = newPath + suffix;

            if (existing.Contains(newTag) && !IsSameOrChild(oldPath, newTag))
            {
                Debug.LogError($"Rename 冲突: {newTag} 已存在");
                return false;
            }

            changes.Add((i, newTag));
        }

        foreach (var change in changes)
        {
            entries[change.index].path = change.newTag;
        }

        RemapParentKey(oldPath, newPath);
        Sort();
        return true;
    }

    public void EnsureMigrated()
    {
        if (entries == null)
            entries = new List<GameplayTagEntry>();

        if (parentCursors == null)
            parentCursors = new List<GameplayTagParentCursor>();

        if (retiredIds == null)
            retiredIds = new List<GameplayTagRetiredId>();

        if (recycledPool == null)
            recycledPool = new List<GameplayTagRecycledId>();

        if (legacyTags == null)
            legacyTags = new List<string>();

        if (legacyTags.Count == 0)
            return;

        if (entries.Count > 0)
        {
            legacyTags.Clear();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            return;
        }

        var paths = new List<string>(legacyTags.Count);
        for (int i = 0; i < legacyTags.Count; i++)
        {
            string p = NormalizeTag(legacyTags[i]);
            if (!string.IsNullOrEmpty(p) && !paths.Contains(p))
                paths.Add(p);
        }

        paths.Sort(StringComparer.Ordinal);
        legacyTags.Clear();
        entries.Clear();
        parentCursors.Clear();
        retiredIds.Clear();
        recycledPool.Clear();

        var expanded = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string path in paths)
        {
            if (!IsValidTagPath(path, out _))
                continue;

            var parts = path.Split('.');
            string full = "";
            for (int i = 0; i < parts.Length; i++)
            {
                full = i == 0 ? parts[i] : full + "." + parts[i];
                expanded.Add(full);
            }
        }

        var childrenByParent = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (string path in expanded)
        {
            string parent = GetParentPath(path);
            if (!childrenByParent.TryGetValue(parent, out var list))
            {
                list = new List<string>();
                childrenByParent.Add(parent, list);
            }

            list.Add(path);
        }

        foreach (var pair in childrenByParent)
        {
            pair.Value.Sort(StringComparer.Ordinal);
            int id = 1;
            foreach (string childPath in pair.Value)
            {
                entries.Add(new GameplayTagEntry(childPath, id));
                id++;
            }

            SetNextIdCursor(pair.Key, id);
        }

        Sort();

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private bool EnsurePath(string tag, out string error)
    {
        error = null;
        var parts = tag.Split('.');
        string full = "";

        for (int i = 0; i < parts.Length; i++)
        {
            full = i == 0 ? parts[i] : full + "." + parts[i];

            if (IndexOf(full) >= 0)
                continue;

            string parent = GetParentPath(full);
            if (!TryAllocateSiblingId(parent, out int siblingId, out error))
                return false;

            entries.Add(new GameplayTagEntry(full, siblingId));
            RemoveRetired(parent, siblingId);
            RemoveRecycled(parent, siblingId);
        }

        return true;
    }

    private bool TryAllocateSiblingId(string parentPath, out int siblingId, out string error)
    {
        error = null;
        parentPath = NormalizeTag(parentPath ?? string.Empty);

        // 1) Prefer user-recycled free pool (smallest id).
        if (TryTakeRecycledId(parentPath, out siblingId))
            return true;

        // 2) Advance high-water cursor (deleted ids are NOT auto-reused).
        int next = GetNextIdCursor(parentPath);
        if (next > MaxSiblingId)
        {
            int retired = GetRetiredCount(parentPath);
            if (retired > 0)
            {
                error =
                    $"同级 GameplayTag 已满（{MaxSiblingId}/{MaxSiblingId}）。" +
                    $"父节点: '{FormatParent(parentPath)}'。" +
                    $"有 {retired} 个已删除 Id 待回收，请打开「Recycle Sibling IDs」回收后再添加。";
            }
            else
            {
                error =
                    $"同级 GameplayTag 已满（{MaxSiblingId}/{MaxSiblingId}）。" +
                    $"父节点: '{FormatParent(parentPath)}'。" +
                    "没有可回收的弃用 Id；请拆分子层级，或使用其它 Domain。";
            }

            siblingId = 0;
            return false;
        }

        while (next <= MaxSiblingId && IsSiblingIdTaken(parentPath, next, ignorePath: null))
            next++;

        if (next > MaxSiblingId)
        {
            int retired = GetRetiredCount(parentPath);
            error =
                $"同级 GameplayTag 已满（{MaxSiblingId}/{MaxSiblingId}）。" +
                $"父节点: '{FormatParent(parentPath)}'。" +
                (retired > 0
                    ? $"有 {retired} 个已删除 Id 待回收，请打开「Recycle Sibling IDs」。"
                    : "没有可用 siblingId。");
            siblingId = 0;
            return false;
        }

        siblingId = next;
        SetNextIdCursor(parentPath, next + 1);
        return true;
    }

    private bool TryTakeRecycledId(string parentPath, out int siblingId)
    {
        siblingId = 0;
        int bestIndex = -1;
        int bestId = int.MaxValue;

        for (int i = 0; i < recycledPool.Count; i++)
        {
            var item = recycledPool[i];
            if (!string.Equals(item.parentPath, parentPath, StringComparison.Ordinal))
                continue;

            if (item.siblingId < 1 || item.siblingId > MaxSiblingId)
                continue;

            if (IsSiblingIdTaken(parentPath, item.siblingId, ignorePath: null))
                continue;

            if (item.siblingId < bestId)
            {
                bestId = item.siblingId;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
            return false;

        siblingId = bestId;
        recycledPool.RemoveAt(bestIndex);
        return true;
    }

    private bool IsSiblingIdTaken(string parentPath, int siblingId, string ignorePath)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (ignorePath != null && string.Equals(e.path, ignorePath, StringComparison.Ordinal))
                continue;

            if (GetParentPath(e.path) == parentPath && e.siblingId == siblingId)
                return true;
        }

        return false;
    }

    private bool TryEncodeExistingPath(string path, out uint value, out uint mask)
    {
        value = 0;
        mask = 0;

        if (!IsValidTagPath(path, out _))
            return false;

        var parts = path.Split('.');
        string fullPath = string.Empty;
        for (int i = 0; i < parts.Length; i++)
        {
            fullPath = i == 0 ? parts[i] : fullPath + "." + parts[i];
            if (!TryGetSiblingId(fullPath, out int siblingId))
                return false;

            GameplayTagEncoding.EncodeSibling(ref value, ref mask, siblingId, i + 1);
        }

        return true;
    }

    private bool TryEncodeSiblingSlot(string parentPath, int siblingId, out uint value, out uint mask)
    {
        value = 0;
        mask = 0;

        if (siblingId < 1 || siblingId > MaxSiblingId)
            return false;

        parentPath = NormalizeTag(parentPath ?? string.Empty);
        int depth = 1;
        if (!string.IsNullOrEmpty(parentPath))
        {
            if (!TryEncodeExistingPath(parentPath, out value, out mask))
                return false;

            depth = parentPath.Split('.').Length + 1;
            if (depth > MaxDepth)
                return false;
        }

        GameplayTagEncoding.EncodeSibling(ref value, ref mask, siblingId, depth);
        return true;
    }

    private int CountLiveSiblings(string parentPath)
    {
        int live = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (GetParentPath(entries[i].path) == parentPath)
                live++;
        }

        return live;
    }

    private int GetNextIdCursor(string parentPath)
    {
        parentPath = NormalizeTag(parentPath ?? string.Empty);

        for (int i = 0; i < parentCursors.Count; i++)
        {
            if (string.Equals(parentCursors[i].parentPath, parentPath, StringComparison.Ordinal))
                return Mathf.Clamp(parentCursors[i].nextId, 1, MaxSiblingId + 1);
        }

        int maxId = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (GetParentPath(entries[i].path) == parentPath)
                maxId = Mathf.Max(maxId, entries[i].siblingId);
        }

        int derived = maxId + 1;
        SetNextIdCursor(parentPath, derived);
        return derived;
    }

    private void AdvanceCursorAtLeast(string parentPath, int nextId)
    {
        parentPath = NormalizeTag(parentPath ?? string.Empty);
        int current = GetNextIdCursor(parentPath);
        if (nextId > current)
            SetNextIdCursor(parentPath, nextId);
    }

    private void SetNextIdCursor(string parentPath, int nextId)
    {
        parentPath = NormalizeTag(parentPath ?? string.Empty);
        nextId = Mathf.Clamp(nextId, 1, MaxSiblingId + 1);

        for (int i = 0; i < parentCursors.Count; i++)
        {
            if (string.Equals(parentCursors[i].parentPath, parentPath, StringComparison.Ordinal))
            {
                parentCursors[i].nextId = nextId;
                return;
            }
        }

        parentCursors.Add(new GameplayTagParentCursor
        {
            parentPath = parentPath,
            nextId = nextId
        });
    }

    private void UpsertRetired(string parentPath, int siblingId, string lastPath, uint encodedValue, uint encodedMask)
    {
        parentPath = NormalizeTag(parentPath ?? string.Empty);

        for (int i = 0; i < retiredIds.Count; i++)
        {
            if (string.Equals(retiredIds[i].parentPath, parentPath, StringComparison.Ordinal) &&
                retiredIds[i].siblingId == siblingId)
            {
                retiredIds[i].lastPath = lastPath ?? string.Empty;
                retiredIds[i].encodedValue = unchecked((int)encodedValue);
                retiredIds[i].encodedMask = unchecked((int)encodedMask);
                return;
            }
        }

        retiredIds.Add(new GameplayTagRetiredId
        {
            parentPath = parentPath,
            siblingId = siblingId,
            lastPath = lastPath ?? string.Empty,
            encodedValue = unchecked((int)encodedValue),
            encodedMask = unchecked((int)encodedMask)
        });

        SortRetired();
    }

    private bool RemoveRetired(string parentPath, int siblingId)
    {
        parentPath = NormalizeTag(parentPath ?? string.Empty);
        int removed = retiredIds.RemoveAll(r =>
            string.Equals(r.parentPath, parentPath, StringComparison.Ordinal) &&
            r.siblingId == siblingId);
        return removed > 0;
    }

    private bool ContainsRetired(string parentPath, int siblingId)
    {
        parentPath = NormalizeTag(parentPath ?? string.Empty);
        for (int i = 0; i < retiredIds.Count; i++)
        {
            if (string.Equals(retiredIds[i].parentPath, parentPath, StringComparison.Ordinal) &&
                retiredIds[i].siblingId == siblingId)
            {
                return true;
            }
        }

        return false;
    }

    private bool RemoveRecycled(string parentPath, int siblingId)
    {
        parentPath = NormalizeTag(parentPath ?? string.Empty);
        int removed = recycledPool.RemoveAll(r =>
            string.Equals(r.parentPath, parentPath, StringComparison.Ordinal) &&
            r.siblingId == siblingId);
        return removed > 0;
    }

    private bool ContainsRecycled(string parentPath, int siblingId)
    {
        parentPath = NormalizeTag(parentPath ?? string.Empty);
        for (int i = 0; i < recycledPool.Count; i++)
        {
            if (string.Equals(recycledPool[i].parentPath, parentPath, StringComparison.Ordinal) &&
                recycledPool[i].siblingId == siblingId)
            {
                return true;
            }
        }

        return false;
    }

    private void PruneSlotRecordsForMissingParents()
    {
        // Keep retired/recycled even if parent tag path is gone (parent may be root or intermediate hole).
        // Only drop invalid ids.
        retiredIds.RemoveAll(r => r.siblingId < 1 || r.siblingId > MaxSiblingId);
        recycledPool.RemoveAll(r => r.siblingId < 1 || r.siblingId > MaxSiblingId);
    }

    private void RemapParentKey(string oldPath, string newPath)
    {
        for (int i = 0; i < parentCursors.Count; i++)
        {
            string cursorParent = parentCursors[i].parentPath ?? string.Empty;
            if (string.Equals(cursorParent, oldPath, StringComparison.Ordinal))
                parentCursors[i].parentPath = newPath;
            else if (cursorParent.StartsWith(oldPath + ".", StringComparison.Ordinal))
                parentCursors[i].parentPath = newPath + cursorParent.Substring(oldPath.Length);
        }

        for (int i = 0; i < retiredIds.Count; i++)
        {
            string p = retiredIds[i].parentPath ?? string.Empty;
            if (string.Equals(p, oldPath, StringComparison.Ordinal))
                retiredIds[i].parentPath = newPath;
            else if (p.StartsWith(oldPath + ".", StringComparison.Ordinal))
                retiredIds[i].parentPath = newPath + p.Substring(oldPath.Length);

            string last = retiredIds[i].lastPath ?? string.Empty;
            if (string.Equals(last, oldPath, StringComparison.Ordinal))
                retiredIds[i].lastPath = newPath;
            else if (last.StartsWith(oldPath + ".", StringComparison.Ordinal))
                retiredIds[i].lastPath = newPath + last.Substring(oldPath.Length);
        }

        for (int i = 0; i < recycledPool.Count; i++)
        {
            string p = recycledPool[i].parentPath ?? string.Empty;
            if (string.Equals(p, oldPath, StringComparison.Ordinal))
                recycledPool[i].parentPath = newPath;
            else if (p.StartsWith(oldPath + ".", StringComparison.Ordinal))
                recycledPool[i].parentPath = newPath + p.Substring(oldPath.Length);
        }
    }

    private void SortRetired()
    {
        retiredIds.Sort((a, b) =>
        {
            int cmp = string.CompareOrdinal(a.parentPath, b.parentPath);
            return cmp != 0 ? cmp : a.siblingId.CompareTo(b.siblingId);
        });
    }

    private void SortRecycledPool()
    {
        recycledPool.Sort((a, b) =>
        {
            int cmp = string.CompareOrdinal(a.parentPath, b.parentPath);
            return cmp != 0 ? cmp : a.siblingId.CompareTo(b.siblingId);
        });
    }

    private static int CompareSlots(GameplayTagSiblingSlotInfo a, GameplayTagSiblingSlotInfo b)
    {
        int cmp = string.CompareOrdinal(a.ParentPath, b.ParentPath);
        return cmp != 0 ? cmp : a.SiblingId.CompareTo(b.SiblingId);
    }

    private int IndexOf(string tag)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (string.Equals(entries[i].path, tag, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    private void Sort()
    {
        entries.Sort((a, b) => string.CompareOrdinal(a.path, b.path));
    }

    public static string GetParentPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        int index = path.LastIndexOf('.');
        return index > 0 ? path.Substring(0, index) : string.Empty;
    }

    public static string FormatParent(string parentPath)
    {
        return string.IsNullOrEmpty(parentPath) ? "<root>" : parentPath;
    }

    private static string NormalizeTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return string.Empty;

        var parts = tag.Split('.');
        for (int i = 0; i < parts.Length; i++)
            parts[i] = parts[i].Trim();

        return string.Join(".", parts);
    }

    private static bool IsSameOrChild(string parent, string path)
    {
        return string.Equals(parent, path, StringComparison.Ordinal)
               || path.StartsWith(parent + ".", StringComparison.Ordinal);
    }

    public static bool IsValidTagPath(string tag, out string error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(tag))
        {
            error = "Tag 不能为空";
            return false;
        }

        var parts = tag.Split('.');

        if (parts.Length > MaxDepth)
        {
            error = $"当前 uint 编码最多支持 {MaxDepth} 层";
            return false;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            if (!IsValidTagSegment(parts[i], out error))
                return false;
        }

        return true;
    }

    private static bool IsValidTagSegment(string segment, out string error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(segment))
        {
            error = "节点名不能为空";
            return false;
        }

        if (!(char.IsLetter(segment[0]) || segment[0] == '_'))
        {
            error = "节点名必须以字母或下划线开头";
            return false;
        }

        for (int i = 1; i < segment.Length; i++)
        {
            char c = segment[i];
            if (!(char.IsLetterOrDigit(c) || c == '_'))
            {
                error = "节点名只能包含字母、数字、下划线";
                return false;
            }
        }

        return true;
    }

    private void OnEnable()
    {
        EnsureMigrated();
    }

    private void OnValidate()
    {
        EnsureMigrated();
    }
}
