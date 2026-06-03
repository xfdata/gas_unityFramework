using UnityEngine;
using UnityEngine.Timeline;

namespace GAS
{
    [CreateAssetMenu(menuName = "BattleCommon/GAS/Ability/Born")]
    public class BornAbilityDefinition : GameplayAbilityDefinition
    {
        [Header("Born Animation")]
        [Tooltip("出生 Timeline 动画（优先，支持 PlayableDirector 的 Boss/特殊怪物）")]
        public TimelineAsset BornTimeline;

        [Tooltip("出生 Animancer 动画片段（Timeline 不可用时使用）")]
        public AnimationClip BornClip;

        [Tooltip("动画过渡时间")]
        [Min(0f)]
        public float TransitionDuration = 0.1f;

        [Header("Born Effects")]
        [Tooltip("出生时对自身施加的效果（如出场 Buff）")]
        public GameplayEffectDefinition SelfBornEffect;

        [Tooltip("出生时对目标施加的效果（如对附近敌人造成伤害）")]
        public GameplayEffectDefinition TargetBornEffect;

        public override void ActivateAbility(GameplayAbilitySpec spec)
        {
            ApplyConfiguredEffects(spec);

            if (spec.IsEnded)
                return;

            StartDelayedEffects(spec);

            ApplySelfEffect(spec);

            if (spec.IsEnded)
                return;

            var timelineProvider = spec.Source?.AttributeOwner as ITimelineProvider;
            if (BornTimeline != null && timelineProvider?.Director != null)
            {
                ActivateTimelineBorn(spec, timelineProvider);
                return;
            }

            if (BornClip != null)
            {
                ActivateMontageBorn(spec);
                return;
            }

            spec.EndAbility(GameplayAbilityEndReason.Completed);
        }

        private void ApplySelfEffect(GameplayAbilitySpec spec)
        {
            if (SelfBornEffect == null)
                return;

            spec.ApplyGameplayEffect(SelfBornEffect, GameplayAbilityTargetPolicy.Source);
        }

        private void ActivateTimelineBorn(
            GameplayAbilitySpec spec,
            ITimelineProvider timelineProvider)
        {
            var timelineTask = spec.AddTask(new AbilityTaskPlayTimeline(BornTimeline, task =>
            {
                if (spec.IsEnded)
                    return;

                spec.EndAbility(GameplayAbilityEndReason.Completed);
            }));

            if (timelineTask == null || timelineTask.IsFinished)
            {
                spec.EndAbility(GameplayAbilityEndReason.Completed);
                return;
            }

            if (TargetBornEffect != null)
            {
                timelineTask.OnEnableCollision += () =>
                {
                    if (spec.IsEnded)
                        return;

                    spec.ApplyGameplayEffect(TargetBornEffect, GameplayAbilityTargetPolicy.Target);
                };

                timelineTask.OnDisableCollision += () =>
                {
                };
            }
        }

        private void ActivateMontageBorn(GameplayAbilitySpec spec)
        {
            var montageTask = spec.AddTask(new AbilityTaskPlayMontage(BornClip, TransitionDuration, task =>
            {
                if (spec.IsEnded)
                    return;

                spec.EndAbility(GameplayAbilityEndReason.Completed);
            }));

            if (montageTask == null || montageTask.IsFinished)
            {
                spec.EndAbility(GameplayAbilityEndReason.Completed);
                return;
            }

            if (TargetBornEffect != null)
            {
                montageTask.OnEnableCollision += () =>
                {
                    if (spec.IsEnded)
                        return;

                    spec.ApplyGameplayEffect(TargetBornEffect, GameplayAbilityTargetPolicy.Target);
                };

                montageTask.OnDisableCollision += () =>
                {
                };
            }
        }
    }
}
