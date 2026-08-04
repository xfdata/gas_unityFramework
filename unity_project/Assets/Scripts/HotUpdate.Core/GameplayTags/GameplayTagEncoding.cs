/// <summary>
/// Single source of truth for GameplayTag bit encoding.
///
/// 32-bit value/mask layout: each level occupies one byte, up to <see cref="MaxLevels"/> levels.
/// Level 1 (root) sits at the highest byte (shift <see cref="FirstLevelShift"/>),
/// each subsequent level shifts down by <see cref="BitsPerLevel"/>.
///
/// 64-bit lookup key layout: high 32 bits = Domain, low 32 bits = value.
///
/// All runtime (container counts, debug paths) and editor (code generation, reference
/// scanning, legacy fixup) encoding math must go through this class. Never hand-write
/// shift / mask literals in other files.
/// </summary>
public static class GameplayTagEncoding
{
    /// <summary>Bits per level in the 32-bit value.</summary>
    public const int BitsPerLevel = 8;

    /// <summary>Mask for a single level byte.</summary>
    public const uint LevelMask = 0xFFu;

    /// <summary>Mask that preserves higher-level bytes when walking the ancestor chain.</summary>
    public const uint AncestorMask = 0xFFFFFF00u;

    /// <summary>Shift of the first (root) level byte.</summary>
    public const int FirstLevelShift = 24;

    /// <summary>Maximum number of levels (constrained by a 32-bit value).</summary>
    public const int MaxLevels = 4;

    /// <summary>
    /// Returns the bit shift for level <paramref name="depth"/> (1-based; root = 1).
    /// </summary>
    public static int GetLevelShift(int depth)
    {
        return FirstLevelShift - (depth - 1) * BitsPerLevel;
    }

    /// <summary>Returns the full byte mask for level <paramref name="depth"/> (already shifted).</summary>
    public static uint GetLevelByteMask(int depth)
    {
        return LevelMask << GetLevelShift(depth);
    }

    /// <summary>Extracts the siblingId from <paramref name="value"/> at level <paramref name="depth"/>.</summary>
    public static int GetSiblingId(uint value, int depth)
    {
        return (int)((value >> GetLevelShift(depth)) & LevelMask);
    }

    /// <summary>
    /// Encodes <paramref name="siblingId"/> into <paramref name="value"/> / <paramref name="mask"/>
    /// at level <paramref name="depth"/>.
    /// </summary>
    public static void EncodeSibling(ref uint value, ref uint mask, int siblingId, int depth)
    {
        int shift = GetLevelShift(depth);
        value |= ((uint)siblingId & LevelMask) << shift;
        mask |= LevelMask << shift;
    }

    /// <summary>64-bit lookup key: Domain (high 32 bits) | value (low 32 bits).</summary>
    public static ulong MakeDomainValueKey(GameplayTagDomain domain, uint value)
    {
        return ((ulong)(byte)domain << 32) | value;
    }

    /// <summary>64-bit lookup key: value (high 32 bits) | mask (low 32 bits).</summary>
    public static ulong MakeValueMaskKey(uint value, uint mask)
    {
        return ((ulong)value << 32) | mask;
    }
}
