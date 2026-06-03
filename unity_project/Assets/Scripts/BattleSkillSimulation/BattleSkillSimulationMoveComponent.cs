using BattleCommon;
using UnityEngine;

namespace BattleSkillSimulation
{
    public sealed class BattleSkillSimulationMoveComponent : CombatComponentBase
    {
        private float _moveSpeed;

        public void SetMoveSpeed(float moveSpeed)
        {
            _moveSpeed = Mathf.Max(0f, moveSpeed);
        }

        public void Move(Vector3 direction, float deltaTime)
        {
            if (Owner == null || !Owner.IsAlive)
                return;

            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f)
                return;

            direction.Normalize();
            float speed = ResolveMoveSpeed();
            Owner.Position += direction * speed * Mathf.Max(0f, deltaTime);
            Owner.Rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        public void Face(Vector3 targetPosition)
        {
            if (Owner == null || Owner.Transform == null)
                return;

            Vector3 direction = targetPosition - Owner.Position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
                Owner.Rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private float ResolveMoveSpeed()
        {
            if (_moveSpeed > 0f)
                return _moveSpeed;

            return Owner.Get<CombatAttributeComponent>()?.MoveSpeed ?? 0f;
        }
    }
}
