using UnityEngine;
using UnityEngine.Timeline;

namespace GAS
{
    [CreateAssetMenu(menuName = "BattleCommon/GAS/Ability/Melee Attack")]
    public class MeleeAttackAbilityDefinition : GameplayAbilityDefinition
    {
        private const string DefaultHitEventName = "Hit";

        [Header("Animation")]
        [Tooltip("攻击动画片段（使用 Animancer 播放）")]
        public AnimationClip AttackClip;

        [Tooltip("动画过渡时间")]
        [Min(0f)]
        public float TransitionDuration = 0.1f;

        [Tooltip("Timeline 攻击动画（优先于 AttackClip，使用 PlayableDirector 播放）")]
        public TimelineAsset AttackTimeline;

        public string HitEventName = DefaultHitEventName;

        [Header("Synchronized Melee")]
        public bool PreferSynchronizedMelee = true;
        public MeleeHitDefinition HitDefinition = new MeleeHitDefinition();

        [Header("Damage")]
        [Tooltip("碰撞命中时施加的伤害效果")]
        public GameplayEffectDefinition DamageEffect;

        public override void ActivateAbility(GameplayAbilitySpec spec)
        {
            ApplyConfiguredEffects(spec);

            if (spec.IsEnded)
                return;

            StartDelayedEffects(spec);

            var timelineProvider = spec.Source?.AttributeOwner as ITimelineProvider;
            if (AttackTimeline != null && timelineProvider?.Director != null)
            {
                ActivateTimelineMelee(spec, timelineProvider);
                return;
            }

            var meleeProvider = spec.Source?.AttributeOwner as IMeleeAttackSourceProvider;
            if (meleeProvider != null)
            {
                ActivateSynchronizedMelee(spec, meleeProvider);
                return;
            }

            spec.EndAbility(GameplayAbilityEndReason.Failed);
        }

        private void ActivateSynchronizedMelee(
            GameplayAbilitySpec spec,
            IMeleeAttackSourceProvider meleeProvider)
        {
            if (DamageEffect == null || HitDefinition == null)
            {
                spec.EndAbility(GameplayAbilityEndReason.Failed);
                return;
            }

            bool hasAppliedHit = false;

            bool ApplyHit()
            {
                if (hasAppliedHit || spec.IsEnded)
                    return true;

                hasAppliedHit = true;
                spec.AddTask(new AbilityTaskApplyMeleeHit(
                    meleeProvider,
                    HitDefinition,
                    DamageEffect));
                return true;
            }

            if (AttackClip == null)
            {
                ApplyHit();
                return;
            }

            var montageTask = spec.AddTask(new AbilityTaskPlayMontage(AttackClip, TransitionDuration, task =>
            {
                if (spec.IsEnded)
                    return;

                spec.EndAbility(GameplayAbilityEndReason.Completed);
            }));

            if (montageTask == null || montageTask.IsFinished)
            {
                ApplyHit();
                return;
            }

            bool registeredHitEvent = montageTask.TryRegisterEvent(
                string.IsNullOrEmpty(HitEventName) ? DefaultHitEventName : HitEventName,
                () => ApplyHit());

            if (!registeredHitEvent)
            {
                ApplyHit();
            }
        }

        private void ActivateTimelineMelee(
            GameplayAbilitySpec spec,
            ITimelineProvider timelineProvider)
        {
            if (DamageEffect == null || HitDefinition == null)
            {
                spec.EndAbility(GameplayAbilityEndReason.Failed);
                return;
            }

            var meleeProvider = spec.Source?.AttributeOwner as IMeleeAttackSourceProvider;
            if (meleeProvider == null)
            {
                spec.EndAbility(GameplayAbilityEndReason.Failed);
                return;
            }

            bool hasAppliedHit = false;

            bool ApplyHit()
            {
                if (hasAppliedHit || spec.IsEnded)
                    return true;

                hasAppliedHit = true;
                spec.AddTask(new AbilityTaskApplyMeleeHit(
                    meleeProvider,
                    HitDefinition,
                    DamageEffect));
                return true;
            }

            var timelineTask = spec.AddTask(new AbilityTaskPlayTimeline(AttackTimeline, task =>
            {
                if (spec.IsEnded)
                    return;

                spec.EndAbility(GameplayAbilityEndReason.Completed);
            }));

            if (timelineTask == null || timelineTask.IsFinished)
            {
                ApplyHit();
                return;
            }

            string hitEvent = string.IsNullOrEmpty(HitEventName) ? DefaultHitEventName : HitEventName;
            bool registeredHitEvent = timelineTask.TryRegisterEvent(hitEvent, () => ApplyHit());

            if (!registeredHitEvent)
            {
                ApplyHit();
            }
        }
    }
}
