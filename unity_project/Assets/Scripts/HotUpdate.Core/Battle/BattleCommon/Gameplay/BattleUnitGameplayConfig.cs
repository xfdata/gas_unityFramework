using System.Collections.Generic;
using UnityEngine;

namespace BattleCommon
{
    /// <summary>
    /// Business-facing unit gameplay configuration. All skill values are Battle SkillIds.
    /// </summary>
    [CreateAssetMenu(menuName = "Battle/Unit Gameplay Config")]
    public sealed class BattleUnitGameplayConfig : ScriptableObject
    {
        [SerializeField] private BattleGameplayCatalog gameplayCatalog;
        [SerializeField] private List<int> grantedSkillIds = new List<int>();
        [SerializeField] private int basicAttackSkillId;
        [SerializeField] private List<int> aiSkillIds = new List<int>();

        public BattleGameplayCatalog GameplayCatalog => gameplayCatalog;
        public IReadOnlyList<int> GrantedSkillIds => grantedSkillIds;
        public int BasicAttackSkillId => basicAttackSkillId;
        public IReadOnlyList<int> AiSkillIds => aiSkillIds;

        public bool IsValid
        {
            get
            {
                var errors = new List<string>();
                return BattleGameplayActorConfigurator.ValidateUnitConfig(this, errors);
            }
        }

        public void GetValidationErrors(List<string> results)
        {
            if (results == null)
                return;

            BattleGameplayActorConfigurator.ValidateUnitConfig(this, results);
        }

        public void SetGameplayCatalog(BattleGameplayCatalog value)
        {
            gameplayCatalog = value;
        }

        public void RegisterGrantedSkill(int skillId)
        {
            if (skillId > 0 && !grantedSkillIds.Contains(skillId))
                grantedSkillIds.Add(skillId);
        }

        public void SetBasicAttackSkill(int skillId)
        {
            basicAttackSkillId = skillId;
        }

        public void RegisterAiSkill(int skillId)
        {
            if (skillId > 0 && !aiSkillIds.Contains(skillId))
                aiSkillIds.Add(skillId);
        }
    }
}
