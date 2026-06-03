using UnityEngine;

namespace GAS
{
    [CreateAssetMenu(menuName = "BattleCommon/GAS/Ability/Remote Attack")]
    public class RemoteAttackAbilityDefinition : GameplayAbilityDefinition
    {
        private const string DefaultFireEventName = "Fire";

        [Header("Animation")]
        public AnimationClip AttackClip;

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

            if (ResolveProjectileDefinition(sourceProvider) == null || DamageEffect == null)
                return false;

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
            var target = ResolveTarget(spec);
            var projectileDefinition = ResolveProjectileDefinition(sourceProvider);

            if (sourceProvider == null ||
                target == null ||
                !target.IsValidTarget ||
                target.Effects == null ||
                projectileDefinition == null ||
                DamageEffect == null ||
                sourceProvider.ProjectileRuntime == null ||
                (RequireRangedWeapon && !sourceProvider.HasRangedWeapon))
            {
                spec.EndAbility(GameplayAbilityEndReason.Failed);
                return;
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
                    target));

                return projectileTask != null && projectileTask.Handle.IsValid;
            }

            if (AttackClip == null)
            {
                if (!FireProjectile())
                {
                    spec.EndAbility(GameplayAbilityEndReason.Failed);
                }

                return;
            }

            var montageTask = spec.AddTask(new AbilityTaskPlayMontage(AttackClip, TransitionDuration));
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

        private static IRangedTarget ResolveTarget(GameplayAbilitySpec spec)
        {
            if (spec?.Target?.AttributeOwner is IRangedTarget target)
                return target;

            return spec?.TriggerEventData.UserData as IRangedTarget;
        }
    }
}
