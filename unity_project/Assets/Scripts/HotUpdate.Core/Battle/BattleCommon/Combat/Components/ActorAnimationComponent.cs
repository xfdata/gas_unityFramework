using System;
using Animancer;
using BattleCommon;
using BattleFoundation;
using Framework;
using GAS;
using UnityEngine;

namespace BattleCommon
{
    public class ActorAnimationComponent : CombatComponentBase
    {
        private const float DefaultIdleFadeDuration = 0.05f;

        private static readonly string[] DefaultIdleStateNames =
        {
            "idle",
            "Stand",
            "Standby",
        };

        private CombatActor _combatActor;
        private CombatAbilityComponent _ability;
        private IGameplayEffectRuntimeContext _abilityRuntimeContext;
        private IUnitAnimationConfig _unitConfig;
        private bool _playIdleWhenAbilitiesEnd;
        private float _pendingIdleFadeDuration = DefaultIdleFadeDuration;

        public override void Attach(BattleEntity owner)
        {
            base.Attach(owner);
            _combatActor = owner as CombatActor;
            _ability = _combatActor?.Get<CombatAbilityComponent>();
        }

        public override void Initialize()
        {
            base.Initialize();
            _ability = _combatActor?.Get<CombatAbilityComponent>();
            _unitConfig = null;
            _playIdleWhenAbilitiesEnd = false;
            _pendingIdleFadeDuration = DefaultIdleFadeDuration;
            SubscribeAbilityEvents();
        }

        public override void Update(float deltaTime)
        {
            using (new AutoProfiler("BattleCommon.AnimationComponent.Update"))
            {
                TickIdleRecovery();
            }
        }

        public ClipTransition GetAbilityMontage(GameplayAbilityDefinition ability)
        {
            if (ability is BornAbilityDefinition)
                return ResolveValidClip(ResolveUnitConfig()?.SpawnAnim);

            if (ability is DeathAbilityDefinition)
                return ResolveValidClip(ResolveUnitConfig()?.DeathAnim);

            if (ability is MeleeAttackAbilityDefinition || ability is RemoteAttackAbilityDefinition)
                return ResolveValidClip(ResolveUnitConfig()?.AttackAnim);

            return null;
        }

        public bool PlayIdle(float fadeDuration = DefaultIdleFadeDuration)
        {
            var animator = _combatActor?.Animator;
            if (animator == null || !animator.isActiveAndEnabled)
                return false;

            if (TryPlayIdleWithAnimancer(animator, fadeDuration))
                return true;

            if (animator.runtimeAnimatorController == null)
                return false;

            for (int i = 0; i < DefaultIdleStateNames.Length; i++)
            {
                int stateHash = Animator.StringToHash(DefaultIdleStateNames[i]);
                if (!animator.HasState(0, stateHash))
                    continue;

                animator.CrossFade(stateHash, fadeDuration, 0, 0f);
                return true;
            }

            return false;
        }

        public void RequestIdleWhenReady(float fadeDuration = DefaultIdleFadeDuration)
        {
            _playIdleWhenAbilitiesEnd = true;
            _pendingIdleFadeDuration = Mathf.Max(0f, fadeDuration);
            TryPlayRequestedIdle();
        }

        public override void DeactivateForPool()
        {
            UnsubscribeAbilityEvents();
            _unitConfig = null;
            _playIdleWhenAbilitiesEnd = false;
            _pendingIdleFadeDuration = DefaultIdleFadeDuration;
            base.DeactivateForPool();
        }

        protected override void OnDispose()
        {
            UnsubscribeAbilityEvents();
            _combatActor = null;
            _ability = null;
            _unitConfig = null;
            _playIdleWhenAbilitiesEnd = false;
            _pendingIdleFadeDuration = DefaultIdleFadeDuration;
            base.OnDispose();
        }

        private void SubscribeAbilityEvents()
        {
            UnsubscribeAbilityEvents();

            _abilityRuntimeContext = _ability?.RuntimeContext;
            _abilityRuntimeContext?.Subscribe(GameplayEffectEventType.AbilityActivated, OnAbilityActivated);
            _abilityRuntimeContext?.Subscribe(GameplayEffectEventType.AbilityEnded, OnAbilityEnded);
        }

        private void UnsubscribeAbilityEvents()
        {
            if (_abilityRuntimeContext == null)
                return;

            _abilityRuntimeContext.Unsubscribe(GameplayEffectEventType.AbilityActivated, OnAbilityActivated);
            _abilityRuntimeContext.Unsubscribe(GameplayEffectEventType.AbilityEnded, OnAbilityEnded);
            _abilityRuntimeContext = null;
        }

