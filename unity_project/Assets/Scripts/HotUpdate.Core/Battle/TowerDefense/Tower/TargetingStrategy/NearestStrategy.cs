using System.Collections.Generic;
using BattleCommon;
using BattleFoundation;

namespace TowerDefense
{
    /// <summary>
    /// 最近目标策略 —— 选择距离防御塔最近的敌人。
    /// </summary>
    public sealed class NearestStrategy : ITargetingStrategy
    {
        private readonly NearestCombatTargetStrategy _inner = new NearestCombatTargetStrategy();

        public string StrategyName => _inner.StrategyName;

        public TDEnemyActor FindBestTarget(IReadOnlyList<BattleEntity> enemies, BattleEntity owner, float rangeSqr)
        {
            return _inner.FindBestTarget(enemies, owner, rangeSqr) as TDEnemyActor;
        }
    }
}
