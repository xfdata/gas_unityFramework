using System;
using System.Collections.Generic;
using BattleFoundation;
using GAS;
using UnityEngine;

namespace BattleCommon
{
    public class CombatAbilityComponent : CombatComponentBase, ICombatAbilityComponent
    {
        private readonly List<GameplayAbilityDefinition> _initialAbilities = new List<GameplayAbilityDefinition>();
        private readonly List<GameplayAbilityDefinition> _lightAbilities = new List<GameplayAbilityDefinition>(8);
        private readonly List<LightweightActiveAbility> _lightActiveAbilities = new List<LightweightActiveAbility>(4);
        private GameplayAbilitySystem _gas;
        private GameplayEffectRuntime _lightEffects;
        private float _lightTime;

        public GameplayAbilitySystem GAS => _gas;
        public GameplayEffectRuntime Effects => _gas != null ? _gas.Effects : _lightEffects;
        public IGameplayEffectRuntimeContext RuntimeContext => _gas != null ? _gas.RuntimeContext : _lightEffects?.RuntimeContext;
        public CombatAbilityRuntimeMode RuntimeMode { get; set; } = CombatAbilityRuntimeMode.FullGas;
        public bool IsDead => Owner == null || !Owner.IsAlive;
        public bool IsLightweight => RuntimeMode == CombatAbilityRuntimeMode.Lightweight;

        // R3-S10: 表现层 Sink 桥接。由上层注入，用于将 GAS 事件转发到表现层单通路。
        // null 时不转发（保证无表现层时逻辑层正常运行）。
        public BattlePresentationSink PresentationSink { get; set; }

        // R3-S10 修复: GAS 事件 per-actor 内部分发。
        // ActorPresentationComponent 通过此事件接收 GAS 通知（不再直接订阅 GAS），
        // CombatAbilityComponent 作为唯一 GAS 订阅点，避免重复订阅。
        public event Action<GameplayEffectEvent> GASForwarded;

        // Owner 继承自 EntityComponent，类型为 BattleEntity。需要 CombatActor 特有成员时通过 Actor 访问。
        protected CombatActor Actor => Owner as CombatActor;

        public void SetInitialAbilities(IEnumerable<GameplayAbilityDefinition> abilities)
        {
            _initialAbilities.Clear();
            if (abilities != null) _initialAbilities.AddRange(abilities);
        }

        public override void Initialize()
        {
            base.Initialize();
            _lightTime = 0f;
            _lightAbilities.Clear();
            _lightActiveAbilities.Clear();
            _gas?.Dispose();
            _gas = null;
            _lightEffects?.Dispose();
            _lightEffects = null;

            var services = Actor?.AbilityServices;
            var gasRuntimeContext = CreateRuntimeContext(Owner?.Engine?.Context?.Random);
            if (RuntimeMode == CombatAbilityRuntimeMode.Lightweight)
            {
                _lightEffects = new GameplayEffectRuntime(
                    Owner?.Id ?? 0,
                    Actor,
                    gasRuntimeContext,
                    services?.GameplayCueManager);
            }
            else
            {
                _gas = new GameplayAbilitySystem(Owner?.Id ?? 0, Actor, gasRuntimeContext, services?.AbilityCatalog,
                    services?.GameplayCueManager);
            }

            // R3-S10: 订阅 GAS 事件，作为唯一订阅点。
            // 收敛 F4 痛点：ActorPresentationComponent 不再直接订阅 GAS，由本组件统一转发。
            // 始终订阅（不依赖 PresentationSink 是否注入），保证 per-actor 表现组件能收到事件。
            var runtimeContext = RuntimeContext;
            if (runtimeContext != null)
            {
                runtimeContext.Subscribe(GameplayEffectEventType.AbilityActivated, OnGASEventForwarded);
                runtimeContext.Subscribe(GameplayEffectEventType.AbilityEnded, OnGASEventForwarded);
                runtimeContext.Subscribe(GameplayEffectEventType.AttributeChanged, OnGASEventForwarded);
                runtimeContext.Subscribe(GameplayEffectEventType.CueTriggered, OnGASEventForwarded);
            }

            for (int i = 0; i < _initialAbilities.Count; i++)
                GrantAbility(_initialAbilities[i]);
        }

