using System;
using System.Collections.Generic;
using BattleFoundation;
using GAS;

namespace BattleCommon
{
    public enum BattleCastFailureReason
    {
        None,
        SkillNotFound,
        UnitDead,
        TargetInvalid,
        BattleUnavailable,
        UnsupportedRuntimeMode,
        NotGranted,
        Blocked,
        RequirementsNotMet,
        ResourceInsufficient,
        Cooldown,
        Stunned,
        Silenced,
        StateBlocked,
        Failed,
    }

    public readonly struct BattleCastResult
    {
        public readonly bool Success;
        public readonly BattleCastFailureReason Failure;
        public readonly int SkillId;

        public BattleCastResult(bool success, BattleCastFailureReason failure, int skillId)
        {
            Success = success;
            Failure = failure;
            SkillId = skillId;
        }
    }

    public enum BattleAttribute
    {
        Health,
        MaxHealth,
        Attack,
        Defense,
        MoveSpeed,
        AttackRange,
        AttackInterval,
        CritRate,
        CritDamage,
        DamageReduce,
    }

    public enum BattleState
    {
        Dead,
        Attacking,
        Casting,
        Hit,
        Invincible,
        Moving,
        Poisoned,
        BornInvincible,
        Stunned,
        Rooted,
        Silenced,
        Untargetable,
    }

    public readonly struct BattleBuffHandle
    {
        public readonly long TargetEntityId;
        public readonly int RuntimeEffectId;
        public bool IsValid => TargetEntityId != 0 && RuntimeEffectId != 0;

        public BattleBuffHandle(long targetEntityId, int runtimeEffectId)
        {
            TargetEntityId = targetEntityId;
            RuntimeEffectId = runtimeEffectId;
        }
    }

    public readonly struct BattleBuffView
    {
        public readonly int BuffId;
        public readonly int StackCount;
        public readonly float RemainingTime;
        public readonly bool IsDebuff;
        public readonly bool CanDispel;
        public readonly BattleBuffHandle Handle;

        public BattleBuffView(
            int buffId,
            int stackCount,
            float remainingTime,
            bool isDebuff,
            bool canDispel,
            BattleBuffHandle handle)
        {
            BuffId = buffId;
            StackCount = stackCount;
            RemainingTime = remainingTime;
            IsDebuff = isDebuff;
            CanDispel = canDispel;
            Handle = handle;
        }
    }

    public enum BattleBuffChangeType
    {
        Applied,
        StackChanged,
        Removed,
    }

    public readonly struct BattleBuffChangedEvent
    {
        public readonly BattleBuffChangeType ChangeType;
        public readonly BattleBuffView Buff;

        public BattleBuffChangedEvent(BattleBuffChangeType changeType, BattleBuffView buff)
        {
            ChangeType = changeType;
            Buff = buff;
        }
    }

    public readonly struct BattleEffectParams
    {
        public readonly int Level;
        public readonly float? AttackFactor;
        public readonly float? AttackOverride;
        public readonly float? DamageUp1;
        public readonly float? DamageUp2;
        public readonly object ContextData;
        public readonly object UserData;

        public BattleEffectParams(
            int level = 1,
            float? attackFactor = null,
            float? attackOverride = null,
            float? damageUp1 = null,
            float? damageUp2 = null,
            object contextData = null,
            object userData = null)
        {
            Level = level;
            AttackFactor = attackFactor;
            AttackOverride = attackOverride;
            DamageUp1 = damageUp1;
            DamageUp2 = damageUp2;
            ContextData = contextData;
            UserData = userData;
        }
    }

    public readonly struct BattleEffectResult
    {
        public readonly bool Success;
        public readonly bool IsInstant;
        public readonly BattleBuffHandle BuffHandle;

        public BattleEffectResult(bool success, bool isInstant, BattleBuffHandle buffHandle)
        {
            Success = success;
            IsInstant = isInstant;
            BuffHandle = buffHandle;
        }
    }

