using UnityEngine;
using UnityEngine.AI;

namespace BattleCommon
{
    public sealed class NavMeshMovementMotor : IMovementMotor
    {
        private readonly CombatActor _actor;
        private readonly NavMeshAgent _agent;

        public bool IsMoving { get; private set; }
        public bool HasArrived => _agent != null && _agent.enabled && !_agent.pathPending &&
                                  _agent.remainingDistance <= _agent.stoppingDistance;
        public float RemainingDistance => _agent != null && _agent.enabled ? _agent.remainingDistance : 0f;

        public NavMeshMovementMotor(CombatActor actor, NavMeshAgent agent)
        {
            _actor = actor;
            _agent = agent;
        }

        public void MoveTo(Vector3 destination, float speed)
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;
            IsMoving = true;
            _agent.isStopped = false;
            _agent.speed = speed;
            _agent.SetDestination(destination);
        }

        public void Stop()
        {
            IsMoving = false;
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
                _agent.isStopped = true;
        }

        public void Teleport(Vector3 position)
        {
            _actor.Position = position;
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
                _agent.Warp(position);
        }
    }
}
