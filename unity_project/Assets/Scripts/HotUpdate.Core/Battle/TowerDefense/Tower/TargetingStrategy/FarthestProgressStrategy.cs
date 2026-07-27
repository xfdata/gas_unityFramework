using System.Collections.Generic;
using BattleCommon;
using BattleFoundation;

namespace TowerDefense
{
    /// <summary>
    /// 最远进度策略 —— 选择距离主城最近的敌人（最大威胁）。
    /// 原使用 PathFollower.Progress01，现改为基于距离计算。
    /// </summary>
    public sealed class FarthestProgressStrategy : ITargetingStrategy, ICombatTargetStrategy
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
                var progressTarget = tdEnemy as ICombatProgressTarget;
                if (progressTarget == null) continue;

                float distSqr = (tdEnemy.Position - owner.Position).sqrMagnitude;
                if (distSqr > rangeSqr) continue;

                // 距离主城越近 = 进度越大（用距离倒数近似）
                float progress = progressTarget.Progress;
                if (progress > bestProgress)
                {
                    bestProgress = progress;
                    best = tdEnemy;
                }
            }

            return best;
        }

        CombatActor ICombatTargetStrategy.FindBestTarget(
            IReadOnlyList<BattleEntity> candidates,
            BattleEntity owner,
            float rangeSqr)
        {
            return FindBestTarget(candidates, owner, rangeSqr);
        }
    }
}
