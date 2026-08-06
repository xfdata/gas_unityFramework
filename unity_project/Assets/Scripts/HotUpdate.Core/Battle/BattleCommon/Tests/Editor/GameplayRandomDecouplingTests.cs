using BattleFoundation;
using GAS;
using NUnit.Framework;

namespace BattleCommon.Tests
{
    [TestFixture]
    public class GameplayRandomDecouplingTests
    {
        [Test]
        public void DefaultRandom_MatchesBattleRandomForSameSeed()
        {
            const int seed = 13579;
            var gasRandom = new DefaultRandom(seed);
            var battleRandom = new BattleRandom(seed);

            for (int i = 0; i < 32; i++)
            {
                Assert.AreEqual(battleRandom.Next(97), gasRandom.Next(97));
                Assert.AreEqual(battleRandom.Next(-11, 29), gasRandom.Next(-11, 29));
                Assert.AreEqual((double)battleRandom.Value, gasRandom.NextDouble());
            }
        }

    }
}
