using BattleCommon;
using BattleFoundation;
using UnityEngine;

namespace BattleSkillSimulation
{
    public sealed class BattleSkillSimulationMoveComponent : CombatComponentBase
    {
        private float _moveSpeed;
        private CombatActor Actor => Owner as CombatActor;

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
            Owner.Position += new Float3(direction.x, direction.y, direction.z) * speed * Mathf.Max(0f, deltaTime);
            var rotation = Quaternion.LookRotation(direction, Vector3.up);
            Owner.Rotation = new Float4(rotation.x, rotation.y, rotation.z, rotation.w);
        }

        public void Face(Vector3 targetPosition)
        {
            if (Owner == null || Actor?.Transform == null)
                return;

            Vector3 direction = targetPosition - new Vector3(Owner.Position.x, Owner.Position.y, Owner.Position.z);
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                var rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                Owner.Rotation = new Float4(rotation.x, rotation.y, rotation.z, rotation.w);
            }
        }

        private float ResolveMoveSpeed()
        {
            if (_moveSpeed > 0f)
                return _moveSpeed;

            return Owner.Get<CombatAttributeComponent>()?.MoveSpeed ?? 0f;
        }
    }
}