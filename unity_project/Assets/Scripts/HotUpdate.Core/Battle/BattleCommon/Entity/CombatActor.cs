using System;
using System.Collections.Generic;
using Animancer;
using BattleFoundation;
using GAS;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BattleCommon
{
    public class CombatActor : BattleEntity, IGameplayAttributeOwner, IGameplayAttributeSetProvider, ICombatTarget, IMeleeSource, IAbilityAnimationProvider
    {
        private readonly List<IRangedTarget> _meleeTargetCache = new List<IRangedTarget>(16);
        private AnimancerComponent _animancer;
        private PlayableDirector _director;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation = Quaternion.identity;

        public GameObject GameObject { get; set; }
        public Transform Transform { get; set; }
        public Animator Animator { get; set; }
        public ICombatAbilityServices AbilityServices { get; set; }
        public ClipTransition AttackClip { get; protected set; }
        public Vector3 SpawnPosition
        {
            get => _spawnPosition;
            set => _spawnPosition = value;
        }
        public AnimancerComponent Animancer => ResolveAnimancer();
        public PlayableDirector Director => ResolveDirector();
        public virtual float HitRadius { get; protected set; } = 0.5f;
        public virtual bool IsValidTarget => IsAlive;
        public Vector3 MeleeOrigin => Position;
        public Vector3 MeleeForward => Transform != null ? Transform.forward : Rotation * Vector3.forward;

        public override Vector3 Position
        {
            get => Transform != null ? Transform.position : _spawnPosition;
            set
            {
                _spawnPosition = value;
                if (Transform != null) Transform.position = value;
            }
        }

        public override Quaternion Rotation
        {
            get => Transform != null ? Transform.rotation : _spawnRotation;
            set
            {
                _spawnRotation = value;
                if (Transform != null) Transform.rotation = value;
            }
        }

        public override bool IsAlive
        {
            get
            {
                var health = Get<CombatHealthComponent>();
                return health == null ? base.IsAlive : health.IsAlive;
            }
            set => base.IsAlive = value;
        }

        public virtual AttributeSet AttributeSet => Get<CombatAttributeComponent>()?.AttributeSet;
        public virtual GameplayEffectRuntime Effects => Get<CombatAbilityComponent>()?.Effects;
        public virtual float GetAttribute(int attributeId) => Get<CombatAttributeComponent>()?.GetAttribute(attributeId) ?? 0f;
        public virtual void AddAttributeBaseValue(int attributeId, float delta) => Get<CombatAttributeComponent>()?.AddAttributeBaseValue(attributeId, delta);
        public virtual AttributeModifierHandle AddModifier(int attributeId, AttributeModifierOp op, float value, object source)
            => Get<CombatAttributeComponent>()?.AddModifier(attributeId, op, value, source) ?? AttributeModifierHandle.Invalid;
        public virtual void RemoveModifier(AttributeModifierHandle handle) => Get<CombatAttributeComponent>()?.RemoveModifier(handle);

        public virtual bool CanRecycle
        {
            get
            {
                var health = Get<CombatHealthComponent>();
                return health != null && health.IsDead;
            }
        }

        public IReadOnlyList<IRangedTarget> GetMeleeTargets(MeleeHitDefinition hitDefinition)
        {
            _meleeTargetCache.Clear();
            var query = Engine?.Context?.GetSystem<CombatTargetQuerySystem>();
            query?.FindMeleeTargets(this, hitDefinition, _meleeTargetCache);
            return _meleeTargetCache;
        }

        public virtual ClipTransition GetAbilityMontage(GameplayAbilityDefinition ability)
        {
            if (!IsAttackAbility(ability) || !IsValidClip(AttackClip))
                return null;

            return AttackClip;
        }

        public virtual TimelineAsset GetAbilityTimeline(GameplayAbilityDefinition ability)
        {
            return null;
        }

        public virtual void BeginDeathFadeOut(float duration)
        {
        }

        public void MoveTo(Vector3 destination)
        {
            Get<CombatMovementComponent>()?.MoveTo(destination);
        }

        public void StopMove()
        {
            Get<CombatMovementComponent>()?.StopMove();
        }

        public override void DeactivateForPool()
        {
            base.DeactivateForPool();
            _animancer = null;
            _director = null;
            AttackClip = null;
        }

        protected static bool IsAttackAbility(GameplayAbilityDefinition ability)
        {
            return ability != null &&
                   (ability is MeleeAttackAbilityDefinition ||
                    ability is RemoteAttackAbilityDefinition);
        }

        protected static bool IsBornAbility(GameplayAbilityDefinition ability)
        {
            return ability != null &&
                   ability is BornAbilityDefinition;
        }

        protected static bool IsDeathAbility(GameplayAbilityDefinition ability)
        {
            return ability != null &&
                   ability is DeathAbilityDefinition;
        }

        protected static bool IsValidClip(ClipTransition clip)
        {
            return clip != null && clip.Clip != null;
        }

        private AnimancerComponent ResolveAnimancer()
        {
            if (_animancer == null && GameObject != null)
                _animancer = GameObject.GetComponentInChildren<AnimancerComponent>();
            return _animancer;
        }

        private PlayableDirector ResolveDirector()
        {
            if (_director == null && GameObject != null)
                _director = GameObject.GetComponentInChildren<PlayableDirector>();
            return _director;
        }

        protected override void OnDispose()
        {
            _meleeTargetCache.Clear();
            if (GameObject != null)
                UnityEngine.Object.Destroy(GameObject);
            GameObject = null;
            Transform = null;
            Animator = null;
            _animancer = null;
            _director = null;
            AbilityServices = null;
            AttackClip = null;
            base.OnDispose();
        }
    }
}