    public readonly struct BattleAttributeChangedEvent
    {
        public readonly BattleAttribute Attribute;
        public readonly float Previous;
        public readonly float Current;

        public BattleAttributeChangedEvent(BattleAttribute attribute, float previous, float current)
        {
            Attribute = attribute;
            Previous = previous;
            Current = current;
        }
    }

    public interface IBattleSkills
    {
        BattleCastResult TryCast(int skillId, ICombatActor target, int level = 1);
    }

    public interface IBattleEffects
    {
        BattleEffectResult Apply(int effectId, ICombatActor target, BattleEffectParams parameters = default);
    }

    public interface IBattleBuffs
    {
        event Action<BattleBuffChangedEvent> BuffChanged;
        // The facade owner is always the buff target. The optional source supplies
        // the outgoing effect context and defaults to the target for self-buffs.
        BattleEffectResult Apply(int buffId, ICombatActor source, BattleEffectParams parameters = default);
        bool Dispel(BattleBuffHandle handle);
        bool Remove(BattleBuffHandle handle);
        void GetBuffs(List<BattleBuffView> results);
        bool TryGetBuff(BattleBuffHandle handle, out BattleBuffView view);
    }

    public interface IBattleAttributes
    {
        event Action<BattleAttributeChangedEvent> Changed;
        float Get(BattleAttribute attribute);
    }

    public interface IBattleStates
    {
        bool Has(BattleState state);
        bool CanMove();
        bool CanAttack();
        bool CanCastSkill();
        bool CanBeTargeted();
    }

    public interface IBattleGameplay
    {
        IBattleSkills Skills { get; }
        IBattleEffects Effects { get; }
        IBattleBuffs Buffs { get; }
        IBattleAttributes Attributes { get; }
        IBattleStates States { get; }
    }

