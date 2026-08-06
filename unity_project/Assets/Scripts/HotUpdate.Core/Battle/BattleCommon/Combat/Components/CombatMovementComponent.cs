using System.Collections.Generic;
using BattleFoundation;
using UnityEngine;

namespace BattleCommon
{
    public class CombatMovementComponent : CombatComponentBase
    {
        private readonly List<Vector3> _currentPath = new List<Vector3>();
        private CombatAttributeComponent _attributes;
        private CombatHealthComponent _health;
        private IMovementMotor _motor;
        private int _currentPathIndex;

        // Owner 继承自 EntityComponent，类型为 BattleEntity。需要 CombatActor 特有成员时通过 Actor 访问。
        protected CombatActor Actor => Owner as CombatActor;

        public bool IsMoving => _motor?.IsMoving ?? false;
        public float RemainingDistance => _motor?.RemainingDistance ?? 0f;

        public override void Attach(BattleEntity owner)
        {
            base.Attach(owner);
            _attributes = Owner?.Get<CombatAttributeComponent>();
            _health = Owner?.Get<CombatHealthComponent>();
        }

        public void SetMotor(IMovementMotor motor) => _motor = motor;

        // R3-S8: SetNavAgent(NavMeshAgent) 已移除，避免 L2 逻辑层直接依赖 UnityEngine.AI。
        // 上层（Samples/工厂）应调用 SetMotor(NavMeshMovementMotor.CreateFromViewBinding(actor))。
        // NavMeshMovementMotor 已迁移到 Presentation 目录（L3 层）。

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
