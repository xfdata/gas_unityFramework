using BattleFoundation;

namespace BattleCommon
{
    /// <summary>
    /// Battle-owned presentation data passed through generic GAS events unchanged.
    /// </summary>
    internal sealed class CombatEffectPresentationContext
    {
        public readonly Float3 Position;

        public CombatEffectPresentationContext(in Float3 position)
        {
            Position = position;
        }
    }
}
