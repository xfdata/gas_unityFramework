using System.Collections.Generic;
using UnityEngine;

namespace BattleCommon
{
    public static class CombatAIPresetBuilder
    {
        public static CombatAIComponent BuildMeleeMonsterAI(CombatActor owner, CombatAIProfile profile)
        {
            var ai = owner.AddComponent<CombatAIComponent>();
            ai.SetProfile(CreateRuntimeProfile(profile));
            return ai;
        }

        public static CombatAIComponent BuildRangedMonsterAI(CombatActor owner, CombatAIProfile profile)
        {
            var ai = owner.AddComponent<CombatAIComponent>();
            ai.SetProfile(CreateRuntimeProfile(profile));
            return ai;
        }

        public static CombatAIComponent BuildBossMonsterAI(CombatActor owner, CombatAIProfile profile, List<int> skillAbilityIds = null)
        {
            var runtimeProfile = CreateRuntimeProfile(profile);
            if (runtimeProfile != null && skillAbilityIds != null)
            {
                runtimeProfile.SkillAbilityIds.Clear();
                runtimeProfile.SkillAbilityIds.AddRange(skillAbilityIds);
            }

            var ai = owner.AddComponent<CombatAIComponent>();
            ai.SetProfile(runtimeProfile);
            return ai;
        }

        public static CombatAIComponent BuildPatrolMonsterAI(CombatActor owner, CombatAIProfile profile, List<Vector3> waypoints = null)
        {
            var runtimeProfile = CreateRuntimeProfile(profile);
            if (runtimeProfile != null && waypoints != null)
            {
                runtimeProfile.PatrolWaypoints.Clear();
                runtimeProfile.PatrolWaypoints.AddRange(waypoints);
            }

            var ai = owner.AddComponent<CombatAIComponent>();
            ai.SetProfile(runtimeProfile);
            return ai;
        }

        public static CombatAIComponent BuildPlayerHeroAI(CombatActor owner, CombatAIProfile profile)
        {
            var ai = owner.AddComponent<CombatAIComponent>();
            ai.SetProfile(CreateRuntimeProfile(profile));
            return ai;
        }

        public static CombatAIComponent BuildAggressiveAI(CombatActor owner, CombatAIProfile profile)
        {
            var ai = owner.AddComponent<CombatAIComponent>();
            ai.SetProfile(CreateRuntimeProfile(profile));
            return ai;
        }

        public static CombatAIComponent BuildDefensiveAI(CombatActor owner, CombatAIProfile profile)
        {
            var runtimeProfile = CreateRuntimeProfile(profile);
            var ai = owner.AddComponent<CombatAIComponent>();
            if (runtimeProfile != null)
                runtimeProfile.CanFlee = true;
            ai.SetProfile(runtimeProfile);
            return ai;
        }

        private static CombatAIProfile CreateRuntimeProfile(CombatAIProfile profile)
        {
            return profile?.Clone();
        }
    }
}
