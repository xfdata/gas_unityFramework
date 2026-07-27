using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// How a TagQuery evaluates its node list against a tag or container.
/// Serialized values: All=0, Any=1, None=2 (formerly misnamed NotAll=2).
/// </summary>
public enum TagQueryOp : byte
{
    /// <summary>Every listed tag must match / be present.</summary>
    All = 0,

    /// <summary>At least one listed tag must match / be present.</summary>
    Any = 1,

    /// <summary>
    /// None of the listed tags may match / be present.
    /// Typical for BlockedTags: fail if any blocked tag exists.
    /// (Previously misnamed <c>NotAll</c>; serialized value remains 2.)
    /// </summary>
    None = 2,
}

[Serializable]
public class TagQuery
{
    [SerializeField]
    [LabelText("Operation")]
    [EnumToggleButtons]
    private TagQueryOp operation = TagQueryOp.All;

    [SerializeField]
    [LabelText("Tags")]
    [ListDrawerSettings(
        Expanded = true,
        DraggableItems = false,
        ShowPaging = false
    )]
    private List<GameplayTag> nodes = new();

    public TagQueryOp Operation => operation;
    public IReadOnlyList<GameplayTag> Nodes => nodes;

    public TagQuery()
    {
    }

    public TagQuery(TagQueryOp operation)
    {
        this.operation = operation;
    }

    public TagQuery(GameplayTag[] nodes, TagQueryOp operation = TagQueryOp.All)
    {
        this.operation = operation;
        this.nodes = nodes != null
            ? new List<GameplayTag>(nodes)
            : new List<GameplayTag>();
    }

    public bool Match(GameplayTagContainer container)
    {
        return Match(container, operation);
    }

    public bool Match(GameplayTag tag)
    {
        if (nodes == null || nodes.Count == 0)
            return true;

        if (!tag.IsValid)
            return false;

        switch (operation)
        {
            case TagQueryOp.All:
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (!tag.Matches(nodes[i]))
                        return false;
                }

                return true;
            }

            case TagQueryOp.Any:
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (tag.Matches(nodes[i]))
                        return true;
                }

                return false;
            }

            case TagQueryOp.None:
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (tag.Matches(nodes[i]))
                        return false;
                }

                return true;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
        }
    }

    public bool Match(GameplayTagContainer container, TagQueryOp oper)
    {
        if (nodes == null || nodes.Count == 0)
            return true;

        if (container == null)
            return false;

        switch (oper)
        {
            case TagQueryOp.All:
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (!container.HasTag(nodes[i]))
                        return false;
                }

                return true;
            }

            case TagQueryOp.Any:
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (container.HasTag(nodes[i]))
                        return true;
                }

                return false;
            }

            case TagQueryOp.None:
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (container.HasTag(nodes[i]))
                        return false;
                }

                return true;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(oper), oper, null);
        }
    }
}
