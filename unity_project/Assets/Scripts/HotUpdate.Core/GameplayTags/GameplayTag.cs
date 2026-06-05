using System;
using UnityEngine;

[Serializable]
public struct GameplayTag : IEquatable<GameplayTag>
{
    public static readonly GameplayTag None = default;

    [SerializeField, HideInInspector]
    private int value;

    [SerializeField, HideInInspector]
    private int mask;

    public uint Value => unchecked((uint)value);
    public uint Mask => unchecked((uint)mask);

    public bool IsValid => Mask != 0;
    public bool IsNone => !IsValid;

    public GameplayTag(uint value, uint mask)
    {
        this.value = unchecked((int)(value & mask));
        this.mask = unchecked((int)mask);
    }

    public bool Matches(GameplayTag parent)
    {
        if (!IsValid || !parent.IsValid)
            return false;

        return (Value & parent.Mask) == parent.Value;
    }

    public bool Equals(GameplayTag other)
    {
        return Value == other.Value && Mask == other.Mask;
    }

    public override bool Equals(object obj)
    {
        return obj is GameplayTag other && Equals(other);
    }

    public override int GetHashCode()
    {
        return unchecked((value * 397) ^ mask);
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
        if (!IsValid)
            return "None";

        return $"0x{Value:X8}/0x{Mask:X8}";
    }
}