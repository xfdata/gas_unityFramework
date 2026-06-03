using System;
using System.Collections.Generic;
using BattleFoundation;
using GAS;
using UnityEngine;
using UnityEngine.AI;

namespace BattleCommon
{
    public interface ICombatAbilityServices
    {
        GameplayDefinitionCatalog AbilityCatalog { get; }
        IGameplayCueManager GameplayCueManager { get; }
        ProjectileRuntime ProjectileRuntime { get; }
    }

    public abstract class CombatComponentBase : EntityComponent
    {
        public new CombatActor Owner => base.Owner as CombatActor;
    }

    public class CombatAttributeComponent : CombatComponentBase, IGameplayAttributeOwner, IGameplayAttributeSetProvider
    {
        private readonly AttributeSet _attributeSet = new AttributeSet();

        public AttributeSet AttributeSet => _attributeSet;

        public event Action<int, float, float> OnAttributeChanged
        {
            add => _attributeSet.OnAttributeChanged += value;
            remove => _attributeSet.OnAttributeChanged -= value;
        }

        public float HP { get => Get(CombatAttributeIds.HP); set => Set(CombatAttributeIds.HP, value); }
        public float MaxHP { get => Get(CombatAttributeIds.MaxHP); set => Set(CombatAttributeIds.MaxHP, value); }
        public float Attack { get => Get(CombatAttributeIds.Attack); set => Set(CombatAttributeIds.Attack, value); }
        public float Defense { get => Get(CombatAttributeIds.Defense); set => Set(CombatAttributeIds.Defense, value); }
        public float MoveSpeed { get => Get(CombatAttributeIds.MoveSpeed); set => Set(CombatAttributeIds.MoveSpeed, value); }
        public float AttackRange { get => Get(CombatAttributeIds.AttackRange); set => Set(CombatAttributeIds.AttackRange, value); }
        public float AttackInterval { get => Get(CombatAttributeIds.AttackInterval); set => Set(CombatAttributeIds.AttackInterval, value); }
        public float CritRate { get => Get(CombatAttributeIds.CritRate); set => Set(CombatAttributeIds.CritRate, value); }
        public float CritDamage { get => Get(CombatAttributeIds.CritDamage); set => Set(CombatAttributeIds.CritDamage, value); }
        public float CritDamageMul { get => CritDamage; set => CritDamage = value; }
        public float DamageReduce { get => Get(CombatAttributeIds.DamageReduce); set => Set(CombatAttributeIds.DamageReduce, value); }

        public void ApplyLoadout(CombatLoadoutDefinition loadout)
        {
            if (loadout == null) return;
            MaxHP = loadout.MaxHP;
            HP = loadout.MaxHP;
            Attack = loadout.Attack;
            Defense = loadout.Defense;
            MoveSpeed = loadout.MoveSpeed;
            AttackRange = loadout.AttackRange;
            AttackInterval = loadout.AttackInterval;
            CritRate = loadout.CritRate;
            CritDamage = loadout.CritDamage;
            DamageReduce = loadout.DamageReduce;
        }

        public float Get(int attributeId) => _attributeSet.GetAttribute(attributeId);
        public float GetAttribute(int attributeId) => _attributeSet.GetAttribute(attributeId);
        public void Set(int attributeId, float value) => _attributeSet.SetBaseValue(attributeId, value);
        public void SetBaseValue(int attributeId, float value) => _attributeSet.SetBaseValue(attributeId, value);
        public void AddBaseValue(int attributeId, float delta) => _attributeSet.AddAttributeBaseValue(attributeId, delta);
        public void AddAttributeBaseValue(int attributeId, float delta) => _attributeSet.AddAttributeBaseValue(attributeId, delta);
        public AttributeModifierHandle AddModifier(int attributeId, AttributeModifierOp op, float value, object source)
            => _attributeSet.AddModifier(attributeId, op, value, source);
        public void RemoveModifier(AttributeModifierHandle handle) => _attributeSet.RemoveModifier(handle);
        public void ClearAllModifiers() => _attributeSet.ClearAllModifiers();

