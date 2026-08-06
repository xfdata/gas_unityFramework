namespace GAS
{
    public enum GameplayEffectEventType : byte
    {
        EffectApplied,
        EffectExecuted,
        EffectStackChanged,
        EffectRemoved,
        AttributeChanged,
        ModifierAdded,
        ModifierRemoved,
        TagAdded,
        TagRemoved,
        CueTriggered,
        AbilityActivated,
        AbilityCommitted,
        AbilityEnded,
        AbilityFailed,
        AbilityTaskStarted,
        AbilityTaskEnded,
        ProjectileSpawned,
        ProjectileHit,
        ProjectileCancelled,
        ProjectileTimedOut,
        ProjectileTargetInvalid,
        MeleeWindowStarted,
        MeleeHit,
        MeleeWindowEnded,
        RestoreEffectSkipped,
        RestoreAbilitySkipped,
        DamageBlocked,
    }

    public struct GameplayEffectEvent
    {
        public int Frame;
        public GameplayEffectEventType Type;

        public long SourceEntityId;
        public long TargetEntityId;

        public int EffectId;
        public int SpecId;
        public int RuntimeEffectId;
        public int AbilityId;
        public int AbilitySpecId;
        public int AbilityTaskId;
        public int ProjectileId;
        public int ProjectileDefinitionId;
        public int MeleeDefinitionId;

        // 溯源字段：触发该 Effect 的源（与 SpecId/RuntimeEffectId/AbilitySpecId 区分）
        // - SourceAbilitySpecId：触发该 spec 的源 AbilitySpec 的 SpecId
        // - SourceRuntimeEffectId：触发该 spec 的源 ActiveGameplayEffect 的 RuntimeEffectId
        // 仅 Effect 相关事件（经 CreateBaseEvent 构造）从 spec 读取填充；
        // Ability/Projectile/Melee 等源头事件本身不需要溯源，保持 0。
        public int SourceAbilitySpecId;
        public int SourceRuntimeEffectId;

        public int AttributeId;
        public float OldValue;
        public float NewValue;
        public float Delta;

        public GameplayTag GameplayTag;
        public GameplayTag CueTag;

        public object ContextData;
        public float Magnitude;
    }
}
