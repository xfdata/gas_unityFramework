using System;
using System.Collections.Generic;
using UnityEngine;

namespace GAS
{
    // Common task: the source supplies target querying, the task only applies effects.
    public class AbilityTaskApplyMeleeHit : AbilityTask
    {
        private readonly IMeleeAttackSourceProvider sourceProvider;
        private readonly MeleeHitDefinition hitDefinition;
        private readonly GameplayEffectDefinition damageEffect;
        private readonly object userData;
        private readonly Action<AbilityTaskApplyMeleeHit, int> onCompleted;

        public int HitCount { get; private set; }

        public AbilityTaskApplyMeleeHit(
            IMeleeAttackSourceProvider sourceProvider,
            MeleeHitDefinition hitDefinition,
            GameplayEffectDefinition damageEffect,
            object userData = null,
            Action<AbilityTaskApplyMeleeHit, int> onCompleted = null)
        {
            this.sourceProvider = sourceProvider;
            this.hitDefinition = hitDefinition;
            this.damageEffect = damageEffect;
            this.userData = userData;
            this.onCompleted = onCompleted;
        }

        protected override void OnActivate()
        {
            if (AbilitySpec?.Source == null || sourceProvider == null || hitDefinition == null)
            {
                EndTask();
                return;
            }

            RecordMeleeEvent(GameplayEffectEventType.MeleeWindowStarted, 0, sourceProvider.MeleeOrigin);

            var targets = sourceProvider.GetMeleeTargets(hitDefinition);
            ApplyTargets(targets);

            RecordMeleeEvent(GameplayEffectEventType.MeleeWindowEnded, 0, sourceProvider.MeleeOrigin);
            onCompleted?.Invoke(this, HitCount);
            EndTask();
        }

        private void ApplyTargets(IReadOnlyList<IRangedTarget> targets)
        {
            if (targets == null || damageEffect == null)
                return;

            var hitEntities = new HashSet<long>();
            int maxTargets = Mathf.Max(1, hitDefinition.MaxTargets);

            for (int i = 0; i < targets.Count && HitCount < maxTargets; i++)
            {
                var target = targets[i];
                if (target == null || !target.IsValidTarget || target.Effects == null)
                    continue;

                var targetRuntime = target.Effects;
                if (!hitEntities.Add(targetRuntime.EntityId))
                    continue;

                var effectSpec = AbilitySpec.Source.MakeOutgoingSpec(
                    targetRuntime,
                    damageEffect,
                    AbilitySpec.Level);

                if (effectSpec == null)
                    continue;

                effectSpec.SourceEntityId = AbilitySpec.SourceEntityId;
                effectSpec.TargetEntityId = targetRuntime.EntityId;
                effectSpec.Position = target.Position;
                effectSpec.UserData = userData ?? target;

                AbilitySpec.Source.ApplySpecToTarget(effectSpec, targetRuntime);
                HitCount++;

                RecordMeleeEvent(
                    GameplayEffectEventType.MeleeHit,
                    targetRuntime.EntityId,
                    target.Position);
            }
        }

        private void RecordMeleeEvent(
            GameplayEffectEventType eventType,
            long targetEntityId,
            Vector3 position)
        {
            var context = AbilitySpec.RuntimeContext;
            context.RecordEvent(new GameplayEffectEvent
            {
                Frame = context.CurrentFrame,
                Type = eventType,
                SourceEntityId = AbilitySpec.SourceEntityId,
                TargetEntityId = targetEntityId,
                EffectId = damageEffect != null ? damageEffect.EffectId : 0,
                AbilityId = AbilitySpec.Ability != null ? AbilitySpec.Ability.AbilityId : 0,
                AbilitySpecId = AbilitySpec.AbilitySpecId,
                AbilityTaskId = TaskId,
                MeleeDefinitionId = hitDefinition != null ? hitDefinition.MeleeDefinitionId : 0,
                Position = position,
                Magnitude = HitCount,
            });
        }
    }
}