        /// <summary>
        /// R3-S10: GAS 事件转发回调。作为唯一订阅点，分发给 per-actor 表现组件 + 转发到 PresentationSink。
        /// 替代 ActorPresentationComponent 直接订阅 GAS 的并行通路。
        /// </summary>
        private void OnGASEventForwarded(GameplayEffectEvent evt)
        {
            // per-actor 内部分发（ActorPresentationComponent 等）
            GASForwarded?.Invoke(evt);
            // battle 级 Sink 转发（注入时才转发）
            PresentationSink?.ForwardGASEvent(evt);
        }

        public void GrantAbility(GameplayAbilityDefinition ability)
        {
            if (RuntimeMode == CombatAbilityRuntimeMode.Lightweight)
            {
                if (ability != null && !_lightAbilities.Contains(ability))
                    _lightAbilities.Add(ability);
                return;
            }

            _gas?.GrantAbility(ability);
        }

        public void GrantAbility(int abilityId)
        {
            if (RuntimeMode == CombatAbilityRuntimeMode.Lightweight)
            {
                GrantAbility(Actor?.AbilityServices?.AbilityCatalog?.GetAbility(abilityId));
                return;
            }

            _gas?.GrantAbility(abilityId);
        }

        public void AddTag(GameplayTag tag) => Effects?.OwnedTags?.AddTag(tag);
        public void RemoveTag(GameplayTag tag) => Effects?.OwnedTags?.RemoveTag(tag);
        public bool HasTag(GameplayTag tag) => Effects?.OwnedTags?.HasTag(tag) ?? false;

        public bool TryActivateBornAbility()
        {
            if (IsDead) return false;
            var ability = FindAbility<BornAbilityDefinition>();
            if (ability == null) return false;
            return RuntimeMode == CombatAbilityRuntimeMode.Lightweight
                ? ActivateLightBornAbility(ability)
                : _gas != null && _gas.ActivateAbility(ability) != null;
        }

        public bool TryActivateAttackAbility(CombatActor target)
        {
            if (IsDead || target == null) return false;
            var ability = FindAttackAbility();
            if (ability == null) return false;

            var targetEffects = target.Get<CombatAbilityComponent>()?.Effects;
            return RuntimeMode == CombatAbilityRuntimeMode.Lightweight
                ? ActivateLightAttackAbility(ability, target, targetEffects)
                : _gas != null && _gas.ActivateAbility(ability, targetEffects) != null;
        }

        public bool TryActivateDeathAbility(CombatActor killer)
        {
            var ability = FindAbility<DeathAbilityDefinition>();
            if (ability == null) return false;

            var killerEffects = killer?.Get<CombatAbilityComponent>()?.Effects;
            return RuntimeMode == CombatAbilityRuntimeMode.Lightweight
                ? ActivateLightDeathAbility(ability, killerEffects)
                : _gas != null && _gas.ActivateAbility(ability, killerEffects) != null;
        }

        public bool TryActivateById(int abilityId)
        {
            if (abilityId <= 0) return false;
            var ability = FindAbilityById(abilityId);
            if (ability == null) return false;

            if (RuntimeMode != CombatAbilityRuntimeMode.Lightweight)
                return _gas != null && _gas.ActivateAbility(ability) != null;

            if (ability is BornAbilityDefinition bornAbility)
                return ActivateLightBornAbility(bornAbility);
            if (ability is DeathAbilityDefinition deathAbility)
                return ActivateLightDeathAbility(deathAbility, null);
            return false;
        }

        public bool TryBlockIncomingDamage(DamageBlockContext blockContext)
        {
            if (_gas?.Abilities == null || blockContext == null)
                return false;

            foreach (var ability in _gas.Abilities.GrantedAbilities)
            {
                if (ability is DamageBlockAbilityDefinition blockAbility &&
                    blockAbility.TryActivateBlock(_gas, blockContext))
                {
                    return true;
                }
            }

            return false;
        }

