using System;
using BattleFoundation;
using GAS;

namespace BattleCommon
{
    /// <summary>
    /// Adapts the battle simulation random stream to the narrower GAS contract.
    /// </summary>
    internal sealed class BattleGameplayRandomAdapter : IGameplayRandom
    {
        private readonly IRandom random;

        public BattleGameplayRandomAdapter(IRandom random)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public int Next(int maxValue)
        {
            return random.Range(maxValue);
        }

        public int Next(int minValue, int maxValue)
        {
            return random.Range(minValue, maxValue);
        }

        public double NextDouble()
        {
            return random.Value;
        }
    }
}
