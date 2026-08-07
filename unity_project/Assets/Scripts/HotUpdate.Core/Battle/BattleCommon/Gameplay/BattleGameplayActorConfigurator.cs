using System;
using System.Collections.Generic;
using GAS;

namespace BattleCommon
{
    /// <summary>
    /// Applies the business GAS catalog and initial grants before an actor enters the battle runtime.
    /// </summary>
    public static class BattleGameplayActorConfigurator
    {
        public static bool ValidateUnitConfig(BattleUnitGameplayConfig config, List<string> validationErrors)
        {
            if (validationErrors == null)
                throw new ArgumentNullException(nameof(validationErrors));

            validationErrors.Clear();
            if (config == null)
            {
                validationErrors.Add("BattleUnitGameplayConfig is required.");
                return false;
            }

            var grantedAbilities = ResolveGrantedAbilities(
                config.GameplayCatalog,
                config.GrantedSkillIds,
                validationErrors);
            var requiredSkillIds = CollectRequiredSkillIds(config, validationErrors);
            if (validationErrors.Count > 0)
                return false;

            return Validate(config.GameplayCatalog, grantedAbilities, requiredSkillIds, validationErrors);
        }

        public static bool ConfigureBeforeInitialize(
            CombatActor actor,
            BattleUnitGameplayConfig config,
            List<string> validationErrors = null)
        {
            var errors = validationErrors ?? new List<string>();
            if (!ValidateUnitConfig(config, errors))
                return false;

            var catalog = config.GameplayCatalog;
            var grantedAbilities = ResolveGrantedAbilities(catalog, config.GrantedSkillIds, errors);
            var requiredSkillIds = CollectRequiredSkillIds(config, errors);

            if (config.BasicAttackSkillId > 0 && actor?.Get<CombatAttackComponent>() == null)
            {
                errors.Add("BasicAttackSkillId requires CombatAttackComponent.");
                return false;
            }

            if (!ConfigureBeforeInitialize(actor, catalog, grantedAbilities, requiredSkillIds, errors))
                return false;

            if (config.BasicAttackSkillId > 0)
                actor.Get<CombatAttackComponent>().BasicAttackSkillId = config.BasicAttackSkillId;

            return true;
        }

        public static bool ConfigureBeforeInitialize(
            CombatActor actor,
            IBattleGameplayCatalog catalog,
            IEnumerable<GameplayAbilityDefinition> initialAbilities,
            IEnumerable<int> requiredSkillIds,
            List<string> validationErrors = null)
        {
            var errors = validationErrors ?? new List<string>();
            errors.Clear();

            if (actor == null)
                errors.Add("Gameplay actor is required.");
            else if (actor.IsInitialized)
                errors.Add("Gameplay actor must be configured before initialization.");

            var abilities = actor?.Get<CombatAbilityComponent>();
            if (actor != null && abilities == null)
                errors.Add("Gameplay actor requires CombatAbilityComponent.");

            var grantedAbilities = CollectAbilities(initialAbilities, errors);
            Validate(catalog, grantedAbilities, requiredSkillIds, errors);
            if (errors.Count > 0)
                return false;

            actor.GameplayCatalog = catalog;
            abilities.SetInitialAbilities(grantedAbilities);
            return true;
        }

