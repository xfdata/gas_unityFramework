using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[Serializable]
public class GameplayTagContainer : ISerializationCallbackReceiver
{
    public class TagEventListener
    {
        public GameplayTag Tag;
        public Action<bool> Callback;
    }

    [SerializeField]
    [LabelText("Tags")]
    [ListDrawerSettings(
        Expanded = true,
        DraggableItems = false,
        ShowPaging = false
    )]
    private List<GameplayTag> tags = new();

    [NonSerialized]
    private Dictionary<ulong, int> exactTagCount;

    [NonSerialized]
    private Dictionary<ulong, int> matchedTagCount;

    /// <summary>Maps exact tag key -> index in <see cref="tags"/>.</summary>
    [NonSerialized]
    private Dictionary<ulong, int> serializedIndex;

    [NonSerialized]
    private List<TagEventListener> listeners;

    [NonSerialized]
    private List<GameplayTag> scratchTags;

    [NonSerialized]
    private int notifyDepth;

    public IReadOnlyList<GameplayTag> Tags => tags;

    public int Count
    {
        get
        {
            EnsureRuntime();
            return tags.Count;
        }
    }

    public GameplayTagContainer()
    {
        EnsureRuntime();
    }

    public GameplayTagContainer(GameplayTag[] initTags)
    {
        EnsureRuntime();

        if (initTags == null)
            return;

        for (int i = 0; i < initTags.Length; i++)
        {
            AddTag(initTags[i]);
        }
    }

    public void AddTag(GameplayTag tag)
    {
        EnsureRuntime();

        if (!IsValidTag(tag))
            return;

        ulong key = MakeCountKey(tag);

        if (exactTagCount.TryGetValue(key, out int count))
        {
            exactTagCount[key] = count + 1;
            return;
        }

        exactTagCount[key] = 1;

        if (!serializedIndex.ContainsKey(key))
        {
            serializedIndex[key] = tags.Count;
            tags.Add(tag);
        }

        UpdateHierarchyTagCounts(tag, 1, true);
    }

    /// <summary>
    /// Decrements the exact-tag stack count by 1. When count reaches 0, removes the tag and hierarchy matches.
    /// </summary>
    public void RemoveTag(GameplayTag tag)
    {
        RemoveTagInternal(tag, removeAllStacks: false);
    }

    /// <summary>
    /// Removes every stack of the exact tag (count goes to 0 in one call).
    /// </summary>
    public void RemoveTagCompletely(GameplayTag tag)
    {
        RemoveTagInternal(tag, removeAllStacks: true);
    }

    /// <summary>
    /// Removes tags matching <paramref name="tag"/>.
    /// </summary>
    /// <param name="tag">Exact tag, or parent when <paramref name="includeChildren"/> is true.</param>
    /// <param name="includeChildren">Also remove exact tags that are children of <paramref name="tag"/>.</param>
    /// <param name="removeAllStacks">
    /// If true, clear all stacks of each matched exact tag.
    /// If false, decrement each matched exact tag by 1 stack.
    /// </param>
    public void RemoveTag(GameplayTag tag, bool includeChildren, bool removeAllStacks = false)
    {
        EnsureRuntime();

        if (!IsValidTag(tag))
            return;

        if (!includeChildren)
        {
            RemoveTagInternal(tag, removeAllStacks);
            return;
        }

        CollectMatchingExactTags(tag, scratchTags);

        for (int i = 0; i < scratchTags.Count; i++)
        {
            RemoveTagInternal(scratchTags[i], removeAllStacks);
        }

        scratchTags.Clear();
    }

    /// <summary>
    /// Convenience: remove all stacks under a parent (or exact if no children).
    /// Equivalent to <c>RemoveTag(tag, includeChildren: true, removeAllStacks: true)</c>.
    /// </summary>
    public void RemoveMatching(GameplayTag tag)
    {
        RemoveTag(tag, includeChildren: true, removeAllStacks: true);
    }

    public bool HasTag(GameplayTag query)
    {
        EnsureRuntime();

        if (!IsValidTag(query))
            return false;

        return matchedTagCount.TryGetValue(MakeCountKey(query), out int count) && count > 0;
    }

    /// <summary>Exact stack count for this tag (not hierarchy).</summary>
    public int GetTagCount(GameplayTag tag)
    {
        EnsureRuntime();

        if (!IsValidTag(tag))
            return 0;

        return exactTagCount.TryGetValue(MakeCountKey(tag), out int count) ? count : 0;
    }

