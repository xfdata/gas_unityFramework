using System;
using UnityEngine;

namespace GAS
{
    // Common task for abilities that emit a simulated projectile.
    public class AbilityTaskSpawnProjectile : AbilityTask
    {
        private readonly ProjectileRuntime projectileRuntime;
        private readonly IRangedTarget target;
        private readonly RangedProjectileDefinition projectileDefinition;
        private readonly GameplayEffectDefinition damageEffect;
        private readonly Vector3 startPosition;
        private readonly object userData;
        private readonly Action<AbilityTaskSpawnProjectile, RangedProjectileResult> onCompleted;

        private RangedProjectileHandle handle;
        private bool isProjectileComplete;

        public RangedProjectileHandle Handle => handle;
        public RangedProjectileResult Result { get; private set; }

        public AbilityTaskSpawnProjectile(
            ProjectileRuntime projectileRuntime,
            IRangedTarget target,
            RangedProjectileDefinition projectileDefinition,
            GameplayEffectDefinition damageEffect,
            Vector3 startPosition,
            object userData = null,
            Action<AbilityTaskSpawnProjectile, RangedProjectileResult> onCompleted = null)
        {
            this.projectileRuntime = projectileRuntime;
            this.target = target;
            this.projectileDefinition = projectileDefinition;
            this.damageEffect = damageEffect;
            this.startPosition = startPosition;
            this.userData = userData;
            this.onCompleted = onCompleted;
        }

        protected override void OnActivate()
        {
            if (projectileRuntime == null ||
                target == null ||
                projectileDefinition == null ||
                AbilitySpec?.Source == null)
            {
                EndTask();
                return;
            }

            handle = projectileRuntime.Spawn(new RangedProjectileRequest
            {
                Source = AbilitySpec.Source,
                Target = target,
                Definition = projectileDefinition,
                DamageEffect = damageEffect,
                Level = AbilitySpec.Level,
                StartPosition = startPosition,
                UserData = userData,
                AbilityId = AbilitySpec.Ability != null ? AbilitySpec.Ability.AbilityId : 0,
                AbilitySpecId = AbilitySpec.AbilitySpecId,
                AbilityTaskId = TaskId,
                OnCompleted = HandleProjectileCompleted,
            });

            if (!handle.IsValid)
            {
                EndTask();
            }
        }

        protected override void OnEnd()
        {
            if (!isProjectileComplete && projectileRuntime != null && handle.IsValid)
            {
                projectileRuntime.Cancel(handle);
            }
        }

        private void HandleProjectileCompleted(RangedProjectileResult result)
        {
            if (IsFinished)
                return;

            isProjectileComplete = true;
            Result = result;
            onCompleted?.Invoke(this, result);
            EndTask();
        }
    }
}
