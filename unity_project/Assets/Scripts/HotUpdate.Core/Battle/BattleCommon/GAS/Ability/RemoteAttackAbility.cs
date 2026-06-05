using Animancer;
using UnityEngine;
using UnityEngine.Timeline;

namespace GAS
{
    [CreateAssetMenu(menuName = "BattleCommon/GAS/Ability/Remote Attack")]
    public class RemoteAttackAbilityDefinition : GameplayAbilityDefinition
    {
        private const string DefaultFireEventName = "Fire";

        [Header("Animation")]
        public ClipTransition AttackClip;
        public TimelineAsset AttackTimeline;

        [Min(0f)]
        public float TransitionDuration = 0.1f;

        public string FireEventName = DefaultFireEventName;

        [Header("Remote Weapon")]
        public bool RequireRangedWeapon = true;
        public RangedProjectileDefinition ProjectileDefinition;

        [Header("Damage")]
        public GameplayEffectDefinition DamageEffect;

        public override bool CanActivateAbility(GameplayAbilitySpec spec)
        {
            if (!base.CanActivateAbility(spec))
                return false;

            var sourceProvider = ResolveSourceProvider(spec);
            if (sourceProvider == null || sourceProvider.ProjectileRuntime == null)
                return false;

            if (RequireRangedWeapon && !sourceProvider.HasRangedWeapon)
                return false;

            var projectileDef = ResolveProjectileDefinition(sourceProvider);
            if (projectileDef == null || DamageEffect == null)
                return false;

            if (projectileDef.TargetType == ProjectileTargetType.PositionTarget)
            {
                return ResolveTargetPosition(spec).HasValue;
            }

            var target = ResolveTarget(spec);
            return target != null && target.IsValidTarget && target.Effects != null;
        }

        public override void ActivateAbility(GameplayAbilitySpec spec)
        {
            ApplyConfiguredEffects(spec);

            if (spec.IsEnded)
                return;

            StartDelayedEffects(spec);

            var sourceProvider = ResolveSourceProvider(spec);
            var projectileDefinition = ResolveProjectileDefinition(sourceProvider);

            if (sourceProvider == null ||
                projectileDefinition == null ||
                DamageEffect == null ||
                sourceProvider.ProjectileRuntime == null ||
                (RequireRangedWeapon && !sourceProvider.HasRangedWeapon))
            {
                spec.EndAbility(GameplayAbilityEndReason.Failed);
                return;
            }

            bool isPositionTarget = projectileDefinition.TargetType == ProjectileTargetType.PositionTarget;

            IRangedTarget target = null;
            Vector3? targetPosition = null;

            if (isPositionTarget)
            {
                targetPosition = ResolveTargetPosition(spec);
                if (!targetPosition.HasValue)
                {
                    spec.EndAbility(GameplayAbilityEndReason.Failed);
                    return;
                }
            }
            else
            {
                target = ResolveTarget(spec);
                if (target == null || !target.IsValidTarget || target.Effects == null)
                {
                    spec.EndAbility(GameplayAbilityEndReason.Failed);
                    return;
                }
            }

            bool hasFired = false;

            bool FireProjectile()
            {
                if (hasFired || spec.IsEnded)
                    return true;

                hasFired = true;

                var projectileTask = spec.AddTask(new AbilityTaskSpawnProjectile(
                    sourceProvider.ProjectileRuntime,
                    target,
                    projectileDefinition,
                    DamageEffect,
                    sourceProvider.FirePosition,
                    isPositionTarget ? null : target,
                    null,
                    targetPosition));

                return projectileTask != null && projectileTask.Handle.IsValid;
            }

            var animationProvider = ResolveAnimationProvider(spec);
            var attackTimeline = ResolveAbilityTimeline(animationProvider);
            if (attackTimeline != null && animationProvider?.Director != null)
            {
                var timelineTask = spec.AddTask(new AbilityTaskPlayTimeline(attackTimeline));
                if (timelineTask == null || timelineTask.IsFinished)
                {
                    if (!FireProjectile())
                    {
                        spec.EndAbility(GameplayAbilityEndReason.Failed);
                    }

                    return;
                }

                bool registeredTimelineFireEvent = timelineTask.TryRegisterEvent(
                    string.IsNullOrEmpty(FireEventName) ? DefaultFireEventName : FireEventName,
                    () =>
                    {
                        if (!FireProjectile())
                        {
                            spec.EndAbility(GameplayAbilityEndReason.Failed);
                        }
                    });

                if (!registeredTimelineFireEvent && !FireProjectile())
                {
                    spec.EndAbility(GameplayAbilityEndReason.Failed);
                }

                return;
            }

            var attackClip = ResolveAbilityMontage(animationProvider);
            if (!IsValidClip(attackClip))
            {
                if (!FireProjectile())
                {
                    spec.EndAbility(GameplayAbilityEndReason.Failed);
                }

                return;
            }

            var montageTask = spec.AddTask(new AbilityTaskPlayMontage(attackClip, TransitionDuration));
            if (montageTask == null || montageTask.IsFinished)
            {
                if (!FireProjectile())
                {
                    spec.EndAbility(GameplayAbilityEndReason.Failed);
                }

                return;
            }

            bool registeredFireEvent = montageTask.TryRegisterEvent(
                string.IsNullOrEmpty(FireEventName) ? DefaultFireEventName : FireEventName,
                () =>
                {
                    if (!FireProjectile())
                    {
                        spec.EndAbility(GameplayAbilityEndReason.Failed);
                    }
                });

            if (!registeredFireEvent && !FireProjectile())
            {
                spec.EndAbility(GameplayAbilityEndReason.Failed);
            }
        }

        private RangedProjectileDefinition ResolveProjectileDefinition(IRangedAttackSourceProvider sourceProvider)
        {
            if (sourceProvider == null)
                return ProjectileDefinition;

            return sourceProvider.ProjectileDefinition != null
                ? sourceProvider.ProjectileDefinition
                : ProjectileDefinition;
        }

        private static IRangedAttackSourceProvider ResolveSourceProvider(GameplayAbilitySpec spec)
        {
            return spec?.Source?.AttributeOwner as IRangedAttackSourceProvider;
        }

        private TimelineAsset ResolveAbilityTimeline(IAbilityAnimationProvider animationProvider)
        {
            return AttackTimeline != null
                ? AttackTimeline
                : animationProvider?.GetAbilityTimeline(this);
        }

        private ClipTransition ResolveAbilityMontage(IAbilityAnimationProvider animationProvider)
        {
            return IsValidClip(AttackClip)
                ? AttackClip
                : animationProvider?.GetAbilityMontage(this);
        }

        private static IAbilityAnimationProvider ResolveAnimationProvider(GameplayAbilitySpec spec)
        {
            return spec?.Source?.AttributeOwner as IAbilityAnimationProvider;
        }

        private static bool IsValidClip(ClipTransition clip)
        {
            return clip != null && clip.Clip != null;
        }

        private static IRangedTarget ResolveTarget(GameplayAbilitySpec spec)
        {
            if (spec?.Target?.AttributeOwner is IRangedTarget target)
                return target;

            return spec?.TriggerEventData.UserData as IRangedTarget;
        }

        private static Vector3? ResolveTargetPosition(GameplayAbilitySpec spec)
        {
            if (spec?.TriggerEventData.UserData is Vector3 position)
                return position;

            if (spec?.Target?.AttributeOwner is IRangedTarget rangedTarget)
                return rangedTarget.Position;

            return null;
        }
    }
}