        public override void Update(float deltaTime)
        {
            if (RuntimeMode != CombatAbilityRuntimeMode.Lightweight)
            {
                _gas?.Tick(deltaTime);
                return;
            }

            if (deltaTime <= 0f)
                return;

            _lightTime += deltaTime;
            _lightEffects?.Tick(deltaTime);
            for (int i = _lightActiveAbilities.Count - 1; i >= 0; i--)
            {
                if (_lightTime < _lightActiveAbilities[i].EndTime)
                    continue;

                EndLightAbilityAt(i);
            }
        }

        public bool HasActiveAbility(GameplayTag abilityTag)
        {
            if (!abilityTag.IsValid)
                return false;

            if (RuntimeMode != CombatAbilityRuntimeMode.Lightweight)
            {
                var activeAbilities = _gas?.ActiveAbilities;
                if (activeAbilities == null)
                    return false;

                for (int i = 0; i < activeAbilities.Count; i++)
                {
                    var spec = activeAbilities[i];
                    var tag = spec?.Ability != null ? spec.Ability.AbilityTag : GameplayTag.None;
                    if (spec != null && spec.IsActive && !spec.IsEnded && tag.Matches(abilityTag))
                        return true;
                }

                return false;
            }

            for (int i = 0; i < _lightActiveAbilities.Count; i++)
            {
                var ability = _lightActiveAbilities[i].Ability;
                if (ability != null && ability.AbilityTag.Matches(abilityTag))
                    return true;
            }

            return false;
        }

        public bool HasActiveAbility(Func<GameplayAbilityDefinition, bool> predicate)
        {
            if (predicate == null)
                return false;

            if (RuntimeMode != CombatAbilityRuntimeMode.Lightweight)
            {
                var activeAbilities = _gas?.ActiveAbilities;
                if (activeAbilities == null)
                    return false;

                for (int i = 0; i < activeAbilities.Count; i++)
                {
                    var spec = activeAbilities[i];
                    if (spec != null && spec.IsActive && !spec.IsEnded && predicate(spec.Ability))
                        return true;
                }

                return false;
            }

            for (int i = 0; i < _lightActiveAbilities.Count; i++)
            {
                if (predicate(_lightActiveAbilities[i].Ability))
                    return true;
            }

            return false;
        }

        public GameplayAbilityDefinition FindGrantedAbilityDefinition(int abilityId)
        {
            if (abilityId == 0)
                return null;

            return FindAbilityById(abilityId);
        }

        private T FindAbility<T>() where T : GameplayAbilityDefinition
        {
            var abilities = GetGrantedAbilities();
            if (abilities == null) return null;
            foreach (var ability in abilities)
                if (ability is T typedAbility) return typedAbility;
            return null;
        }

        private GameplayAbilityDefinition FindAbilityById(int abilityId)
        {
            var abilities = GetGrantedAbilities();
            if (abilities == null) return null;
            foreach (var ability in abilities)
                if (ability.AbilityId == abilityId) return ability;
            return null;
        }

        private GameplayAbilityDefinition FindAttackAbility()
        {
            var abilities = GetGrantedAbilities();
            if (abilities == null) return null;

            if (Owner is IRangedAttackSourceProvider rangedSource && rangedSource.HasRangedWeapon)
            {
                foreach (var ability in abilities)
                {
                    if (ability is RemoteAttackAbilityDefinition)
                        return ability;
                }
            }

            return (GameplayAbilityDefinition)FindAbility<MeleeAttackAbilityDefinition>() ??
                   FindAbility<RemoteAttackAbilityDefinition>();
        }

        private IEnumerable<GameplayAbilityDefinition> GetGrantedAbilities()
        {
            return RuntimeMode == CombatAbilityRuntimeMode.Lightweight
                ? _lightAbilities
                : _gas?.Abilities?.GrantedAbilities;
        }

        private bool ActivateLightBornAbility(BornAbilityDefinition ability)
        {
            if (!CanActivateLightAbility(ability, _lightEffects))
            {
                RecordLightAbilityEvent(ability, _lightEffects, GameplayEffectEventType.AbilityFailed);
                return false;
            }

            var abilitySpecId = BeginLightAbility(ability, _lightEffects, GetLightAbilityDuration(ability));
            ApplyLightConfiguredEffects(ability, _lightEffects, abilitySpecId);
            ApplyLightEffect(ability.SelfBornEffect, _lightEffects, default, null, abilitySpecId);
            return true;
        }