        public static bool Validate(
            IBattleGameplayCatalog catalog,
            IEnumerable<GameplayAbilityDefinition> grantedAbilities,
            IEnumerable<int> requiredSkillIds,
            List<string> validationErrors)
        {
            if (validationErrors == null)
                throw new ArgumentNullException(nameof(validationErrors));

            if (catalog == null)
            {
                validationErrors.Add("BattleGameplayCatalog is required.");
                return false;
            }

            if (catalog is BattleGameplayCatalog concreteCatalog)
                concreteCatalog.GetValidationErrors(validationErrors);

            var granted = new HashSet<GameplayAbilityDefinition>();
            if (grantedAbilities != null)
            {
                foreach (var ability in grantedAbilities)
                {
                    if (ability != null)
                        granted.Add(ability);
                }
            }

            if (requiredSkillIds != null)
            {
                foreach (var skillId in requiredSkillIds)
                {
                    if (skillId <= 0)
                    {
                        validationErrors.Add("Required SkillId must be positive.");
                        continue;
                    }

                    if (!catalog.TryGetSkill(skillId, out var config) || config?.Ability == null)
                    {
                        validationErrors.Add($"Required SkillId {skillId} is not present in BattleGameplayCatalog.");
                        continue;
                    }

                    if (!granted.Contains(config.Ability))
                    {
                        validationErrors.Add(
                            $"Required SkillId {skillId} maps to an Ability that is not granted to the actor.");
                    }
                }
            }

            return validationErrors.Count == 0;
        }

        private static List<GameplayAbilityDefinition> CollectAbilities(
            IEnumerable<GameplayAbilityDefinition> initialAbilities,
            List<string> validationErrors)
        {
            var results = new List<GameplayAbilityDefinition>();
            if (initialAbilities == null)
                return results;

            foreach (var ability in initialAbilities)
            {
                if (ability == null)
                {
                    validationErrors.Add("Initial Ability cannot be null.");
                    continue;
                }

                if (!results.Contains(ability))
                    results.Add(ability);
            }

            return results;
        }

        private static List<GameplayAbilityDefinition> ResolveGrantedAbilities(
            IBattleGameplayCatalog catalog,
            IReadOnlyList<int> grantedSkillIds,
            List<string> validationErrors)
        {
            var results = new List<GameplayAbilityDefinition>();
            if (catalog == null)
            {
                validationErrors.Add("BattleGameplayCatalog is required.");
                return results;
            }

            if (grantedSkillIds == null)
                return results;

            var seenSkillIds = new HashSet<int>();
            for (int i = 0; i < grantedSkillIds.Count; i++)
            {
                var skillId = grantedSkillIds[i];
                if (skillId <= 0)
                {
                    validationErrors.Add("Granted SkillId must be positive.");
                    continue;
                }

                if (!seenSkillIds.Add(skillId))
                {
                    validationErrors.Add($"Granted SkillId {skillId} is duplicated.");
                    continue;
                }

                if (!catalog.TryGetSkill(skillId, out var config) || config?.Ability == null)
                {
                    validationErrors.Add($"Granted SkillId {skillId} is not present in BattleGameplayCatalog.");
                    continue;
                }

                if (!results.Contains(config.Ability))
                    results.Add(config.Ability);
            }

            return results;
        }

        private static void AddRequiredSkill(List<int> skillIds, int skillId)
        {
            if (skillId > 0 && !skillIds.Contains(skillId))
                skillIds.Add(skillId);
        }

        private static List<int> CollectRequiredSkillIds(
            BattleUnitGameplayConfig config,
            List<string> validationErrors)
        {
            var results = new List<int>(config.GrantedSkillIds);
            AddRequiredSkill(results, config.BasicAttackSkillId);
            AddAiSkillIds(results, config.AiSkillIds, validationErrors);
            return results;
        }

        private static void AddAiSkillIds(
            List<int> requiredSkillIds,
            IReadOnlyList<int> aiSkillIds,
            List<string> validationErrors)
        {
            if (aiSkillIds == null)
                return;

            var seenAiSkillIds = new HashSet<int>();
            for (int i = 0; i < aiSkillIds.Count; i++)
            {
                var skillId = aiSkillIds[i];
                if (skillId <= 0)
                {
                    validationErrors.Add("AI SkillId must be positive.");
                    continue;
                }

                if (!seenAiSkillIds.Add(skillId))
                {
                    validationErrors.Add($"AI SkillId {skillId} is duplicated.");
                    continue;
                }

                AddRequiredSkill(requiredSkillIds, skillId);
            }
        }
    }
}
