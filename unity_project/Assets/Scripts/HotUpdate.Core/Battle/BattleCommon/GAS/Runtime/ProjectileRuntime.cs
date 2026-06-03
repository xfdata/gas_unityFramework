using System;
using System.Collections.Generic;
using UnityEngine;

namespace GAS
{
    public readonly struct RangedProjectileHandle : IEquatable<RangedProjectileHandle>
    {
        public static readonly RangedProjectileHandle Invalid = new RangedProjectileHandle(0);

        public readonly int Id;

        public bool IsValid => Id != 0;

        public RangedProjectileHandle(int id)
        {
            Id = id;
        }

        public bool Equals(RangedProjectileHandle other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            return obj is RangedProjectileHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id;
        }

        public static bool operator ==(RangedProjectileHandle left, RangedProjectileHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RangedProjectileHandle left, RangedProjectileHandle right)
        {
            return !left.Equals(right);
        }
    }

    public enum RangedProjectileEndReason
    {
        Hit,
        Cancelled,
        TimedOut,
        TargetInvalid,
    }

    public readonly struct RangedProjectileResult
    {
        public readonly RangedProjectileHandle Handle;
        public readonly RangedProjectileEndReason Reason;
        public readonly Vector3 Position;

        public bool DidHit => Reason == RangedProjectileEndReason.Hit;

        public RangedProjectileResult(
            RangedProjectileHandle handle,
            RangedProjectileEndReason reason,
            Vector3 position)
        {
            Handle = handle;
            Reason = reason;
            Position = position;
        }
    }

    public readonly struct RangedProjectileState
    {
        public readonly int ProjectileId;
        public readonly int ProjectileDefinitionId;
        public readonly long SourceEntityId;
        public readonly long TargetEntityId;
        public readonly Vector3 Position;
        public readonly float Elapsed;

        public RangedProjectileState(
            int projectileId,
            int projectileDefinitionId,
            long sourceEntityId,
            long targetEntityId,
            Vector3 position,
            float elapsed)
        {
            ProjectileId = projectileId;
            ProjectileDefinitionId = projectileDefinitionId;
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            Position = position;
            Elapsed = elapsed;
        }
    }

    public class RangedProjectileRequest
    {
        public GameplayEffectRuntime Source;
        public IRangedTarget Target;
        public RangedProjectileDefinition Definition;
        public GameplayEffectDefinition DamageEffect;
        public int Level = 1;
        public Vector3 StartPosition;
        public object UserData;
        public int AbilityId;
        public int AbilitySpecId;
        public int AbilityTaskId;
        public Action<RangedProjectileResult> OnCompleted;
    }

    public class ProjectileSpawner
    {
        private readonly ProjectileRuntime runtime;

        public ProjectileSpawner(ProjectileRuntime runtime)
        {
            this.runtime = runtime;
        }

        public RangedProjectileHandle Spawn(RangedProjectileRequest request)
        {
            if (runtime == null ||
                request == null ||
                request.Source == null ||
                request.Target == null ||
                request.Definition == null)
                return RangedProjectileHandle.Invalid;

            if (!ProjectileRuntime.IsTargetValid(request.Target))
                return RangedProjectileHandle.Invalid;

            var context = request.Source.RuntimeContext;
            var handle = new RangedProjectileHandle(context.NewProjectileId());

            runtime.AddProjectile(new ProjectileRuntime.ProjectileInstance
            {
                Handle = handle,
                Source = request.Source,
                Target = request.Target,
                Definition = request.Definition,
                DamageEffect = request.DamageEffect,
                Level = request.Level,
                Position = request.StartPosition,
                UserData = request.UserData,
                AbilityId = request.AbilityId,
                AbilitySpecId = request.AbilitySpecId,
                AbilityTaskId = request.AbilityTaskId,
                OnCompleted = request.OnCompleted,
            });

            runtime.RecordProjectileEvent(
                GameplayEffectEventType.ProjectileSpawned,
                handle,
                request.Source.EntityId,
                request.Target.Effects.EntityId,
                request.Definition,
                request.DamageEffect,
                request.AbilityId,
                request.AbilitySpecId,
                request.AbilityTaskId,
                request.StartPosition);

            return handle;
        }
    }

