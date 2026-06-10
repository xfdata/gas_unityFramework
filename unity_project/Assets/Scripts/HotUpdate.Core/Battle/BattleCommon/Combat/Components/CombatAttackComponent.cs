using System;
using BattleFoundation;
using UnityEngine;

namespace BattleCommon
{
    public class CombatAttackComponent : CombatComponentBase
    {
        private CombatAttributeComponent _attributes;
        private CombatHealthComponent _health;
        private float _attackTimer = float.MaxValue;

        public CombatActor CurrentTarget { get; private set; }

        public override void Attach(BattleEntity owner)
        {
            base.Attach(owner);
            _attributes = Owner?.Get<CombatAttributeComponent>();
            _health = Owner?.Get<CombatHealthComponent>();
        }

        public override void Update(float deltaTime)
        {
            _attackTimer += deltaTime;
        }

        public bool TryAttack(CombatActor target)
        {
            if (_health == null || _health.IsDead || target == null || target == Owner || !target.IsAlive) return false;
            if (Owner?.Get<CombatStateComponent>() is {} state && !state.CanAct) return false;
            if (target?.Get<CombatStateComponent>() is {} tgtState && !tgtState.CanBeAttacked) return false;

            float range = _attributes?.AttackRange ?? 2f;
            float interval = _attributes?.AttackInterval ?? 1.5f;
            if ((Owner.Position - target.Position).sqrMagnitude > range * range || _attackTimer < interval) return false;
            _attackTimer = 0f;
            CurrentTarget = target;
            return Owner.Get<CombatAbilityComponent>()?.TryActivateAttackAbility(target) ?? false;
        }

        public CombatActor FindTarget(Func<CombatActor, bool> filter, CombatTargetPriority priority = CombatTargetPriority.Nearest)
        {
            if (Owner?.Get<CombatStateComponent>() is {} state && !state.CanAct)
                return null;

            var query = Owner?.Engine?.Context?.GetSystem<CombatTargetQuerySystem>();
            float range = (_attributes?.AttackRange ?? 2f) * 2f;
            return query?.FindTarget(Owner, filter, priority, range);
        }

        public override void DeactivateForPool()
        {
            CurrentTarget = null;
            _attackTimer = float.MaxValue;
            base.DeactivateForPool();
        }
    }
}