    public bool Match(GameplayTagContainer container, TagQueryOp oper = TagQueryOp.All)
    {
        EnsureRuntime();

        // 空容器视为匹配所有（与 TagQuery 行为一致）
        if (tags == null || tags.Count == 0)
            return true;

        if (container == null)
            return false;

        switch (oper)
        {
            case TagQueryOp.All:
            {
                for (int i = 0; i < tags.Count; i++)
                {
                    if (!container.HasTag(tags[i]))
                        return false;
                }

                return true;
            }

            case TagQueryOp.Any:
            {
                for (int i = 0; i < tags.Count; i++)
                {
                    if (container.HasTag(tags[i]))
                        return true;
                }

                return false;
            }

            case TagQueryOp.None:
            {
                for (int i = 0; i < tags.Count; i++)
                {
                    if (container.HasTag(tags[i]))
                        return false;
                }

                return true;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(oper), oper, null);
        }
    }

    public void Clear()
    {
        EnsureRuntime();

        if (tags.Count == 0)
        {
            exactTagCount.Clear();
            matchedTagCount.Clear();
            serializedIndex.Clear();
            // Keep listeners; clear does not unregister.
            return;
        }

        // Remove from end to avoid index churn; notify hierarchy with full stack delta.
        for (int i = tags.Count - 1; i >= 0; i--)
        {
            var tag = tags[i];
            ulong key = MakeCountKey(tag);
            int count = exactTagCount.TryGetValue(key, out int tagCount) ? tagCount : 1;

            exactTagCount.Remove(key);
            serializedIndex.Remove(key);
            tags.RemoveAt(i);

            if (count > 0)
                UpdateHierarchyTagCounts(tag, -count, true);
        }

        exactTagCount.Clear();
        matchedTagCount.Clear();
        serializedIndex.Clear();
    }

    public void RegisterListener(GameplayTag tag, Action<bool> callback)
    {
        EnsureRuntime();

        if (callback == null)
            return;

        listeners.Add(new TagEventListener
        {
            Tag = tag,
            Callback = callback
        });
    }

    public void UnregisterListener(Action<bool> callback)
    {
        EnsureRuntime();

        if (callback == null || listeners.Count == 0)
            return;

        if (notifyDepth > 0)
        {
            for (int i = 0; i < listeners.Count; i++)
            {
                if (listeners[i].Callback == callback)
                    listeners[i].Callback = null;
            }

            return;
        }

        listeners.RemoveAll(l => l.Callback == callback || l.Callback == null);
    }

