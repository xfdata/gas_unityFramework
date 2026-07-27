using System.Collections.Generic;
using BattleCommon;
using BattleFoundation;

namespace TowerDefense
{
    /// <summary>
    /// Tower-defense adapter for the shared lowest-HP combat target strategy.
    /// </summary>
    public sealed class LowestHPStrategy : ITargetingStrategy
    {
        private readonly LowestHpCombatTargetStrategy _inner = new LowestHpCombatTargetStrategy();

        public string StrategyName => _inner.StrategyName;

        public TDEnemyActor FindBestTarget(IReadOnlyList<BattleEntity> enemies, BattleEntity owner, float rangeSqr)
        {
            return _inner.FindBestTarget(enemies, owner, rangeSqr) as TDEnemyActor;
        }
    }
}
