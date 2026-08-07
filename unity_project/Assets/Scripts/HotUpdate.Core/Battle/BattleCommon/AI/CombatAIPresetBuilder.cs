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

        public static CombatAIComponent BuildBossMonsterAI(CombatActor owner, CombatAIProfile profile, List<int> skillIds = null)
        {
            var runtimeProfile = CreateRuntimeProfile(profile);
            if (runtimeProfile != null && skillIds != null)
            {
                runtimeProfile.SkillIds.Clear();
                runtimeProfile.SkillIds.AddRange(skillIds);
            }

            var ai = owner.AddComponent<CombatAIComponent>();
            ai.SetProfile(runtimeProfile);
            return ai;
        }

        public static void ApplyGameplaySkillIds(CombatAIProfile profile, BattleUnitGameplayConfig gameplayConfig)
        {
            if (profile == null || gameplayConfig == null)
                return;

            profile.SkillIds.Clear();
            for (int i = 0; i < gameplayConfig.AiSkillIds.Count; i++)
                profile.SkillIds.Add(gameplayConfig.AiSkillIds[i]);
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
