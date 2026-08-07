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
    public class CombatActor : BattleEntity, ICombatActor, IGameplayAttributeOwner, IGameplayAttributeSetProvider, ICombatTarget, IMeleeSource, IAbilityAnimationProvider
    {
        private readonly List<IRangedTarget> _meleeTargetCache = new List<IRangedTarget>(16);

        // R3-S4/S7: 纯逻辑数据字段（逻辑真相），不再读写 Transform。
        // 表现层（ActorViewBinder）通过 IActorViewBinding.SyncTransform 接收单向投影。
        private Float3 _position;
        private Float4 _rotation = new Float4(0f, 0f, 0f, 1f);

        public ICombatAbilityServices AbilityServices { get; set; }
        public IBattleGameplayCatalog GameplayCatalog { get; set; }
        public IBattleGameplay Gameplay => Get<BattleGameplayFacadeComponent>();

        /// <summary>
        /// R3-S4: 表现层绑定契约。L3 ActorViewBinder 实现此接口，
        /// L2 通过接口下发单向指令（SyncTransform/PlayHit/PlayDeath/DestroyView 等）。
        /// </summary>
        public IActorViewBinding ViewBinding { get; set; }

        /// <summary>
        /// R3-S7: 攻击动画片段（ClipTransition 是 Animancer 数据类，非 Unity 引擎对象）。
        /// 保留在 CombatActor 上供 IAbilityAnimationProvider 使用，S8 迁移到 ActorViewBinder。
        /// </summary>
        public ClipTransition AttackClip { get; protected set; }

        // R3-S7: Unity 引擎对象通过 ViewBinding 转发，CombatActor 不再直接持有。
        // 过渡期表现组件（ActorPresentationComponent/ActorAnimationComponent）通过这些属性访问 Unity 资源。
        public GameObject GameObject => (ViewBinding as IActorViewResources)?.GameObject;
        public Transform Transform => (ViewBinding as IActorViewResources)?.Transform;
        public Animator Animator => (ViewBinding as IActorViewResources)?.Animator;
        public AnimancerComponent Animancer => (ViewBinding as IActorViewResources)?.Animancer;
        public PlayableDirector Director => (ViewBinding as IActorViewResources)?.Director;

        public virtual float HitRadius { get; protected set; } = 0.5f;
        public virtual bool IsValidTarget => IsAlive && (Gameplay?.States.CanBeTargeted() ?? true);

        // R3-S4: MeleeOrigin/MeleeForward 保持 Vector3 类型（IMeleeAttackSourceProvider 契约），
        // 内部从纯逻辑数据计算，不再读 Transform。
        public Vector3 MeleeOrigin => new Vector3(_position.x, _position.y, _position.z);
        public Vector3 MeleeForward => ComputeForward(_rotation);

        public override void Initialize()
        {
            if (Get<BattleGameplayFacadeComponent>() == null)
                AddComponent<BattleGameplayFacadeComponent>();

            base.Initialize();
        }
        public override Float3 Position
        {
            get => _position;
            set
            {
                _position = value;
                ViewBinding?.SyncTransform(_position, _rotation);
            }
        }

        // 显式实现 IRangedTarget.Position（Vector3），保持与 ProjectileRuntime 的 Vector3 体系兼容
        Vector3 IRangedTarget.Position => new Vector3(_position.x, _position.y, _position.z);

        public override Float4 Rotation
        {
            get => _rotation;
            set
            {
                _rotation = value;
                ViewBinding?.SyncTransform(_position, _rotation);
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
            // R3-S4: 转发到表现层绑定。
            ViewBinding?.BeginDeathFadeOut(duration);
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
            // R3-S4: 表现侧缓存清理委托给 ViewBinding。
            ViewBinding?.OnRecycle();
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

        // R3-S4: 从 Float4 Rotation 纯数学推导 forward 向量，不读 Transform。
        private static Vector3 ComputeForward(Float4 r)
        {
            float x = r.x, y = r.y, z = r.z, w = r.w;
            return new Vector3(
                2f * (x * z + w * y),
                2f * (y * z - w * x),
                1f - 2f * (x * x + y * y));
        }

        protected override void OnDispose()
        {
            _meleeTargetCache.Clear();
            // R3-S4/S7: GameObject 销毁委托给 ViewBinding。
            ViewBinding?.DestroyView();
            ViewBinding = null;
            AbilityServices = null;
            AttackClip = null;
            base.OnDispose();
        }
    }
}