    // Shared deterministic projectile state; presentation remains in each gameplay mode.
    public class ProjectileRuntime
    {
        private readonly List<ProjectileInstance> projectiles = new List<ProjectileInstance>();

        public ProjectileRuntime()
        {
            Spawner = new ProjectileSpawner(this);
        }

        public ProjectileSpawner Spawner { get; }
        public int ActiveCount => projectiles.Count;

        public RangedProjectileHandle Spawn(RangedProjectileRequest request)
        {
            return Spawner.Spawn(request);
        }

        public bool Cancel(RangedProjectileHandle handle)
        {
            if (!handle.IsValid)
                return false;

            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                if (projectiles[i].Handle != handle)
                    continue;

                CompleteAt(i, RangedProjectileEndReason.Cancelled, false);
                return true;
            }

            return false;
        }

        public bool IsActive(RangedProjectileHandle handle)
        {
            if (!handle.IsValid)
                return false;

            for (int i = 0; i < projectiles.Count; i++)
            {
                if (projectiles[i].Handle == handle)
                    return true;
            }

            return false;
        }

        public RangedProjectileState[] CaptureStates()
        {
            if (projectiles.Count == 0)
                return Array.Empty<RangedProjectileState>();

            var states = new RangedProjectileState[projectiles.Count];
            for (int i = 0; i < projectiles.Count; i++)
            {
                var projectile = projectiles[i];
                states[i] = new RangedProjectileState(
                    projectile.Handle.Id,
                    GetProjectileDefinitionId(projectile.Definition),
                    projectile.Source != null ? projectile.Source.EntityId : 0,
                    projectile.Target?.Effects != null ? projectile.Target.Effects.EntityId : 0,
                    projectile.Position,
                    projectile.Elapsed);
            }

            return states;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
                deltaTime = 0f;

            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                var projectile = projectiles[i];

                if (!IsTargetValid(projectile.Target))
                {
                    CompleteAt(i, RangedProjectileEndReason.TargetInvalid, true);
                    continue;
                }

                projectile.Elapsed += deltaTime;
                if (projectile.Elapsed >= projectile.Definition.MaxLifeTime)
                {
                    CompleteAt(i, RangedProjectileEndReason.TimedOut, true);
                    continue;
                }

                var targetPosition = projectile.Target.Position;
                var hitRadius = Mathf.Max(projectile.Definition.HitRadius, projectile.Target.HitRadius);

                if (Vector3.Distance(projectile.Position, targetPosition) <= hitRadius)
                {
                    projectiles[i] = projectile;
                    CompleteAt(i, RangedProjectileEndReason.Hit, true);
                    continue;
                }

                var speed = Mathf.Max(0f, projectile.Definition.Speed);
                if (speed > 0f)
                {
                    projectile.Position = Vector3.MoveTowards(
                        projectile.Position,
                        targetPosition,
                        speed * deltaTime);
                }

                projectiles[i] = projectile;

                if (Vector3.Distance(projectile.Position, targetPosition) <= hitRadius)
                {
                    CompleteAt(i, RangedProjectileEndReason.Hit, true);
                }
            }
        }

        internal static bool IsTargetValid(IRangedTarget target)
        {
            return target != null && target.IsValidTarget && target.Effects != null;
        }

        internal void AddProjectile(ProjectileInstance projectile)
        {
            projectiles.Add(projectile);
        }

        private void CompleteAt(int index, RangedProjectileEndReason reason, bool notify)
        {
            var projectile = projectiles[index];
            var eventType = GetEventType(reason);

            RecordProjectileEvent(
                eventType,
                projectile.Handle,
                projectile.Source != null ? projectile.Source.EntityId : 0,
                projectile.Target?.Effects != null ? projectile.Target.Effects.EntityId : 0,
                projectile.Definition,
                projectile.DamageEffect,
                projectile.AbilityId,
                projectile.AbilitySpecId,
                projectile.AbilityTaskId,
                projectile.Position);

            if (reason == RangedProjectileEndReason.Hit)
            {
                ApplyDamage(projectile);
            }

            projectiles.RemoveAt(index);

            if (notify)
            {
                projectile.OnCompleted?.Invoke(
                    new RangedProjectileResult(projectile.Handle, reason, projectile.Position));
            }
        }

        internal void RecordProjectileEvent(
            GameplayEffectEventType eventType,
            RangedProjectileHandle handle,
            long sourceEntityId,
            long targetEntityId,
            RangedProjectileDefinition projectileDefinition,
            GameplayEffectDefinition damageEffect,
            int abilityId,
            int abilitySpecId,
            int abilityTaskId,
            Vector3 position)
        {
            var context = ResolveContext(sourceEntityId, targetEntityId);
            if (context == null)
                return;

            context.RecordEvent(new GameplayEffectEvent
            {
                Frame = context.CurrentFrame,
                Type = eventType,
                SourceEntityId = sourceEntityId,
                TargetEntityId = targetEntityId,
                EffectId = damageEffect != null ? damageEffect.EffectId : 0,
                AbilityId = abilityId,
                AbilitySpecId = abilitySpecId,
                AbilityTaskId = abilityTaskId,
                ProjectileId = handle.Id,
                ProjectileDefinitionId = GetProjectileDefinitionId(projectileDefinition),
                Position = position,
            });
        }

        private IGameplayEffectRuntimeContext ResolveContext(long sourceEntityId, long targetEntityId)
        {
            for (int i = 0; i < projectiles.Count; i++)
            {
                var projectile = projectiles[i];
                if (projectile.Source != null && projectile.Source.EntityId == sourceEntityId)
                    return projectile.Source.RuntimeContext;

                if (projectile.Target?.Effects != null && projectile.Target.Effects.EntityId == targetEntityId)
                    return projectile.Target.Effects.RuntimeContext;
            }

            return null;
        }

        private static GameplayEffectEventType GetEventType(RangedProjectileEndReason reason)
        {
            switch (reason)
            {
                case RangedProjectileEndReason.Hit:
                    return GameplayEffectEventType.ProjectileHit;

                case RangedProjectileEndReason.Cancelled:
                    return GameplayEffectEventType.ProjectileCancelled;

                case RangedProjectileEndReason.TimedOut:
                    return GameplayEffectEventType.ProjectileTimedOut;

                case RangedProjectileEndReason.TargetInvalid:
                    return GameplayEffectEventType.ProjectileTargetInvalid;

                default:
                    return GameplayEffectEventType.ProjectileCancelled;
            }
        }

        private static int GetProjectileDefinitionId(RangedProjectileDefinition definition)
        {
            return definition != null ? definition.ProjectileDefinitionId : 0;
        }

        private void ApplyDamage(ProjectileInstance projectile)
        {
            if (projectile.Source == null ||
                projectile.Target == null ||
                projectile.DamageEffect == null)
                return;

            var targetRuntime = projectile.Target.Effects;
            if (targetRuntime == null)
                return;

            var effectSpec = projectile.Source.MakeOutgoingSpec(
                targetRuntime,
                projectile.DamageEffect,
                projectile.Level);

            if (effectSpec == null)
                return;

            effectSpec.SourceEntityId = projectile.Source.EntityId;
            effectSpec.TargetEntityId = targetRuntime.EntityId;
            effectSpec.Position = projectile.Position;
            effectSpec.UserData = projectile.UserData ?? projectile.Target;

            projectile.Source.ApplySpecToTarget(effectSpec, targetRuntime);
        }

        public struct ProjectileInstance
        {
            public RangedProjectileHandle Handle;
            public GameplayEffectRuntime Source;
            public IRangedTarget Target;
            public RangedProjectileDefinition Definition;
            public GameplayEffectDefinition DamageEffect;
            public int Level;
            public Vector3 Position;
            public float Elapsed;
            public object UserData;
            public int AbilityId;
            public int AbilitySpecId;
            public int AbilityTaskId;
            public Action<RangedProjectileResult> OnCompleted;
        }
    }
}
