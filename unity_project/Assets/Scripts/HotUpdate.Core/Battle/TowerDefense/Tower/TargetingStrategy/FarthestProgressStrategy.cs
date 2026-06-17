using System.Collections.Generic;
using BattleFoundation;

namespace TowerDefense
{
    /// <summary>
    /// 最远进度策略 —— 选择沿路径进度最大（最靠近主城）的敌人。
    /// 利用 PathFollower.Progress01 比较。
    /// </summary>
    public sealed class FarthestProgressStrategy : ITargetingStrategy
    {
        public string StrategyName => "FarthestProgress";

        public TDEnemyActor FindBestTarget(IReadOnlyList<BattleEntity> enemies, BattleEntity owner, float rangeSqr)
        {
            TDEnemyActor best = null;
            float bestProgress = -1f;

            for (int i = 0; i < enemies.Count; i++)
            {
                var tdEnemy = enemies[i] as TDEnemyActor;
                if (tdEnemy == null || !tdEnemy.IsAlive) continue;

                float distSqr = (tdEnemy.Position - owner.Position).sqrMagnitude;
                if (distSqr > rangeSqr) continue;

                float progress = tdEnemy.PathFollower?.Progress01 ?? 0f;
                if (progress > bestProgress)
                {
                    bestProgress = progress;
                    best = tdEnemy;
                }
            }

            return best;
        }
    }
}
