using System.Collections.Generic;
using BattleFoundation;
using UnityEngine;
using UnityEngine.AI;

namespace BattleCommon
{
    public class CombatMovementComponent : CombatComponentBase
    {
        private readonly List<Vector3> _currentPath = new List<Vector3>();
        private CombatAttributeComponent _attributes;
        private CombatHealthComponent _health;
        private IMovementMotor _motor;
        private int _currentPathIndex;

        public bool IsMoving => _motor?.IsMoving ?? false;
        public float RemainingDistance => _motor?.RemainingDistance ?? 0f;

        public override void Attach(BattleEntity owner)
        {
            base.Attach(owner);
            _attributes = Owner?.Get<CombatAttributeComponent>();
            _health = Owner?.Get<CombatHealthComponent>();
        }

        public void SetMotor(IMovementMotor motor) => _motor = motor;

        public void SetNavAgent(NavMeshAgent navAgent)
        {
            _motor = navAgent == null ? null : new NavMeshMovementMotor(Owner, navAgent);
        }

        public void MoveTo(Vector3 destination)
        {
            if (_health != null && _health.IsDead) return;
            _motor?.MoveTo(destination, _attributes?.MoveSpeed ?? 3f);
        }

        public void StopMove()
        {
            _motor?.Stop();
            _currentPath.Clear();
            _currentPathIndex = 0;
        }

        public void FollowPath(IReadOnlyList<Vector3> path)
        {
            _currentPath.Clear();
            _currentPathIndex = 0;
            if (path == null) return;
            for (int i = 0; i < path.Count; i++)
                _currentPath.Add(path[i]);
            if (_currentPath.Count > 0)
                MoveTo(_currentPath[0]);
        }

        public void Teleport(Vector3 position) => _motor?.Teleport(position);

        public override void Update(float deltaTime)
        {
            if (_currentPath.Count == 0 || _motor == null || !_motor.HasArrived) return;

            _currentPathIndex++;
            if (_currentPathIndex < _currentPath.Count)
                MoveTo(_currentPath[_currentPathIndex]);
            else
                StopMove();
        }

        public override void DeactivateForPool()
        {
            StopMove();
            base.DeactivateForPool();
        }
    }
}
