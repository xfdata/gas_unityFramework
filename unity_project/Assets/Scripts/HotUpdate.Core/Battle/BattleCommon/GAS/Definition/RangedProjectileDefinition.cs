using UnityEngine;

namespace GAS
{
    [CreateAssetMenu(menuName = "BattleCommon/GAS/Ranged Projectile")]
    public class RangedProjectileDefinition : ScriptableObject
    {
        public int ProjectileDefinitionId;

        [Min(0f)]
        public float Speed = 10f;

        [Min(0.01f)]
        public float MaxLifeTime = 5f;

        [Min(0f)]
        public float HitRadius = 0.25f;

        public string VisualKey;
    }

    public interface IRangedAttackSourceProvider
    {
        bool HasRangedWeapon { get; }
        Vector3 FirePosition { get; }
        RangedProjectileDefinition ProjectileDefinition { get; }
        ProjectileRuntime ProjectileRuntime { get; }
    }

    public interface IRangedTarget
    {
        GameplayEffectRuntime Effects { get; }
        Vector3 Position { get; }
        float HitRadius { get; }
        bool IsValidTarget { get; }
    }
}
