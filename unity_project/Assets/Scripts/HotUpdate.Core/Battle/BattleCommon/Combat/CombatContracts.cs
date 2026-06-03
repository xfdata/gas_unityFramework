using System;
using System.Collections.Generic;
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
}
