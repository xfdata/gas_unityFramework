using System;

namespace GAS
{
    public enum AttributeModifierOp : byte
    {
        Add,
        Multiply,
        Override,
    }

    public readonly struct AttributeModifierHandle : IEquatable<AttributeModifierHandle>
    {
        public static readonly AttributeModifierHandle Invalid = new AttributeModifierHandle(0);

        public readonly int Id;

        public bool IsValid => Id != 0;

        public AttributeModifierHandle(int id)
        {
            Id = id;
        }

        public bool Equals(AttributeModifierHandle other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            return obj is AttributeModifierHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id;
        }

        public static bool operator ==(AttributeModifierHandle left, AttributeModifierHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AttributeModifierHandle left, AttributeModifierHandle right)
        {
            return !left.Equals(right);
        }
    }

    public interface IGameplayAttributeOwner
    {
        float GetAttribute(int attributeId);

        void AddAttributeBaseValue(int attributeId, float delta);

        AttributeModifierHandle AddModifier(
            int attributeId,
            AttributeModifierOp op,
            float value,
            object source
        );

        void RemoveModifier(AttributeModifierHandle handle);
    }
}