        public override void DeactivateForPool()
        {
            base.DeactivateForPool();
            _attributeSet.ClearAllModifiers();
        }

        protected override void OnDispose()
        {
            _attributeSet.Clear();
            base.OnDispose();
        }
    }

    public class CombatHealthComponent : CombatComponentBase
    {
        private CombatAttributeComponent _attributes;
        private CombatActor _lastDamageSource;
        private bool _hasDied;

        public float HP
        {
            get => _attributes?.HP ?? 0f;
            set
            {
                if (_attributes != null)
                    _attributes.HP = Mathf.Clamp(value, 0f, _attributes.MaxHP);
            }
        }

        public float MaxHP => _attributes?.MaxHP ?? 0f;
        public bool IsDead => HP <= 0f;
        public bool IsAlive => !IsDead;
        public float HPPercent => MaxHP > 0f ? HP / MaxHP : 0f;

        public event Action<CombatActor> OnDeath;
        public event Action<float, CombatActor> OnDamaged;
        public event Action<float> OnHealed;

        public override void Attach(BattleEntity owner)
        {
            base.Attach(owner);
            _attributes = Owner?.Get<CombatAttributeComponent>();
        }

        public override void Initialize()
        {
            base.Initialize();
            _hasDied = false;
            _lastDamageSource = null;
            if (_attributes != null)
            {
                _attributes.OnAttributeChanged -= OnAttributeChanged;
                _attributes.OnAttributeChanged += OnAttributeChanged;
            }
        }

        public void TakeDamage(float rawDamage, CombatActor source)
        {
            if (IsDead) return;
            float finalDamage = Mathf.Max(1f, rawDamage - (_attributes?.Defense ?? 0f));
            _lastDamageSource = source;
            HP -= finalDamage;
            OnDamaged?.Invoke(finalDamage, source);
            if (HP <= 0f) Die(source);
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            float oldHP = HP;
            HP += amount;
            OnHealed?.Invoke(HP - oldHP);
        }

        public void SetFullHP()
        {
            HP = MaxHP;
        }

        private void Die(CombatActor killer)
        {
            if (_hasDied) return;
            _hasDied = true;
            Owner.Get<CombatAbilityComponent>()?.TryActivateDeathAbility(killer);
            OnDeath?.Invoke(killer);
        }

        private void OnAttributeChanged(int attributeId, float oldValue, float newValue)
        {
            if (attributeId == CombatAttributeIds.HP && oldValue > 0f && newValue <= 0f)
                Die(_lastDamageSource);
        }

        public override void DeactivateForPool()
        {
            if (_attributes != null)
                _attributes.OnAttributeChanged -= OnAttributeChanged;
            _lastDamageSource = null;
            _hasDied = false;
            OnDeath = null;
            OnDamaged = null;
            OnHealed = null;
            base.DeactivateForPool();
        }

        protected override void OnDispose()
        {
            if (_attributes != null)
                _attributes.OnAttributeChanged -= OnAttributeChanged;
            _attributes = null;
            base.OnDispose();
        }
    }

    public class CombatAbilityComponent : CombatComponentBase
    {
        private readonly List<GameplayAbilityDefinition> _initialAbilities = new List<GameplayAbilityDefinition>();
        private GameplayAbilitySystem _gas;

        public GameplayAbilitySystem GAS => _gas;
        public bool IsDead => HasTag(CombatGameplayTags.State_Dead);

        public void SetInitialAbilities(IEnumerable<GameplayAbilityDefinition> abilities)
        {
            _initialAbilities.Clear();
            if (abilities != null) _initialAbilities.AddRange(abilities);
        }

