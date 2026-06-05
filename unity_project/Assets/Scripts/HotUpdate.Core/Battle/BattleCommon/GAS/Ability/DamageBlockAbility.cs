using UnityEngine;

namespace GAS
{
    public class DamageBlockContext
    {
        public GameplayEffectSpec DamageSpec;
        public GameplayEffectRuntime Source;
        public GameplayEffectRuntime Target;
        public long SourceEntityId;
        public long TargetEntityId;
        public float IncomingDamage;
        public float BlockedDamage;
        public float RemainingDamage;

        public bool IsBlocked => BlockedDamage > 0f;

        public DamageBlockContext(
            GameplayEffectSpec damageSpec,
            GameplayEffectRuntime source,
            GameplayEffectRuntime target,
            float incomingDamage)
        {
            DamageSpec = damageSpec;
            Source = source;
            Target = target;
            SourceEntityId = source != null ? source.EntityId : damageSpec != null ? damageSpec.SourceEntityId : 0;
            TargetEntityId = target != null ? target.EntityId : damageSpec != null ? damageSpec.TargetEntityId : 0;
            IncomingDamage = Mathf.Max(0f, incomingDamage);
            RemainingDamage = IncomingDamage;
        }
    }

    [CreateAssetMenu(menuName = "BattleCommon/GAS/Ability/Passive Damage Block")]
    public class DamageBlockAbilityDefinition : GameplayAbilityDefinition
    {
        [Header("Block")]
        public GameplayTag DamageTakenEventTag;

        [Range(0f, 1f)]
        public float BlockChance = 0.5f;

        [Range(0f, 1f)]
        public float BlockDamageRatio = 1f;

        public bool TryActivateBlock(GameplayAbilitySystem ownerGAS, DamageBlockContext blockContext)
        {
            if (ownerGAS?.Abilities == null || blockContext == null || blockContext.IncomingDamage <= 0f)
                return false;

            var triggerData = new GameplayEventData
            {
                EventTag = DamageTakenEventTag,
                Source = blockContext.Source,
                Target = blockContext.Target,
                SourceEntityId = blockContext.SourceEntityId,
                TargetEntityId = blockContext.TargetEntityId,
                UserData = blockContext,
            };

            ownerGAS.Abilities.ActivateAbility(this, null, 1, triggerData);
            return blockContext.IsBlocked;
        }

        public override bool CanActivateAbility(GameplayAbilitySpec spec)
        {
            if (!base.CanActivateAbility(spec))
                return false;

            if (!(spec.TriggerEventData.UserData is DamageBlockContext blockContext) ||
                blockContext.IncomingDamage <= 0f)
            {
                return false;
            }

            float chance = Mathf.Clamp01(BlockChance);
            return chance > 0f && spec.Source != null && spec.Source.NextRandomFloat() < chance;
        }

        public override void ActivateAbility(GameplayAbilitySpec spec)
        {
            if (!(spec.TriggerEventData.UserData is DamageBlockContext blockContext))
            {
                spec.EndAbility(GameplayAbilityEndReason.Failed);
                return;
            }

            float blockedDamage = blockContext.IncomingDamage * Mathf.Clamp01(BlockDamageRatio);
            blockContext.BlockedDamage = Mathf.Max(0f, blockedDamage);
            blockContext.RemainingDamage = Mathf.Max(0f, blockContext.IncomingDamage - blockContext.BlockedDamage);

            RecordDamageBlockedEvent(spec, blockContext);
            spec.EndAbility(GameplayAbilityEndReason.Completed);
        }

        private void RecordDamageBlockedEvent(GameplayAbilitySpec spec, DamageBlockContext blockContext)
        {
            var runtimeContext = spec?.RuntimeContext;
            if (runtimeContext == null || blockContext == null || blockContext.BlockedDamage <= 0f)
                return;

            var damageSpec = blockContext.DamageSpec;
            runtimeContext.RecordEvent(new GameplayEffectEvent
            {
                Frame = runtimeContext.CurrentFrame,
                Type = GameplayEffectEventType.DamageBlocked,
                SourceEntityId = blockContext.SourceEntityId,
                TargetEntityId = blockContext.TargetEntityId,
                EffectId = damageSpec?.Asset != null ? damageSpec.Asset.EffectId : 0,
                SpecId = damageSpec != null ? damageSpec.SpecId : 0,
                RuntimeEffectId = damageSpec != null ? damageSpec.RuntimeEffectId : 0,
                AbilityId = spec.Ability != null ? spec.Ability.AbilityId : 0,
                AbilitySpecId = spec.AbilitySpecId,
                AttributeId = BattleCommon.CombatAttributeIds.HP,
                OldValue = blockContext.IncomingDamage,
                NewValue = blockContext.RemainingDamage,
                Delta = -blockContext.BlockedDamage,
                Magnitude = blockContext.BlockedDamage,
            });
        }
    }
}
