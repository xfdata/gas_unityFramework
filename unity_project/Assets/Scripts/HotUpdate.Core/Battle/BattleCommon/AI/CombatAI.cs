using System;
using System.Collections.Generic;
using BattleFoundation;
using Framework;
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
        Custom,
    }

    public enum CombatAIBehaviorState
    {
        Inactive,
        Running,
        Cooldown,
        Blocked,
    }

    public enum CombatAIBehaviorPriority
    {
        Critical = 0,
        High = 1,
        Medium = 2,
        Low = 3,
        Idle = 4,
    }

    public enum CombatAIThreatLevel
    {
        None,
        Low,
        Medium,
        High,
        Critical,
    }

    [Serializable]
    public class CombatAIProfile
    {
        public float DecisionInterval = 0.2f;
        public float RetargetCooldown = 2f;
        public float FleeHealthThreshold = 0.2f;
        public float FleeDistance = 10f;
        public float FleeDuration = 3f;
        public float SkillMinInterval = 2f;
        public float SearchRangeMultiplier = 2f;
        public float ChaseGiveUpDistanceMultiplier = 3f;
        public float ChaseTimeout = 10f;
        public float PatrolWaitTime = 1f;
        public float PatrolRadius = 5f;
        public CombatTargetPriority TargetPriority = CombatTargetPriority.Nearest;
        public bool CanFlee;
        public List<int> SkillAbilityIds = new List<int>();
        public List<Vector3> PatrolWaypoints = new List<Vector3>();

        public CombatAIProfile Clone()
        {
            return new CombatAIProfile
            {
                DecisionInterval = DecisionInterval,
                RetargetCooldown = RetargetCooldown,
                FleeHealthThreshold = FleeHealthThreshold,
                FleeDistance = FleeDistance,
                FleeDuration = FleeDuration,
                SkillMinInterval = SkillMinInterval,
                SearchRangeMultiplier = SearchRangeMultiplier,
                ChaseGiveUpDistanceMultiplier = ChaseGiveUpDistanceMultiplier,
                ChaseTimeout = ChaseTimeout,
                PatrolWaitTime = PatrolWaitTime,
                PatrolRadius = PatrolRadius,
                TargetPriority = TargetPriority,
                CanFlee = CanFlee,
                SkillAbilityIds = SkillAbilityIds != null ? new List<int>(SkillAbilityIds) : new List<int>(),
                PatrolWaypoints = PatrolWaypoints != null ? new List<Vector3>(PatrolWaypoints) : new List<Vector3>(),
            };
        }
    }

    public abstract class CombatBehaviorBase : IDisposable
    {
        protected CombatActor Owner;
        protected CombatAIProfile Profile;
        protected CombatMovementComponent Movement;
        protected CombatAttackComponent Attack;
        protected CombatHealthComponent Health;
        protected CombatAbilityComponent Ability;
        protected CombatAttributeComponent Attributes;

        public CombatAIBehaviorType Type { get; protected set; }
        public CombatAIBehaviorState State { get; protected set; } = CombatAIBehaviorState.Inactive;
        public CombatAIBehaviorPriority Priority { get; protected set; } = CombatAIBehaviorPriority.Medium;

        public virtual void Setup(CombatActor owner, CombatAIProfile profile)
        {
            Owner = owner;
            Profile = profile;
            Movement = owner?.Get<CombatMovementComponent>();
            Attack = owner?.Get<CombatAttackComponent>();
            Health = owner?.Get<CombatHealthComponent>();
            Ability = owner?.Get<CombatAbilityComponent>();
            Attributes = owner?.Get<CombatAttributeComponent>();
        }

        public virtual bool CanEnter(CombatActor target)
        {
            return Owner != null && Owner.IsAlive && (Owner.Get<CombatStateComponent>()?.CanAct ?? true);
        }

        public virtual void Enter(CombatActor target)
        {
            State = CombatAIBehaviorState.Running;
        }

        public virtual void Update(float deltaTime, CombatActor target) { }

        public virtual void Exit()
        {
            State = CombatAIBehaviorState.Inactive;
        }

        public virtual void Dispose()
        {
            Owner = null;
            Profile = null;
            Movement = null;
            Attack = null;
            Health = null;
            Ability = null;
            Attributes = null;
        }
    }

    public sealed class CombatIdleBehavior : CombatBehaviorBase
    {
        public CombatIdleBehavior()
        {
            Type = CombatAIBehaviorType.Idle;
            Priority = CombatAIBehaviorPriority.Idle;
        }

        public override void Enter(CombatActor target)
        {
            base.Enter(target);
            Movement?.StopMove();
        }
    }

    public sealed class CombatAttackBehavior : CombatBehaviorBase
    {
        private float _attackCheckTimer;

        public CombatAttackBehavior()
        {
            Type = CombatAIBehaviorType.Attack;
            Priority = CombatAIBehaviorPriority.Critical;
        }

        public override bool CanEnter(CombatActor target)
        {
            return base.CanEnter(target) && target != null && target.IsAlive;
        }

        public override void Enter(CombatActor target)
        {
            base.Enter(target);
            _attackCheckTimer = 0f;
            Movement?.StopMove();
        }

        public override void Update(float deltaTime, CombatActor target)
        {
            if (target == null || !target.IsAlive)
            {
                State = CombatAIBehaviorState.Inactive;
                return;
            }

            _attackCheckTimer += deltaTime;
            if (_attackCheckTimer < 0.1f)
                return;

            _attackCheckTimer = 0f;
            float attackRange = Attributes?.AttackRange ?? 2f;
            if ((Owner.Position - target.Position).sqrMagnitude > attackRange * attackRange)
            {
                State = CombatAIBehaviorState.Inactive;
                return;
            }

            Movement?.StopMove();
            Attack?.TryAttack(target);
        }
    }

    public sealed class CombatChaseBehavior : CombatBehaviorBase
    {
        private float _chaseTimer;
        private float _giveUpDistanceSqr;

        public CombatChaseBehavior()
        {
            Type = CombatAIBehaviorType.Chase;
            Priority = CombatAIBehaviorPriority.High;
        }

        public override bool CanEnter(CombatActor target)
        {
            return base.CanEnter(target) && target != null && target.IsAlive;
        }

        public override void Enter(CombatActor target)
        {
            base.Enter(target);
            _chaseTimer = 0f;

            float attackRange = Attributes?.AttackRange ?? 2f;
            float multiplier = Profile?.ChaseGiveUpDistanceMultiplier ?? 3f;
            float giveUpDistance = attackRange * Mathf.Max(1f, multiplier);
            _giveUpDistanceSqr = giveUpDistance * giveUpDistance;
        }

        public override void Update(float deltaTime, CombatActor target)
        {
            if (target == null || !target.IsAlive)
            {
                State = CombatAIBehaviorState.Inactive;
                return;
            }

            _chaseTimer += deltaTime;
            if (_chaseTimer > (Profile?.ChaseTimeout ?? 10f))
            {
                State = CombatAIBehaviorState.Inactive;
                return;
            }

            if ((Owner.Position - target.Position).sqrMagnitude > _giveUpDistanceSqr)
            {
                State = CombatAIBehaviorState.Inactive;
                return;
            }

            Movement?.MoveTo(target.Position);
        }

        public override void Exit()
        {
            base.Exit();
            Movement?.StopMove();
        }
    }

    public sealed class CombatFleeBehavior : CombatBehaviorBase
    {
        private Vector3 _destination;
        private float _timer;

        public CombatFleeBehavior()
        {
            Type = CombatAIBehaviorType.Flee;
            Priority = CombatAIBehaviorPriority.Critical;
        }

        public override bool CanEnter(CombatActor target)
        {
            return base.CanEnter(target) &&
                   Profile != null &&
                   Profile.CanFlee &&
                   Health != null &&
                   Health.HPPercent <= Profile.FleeHealthThreshold;
        }

        public override void Enter(CombatActor target)
        {
            base.Enter(target);
            _timer = 0f;

            Vector3 awayDirection = target != null
                ? (Owner.Position - target.Position).normalized
                : -(Owner.Rotation * Vector3.forward);

            if (awayDirection.sqrMagnitude < 0.01f)
            {
                var random = Owner.Engine?.Context?.Random;
                awayDirection = random != null ? random.InsideUnitSphere().normalized : Vector3.back;
            }

            _destination = Owner.Position + awayDirection * Mathf.Max(1f, Profile.FleeDistance);
            Movement?.MoveTo(_destination);
        }

        public override void Update(float deltaTime, CombatActor target)
        {
            _timer += deltaTime;
            if (_timer >= (Profile?.FleeDuration ?? 3f))
            {
                State = CombatAIBehaviorState.Inactive;
                return;
            }

            if (Movement != null && Movement.RemainingDistance < 0.5f)
                Movement.MoveTo(_destination);
        }

        public override void Exit()
        {
            base.Exit();
            Movement?.StopMove();
        }
    }

    public sealed class CombatPatrolBehavior : CombatBehaviorBase
    {
        private readonly List<Vector3> _waypoints = new List<Vector3>();
        private int _waypointIndex;
        private float _waitTimer;
        private bool _waiting;
        private Vector3 _patrolCenter;

        public CombatPatrolBehavior()
        {
            Type = CombatAIBehaviorType.Patrol;
            Priority = CombatAIBehaviorPriority.Low;
        }

        public override void Setup(CombatActor owner, CombatAIProfile profile)
        {
            base.Setup(owner, profile);
            _patrolCenter = owner?.Position ?? Vector3.zero;
            ResetWaypointsFromProfile();
        }

        public override bool CanEnter(CombatActor target)
        {
            return base.CanEnter(target) && _waypoints.Count > 0 && target == null;
        }

        public override void Enter(CombatActor target)
        {
            base.Enter(target);
            _waiting = false;
            _waitTimer = 0f;
            if (_waypoints.Count == 0)
                GenerateRandomWaypoints();
            MoveToCurrentWaypoint();
        }

        public override void Update(float deltaTime, CombatActor target)
        {
            if (target != null)
            {
                State = CombatAIBehaviorState.Inactive;
                return;
            }

            if (_waypoints.Count == 0)
            {
                State = CombatAIBehaviorState.Inactive;
                return;
            }

            if (_waiting)
            {
                _waitTimer += deltaTime;
                if (_waitTimer >= (Profile?.PatrolWaitTime ?? 1f))
                {
                    _waiting = false;
                    _waypointIndex = (_waypointIndex + 1) % _waypoints.Count;
                    MoveToCurrentWaypoint();
                }
                return;
            }

            if (Movement != null && Movement.RemainingDistance < 0.5f)
            {
                _waiting = true;
                _waitTimer = 0f;
            }
        }

        public override void Exit()
        {
            base.Exit();
            Movement?.StopMove();
        }

        private void ResetWaypointsFromProfile()
        {
            _waypoints.Clear();
            if (Profile?.PatrolWaypoints != null)
                _waypoints.AddRange(Profile.PatrolWaypoints);
            _waypointIndex = 0;
        }

        private void MoveToCurrentWaypoint()
        {
            if (_waypointIndex >= 0 && _waypointIndex < _waypoints.Count)
                Movement?.MoveTo(_waypoints[_waypointIndex]);
        }

        private void GenerateRandomWaypoints()
        {
            if (Owner?.Engine?.Context?.Random == null)
                return;

            float radius = Profile?.PatrolRadius ?? 5f;
            int count = Owner.Engine.Context.Random.Range(3, 6);
            for (int i = 0; i < count; i++)
            {
                Vector2 randomCircle = Owner.Engine.Context.Random.InsideUnitCircle() * radius;
                _waypoints.Add(_patrolCenter + new Vector3(randomCircle.x, 0f, randomCircle.y));
            }
        }
    }

    public sealed class CombatSkillBehavior : CombatBehaviorBase
    {
        private readonly int _abilityId;
        private float _cooldown;
        private float _minInterval;

        public CombatSkillBehavior(int abilityId)
        {
            Type = CombatAIBehaviorType.Skill;
            Priority = CombatAIBehaviorPriority.Medium;
            _abilityId = abilityId;
        }

        public override void Setup(CombatActor owner, CombatAIProfile profile)
        {
            base.Setup(owner, profile);
            _minInterval = profile?.SkillMinInterval ?? 2f;
            _cooldown = _minInterval;
        }

        public override bool CanEnter(CombatActor target)
        {
            return base.CanEnter(target) && _abilityId > 0 && _cooldown >= _minInterval;
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
            if (State == CombatAIBehaviorState.Cooldown && _cooldown >= _minInterval)
                State = CombatAIBehaviorState.Inactive;
        }
    }

    public class CombatAIComponent : CombatComponentBase
    {
        private readonly List<CombatBehaviorBase> _behaviors = new List<CombatBehaviorBase>();
        private CombatAIProfile _profile;
        private CombatBehaviorBase _activeBehavior;
        private CombatActor _target;
        private float _decisionTimer;
        private float _retargetTimer;
        private CombatAIThreatLevel _threatLevel;
        private Vector3 _homePosition;

        public CombatActor CurrentTarget => _target;
        public CombatAIThreatLevel ThreatLevel => _threatLevel;
        public CombatBehaviorBase ActiveBehavior => _activeBehavior;
        public Vector3 HomePosition => _homePosition;

        public void SetProfile(CombatAIProfile profile)
        {
            _profile = profile?.Clone();
            ConfigureBehaviors();
        }

        public void SetHomePosition(Vector3 position)
        {
            _homePosition = position;
        }

        public void AddBehavior(CombatBehaviorBase behavior)
        {
            if (behavior == null)
                return;

            behavior.Setup(Owner, _profile);
            _behaviors.Add(behavior);
            _behaviors.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        public T AddBehavior<T>() where T : CombatBehaviorBase, new()
        {
            var behavior = new T();
            AddBehavior(behavior);
            return behavior;
        }

        public T GetBehavior<T>() where T : CombatBehaviorBase
        {
            for (int i = 0; i < _behaviors.Count; i++)
            {
                if (_behaviors[i] is T result)
                    return result;
            }
            return null;
        }

        public void RemoveBehavior<T>() where T : CombatBehaviorBase
        {
            for (int i = _behaviors.Count - 1; i >= 0; i--)
            {
                if (!(_behaviors[i] is T))
                    continue;

                if (_activeBehavior == _behaviors[i])
                    _activeBehavior = null;

                _behaviors[i].Dispose();
                _behaviors.RemoveAt(i);
                break;
            }
        }

        public override void Attach(BattleEntity owner)
        {
            base.Attach(owner);
            ConfigureBehaviors();
        }

        public override void Initialize()
        {
            base.Initialize();
            _homePosition = Owner != null ? Owner.Position : Vector3.zero;
            _decisionTimer = 0f;
            _retargetTimer = 0f;
        }

        public override void Update(float deltaTime)
        {
            using (new AutoProfiler("BattleCommon.AI.Update"))
            {
                if (!CanOwnerAct() || _profile == null)
                    return;

                TickCooldownBehaviors(deltaTime);
                UpdateActiveBehavior(deltaTime);
                _retargetTimer += deltaTime;

                _decisionTimer += deltaTime;
                if (_decisionTimer < Mathf.Max(0.05f, _profile.DecisionInterval))
                    return;

                _decisionTimer = 0f;
                UpdateThreatLevel();
                UpdateTarget();
                EvaluateBehaviors();
            }
        }

        public void ForceBehavior(CombatAIBehaviorType behaviorType)
        {
            for (int i = 0; i < _behaviors.Count; i++)
            {
                if (_behaviors[i].Type != behaviorType)
                    continue;

                SwitchActiveBehavior(_behaviors[i]);
                return;
            }
        }

        public override void DeactivateForPool()
        {
            ExitActiveBehavior();
            _target = null;
            _threatLevel = CombatAIThreatLevel.None;
            _decisionTimer = 0f;
            _retargetTimer = 0f;
            base.DeactivateForPool();
        }

        protected override void OnDispose()
        {
            ExitActiveBehavior();
            for (int i = 0; i < _behaviors.Count; i++)
                _behaviors[i].Dispose();
            _behaviors.Clear();
            _profile = null;
            _target = null;
            base.OnDispose();
        }

        private void ConfigureBehaviors()
        {
            for (int i = 0; i < _behaviors.Count; i++)
                _behaviors[i].Dispose();
            _behaviors.Clear();
            _activeBehavior = null;

            if (_profile == null || Owner == null)
                return;

            AddBehavior(new CombatFleeBehavior());

            if (_profile.SkillAbilityIds != null)
            {
                for (int i = 0; i < _profile.SkillAbilityIds.Count; i++)
                    AddBehavior(new CombatSkillBehavior(_profile.SkillAbilityIds[i]));
            }

            AddBehavior(new CombatAttackBehavior());
            AddBehavior(new CombatChaseBehavior());

            if (_profile.PatrolWaypoints != null && _profile.PatrolWaypoints.Count > 0)
                AddBehavior(new CombatPatrolBehavior());

            AddBehavior(new CombatIdleBehavior());
        }

        private bool CanOwnerAct()
        {
            if (Owner == null || !Owner.IsAlive)
                return false;

            var state = Owner.Get<CombatStateComponent>();
            return state == null || state.CanAct;
        }

        private void TickCooldownBehaviors(float deltaTime)
        {
            for (int i = 0; i < _behaviors.Count; i++)
            {
                if (_behaviors[i].State == CombatAIBehaviorState.Cooldown)
                    _behaviors[i].Update(deltaTime, _target);
            }
        }

        private void UpdateActiveBehavior(float deltaTime)
        {
            if (_activeBehavior == null || _activeBehavior.State != CombatAIBehaviorState.Running)
                return;

            _activeBehavior.Update(deltaTime, _target);
            if (_activeBehavior.State == CombatAIBehaviorState.Inactive)
                _activeBehavior = null;
        }

        private void UpdateThreatLevel()
        {
            var health = Owner.Get<CombatHealthComponent>();
            if (health == null)
            {
                _threatLevel = CombatAIThreatLevel.None;
                return;
            }

            float hpPercent = health.HPPercent;
            if (hpPercent <= 0.15f)
                _threatLevel = CombatAIThreatLevel.Critical;
            else if (hpPercent <= 0.3f)
                _threatLevel = CombatAIThreatLevel.High;
            else if (hpPercent <= 0.6f)
                _threatLevel = CombatAIThreatLevel.Medium;
            else
                _threatLevel = CombatAIThreatLevel.Low;
        }

        private void UpdateTarget()
        {
            if (_retargetTimer < _profile.RetargetCooldown && _target != null && _target.IsAlive)
                return;

            _retargetTimer = 0f;
            _target = Owner.Get<CombatAttackComponent>()?.FindTarget(null, _profile.TargetPriority);
        }

        private void EvaluateBehaviors()
        {
            using (new AutoProfiler("BattleCommon.AI.EvaluateBehaviors"))
            {
                if (_activeBehavior != null && _activeBehavior.State == CombatAIBehaviorState.Running)
                    return;

                CombatBehaviorBase bestBehavior = null;
                for (int i = 0; i < _behaviors.Count; i++)
                {
                    var behavior = _behaviors[i];
                    if (behavior.State == CombatAIBehaviorState.Cooldown ||
                        behavior.State == CombatAIBehaviorState.Blocked)
                    {
                        continue;
                    }

                    if (behavior.CanEnter(_target))
                    {
                        bestBehavior = behavior;
                        break;
                    }
                }

                if (bestBehavior == null)
                {
                    ExitActiveBehavior();
                    return;
                }

                SwitchActiveBehavior(bestBehavior);
            }
        }

        private void SwitchActiveBehavior(CombatBehaviorBase behavior)
        {
            if (_activeBehavior == behavior)
                return;

            ExitActiveBehavior();
            _activeBehavior = behavior;
            _activeBehavior?.Enter(_target);
        }

        private void ExitActiveBehavior()
        {
            if (_activeBehavior == null)
                return;

            _activeBehavior.Exit();
            _activeBehavior = null;
        }
    }
}
