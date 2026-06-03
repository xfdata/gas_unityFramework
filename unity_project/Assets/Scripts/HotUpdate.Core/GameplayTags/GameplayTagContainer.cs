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
    private Dictionary<uint, int> exactTagCount;

    [NonSerialized]
    private Dictionary<uint, int> matchedTagCount;

    [NonSerialized]
    private List<TagEventListener> listeners;

    public IReadOnlyList<GameplayTag> Tags => tags;

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

        if (exactTagCount.TryGetValue(tag.Value, out int count))
        {
            exactTagCount[tag.Value] = count + 1;
            return;
        }

        exactTagCount[tag.Value] = 1;

        if (!ContainsSerializedTag(tag))
            tags.Add(tag);

        UpdateHierarchyTagCounts(tag, 1, true);
    }

    public void RemoveTag(GameplayTag tag)
    {
        EnsureRuntime();

        if (!IsValidTag(tag))
            return;

        if (!exactTagCount.TryGetValue(tag.Value, out int count))
            return;

        if (count <= 1)
        {
            exactTagCount.Remove(tag.Value);
            RemoveSerializedTag(tag);

            UpdateHierarchyTagCounts(tag, -1, true);
        }
        else
        {
            exactTagCount[tag.Value] = count - 1;
        }
    }

    public void RemoveTag(GameplayTag tag, bool includeChild)
    {
        EnsureRuntime();

        if (!IsValidTag(tag))
            return;

        if (!includeChild)
        {
            RemoveTag(tag);
            return;
        }

        var removeTags = new List<GameplayTag>();

        foreach (var value in exactTagCount.Keys)
        {
            if ((value & tag.Mask) == tag.Value)
            {
                var mask = ComputeMask(value);
                removeTags.Add(new GameplayTag(value, mask));
            }
        }

        for (int i = 0; i < removeTags.Count; i++)
        {
            RemoveTag(removeTags[i]);
        }
    }

    public bool HasTag(GameplayTag query)
    {
        EnsureRuntime();

        if (!IsValidTag(query))
            return false;

        return matchedTagCount.TryGetValue(query.Value, out int count) && count > 0;
    }

    public int GetTagCount(GameplayTag tag)
    {
        EnsureRuntime();

        if (!IsValidTag(tag))
            return 0;

        return exactTagCount.TryGetValue(tag.Value, out int count) ? count : 0;
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

            case TagQueryOp.NotAll:
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
            listeners.Clear();
            return;
        }

        var clearTags = new List<GameplayTag>(tags);

        tags.Clear();

        for (int i = 0; i < clearTags.Count; i++)
        {
            var tag = clearTags[i];
            int count = exactTagCount.TryGetValue(tag.Value, out int tagCount)
                ? tagCount
                : 1;

            exactTagCount.Remove(tag.Value);
            UpdateHierarchyTagCounts(tag, -count, true);
        }

        exactTagCount.Clear();
        matchedTagCount.Clear();
        listeners.Clear();
    }

    public void RegisterListener(GameplayTag tag, Action<bool> callback)
    {
        EnsureRuntime();

        listeners.Add(new TagEventListener
        {
            Tag = tag,
            Callback = callback
        });
    }

    public void UnregisterListener(Action<bool> callback)
    {
        EnsureRuntime();

        listeners.RemoveAll(l => l.Callback == callback);
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
            listeners != null)
        {
            return;
        }

        RebuildRuntime();
    }

    private void RebuildRuntime()
    {
        exactTagCount = new Dictionary<uint, int>();
        matchedTagCount = new Dictionary<uint, int>();
        listeners = new List<TagEventListener>();

        if (tags == null)
            tags = new List<GameplayTag>();

        for (int i = tags.Count - 1; i >= 0; i--)
        {
            if (!IsValidTag(tags[i]))
            {
                tags.RemoveAt(i);
            }
        }

        var unique = new HashSet<uint>();

        for (int i = tags.Count - 1; i >= 0; i--)
        {
            if (!unique.Add(tags[i].Value))
            {
                tags.RemoveAt(i);
            }
        }

        for (int i = 0; i < tags.Count; i++)
        {
            var tag = tags[i];

            exactTagCount[tag.Value] = 1;

            UpdateHierarchyTagCounts(tag, 1, false);
        }
    }

    private bool ContainsSerializedTag(GameplayTag tag)
    {
        if (tags == null)
            return false;

        for (int i = 0; i < tags.Count; i++)
        {
            if (tags[i].Equals(tag))
                return true;
        }

        return false;
    }

    private void RemoveSerializedTag(GameplayTag tag)
    {
        if (tags == null)
            return;

        for (int i = tags.Count - 1; i >= 0; i--)
        {
            if (tags[i].Equals(tag))
            {
                tags.RemoveAt(i);
            }
        }
    }

    private static uint ComputeMask(uint value)
    {
        uint mask = 0;
        bool foundNonZero = false;

        for (int i = 0; i < 4; i++)
        {
            uint shift = (uint)(24 - 8 * i);
            uint b = (value >> (int)shift) & 0xFF;

            if (b != 0)
            {
                foundNonZero = true;
                mask |= 0xFFu << (int)shift;
            }
            else if (foundNonZero)
            {
                break;
            }
        }

        return mask;
    }

    private static bool IsValidTag(GameplayTag tag)
    {
        return tag.Mask != 0;
    }

    private void UpdateHierarchyTagCounts(GameplayTag tag, int delta, bool notify)
    {
        uint mask = tag.Mask;

        while (mask != 0)
        {
            UpdateMatchedTagCount(new GameplayTag(tag.Value & mask, mask), delta, notify);

            mask <<= 8;
            mask &= 0xFFFFFF00u;
        }
    }

    private void UpdateMatchedTagCount(GameplayTag tag, int delta, bool notify)
    {
        matchedTagCount.TryGetValue(tag.Value, out int oldCount);

        int newCount = oldCount + delta;

        if (newCount <= 0)
        {
            newCount = 0;
            matchedTagCount.Remove(tag.Value);
        }
        else
        {
            matchedTagCount[tag.Value] = newCount;
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
        if (listeners == null)
            return;

        var snapshot = new List<TagEventListener>(listeners);

        for (int i = 0; i < snapshot.Count; i++)
        {
            var listener = snapshot[i];

            if (listener.Tag.Equals(changedTag))
            {
                listener.Callback?.Invoke(added);
            }
        }
    }

    public void AddTags(GameplayTagContainer tags)
    {
        EnsureRuntime();

        if (tags == null)
            return;

        for (int i = 0; i < tags.Tags.Count; i++)
        {
            AddTag(tags.Tags[i]);
        }
    }

    public void RemoveTags(GameplayTagContainer tags)
    {
        EnsureRuntime();

        if (tags == null)
            return;

        var removeTags = new List<GameplayTag>(tags.Tags);

        for (int i = 0; i < removeTags.Count; i++)
        {
            RemoveTag(removeTags[i]);
        }
    }
}
