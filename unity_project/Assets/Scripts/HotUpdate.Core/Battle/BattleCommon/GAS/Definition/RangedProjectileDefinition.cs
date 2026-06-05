using UnityEngine;

namespace GAS
{
    public enum ProjectileTrajectoryType
    {
        Linear,
        Parabolic,
    }

    public enum ProjectileTargetType
    {
        EntityTarget,
        PositionTarget,
    }

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

        [Header("Trajectory")]
        public ProjectileTrajectoryType TrajectoryType = ProjectileTrajectoryType.Linear;

        [Tooltip("抛物线最大高度（仅 Parabolic 有效，0 表示自动计算为射程的 25%）")]
        [Min(0f)]
        public float ArcHeight;

        [Tooltip("抛物线方向偏移系数（0=纯垂直弧线，1=水平抛物运动，推荐 0.5~1）")]
        [Range(0f, 1f)]
        public float ParabolicHorizontalWeight = 0.7f;

        [Header("Collision")]
        public ProjectileTargetType TargetType = ProjectileTargetType.EntityTarget;

        [Tooltip("爆炸/碰撞半径（>0 时命中目标后对范围内所有敌人施加效果）")]
        [Min(0f)]
        public float ExplosionRadius;

        [Tooltip("飞行过程中每隔多少秒做一次 sweep 碰撞检测（0=仅终点检测）")]
        [Min(0f)]
        public float SweepInterval = 0.1f;

        [Tooltip("sweep 碰撞检测半径")]
        [Min(0f)]
        public float SweepRadius = 0.5f;
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