        private bool ActivateLightAttackAbility(
            GameplayAbilityDefinition ability,
            CombatActor target,
            GameplayEffectRuntime targetEffects)
        {
            if (!CanActivateLightAbility(ability, targetEffects))
            {
                RecordLightAbilityEvent(ability, targetEffects, GameplayEffectEventType.AbilityFailed);
                return false;
            }

            var abilitySpecId = BeginLightAbility(ability, targetEffects, GetLightAbilityDuration(ability));
            ApplyLightConfiguredEffects(ability, targetEffects, abilitySpecId);

            if (ability is RemoteAttackAbilityDefinition remoteAbility)
                return ActivateLightRemoteAttack(remoteAbility, target, targetEffects, abilitySpecId);

            if (ability is MeleeAttackAbilityDefinition meleeAbility)
                return ActivateLightMeleeAttack(meleeAbility, abilitySpecId);

            return false;
        }

        private bool ActivateLightDeathAbility(DeathAbilityDefinition ability, GameplayEffectRuntime killerEffects)
        {
            if (!CanActivateLightAbility(ability, killerEffects))
            {
                RecordLightAbilityEvent(ability, killerEffects, GameplayEffectEventType.AbilityFailed);
                return false;
            }

            var abilitySpecId = BeginLightAbility(ability, killerEffects, GetLightAbilityDuration(ability));
            ApplyLightConfiguredEffects(ability, killerEffects, abilitySpecId);
            ApplyLightEffect(ability.SelfDeathEffect, _lightEffects, default, null, abilitySpecId);
            ApplyLightEffect(ability.KillerEffect, killerEffects, default, null, abilitySpecId);
            Actor?.BeginDeathFadeOut(ability.FadeOutDuration);
            return true;
        }

        private bool ActivateLightMeleeAttack(MeleeAttackAbilityDefinition ability, int abilitySpecId)
        {
            if (ability.DamageEffect == null || ability.HitDefinition == null || !(Owner is IMeleeAttackSourceProvider melee))
                return false;

            var targets = melee.GetMeleeTargets(ability.HitDefinition);
            if (targets == null)
                return true;

            int hitCount = 0;
            int maxTargets = Mathf.Max(1, ability.HitDefinition.MaxTargets);
            var hitEntities = new HashSet<long>();
            for (int i = 0; i < targets.Count && hitCount < maxTargets; i++)
            {
                var target = targets[i];
                var targetEffects = target?.Effects;
                if (target == null || !target.IsValidTarget || targetEffects == null)
                    continue;

                if (!hitEntities.Add(targetEffects.EntityId))
                    continue;

                ApplyLightEffect(ability.DamageEffect, targetEffects, target.Position, target, abilitySpecId);
                hitCount++;
            }

            return true;
        }

        private bool ActivateLightRemoteAttack(
            RemoteAttackAbilityDefinition ability,
            CombatActor target,
            GameplayEffectRuntime targetEffects,
            int abilitySpecId)
        {
            if (!(Owner is IRangedAttackSourceProvider sourceProvider) ||
                sourceProvider.ProjectileRuntime == null ||
                target == null ||
                !target.IsValidTarget ||
                targetEffects == null ||
                ability.DamageEffect == null)
            {
                return false;
            }

            var projectileDefinition = sourceProvider.ProjectileDefinition != null
                ? sourceProvider.ProjectileDefinition
                : ability.ProjectileDefinition;

            if (projectileDefinition == null)
                return false;

            var handle = sourceProvider.ProjectileRuntime.Spawn(new RangedProjectileRequest
            {
                Source = _lightEffects,
                Target = target,
                Definition = projectileDefinition,
                DamageEffect = ability.DamageEffect,
                Level = 1,
                StartPosition = sourceProvider.FirePosition,
                UserData = target,
                AbilityId = ability.AbilityId,
                AbilitySpecId = abilitySpecId,
            });

            return handle.IsValid;
        }

        private bool CanActivateLightAbility(GameplayAbilityDefinition ability, GameplayEffectRuntime target)
        {
            if (ability == null || _lightEffects == null)
                return false;

            return Matches(ability.SourceRequiredTags, _lightEffects) &&
                   Matches(ability.SourceBlockedTags, _lightEffects) &&
                   Matches(ability.TargetRequiredTags, target) &&
                   Matches(ability.TargetBlockedTags, target) &&
                   Matches(ability.ActivationRequiredTags, _lightEffects) &&
                   Matches(ability.ActivationBlockedTags, _lightEffects);
        }

