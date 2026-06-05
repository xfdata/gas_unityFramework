using Animancer;
using UnityEngine;
using UnityEngine.Timeline;

namespace GAS
{
    [CreateAssetMenu(menuName = "BattleCommon/GAS/Ability/Death")]
    public class DeathAbilityDefinition : GameplayAbilityDefinition
    {
        [Header("Death Animation")]
        public TimelineAsset DeathTimeline;
        public ClipTransition DeathClip;

        [Min(0f)]
        public float TransitionDuration = 0.1f;

        [Header("Death Presentation")]
        [Min(0f)]
        public float FadeOutDuration = 1f;

        [Header("Death Effects")]
        public GameplayTag DeathStateTag;
        public GameplayEffectDefinition SelfDeathEffect;
        public GameplayEffectDefinition KillerEffect;

        public override void ActivateAbility(GameplayAbilitySpec spec)
        {
            if (DeathStateTag.IsValid)
                spec.Source?.OwnedTags?.AddTag(DeathStateTag);

            ApplyConfiguredEffects(spec);

            if (spec.IsEnded)
                return;

            StartDelayedEffects(spec);
            ApplyDeathEffects(spec);

            if (spec.IsEnded)
                return;

            var animationProvider = ResolveAnimationProvider(spec);
            var deathTimeline = ResolveAbilityTimeline(animationProvider);
            if (deathTimeline != null && animationProvider?.Director != null)
            {
                ActivateTimelineDeath(spec, deathTimeline);
                return;
            }

            var deathClip = ResolveAbilityMontage(animationProvider);
            if (IsValidClip(deathClip))
            {
                ActivateMontageDeath(spec, deathClip);
                return;
            }

            BeginDeathFadeOut(spec);
        }

        private void ApplyDeathEffects(GameplayAbilitySpec spec)
        {
            if (SelfDeathEffect != null)
            {
                spec.ApplyGameplayEffect(SelfDeathEffect, GameplayAbilityTargetPolicy.Source);
            }

            if (KillerEffect != null)
            {
                spec.ApplyGameplayEffect(KillerEffect, GameplayAbilityTargetPolicy.Target);
            }
        }

        private void ActivateTimelineDeath(GameplayAbilitySpec spec, TimelineAsset deathTimeline)
        {
            var timelineTask = spec.AddTask(new AbilityTaskPlayTimeline(deathTimeline, task =>
            {
                BeginDeathFadeOut(spec);
            }));

            if (timelineTask == null || timelineTask.IsFinished)
            {
                BeginDeathFadeOut(spec);
            }
        }

        private void ActivateMontageDeath(GameplayAbilitySpec spec, ClipTransition deathClip)
        {
            var montageTask = spec.AddTask(new AbilityTaskPlayMontage(deathClip, TransitionDuration, task =>
            {
                BeginDeathFadeOut(spec);
            }));

            if (montageTask == null || montageTask.IsFinished)
            {
                BeginDeathFadeOut(spec);
            }
        }

        private void BeginDeathFadeOut(GameplayAbilitySpec spec)
        {
            if (spec == null || spec.IsEnded)
                return;

            ResolveAnimationProvider(spec)?.BeginDeathFadeOut(FadeOutDuration);

            var fadeTask = spec.AddTask(new AbilityTaskWaitDelay(FadeOutDuration, task =>
            {
                if (!spec.IsEnded)
                    spec.EndAbility(GameplayAbilityEndReason.Completed);
            }));

            if (fadeTask == null || fadeTask.IsFinished)
            {
                spec.EndAbility(GameplayAbilityEndReason.Completed);
            }
        }

        private TimelineAsset ResolveAbilityTimeline(IAbilityAnimationProvider animationProvider)
        {
            return DeathTimeline != null
                ? DeathTimeline
                : animationProvider?.GetAbilityTimeline(this);
        }

        private ClipTransition ResolveAbilityMontage(IAbilityAnimationProvider animationProvider)
        {
            return IsValidClip(DeathClip)
                ? DeathClip
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
    }
}
