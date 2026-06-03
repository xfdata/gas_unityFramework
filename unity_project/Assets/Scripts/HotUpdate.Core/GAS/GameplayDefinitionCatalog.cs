using System.Collections.Generic;
using UnityEngine;

namespace GAS
{
    [CreateAssetMenu(menuName = "PVE/GAS/Gameplay Definition Catalog")]
    public class GameplayDefinitionCatalog : ScriptableObject
    {
        [SerializeField]
        private List<GameplayEffectDefinition> effects = new List<GameplayEffectDefinition>();

        [SerializeField]
        private List<GameplayAbilityDefinition> abilities = new List<GameplayAbilityDefinition>();

        private Dictionary<int, GameplayEffectDefinition> effectMap;
        private Dictionary<int, GameplayAbilityDefinition> abilityMap;

        public IReadOnlyList<GameplayEffectDefinition> Effects => effects;
        public IReadOnlyList<GameplayAbilityDefinition> Abilities => abilities;

        public GameplayEffectDefinition GetEffect(int effectId)
        {
            EnsureMaps();
            return effectMap.TryGetValue(effectId, out var effect) ? effect : null;
        }

        public GameplayAbilityDefinition GetAbility(int abilityId)
        {
            EnsureMaps();
            return abilityMap.TryGetValue(abilityId, out var ability) ? ability : null;
        }

        public void RegisterEffect(GameplayEffectDefinition effect)
        {
            if (effect == null)
                return;

            if (!effects.Contains(effect))
            {
                effects.Add(effect);
            }

            effectMap = null;
        }

        public void RegisterAbility(GameplayAbilityDefinition ability)
        {
            if (ability == null)
                return;

            if (!abilities.Contains(ability))
            {
                abilities.Add(ability);
            }

            abilityMap = null;
        }

        public void RebuildMaps()
        {
            effectMap = null;
            abilityMap = null;
            EnsureMaps();
        }

        private void OnValidate()
        {
            effectMap = null;
            abilityMap = null;
        }

        private void EnsureMaps()
        {
            if (effectMap == null)
            {
                effectMap = new Dictionary<int, GameplayEffectDefinition>();

                for (int i = 0; i < effects.Count; i++)
                {
                    var effect = effects[i];

                    if (effect == null || effect.EffectId == 0)
                        continue;

                    effectMap[effect.EffectId] = effect;
                }
            }

            if (abilityMap == null)
            {
                abilityMap = new Dictionary<int, GameplayAbilityDefinition>();

                for (int i = 0; i < abilities.Count; i++)
                {
                    var ability = abilities[i];

                    if (ability == null || ability.AbilityId == 0)
                        continue;

                    abilityMap[ability.AbilityId] = ability;
                }
            }
        }
    }
}