        private void OnAbilityActivated(GameplayEffectEvent gameplayEvent)
        {
            if (_combatActor == null || gameplayEvent.SourceEntityId != _combatActor.Id)
                return;

            if (IsDeathAbilityEvent(gameplayEvent))
            {
                _playIdleWhenAbilitiesEnd = false;
                return;
            }

            _playIdleWhenAbilitiesEnd = true;
        }

        private void OnAbilityEnded(GameplayEffectEvent gameplayEvent)
        {
            if (_combatActor == null || gameplayEvent.SourceEntityId != _combatActor.Id)
                return;

            if (IsDeathAbilityEvent(gameplayEvent))
                return;

            RequestIdleWhenReady(_pendingIdleFadeDuration);
        }

        private void TickIdleRecovery()
        {
            if (IsDeathPresentationActive())
            {
                _playIdleWhenAbilitiesEnd = false;
                return;
            }

            if (HasActiveNonDeathAbility())
            {
                _playIdleWhenAbilitiesEnd = true;
                return;
            }

            TryPlayRequestedIdle();
        }

        private void TryPlayRequestedIdle()
        {
            if (!_playIdleWhenAbilitiesEnd)
                return;

            if (IsDeathPresentationActive() || HasActiveNonDeathAbility())
                return;

            _playIdleWhenAbilitiesEnd = false;
            PlayIdle(_pendingIdleFadeDuration);
        }

        private IUnitAnimationConfig ResolveUnitConfig()
        {
            if (_unitConfig == null && _combatActor?.GameObject != null)
                _unitConfig = _combatActor.GameObject.GetComponentInChildren<IUnitAnimationConfig>();

            return _unitConfig;
        }

        private bool TryPlayIdleWithAnimancer(Animator animator, float fadeDuration)
        {
            var animancer = _combatActor?.Animancer;
            if (animancer == null)
                return false;

            var idleTransition = ResolveUnitConfig()?.IdleAnim;
            if (IsValidClip(idleTransition))
            {
                var state = animancer.Play(idleTransition, fadeDuration);
                state.Time = 0f;
                state.Events.OnEnd = null;
                return true;
            }

            var clip = FindDefaultIdleClip(animator);
            if (clip == null)
                return false;

            var fallbackState = animancer.Play(clip, fadeDuration);
            fallbackState.Time = 0f;
            fallbackState.Events.OnEnd = null;
            return true;
        }

        private bool HasActiveNonDeathAbility()
        {
            return _combatActor?.Get<CombatAbilityComponent>()?.HasActiveAbility(ability => !IsDeathAbility(ability)) ?? false;
        }

        private bool IsDeathPresentationActive()
        {
            return _combatActor == null ||
                   !_combatActor.IsAlive ||
                   IsAbilityActiveByTag(CombatGameplayTags.Ability_Death);
        }

        private bool IsAbilityActiveByTag(GameplayTag tag)
        {
            return _combatActor?.Get<CombatAbilityComponent>()?.HasActiveAbility(tag) ?? false;
        }

        private bool IsDeathAbilityEvent(GameplayEffectEvent gameplayEvent)
        {
            if (gameplayEvent.AbilityId == CombatAbilityIds.Death)
                return true;

            return IsDeathAbility(FindGrantedAbilityDefinition(gameplayEvent.AbilityId));
        }

        private GameplayAbilityDefinition FindGrantedAbilityDefinition(int abilityId)
        {
            if (abilityId == 0)
                return null;

            return _combatActor?.Get<CombatAbilityComponent>()?.FindGrantedAbilityDefinition(abilityId);
        }

        private static ClipTransition ResolveValidClip(ClipTransition clip)
        {
            return IsValidClip(clip) ? clip : null;
        }

        private static bool IsValidClip(ClipTransition clip)
        {
            return clip != null && clip.Clip != null;
        }

        private static bool IsDeathAbility(GameplayAbilityDefinition ability)
        {
            return ability is DeathAbilityDefinition;
        }

        private static AnimationClip FindDefaultIdleClip(Animator animator)
        {
            var controller = animator.runtimeAnimatorController;
            if (controller?.animationClips == null)
                return null;

            var clips = controller.animationClips;
            for (int nameIndex = 0; nameIndex < DefaultIdleStateNames.Length; nameIndex++)
            {
                string stateName = DefaultIdleStateNames[nameIndex];
                for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
                {
                    var clip = clips[clipIndex];
                    if (clip != null && string.Equals(clip.name, stateName, StringComparison.OrdinalIgnoreCase))
                        return clip;
                }
            }

            return null;
        }
    }
}