        private static bool Matches(TagQuery query, GameplayEffectRuntime runtime)
        {
            return query == null || query.Match(runtime != null ? runtime.OwnedTags : null);
        }

        private int BeginLightAbility(
            GameplayAbilityDefinition ability,
            GameplayEffectRuntime target,
            float duration)
        {
            AddActivationOwnedTags(ability);
            // Lightweight 路径无 GameplayAbilitySpec，但仍需要一个唯一 AbilitySpecId 供溯源链路使用
            var abilitySpecId = RuntimeContext != null ? RuntimeContext.NewAbilitySpecId() : 0;
            _lightActiveAbilities.Add(new LightweightActiveAbility
            {
                Ability = ability,
                Target = target,
                EndTime = _lightTime + Mathf.Max(0.01f, duration),
                AbilitySpecId = abilitySpecId,
            });
            RecordLightAbilityEvent(ability, target, GameplayEffectEventType.AbilityActivated);
            PlayLightAbilityAnimation(ability);
            return abilitySpecId;
        }

        private void EndLightAbilityAt(int index)
        {
            var active = _lightActiveAbilities[index];
            RemoveActivationOwnedTags(active.Ability);
            RecordLightAbilityEvent(active.Ability, active.Target, GameplayEffectEventType.AbilityEnded);

            int last = _lightActiveAbilities.Count - 1;
            _lightActiveAbilities[index] = _lightActiveAbilities[last];
            _lightActiveAbilities.RemoveAt(last);
        }

        private void AddActivationOwnedTags(GameplayAbilityDefinition ability)
        {
            if (ability?.ActivationOwnedTags == null)
                return;

            AddTags(ability.ActivationOwnedTags);
        }

        private void RemoveActivationOwnedTags(GameplayAbilityDefinition ability)
        {
            if (ability?.ActivationOwnedTags == null)
                return;

            RemoveTags(ability.ActivationOwnedTags);
        }

        private void AddTags(GameplayTagContainer tags)
        {
            if (tags == null)
                return;

            for (int i = 0; i < tags.Tags.Count; i++)
                AddTag(tags.Tags[i]);
        }

        private void RemoveTags(GameplayTagContainer tags)
        {
            if (tags == null)
                return;

            for (int i = 0; i < tags.Tags.Count; i++)
                RemoveTag(tags.Tags[i]);
        }

        private void RecordLightAbilityEvent(
            GameplayAbilityDefinition ability,
            GameplayEffectRuntime target,
            GameplayEffectEventType eventType)
        {
            var context = RuntimeContext;
            if (context == null)
                return;

            context.RecordEvent(new GameplayEffectEvent
            {
                Frame = context.CurrentFrame,
                Type = eventType,
                SourceEntityId = _lightEffects != null ? _lightEffects.EntityId : Owner?.Id ?? 0,
                TargetEntityId = target != null ? target.EntityId : 0,
                AbilityId = ability != null ? ability.AbilityId : 0,
            });
        }

        private void ApplyLightConfiguredEffects(
            GameplayAbilityDefinition ability,
            GameplayEffectRuntime target,
            int abilitySpecId = 0)
        {
            if (ability?.EffectsOnActivate == null)
                return;

            for (int i = 0; i < ability.EffectsOnActivate.Count; i++)
            {
                var application = ability.EffectsOnActivate[i];
                if (application == null || application.Effect == null)
                    continue;

                ApplyLightEffect(
                    application.Effect,
                    ResolveLightTarget(application.TargetPolicy, target),
                    default,
                    null,
                    abilitySpecId);
            }
        }

        private GameplayEffectRuntime ResolveLightTarget(
            GameplayAbilityTargetPolicy policy,
            GameplayEffectRuntime target)
        {
            switch (policy)
            {
                case GameplayAbilityTargetPolicy.Source:
                case GameplayAbilityTargetPolicy.Self:
                    return _lightEffects;
                default:
                    return target ?? _lightEffects;
            }
        }

