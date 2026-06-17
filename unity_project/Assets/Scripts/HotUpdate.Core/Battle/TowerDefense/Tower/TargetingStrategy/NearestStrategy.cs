using System.Collections.Generic;
using BattleFoundation;

namespace TowerDefense
{
    /// <summary>
    /// 最近目标策略 —— 选择距离防御塔最近的敌人。
    /// </summary>
    public sealed class NearestStrategy : ITargetingStrategy
    {
        public string StrategyName => "Nearest";

        public TDEnemyActor FindBestTarget(IReadOnlyList<BattleEntity> enemies, BattleEntity owner, float rangeSqr)
        {
            TDEnemyActor best = null;
            float bestDistSqr = float.MaxValue;

            for (int i = 0; i < enemies.Count; i++)
            {
                var tdEnemy = enemies[i] as TDEnemyActor;
                if (tdEnemy == null || !tdEnemy.IsAlive) continue;

                float distSqr = (tdEnemy.Position - owner.Position).sqrMagnitude;
                if (distSqr > rangeSqr) continue;

                if (distSqr < bestDistSqr)
                {
                    bestDistSqr = distSqr;
                    best = tdEnemy;
                }
            }

            return best;
        }
    }
}
