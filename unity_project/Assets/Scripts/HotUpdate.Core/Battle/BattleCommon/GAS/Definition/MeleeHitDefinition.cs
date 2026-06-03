using System.Collections.Generic;
using UnityEngine;

namespace GAS
{
    // Shared melee hit geometry used by any real-time battle mode.
    [System.Serializable]
    public class MeleeHitDefinition
    {
        public int MeleeDefinitionId;

        [Min(0f)]
        public float Range = 1.5f;

        [Min(0f)]
        public float Radius = 0.5f;

        [Min(1)]
        public int MaxTargets = 1;
    }

    public interface IMeleeAttackSourceProvider
    {
        Vector3 MeleeOrigin { get; }
        Vector3 MeleeForward { get; }

        IReadOnlyList<IRangedTarget> GetMeleeTargets(MeleeHitDefinition hitDefinition);
    }
}