        private void ApplyLightEffect(
            GameplayEffectDefinition effect,
            GameplayEffectRuntime target,
            Vector3 position = default,
            object userData = null,
            int abilitySpecId = 0)
        {
            if (effect == null || _lightEffects == null || target == null)
                return;

            var spec = _lightEffects.MakeOutgoingSpec(target, effect, 1);
            if (spec == null)
                return;

            spec.UserData = userData;
            spec.ContextData = new CombatEffectPresentationContext(new Float3(position.x, position.y, position.z));
            // 溯源填充：abilitySpecId 由调用方透传（born/death/attack/configured 均已接入）
            spec.SourceAbilitySpecId = abilitySpecId;
            // SourceRuntimeEffectId = 0 是语义正确值：
            // 该字段仅在"由另一个 ActiveGameplayEffect 触发新 spec"时填充（如 buff 周期触发/DOT），
            // Lightweight 路径由 ability 直接施加效果，不经过 ActiveGameplayEffect，故为 0。
            // 与 FullGas 路径的 4 个填充点（技能/近战/投射物/普攻）语义一致。
            spec.SourceRuntimeEffectId = 0;
            _lightEffects.ApplySpecToTarget(spec, target);
        }

        private static DefaultGameplayEffectRuntimeContext CreateRuntimeContext(
            IRandom battleRandom)
        {
            var context = new DefaultGameplayEffectRuntimeContext();
            if (battleRandom != null)
            {
                context.SetRandom(new BattleGameplayRandomAdapter(battleRandom));
            }

            return context;
        }

        private float GetLightAbilityDuration(GameplayAbilityDefinition ability)
        {
            var clip = Actor?.GetAbilityMontage(ability);
            if (clip != null && clip.Clip != null)
                return clip.Clip.length;

            if (ability is DeathAbilityDefinition deathAbility)
                return Mathf.Max(0.01f, deathAbility.FadeOutDuration);

            return 0.1f;
        }

        private void PlayLightAbilityAnimation(GameplayAbilityDefinition ability)
        {
            var clip = Actor?.GetAbilityMontage(ability);
            if (clip == null || clip.Clip == null)
                return;

            var animancer = Actor?.Animancer;
            if (animancer == null)
                return;

            var state = animancer.Play(clip, 0.05f);
            if (state != null)
                state.Time = 0f;
        }

        public override void DeactivateForPool()
        {
            // R3-S10: 反订阅 GAS 事件，避免 PresentationSink 收到已销毁实体的事件。
            UnsubscribeGASEvents();
            _gas?.Dispose();
            _gas = null;
            _lightEffects?.Dispose();
            _lightEffects = null;
            _lightAbilities.Clear();
            _lightActiveAbilities.Clear();
            _lightTime = 0f;
            base.DeactivateForPool();
        }

        protected override void OnDispose()
        {
            UnsubscribeGASEvents();
            _gas?.Dispose();
            _gas = null;
            _lightEffects?.Dispose();
            _lightEffects = null;
            _initialAbilities.Clear();
            _lightAbilities.Clear();
            _lightActiveAbilities.Clear();
            base.OnDispose();
        }

        /// <summary>R3-S10: 反订阅 GAS 事件转发。</summary>
        private void UnsubscribeGASEvents()
        {
            var runtimeContext = RuntimeContext;
            if (runtimeContext == null) return;
            runtimeContext.Unsubscribe(GameplayEffectEventType.AbilityActivated, OnGASEventForwarded);
            runtimeContext.Unsubscribe(GameplayEffectEventType.AbilityEnded, OnGASEventForwarded);
            runtimeContext.Unsubscribe(GameplayEffectEventType.AttributeChanged, OnGASEventForwarded);
            runtimeContext.Unsubscribe(GameplayEffectEventType.CueTriggered, OnGASEventForwarded);
        }

        private class LightweightActiveAbility
        {
            public GameplayAbilityDefinition Ability;
            public GameplayEffectRuntime Target;
            public float EndTime;
            // Lightweight 路径无 GameplayAbilitySpec，这里分配一个真实 AbilitySpecId 用于溯源（投射物、事件等）
            public int AbilitySpecId;
        }
    }
}
