using System;
using System.Runtime.CompilerServices;
using UnityEngine;

// Grants Assembly-CSharp-Editor access to the internal constructor so editor tools
// (code gen, reference scanner, legacy fixup) can build tags. Business code must go
// through generated static fields (e.g. CombatGameplayTags.State_Dead).
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]

[Serializable]
public struct GameplayTag : IEquatable<GameplayTag>
{
    public static readonly GameplayTag None = default;

    [SerializeField, HideInInspector]
    private GameplayTagDomain domain;

    [SerializeField, HideInInspector]
    private int value;

    [SerializeField, HideInInspector]
    private int mask;

    public GameplayTagDomain Domain => domain;
    public uint Value => unchecked((uint)value);
    public uint Mask => unchecked((uint)mask);

    public bool IsValid => Domain != GameplayTagDomain.None && Mask != 0;

    /// <summary>
    /// True when value/mask were serialized before Domain existed (domain=0, mask!=0).
    /// These tags fail <see cref="IsValid"/> and need editor fixup.
    /// </summary>
    public bool IsLegacyMissingDomain => Domain == GameplayTagDomain.None && Mask != 0;

    /// <summary>
    /// For generated code (*Def.gen.cs / GameplayTagCatalog.gen.cs) and editor tools only.
    /// Business code must reference generated static fields; never hand-write value/mask
    /// (see <see cref="GameplayTagEncoding"/>).
    /// </summary>
    internal GameplayTag(GameplayTagDomain domain, uint value, uint mask)
    {
        this.domain = domain;
        this.value = unchecked((int)(value & mask));
        this.mask = unchecked((int)mask);
    }

    public bool Matches(GameplayTag parent)
    {
        if (!IsValid || !parent.IsValid)
            return false;

        if (Domain != parent.Domain)
            return false;

        return (Value & parent.Mask) == parent.Value;
    }

    public bool Equals(GameplayTag other)
    {
        return Domain == other.Domain
               && Value == other.Value
               && Mask == other.Mask;
    }

    public override bool Equals(object obj)
    {
        return obj is GameplayTag other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = (int)domain;
            hash = (hash * 397) ^ value;
            hash = (hash * 397) ^ mask;
            return hash;
        }
    }

    public static bool operator ==(GameplayTag left, GameplayTag right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(GameplayTag left, GameplayTag right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return GameplayTagDebug.GetPath(this);
    }
}
