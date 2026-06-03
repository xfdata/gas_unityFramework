using UnityEngine;

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

        public int AttributeId;
        public float OldValue;
        public float NewValue;
        public float Delta;

        public GameplayTag GameplayTag;
        public GameplayTag CueTag;

        public Vector3 Position;
        public float Magnitude;
    }
}