    /// <summary>
    /// Business-facing projection over the owning actor's GAS runtime.
    /// It stores no skill, buff, tag, cooldown, or attribute state.
    /// </summary>
    public sealed class BattleGameplayFacadeComponent : CombatComponentBase,
        IBattleGameplay, IBattleSkills, IBattleEffects, IBattleBuffs, IBattleAttributes, IBattleStates
    {
        private CombatAbilityComponent _abilities;
        private CombatAttributeComponent _attributes;
        private CombatStateComponent _states;
        private IGameplayEffectRuntimeContext _runtimeContext;

        public IBattleSkills Skills => this;
        public IBattleEffects Effects => this;
        public IBattleBuffs Buffs => this;
        public IBattleAttributes Attributes => this;
        public IBattleStates States => this;

        public event Action<BattleAttributeChangedEvent> Changed;
        public event Action<BattleBuffChangedEvent> BuffChanged;

        private CombatActor Actor => Owner as CombatActor;

        public override void Initialize()
        {
            base.Initialize();
            _abilities = Owner?.Get<CombatAbilityComponent>();
            _attributes = Owner?.Get<CombatAttributeComponent>();
            _states = Owner?.Get<CombatStateComponent>();

            if (_attributes != null)
                _attributes.OnAttributeChanged += OnAttributeChanged;

            _runtimeContext = _abilities?.RuntimeContext;
            _runtimeContext?.Subscribe(GameplayEffectEventType.EffectApplied, OnGameplayEffectEvent);
            _runtimeContext?.Subscribe(GameplayEffectEventType.EffectStackChanged, OnGameplayEffectEvent);
            _runtimeContext?.Subscribe(GameplayEffectEventType.EffectRemoved, OnGameplayEffectEvent);
        }

        public BattleCastResult TryCast(int skillId, ICombatActor target, int level = 1)
        {
            if (Actor == null || !Actor.IsAlive)
                return Fail(BattleCastFailureReason.UnitDead, skillId);
            if (Actor.Engine == null || Actor.Engine.Phase == EBattlePhase.Ended || Actor.Engine.Phase == EBattlePhase.Disposed)
                return Fail(BattleCastFailureReason.BattleUnavailable, skillId);
            if (target != null &&
                (!target.IsAlive ||
                 (!ReferenceEquals(target, Actor) &&
                  target.Get<BattleGameplayFacadeComponent>()?.States.CanBeTargeted() == false)))
                return Fail(BattleCastFailureReason.TargetInvalid, skillId);
            if (!CanCastSkill())
                return Fail(ResolveStateBlockedFailure(), skillId);
            if (!TryResolveSkill(skillId, out var ability))
                return Fail(BattleCastFailureReason.SkillNotFound, skillId);
            if (_abilities?.FindGrantedAbilityDefinition(ability.AbilityId) != ability)
                return Fail(BattleCastFailureReason.NotGranted, skillId);

            var result = _abilities.TryActivateById(ability.AbilityId, target, level);
            return result.Success
                ? new BattleCastResult(true, BattleCastFailureReason.None, skillId)
                : Fail(MapFailure(result.Failure), skillId);
        }

        BattleEffectResult IBattleEffects.Apply(int effectId, ICombatActor target, BattleEffectParams parameters)
        {
            return ApplyEffect(effectId, target, parameters);
        }

        BattleEffectResult IBattleBuffs.Apply(int buffId, ICombatActor source, BattleEffectParams parameters)
        {
            if (!TryResolveBuff(buffId, out var effect))
                return new BattleEffectResult(false, false, default);

            var targetEffects = _abilities?.Effects;
            var sourceEffects = source?.Get<CombatAbilityComponent>()?.Effects ?? targetEffects;
            return ApplyDefinition(effect, sourceEffects, targetEffects, parameters);
        }

        private BattleEffectResult ApplyEffect(int effectId, ICombatActor target, BattleEffectParams parameters)
        {
            if (!TryResolveEffect(effectId, out var effect))
                return new BattleEffectResult(false, false, default);

            var sourceEffects = _abilities?.Effects;
            var targetEffects = target?.Get<CombatAbilityComponent>()?.Effects ?? sourceEffects;
            return ApplyDefinition(effect, sourceEffects, targetEffects, parameters);
        }

        private BattleEffectResult ApplyDefinition(
            GameplayEffectDefinition effect,
            GameplayEffectRuntime sourceEffects,
            GameplayEffectRuntime targetEffects,
            BattleEffectParams parameters)
        {
            if (sourceEffects == null || targetEffects == null || effect == null)
                return new BattleEffectResult(false, false, default);

            var spec = sourceEffects.MakeOutgoingSpec(targetEffects, effect, Math.Max(1, parameters.Level));
            if (spec == null)
                return new BattleEffectResult(false, false, default);

            spec.ContextData = parameters.ContextData;
            spec.UserData = parameters.UserData;
            ApplyCombatParameters(spec, parameters);

            var result = sourceEffects.ApplySpecToTarget(spec, targetEffects);
            var handle = !result.WasInstant && result.RuntimeEffectId != 0
                ? new BattleBuffHandle(targetEffects.EntityId, result.RuntimeEffectId)
                : default;
            return new BattleEffectResult(result.Success, result.WasInstant, handle);
        }

        public bool Remove(BattleBuffHandle handle)
        {
            if (!handle.IsValid || _abilities?.Effects == null || handle.TargetEntityId != _abilities.Effects.EntityId)
                return false;

            return _abilities.Effects.RemoveActiveGameplayEffect(handle.RuntimeEffectId);
        }

        public bool Dispel(BattleBuffHandle handle)
        {
            return TryGetBuff(handle, out var buff) &&
                   buff.CanDispel &&
                   Remove(handle);
        }

        public void GetBuffs(List<BattleBuffView> results)
        {
            if (results == null)
                return;

            results.Clear();
            var effects = _abilities?.Effects?.ActiveEffects;
            if (effects == null)
                return;

            for (int i = 0; i < effects.Count; i++)
            {
                var active = effects[i];
                if (active?.Definition == null)
                    continue;

                if (!TryResolveBuffId(active.Definition, out var buffId))
                    continue;

                results.Add(CreateBuffView(buffId, active));
            }
        }

        public bool TryGetBuff(BattleBuffHandle handle, out BattleBuffView view)
        {
            view = default;
            if (!handle.IsValid || _abilities?.Effects == null || handle.TargetEntityId != _abilities.Effects.EntityId)
                return false;

            var active = _abilities.Effects.GetActiveEffect(handle.RuntimeEffectId);
            if (active?.Definition == null)
                return false;

            if (!TryResolveBuffId(active.Definition, out var buffId))
                return false;

            view = CreateBuffView(buffId, active);
            return true;
        }

        public float Get(BattleAttribute attribute)
        {
            return _attributes != null ? _attributes.GetAttribute(ToAttributeId(attribute)) : 0f;
        }

        public bool Has(BattleState state)
        {
            var tag = ToTag(state);
            return tag.IsValid && _abilities?.HasTag(tag) == true;
        }

        public bool CanMove() => CanAct() && !Has(BattleState.Stunned) && !Has(BattleState.Rooted);
        public bool CanAttack() => CanAct() && !Has(BattleState.Stunned);
        public bool CanCastSkill() => CanAct() && !Has(BattleState.Stunned) && !Has(BattleState.Silenced);
        public bool CanBeTargeted()
        {
            if (Has(BattleState.Untargetable))
                return false;

            if (_states != null)
                return _states.CanBeTargeted;

            return Actor?.IsAlive == true &&
                   !Has(BattleState.Dead) &&
                   !Has(BattleState.BornInvincible);
        }

        public override void DeactivateForPool()
        {
            Unsubscribe();
            Changed = null;
            BuffChanged = null;
            base.DeactivateForPool();
        }

        protected override void OnDispose()
        {
            Unsubscribe();
            Changed = null;
            BuffChanged = null;
            base.OnDispose();
        }

        private void Unsubscribe()
        {
            if (_attributes != null)
                _attributes.OnAttributeChanged -= OnAttributeChanged;
            _runtimeContext?.Unsubscribe(GameplayEffectEventType.EffectApplied, OnGameplayEffectEvent);
            _runtimeContext?.Unsubscribe(GameplayEffectEventType.EffectStackChanged, OnGameplayEffectEvent);
            _runtimeContext?.Unsubscribe(GameplayEffectEventType.EffectRemoved, OnGameplayEffectEvent);
            _runtimeContext = null;
            _attributes = null;
            _abilities = null;
            _states = null;
        }

        private void OnAttributeChanged(int attributeId, float previous, float current)
        {
            if (TryToBattleAttribute(attributeId, out var attribute))
                Changed?.Invoke(new BattleAttributeChangedEvent(attribute, previous, current));
        }

        private bool CanAct()
        {
            if (_states != null)
                return _states.CanAct;

            return Actor?.IsAlive == true &&
                   !Has(BattleState.Dead) &&
                   !Has(BattleState.BornInvincible);
        }

        private BattleCastFailureReason ResolveStateBlockedFailure()
        {
            if (Has(BattleState.Stunned))
                return BattleCastFailureReason.Stunned;
            if (Has(BattleState.Silenced))
                return BattleCastFailureReason.Silenced;
            return BattleCastFailureReason.StateBlocked;
        }

        private void OnGameplayEffectEvent(GameplayEffectEvent gameplayEvent)
        {
            if (gameplayEvent.TargetEntityId != _abilities?.Effects?.EntityId ||
                !TryResolveBuffId(gameplayEvent.EffectId, out var buffId))
                return;

            BattleBuffChangeType changeType;
            switch (gameplayEvent.Type)
            {
                case GameplayEffectEventType.EffectApplied:
                    changeType = BattleBuffChangeType.Applied;
                    break;
                case GameplayEffectEventType.EffectStackChanged:
                    changeType = BattleBuffChangeType.StackChanged;
                    break;
                case GameplayEffectEventType.EffectRemoved:
                    changeType = BattleBuffChangeType.Removed;
                    break;
                default:
                    return;
            }

            var active = _abilities.Effects.GetActiveEffect(gameplayEvent.RuntimeEffectId);
            var view = CreateBuffView(buffId, active, gameplayEvent.RuntimeEffectId);
            BuffChanged?.Invoke(new BattleBuffChangedEvent(changeType, view));
        }

        private BattleBuffView CreateBuffView(int buffId, ActiveGameplayEffect active, int runtimeEffectId = 0)
        {
            bool isDebuff = false;
            bool canDispel = true;
            if (Actor?.GameplayCatalog?.TryGetBuff(buffId, out var config) == true)
            {
                isDebuff = config.IsDebuff;
                canDispel = config.CanDispel;
            }

            int resolvedRuntimeEffectId = runtimeEffectId != 0
                ? runtimeEffectId
                : active?.RuntimeEffectId ?? 0;
            return new BattleBuffView(
                buffId,
                active?.Stack ?? 0,
                active?.TimeLeft ?? 0f,
                isDebuff,
                canDispel,
                new BattleBuffHandle(_abilities?.Effects?.EntityId ?? 0, resolvedRuntimeEffectId));
        }

        private bool TryResolveSkill(int skillId, out GameplayAbilityDefinition ability)
        {
            ability = null;
            if (skillId <= 0 || _abilities == null)
                return false;

            var catalog = Actor?.GameplayCatalog;
            if (catalog != null)
            {
                if (!catalog.TryGetSkill(skillId, out var config) || config?.Ability == null)
                    return false;

                ability = config.Ability;
            }
            else
            {
                ability = _abilities.FindGrantedAbilityDefinition(skillId);
            }

            return ability != null;
        }

        private bool TryResolveEffect(int effectId, out GameplayEffectDefinition effect)
        {
            effect = null;
            if (effectId <= 0)
                return false;

            var catalog = Actor?.GameplayCatalog;
            if (catalog != null)
            {
                if (!catalog.TryGetEffect(effectId, out var config) || config?.Effect == null)
                    return false;

                effect = config.Effect;
                return true;
            }

            effect = Actor?.AbilityServices?.AbilityCatalog?.GetEffect(effectId);
            return effect != null;
        }

        private bool TryResolveBuff(int buffId, out GameplayEffectDefinition effect)
        {
            effect = null;
            if (buffId <= 0)
                return false;

            var catalog = Actor?.GameplayCatalog;
            if (catalog == null)
            {
                effect = Actor?.AbilityServices?.AbilityCatalog?.GetEffect(buffId);
                return effect != null;
            }

            if (!catalog.TryGetBuff(buffId, out var config) || config?.Effect == null)
                return false;

            effect = config.Effect;
            return true;
        }

        private bool TryResolveBuffId(GameplayEffectDefinition effect, out int buffId)
        {
            buffId = 0;
            if (effect == null)
                return false;

            var catalog = Actor?.GameplayCatalog;
            if (catalog != null)
                return catalog.TryGetBuffId(effect, out buffId);

            buffId = effect.EffectId;
            return buffId > 0;
        }

        private bool TryResolveBuffId(int effectId, out int buffId)
        {
            buffId = 0;
            if (effectId <= 0)
                return false;

            var catalog = Actor?.GameplayCatalog;
            if (catalog != null)
                return catalog.TryGetBuffId(effectId, out buffId);

            buffId = effectId;
            return true;
        }

        private static void ApplyCombatParameters(GameplayEffectSpec spec, BattleEffectParams parameters)
        {
            if (parameters.AttackFactor.HasValue)
                spec.SetByCaller(CombatDamageKeys.AttackFactor, parameters.AttackFactor.Value);
            if (parameters.AttackOverride.HasValue)
                spec.SetByCaller(CombatDamageKeys.Attack, parameters.AttackOverride.Value);
            if (parameters.DamageUp1.HasValue)
                spec.SetByCaller(CombatDamageKeys.DamageUp1, parameters.DamageUp1.Value);
            if (parameters.DamageUp2.HasValue)
                spec.SetByCaller(CombatDamageKeys.DamageUp2, parameters.DamageUp2.Value);
        }

        private static BattleCastResult Fail(BattleCastFailureReason reason, int skillId)
        {
            return new BattleCastResult(false, reason, skillId);
        }

        private static BattleCastFailureReason MapFailure(GameplayAbilityActivationFailure failure)
        {
            switch (failure)
            {
                case GameplayAbilityActivationFailure.NotGranted: return BattleCastFailureReason.NotGranted;
                case GameplayAbilityActivationFailure.Blocked: return BattleCastFailureReason.Blocked;
                case GameplayAbilityActivationFailure.ActivationRequirementsNotMet: return BattleCastFailureReason.RequirementsNotMet;
                case GameplayAbilityActivationFailure.CostRejected: return BattleCastFailureReason.ResourceInsufficient;
                case GameplayAbilityActivationFailure.CooldownRejected: return BattleCastFailureReason.Cooldown;
                default: return BattleCastFailureReason.Failed;
            }
        }

        private static int ToAttributeId(BattleAttribute attribute)
        {
            switch (attribute)
            {
                case BattleAttribute.Health: return CombatAttributeIds.HP;
                case BattleAttribute.MaxHealth: return CombatAttributeIds.MaxHP;
                case BattleAttribute.Attack: return CombatAttributeIds.Attack;
                case BattleAttribute.Defense: return CombatAttributeIds.Defense;
                case BattleAttribute.MoveSpeed: return CombatAttributeIds.MoveSpeed;
                case BattleAttribute.AttackRange: return CombatAttributeIds.AttackRange;
                case BattleAttribute.AttackInterval: return CombatAttributeIds.AttackInterval;
                case BattleAttribute.CritRate: return CombatAttributeIds.CritRate;
                case BattleAttribute.CritDamage: return CombatAttributeIds.CritDamage;
                case BattleAttribute.DamageReduce: return CombatAttributeIds.DamageReduce;
                default: return 0;
            }
        }

        private static bool TryToBattleAttribute(int attributeId, out BattleAttribute attribute)
        {
            switch (attributeId)
            {
                case CombatAttributeIds.HP: attribute = BattleAttribute.Health; return true;
                case CombatAttributeIds.MaxHP: attribute = BattleAttribute.MaxHealth; return true;
                case CombatAttributeIds.Attack: attribute = BattleAttribute.Attack; return true;
                case CombatAttributeIds.Defense: attribute = BattleAttribute.Defense; return true;
                case CombatAttributeIds.MoveSpeed: attribute = BattleAttribute.MoveSpeed; return true;
                case CombatAttributeIds.AttackRange: attribute = BattleAttribute.AttackRange; return true;
                case CombatAttributeIds.AttackInterval: attribute = BattleAttribute.AttackInterval; return true;
                case CombatAttributeIds.CritRate: attribute = BattleAttribute.CritRate; return true;
                case CombatAttributeIds.CritDamage: attribute = BattleAttribute.CritDamage; return true;
                case CombatAttributeIds.DamageReduce: attribute = BattleAttribute.DamageReduce; return true;
                default: attribute = default; return false;
            }
        }

        private static GameplayTag ToTag(BattleState state)
        {
            switch (state)
            {
                case BattleState.Dead: return CombatGameplayTags.State_Dead;
                case BattleState.Attacking: return CombatGameplayTags.State_Attacking;
                case BattleState.Casting: return CombatGameplayTags.State_Casting;
                case BattleState.Hit: return CombatGameplayTags.State_Hit;
                case BattleState.Invincible: return CombatGameplayTags.State_Invincible;
                case BattleState.Moving: return CombatGameplayTags.State_Moving;
                case BattleState.Poisoned: return CombatGameplayTags.State_Poisoned;
                case BattleState.BornInvincible: return CombatGameplayTags.State_BornInvincible;
                case BattleState.Stunned: return CombatGameplayTags.State_Stunned;
                case BattleState.Rooted: return CombatGameplayTags.State_Rooted;
                case BattleState.Silenced: return CombatGameplayTags.State_Silenced;
                case BattleState.Untargetable: return CombatGameplayTags.State_Untargetable;
                default: return GameplayTag.None;
            }
        }
    }
}