    public void UnregisterListener(GameplayTag tag, Action<bool> callback)
    {
        EnsureRuntime();

        if (callback == null || listeners.Count == 0)
            return;

        if (notifyDepth > 0)
        {
            for (int i = 0; i < listeners.Count; i++)
            {
                var listener = listeners[i];
                if (listener.Callback == callback && listener.Tag.Equals(tag))
                    listeners[i].Callback = null;
            }

            return;
        }

        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            var listener = listeners[i];
            if (listener.Callback == callback && listener.Tag.Equals(tag))
                listeners.RemoveAt(i);
        }
    }

    public void OnBeforeSerialize()
    {
    }

    public void OnAfterDeserialize()
    {
        RebuildRuntime();
    }

    private void EnsureRuntime()
    {
        if (exactTagCount != null &&
            matchedTagCount != null &&
            serializedIndex != null &&
            listeners != null &&
            scratchTags != null)
        {
            return;
        }

        RebuildRuntime();
    }

    private void RebuildRuntime()
    {
        int cap = tags != null ? Math.Max(4, tags.Count * 2) : 4;
        exactTagCount = new Dictionary<ulong, int>(cap);
        matchedTagCount = new Dictionary<ulong, int>(cap);
        serializedIndex = new Dictionary<ulong, int>(cap);
        listeners ??= new List<TagEventListener>();
        scratchTags ??= new List<GameplayTag>(8);

        if (tags == null)
            tags = new List<GameplayTag>();

        // Single pass: drop invalid + dedupe (keep first) using serializedIndex as seen-set.
        for (int i = tags.Count - 1; i >= 0; i--)
        {
            if (!IsValidTag(tags[i]))
            {
                tags.RemoveAt(i);
                continue;
            }
        }

        serializedIndex.Clear();
        for (int i = 0; i < tags.Count;)
        {
            ulong key = MakeCountKey(tags[i]);
            if (serializedIndex.ContainsKey(key))
            {
                tags.RemoveAt(i);
                continue;
            }

            serializedIndex[key] = i;
            i++;
        }

        exactTagCount.Clear();
        matchedTagCount.Clear();

        for (int i = 0; i < tags.Count; i++)
        {
            var tag = tags[i];
            ulong key = MakeCountKey(tag);

            exactTagCount[key] = 1;
            serializedIndex[key] = i;

            UpdateHierarchyTagCounts(tag, 1, false);
        }
    }

    private void RemoveTagInternal(GameplayTag tag, bool removeAllStacks)
    {
        EnsureRuntime();

        if (!IsValidTag(tag))
            return;

        ulong key = MakeCountKey(tag);

        if (!exactTagCount.TryGetValue(key, out int count))
            return;

        if (!removeAllStacks && count > 1)
        {
            exactTagCount[key] = count - 1;
            return;
        }

        int delta = removeAllStacks ? count : 1;
        exactTagCount.Remove(key);
        RemoveSerializedTagByKey(key);
        UpdateHierarchyTagCounts(tag, -delta, true);
    }

    private void CollectMatchingExactTags(GameplayTag parent, List<GameplayTag> results)
    {
        results.Clear();

        // Prefer iterating compact serialized tags list (unique exact tags).
        for (int i = 0; i < tags.Count; i++)
        {
            var exact = tags[i];
            if (exact.Domain != parent.Domain)
                continue;

            if ((exact.Value & parent.Mask) == parent.Value)
                results.Add(exact);
        }
    }

    private void RemoveSerializedTagByKey(ulong key)
    {
        if (!serializedIndex.TryGetValue(key, out int index))
        {
            // Fallback linear scan for safety.
            for (int i = tags.Count - 1; i >= 0; i--)
            {
                if (MakeCountKey(tags[i]) == key)
                {
                    RemoveSerializedAt(i);
                    return;
                }
            }

            return;
        }

        RemoveSerializedAt(index);
    }

    private void RemoveSerializedAt(int index)
    {
        if (index < 0 || index >= tags.Count)
            return;

        ulong removedKey = MakeCountKey(tags[index]);
        int last = tags.Count - 1;

        if (index != last)
        {
            var moved = tags[last];
            tags[index] = moved;
            serializedIndex[MakeCountKey(moved)] = index;
        }

        tags.RemoveAt(last);
        serializedIndex.Remove(removedKey);
    }

    private static bool IsValidTag(GameplayTag tag)
    {
        return tag.IsValid;
    }

    private void UpdateHierarchyTagCounts(GameplayTag tag, int delta, bool notify)
    {
        uint mask = tag.Mask;

        while (mask != 0)
        {
            UpdateMatchedTagCount(
                new GameplayTag(tag.Domain, tag.Value & mask, mask),
                delta,
                notify);

            mask <<= 8;
            mask &= 0xFFFFFF00u;
        }
    }

    private void UpdateMatchedTagCount(GameplayTag tag, int delta, bool notify)
    {
        ulong key = MakeCountKey(tag);
        matchedTagCount.TryGetValue(key, out int oldCount);

        int newCount = oldCount + delta;

        if (newCount <= 0)
        {
            newCount = 0;
            matchedTagCount.Remove(key);
        }
        else
        {
            matchedTagCount[key] = newCount;
        }

        if (!notify)
            return;

        if (oldCount == 0 && newCount > 0)
        {
            NotifyTagChanged(tag, true);
        }
        else if (oldCount > 0 && newCount == 0)
        {
            NotifyTagChanged(tag, false);
        }
    }

    private void NotifyTagChanged(GameplayTag changedTag, bool added)
    {
        if (listeners == null || listeners.Count == 0)
            return;

        notifyDepth++;

        try
        {
            // No snapshot allocation; null callbacks are skipped (unregistered mid-notify).
            for (int i = 0; i < listeners.Count; i++)
            {
                var listener = listeners[i];
                if (listener.Callback == null)
                    continue;

                if (listener.Tag.Equals(changedTag))
                {
                    listener.Callback.Invoke(added);
                }
            }
        }
        finally
        {
            notifyDepth--;
            if (notifyDepth == 0)
                CompactListeners();
        }
    }

    private void CompactListeners()
    {
        if (listeners == null || listeners.Count == 0)
            return;

        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            if (listeners[i].Callback == null)
                listeners.RemoveAt(i);
        }
    }

    public void AddTags(GameplayTagContainer other)
    {
        EnsureRuntime();

        if (other == null)
            return;

        other.EnsureRuntime();

        var source = other.tags;
        for (int i = 0; i < source.Count; i++)
        {
            AddTag(source[i]);
        }
    }

    /// <summary>
    /// Decrements each tag from <paramref name="other"/> by one stack.
    /// </summary>
    public void RemoveTags(GameplayTagContainer other)
    {
        RemoveTags(other, removeAllStacks: false);
    }

    /// <summary>
    /// Removes tags present in <paramref name="other"/>.
    /// </summary>
    public void RemoveTags(GameplayTagContainer other, bool removeAllStacks)
    {
        EnsureRuntime();

        if (other == null)
            return;

        other.EnsureRuntime();

        var source = other.tags;
        for (int i = 0; i < source.Count; i++)
        {
            RemoveTagInternal(source[i], removeAllStacks);
        }
    }

    private static ulong MakeCountKey(GameplayTag tag)
    {
        return MakeCountKey(tag.Domain, tag.Value);
    }

    private static ulong MakeCountKey(GameplayTagDomain domain, uint value)
    {
        return ((ulong)(byte)domain << 32) | value;
    }
}
