using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public enum TagQueryOp : byte
{
    All,
    Any,
    NotAll,
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

            case TagQueryOp.NotAll:
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

            case TagQueryOp.NotAll:
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