        public override void Initialize()
        {
            base.Initialize();
            _gas?.Dispose();
            var services = Owner?.AbilityServices;
            _gas = new GameplayAbilitySystem(
                Owner != null ? Owner.Id : 0,
                Owner,
                null,
                services?.AbilityCatalog,
                services?.GameplayCueManager);
            for (int i = 0; i < _initialAbilities.Count; i++)
                GrantAbility(_initialAbilities[i]);
        }

        public void GrantAbility(GameplayAbilityDefinition ability) => _gas?.GrantAbility(ability);
        public void GrantAbility(int abilityId) => _gas?.GrantAbility(abilityId);
        public void AddTag(GameplayTag tag) => _gas?.Effects?.OwnedTags?.AddTag(tag);
        public void RemoveTag(GameplayTag tag) => _gas?.Effects?.OwnedTags?.RemoveTag(tag);
        public bool HasTag(GameplayTag tag) => _gas?.Effects?.OwnedTags?.HasTag(tag) ?? false;

        public bool TryActivateBornAbility()
        {
            if (_gas == null || IsDead) return false;
            var ability = FindAbilityByTag(CombatGameplayTags.Ability_Born);
            return ability != null && _gas.ActivateAbility(ability) != null;
        }

        public bool TryActivateAttackAbility(CombatActor target)
        {
            if (_gas == null || IsDead || target == null) return false;
            var ability = FindAttackAbility();
            return ability != null && _gas.ActivateAbility(ability, target.Get<CombatAbilityComponent>()?.GAS) != null;
        }

        public bool TryActivateDeathAbility(CombatActor killer)
        {
            if (_gas == null || IsDead) return false;
            var ability = FindAbilityByTag(CombatGameplayTags.Ability_Death);
            return ability != null && _gas.ActivateAbility(ability, killer?.Get<CombatAbilityComponent>()?.GAS) != null;
        }

        public bool TryActivateById(int abilityId)
        {
            if (_gas == null || abilityId <= 0) return false;
            var ability = FindAbilityById(abilityId);
            return ability != null && _gas.ActivateAbility(ability) != null;
        }

        public override void Update(float deltaTime) => _gas?.Tick(deltaTime);

        private GameplayAbilityDefinition FindAbilityByTag(GameplayTag tag)
        {
            if (_gas?.Abilities == null) return null;
            foreach (var ability in _gas.Abilities.GrantedAbilities)
                if (ability.AbilityTag == tag) return ability;
            return null;
        }

        private GameplayAbilityDefinition FindAbilityById(int abilityId)
        {
            if (_gas?.Abilities == null) return null;
            foreach (var ability in _gas.Abilities.GrantedAbilities)
                if (ability.AbilityId == abilityId) return ability;
            return null;
        }

        private GameplayAbilityDefinition FindAttackAbility()
        {
            if (_gas?.Abilities == null) return null;

            if (Owner is IRangedAttackSourceProvider rangedSource && rangedSource.HasRangedWeapon)
            {
                foreach (var ability in _gas.Abilities.GrantedAbilities)
                {
                    if (ability is RemoteAttackAbilityDefinition)
                        return ability;
                }
            }

            return FindAbilityByTag(CombatGameplayTags.Ability_Attack);
        }

        public override void DeactivateForPool()
        {
            _gas?.Dispose();
            _gas = null;
            base.DeactivateForPool();
        }

        protected override void OnDispose()
        {
            _gas?.Dispose();
            _gas = null;
            _initialAbilities.Clear();
            base.OnDispose();
        }
    }

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
            float range = _attributes?.AttackRange ?? 2f;
            float interval = _attributes?.AttackInterval ?? 1.5f;
            if ((Owner.Position - target.Position).sqrMagnitude > range * range || _attackTimer < interval) return false;
            _attackTimer = 0f;
            CurrentTarget = target;
            return Owner.Get<CombatAbilityComponent>()?.TryActivateAttackAbility(target) ?? false;
        }

        public CombatActor FindTarget(Func<CombatActor, bool> filter, CombatTargetPriority priority = CombatTargetPriority.Nearest)
        {
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
