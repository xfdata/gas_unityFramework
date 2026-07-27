using System;
using System.Collections.Generic;
using BattleFoundation;
using GAS;
using UnityEngine;

namespace BattleCommon
{
    public interface ICombatTarget : IRangedTarget
    {
    }

    public interface IMeleeSource : IMeleeAttackSourceProvider
    {
    }

    public interface IRangedSource : IRangedAttackSourceProvider
    {
    }

    public interface IMovementMotor
    {
        bool IsMoving { get; }
        bool HasArrived { get; }
        float RemainingDistance { get; }
        void MoveTo(Vector3 destination, float speed);
        void Stop();
        void Teleport(Vector3 position);
    }

    public interface ICombatRelationResolver
    {
        bool AreEnemies(CombatActor source, CombatActor target);
    }

    public interface ICombatTargetQuery
    {
        CombatActor FindTarget(CombatActor source, Func<CombatActor, bool> filter, CombatTargetPriority priority, float range);
        int FindInRange(CombatActor source, float range, List<CombatActor> results);
    }

    public interface ICombatTargetSelector
    {
        CombatActor CurrentTarget { get; }
        float Range { get; set; }
        bool HasValidTarget { get; }
        void ForceRescan();
    }

    public interface ICombatProgressTarget
    {
        float Progress { get; }
    }

    public interface ICombatTargetStrategy
    {
        string StrategyName { get; }
        CombatActor FindBestTarget(IReadOnlyList<BattleEntity> candidates, BattleEntity owner, float rangeSqr);
    }

    public sealed class NearestCombatTargetStrategy : ICombatTargetStrategy
    {
        public string StrategyName => "Nearest";

        public CombatActor FindBestTarget(IReadOnlyList<BattleEntity> candidates, BattleEntity owner, float rangeSqr)
        {
            if (candidates == null || owner == null)
                return null;

            CombatActor best = null;
            float bestDistSqr = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                if (!(candidates[i] is CombatActor target) || !target.IsAlive)
                    continue;

                float distSqr = (target.Position - owner.Position).sqrMagnitude;
                if (distSqr > rangeSqr)
                    continue;

                if (distSqr < bestDistSqr)
                {
                    bestDistSqr = distSqr;
                    best = target;
                }
            }

            return best;
        }
    }

    public sealed class LowestHpCombatTargetStrategy : ICombatTargetStrategy
    {
        public string StrategyName => "LowestHP";

        public CombatActor FindBestTarget(IReadOnlyList<BattleEntity> candidates, BattleEntity owner, float rangeSqr)
        {
            if (candidates == null || owner == null)
                return null;

            CombatActor best = null;
            float lowestHP = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                if (!(candidates[i] is CombatActor target) || !target.IsAlive)
                    continue;

                float distSqr = (target.Position - owner.Position).sqrMagnitude;
                if (distSqr > rangeSqr)
                    continue;

                float hp = target.Get<CombatAttributeComponent>()?.HP ?? float.MaxValue;
                if (hp < lowestHP)
                {
                    lowestHP = hp;
                    best = target;
                }
            }

            return best;
        }
    }
}
