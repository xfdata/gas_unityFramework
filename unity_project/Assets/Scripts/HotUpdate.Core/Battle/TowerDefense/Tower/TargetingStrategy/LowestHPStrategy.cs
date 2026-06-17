using System.Collections.Generic;
using BattleFoundation;
using BattleCommon;

namespace TowerDefense
{
    /// <summary>
    /// 血量最低策略 —— 选择范围内当前 HP 最低的敌人。
    /// 通过 CombatAttributeComponent.HP 获取血量值。
    /// </summary>
    public sealed class LowestHPStrategy : ITargetingStrategy
    {
        public string StrategyName => "LowestHP";

        public TDEnemyActor FindBestTarget(IReadOnlyList<BattleEntity> enemies, BattleEntity owner, float rangeSqr)
        {
            TDEnemyActor best = null;
            float lowestHP = float.MaxValue;

            for (int i = 0; i < enemies.Count; i++)
            {
                var tdEnemy = enemies[i] as TDEnemyActor;
                if (tdEnemy == null || !tdEnemy.IsAlive) continue;

                float distSqr = (tdEnemy.Position - owner.Position).sqrMagnitude;
                if (distSqr > rangeSqr) continue;

                // 通过 CombatAttributeComponent 获取 HP（CombatActor 继承了 CombatAttributeComponent 访问）
                float hp = tdEnemy.Get<CombatAttributeComponent>()?.HP ?? float.MaxValue;
                if (hp < lowestHP)
                {
                    lowestHP = hp;
                    best = tdEnemy;
                }
            }

            return best;
        }
    }
}
