using System.Collections.Generic;
using BattleFoundation;
using GAS;
using UnityEngine;

namespace BattleCommon
{
    public enum CombatAIBehaviorType
    {
        Idle,
        Chase,
        Attack,
        Flee,
        Patrol,
        Skill,
    }

    public enum CombatAIBehaviorState
    {
        Inactive,
        Running,
        Cooldown,
    }

    [System.Serializable]
    public class CombatAIProfile
    {
        public float DecisionInterval = 0.2f;
        public float RetargetCooldown = 2f;
        public float FleeHealthThreshold = 0.2f;
        public float FleeDistance = 10f;
        public float SkillMinInterval = 2f;
        public CombatTargetPriority TargetPriority = CombatTargetPriority.Nearest;
        public bool CanFlee;
        public List<int> SkillAbilityIds = new List<int>();
    }

    public abstract class CombatBehaviorBase
    {
        protected CombatActor Owner;
        protected CombatAIProfile Profile;
        protected CombatMovementComponent Movement;
        protected CombatAttackComponent Attack;
        protected CombatHealthComponent Health;
        protected CombatAbilityComponent Ability;

        public CombatAIBehaviorType Type { get; protected set; }
        public CombatAIBehaviorState State { get; protected set; }

        public virtual void Setup(CombatActor owner, CombatAIProfile profile)
        {
            Owner = owner;
            Profile = profile;
            Movement = owner?.Get<CombatMovementComponent>();
            Attack = owner?.Get<CombatAttackComponent>();
            Health = owner?.Get<CombatHealthComponent>();
            Ability = owner?.Get<CombatAbilityComponent>();
        }

        public virtual bool CanEnter(CombatActor target) => Owner != null && Owner.IsAlive;
        public virtual void Enter(CombatActor target) => State = CombatAIBehaviorState.Running;
        public virtual void Update(float deltaTime, CombatActor target) { }
        public virtual void Exit() => State = CombatAIBehaviorState.Inactive;
    }

    public sealed class CombatIdleBehavior : CombatBehaviorBase
    {
        public CombatIdleBehavior() => Type = CombatAIBehaviorType.Idle;
        public override void Enter(CombatActor target)
        {
            base.Enter(target);
            Movement?.StopMove();
        }
    }

    public sealed class CombatAttackBehavior : CombatBehaviorBase
    {
        public CombatAttackBehavior() => Type = CombatAIBehaviorType.Attack;
        public override bool CanEnter(CombatActor target) => base.CanEnter(target) && target != null && target.IsAlive;
        public override void Update(float deltaTime, CombatActor target)
        {
            if (target == null || !Attack.TryAttack(target))
                State = CombatAIBehaviorState.Inactive;
        }
    }

    public sealed class CombatChaseBehavior : CombatBehaviorBase
    {
        public CombatChaseBehavior() => Type = CombatAIBehaviorType.Chase;
        public override bool CanEnter(CombatActor target) => base.CanEnter(target) && target != null && target.IsAlive;
        public override void Update(float deltaTime, CombatActor target)
        {
            if (target == null || !target.IsAlive)
            {
                State = CombatAIBehaviorState.Inactive;
                return;
            }
            Movement?.MoveTo(target.Position);
        }
    }

    public sealed class CombatFleeBehavior : CombatBehaviorBase
    {
        public CombatFleeBehavior() => Type = CombatAIBehaviorType.Flee;
        public override bool CanEnter(CombatActor target)
        {
            return base.CanEnter(target) && Profile != null && Profile.CanFlee &&
                Health != null && Health.HPPercent <= Profile.FleeHealthThreshold;
        }

        public override void Enter(CombatActor target)
        {
            base.Enter(target);
            Vector3 direction = target != null
                ? (Owner.Position - target.Position).normalized
                : -(Owner.Rotation * Vector3.forward);
            Movement?.MoveTo(Owner.Position + direction * Profile.FleeDistance);
        }
    }

    public sealed class CombatSkillBehavior : CombatBehaviorBase
    {
        private readonly int _abilityId;
        private float _cooldown;

        public CombatSkillBehavior(int abilityId)
        {
            Type = CombatAIBehaviorType.Skill;
            _abilityId = abilityId;
        }

        public override bool CanEnter(CombatActor target)
        {
            return base.CanEnter(target) && _abilityId > 0 && _cooldown >= (Profile?.SkillMinInterval ?? 2f);
        }

        public override void Enter(CombatActor target)
        {
            base.Enter(target);
            Ability?.TryActivateById(_abilityId);
            _cooldown = 0f;
            State = CombatAIBehaviorState.Cooldown;
        }

        public override void Update(float deltaTime, CombatActor target)
        {
            _cooldown += deltaTime;
            if (State == CombatAIBehaviorState.Cooldown && _cooldown >= (Profile?.SkillMinInterval ?? 2f))
                State = CombatAIBehaviorState.Inactive;
        }
    }

    public class CombatAIComponent : CombatComponentBase
    {
        private readonly List<CombatBehaviorBase> _behaviors = new List<CombatBehaviorBase>();
        private CombatAIProfile _profile;
        private CombatActor _target;
        private float _decisionTimer;
        private float _retargetTimer;

        public void SetProfile(CombatAIProfile profile)
        {
            _profile = profile;
            ConfigureBehaviors();
        }

        public override void Update(float deltaTime)
        {
            if (Owner == null || !Owner.IsAlive || _profile == null) return;
            _decisionTimer += deltaTime;
            _retargetTimer += deltaTime;
            if (_decisionTimer < _profile.DecisionInterval) return;
            _decisionTimer = 0f;

            if (_target == null || !_target.IsAlive || _retargetTimer >= _profile.RetargetCooldown)
            {
                _retargetTimer = 0f;
                _target = Owner.Get<CombatAttackComponent>()?.FindTarget(null, _profile.TargetPriority);
            }

            for (int i = 0; i < _behaviors.Count; i++)
            {
                var behavior = _behaviors[i];
                behavior.Update(deltaTime, _target);
                if (!behavior.CanEnter(_target)) continue;
                behavior.Enter(_target);
                behavior.Update(deltaTime, _target);
                return;
            }
        }

        private void ConfigureBehaviors()
        {
            _behaviors.Clear();
            if (_profile == null) return;
            var flee = new CombatFleeBehavior();
            flee.Setup(Owner, _profile);
            _behaviors.Add(flee);
            for (int i = 0; i < _profile.SkillAbilityIds.Count; i++)
            {
                var skill = new CombatSkillBehavior(_profile.SkillAbilityIds[i]);
                skill.Setup(Owner, _profile);
                _behaviors.Add(skill);
            }
            var attack = new CombatAttackBehavior();
            attack.Setup(Owner, _profile);
            _behaviors.Add(attack);
            var chase = new CombatChaseBehavior();
            chase.Setup(Owner, _profile);
            _behaviors.Add(chase);
            var idle = new CombatIdleBehavior();
            idle.Setup(Owner, _profile);
            _behaviors.Add(idle);
        }

        public override void Attach(BattleEntity owner)
        {
            base.Attach(owner);
            ConfigureBehaviors();
        }
    }
}
