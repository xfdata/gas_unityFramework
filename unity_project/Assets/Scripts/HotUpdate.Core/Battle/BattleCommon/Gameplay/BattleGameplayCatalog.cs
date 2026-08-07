using System;
using System.Collections.Generic;
using GAS;
using UnityEngine;

namespace BattleCommon
{
    [Serializable]
    public sealed class BattleSkillConfig
    {
        [Min(1)] public int SkillId;
        public GameplayAbilityDefinition Ability;
        public bool IsBasicAttack;
    }

    [Serializable]
    public sealed class BattleEffectConfig
    {
        [Min(1)] public int EffectId;
        public GameplayEffectDefinition Effect;
    }

    [Serializable]
    public sealed class BattleBuffConfig
    {
        [Min(1)] public int BuffId;
        public GameplayEffectDefinition Effect;
        public bool IsDebuff;
        public bool CanDispel = true;
    }

    public interface IBattleGameplayCatalog
    {
        bool TryGetSkill(int skillId, out BattleSkillConfig config);
        bool TryGetEffect(int effectId, out BattleEffectConfig config);
        bool TryGetBuff(int buffId, out BattleBuffConfig config);
        bool TryGetSkillId(GameplayAbilityDefinition ability, out int skillId);
        bool TryGetEffectId(GameplayEffectDefinition effect, out int effectId);
        bool TryGetBuffId(GameplayEffectDefinition effect, out int buffId);
        bool TryGetBuffId(int effectId, out int buffId);
    }

    /// <summary>
    /// Thin business-to-GAS definition mapping. GAS remains the authority for all runtime state.
    /// </summary>
    [CreateAssetMenu(menuName = "Battle/Gameplay Catalog")]
    public sealed class BattleGameplayCatalog : ScriptableObject, IBattleGameplayCatalog
    {
        [SerializeField] private List<BattleSkillConfig> skills = new List<BattleSkillConfig>();
        [SerializeField] private List<BattleEffectConfig> effects = new List<BattleEffectConfig>();
        [SerializeField] private List<BattleBuffConfig> buffs = new List<BattleBuffConfig>();

        private Dictionary<int, BattleSkillConfig> _skillsById;
        private Dictionary<int, BattleEffectConfig> _effectsById;
        private Dictionary<int, BattleBuffConfig> _buffsById;
        private Dictionary<GameplayAbilityDefinition, int> _skillIdsByAbility;
        private Dictionary<GameplayEffectDefinition, int> _effectIdsByEffect;
        private Dictionary<GameplayEffectDefinition, int> _buffIdsByEffect;
        private Dictionary<int, int> _buffIdsByEffectId;
        private List<string> _validationErrors;

        public IReadOnlyList<BattleSkillConfig> Skills => skills;
        public IReadOnlyList<BattleEffectConfig> Effects => effects;
        public IReadOnlyList<BattleBuffConfig> Buffs => buffs;

        public bool IsValid
        {
            get
            {
                EnsureMaps();
                return _validationErrors.Count == 0;
            }
        }

        public bool TryGetSkill(int skillId, out BattleSkillConfig config)
        {
            EnsureMaps();
            return _skillsById.TryGetValue(skillId, out config);
        }

        public bool TryGetBuff(int buffId, out BattleBuffConfig config)
        {
            EnsureMaps();
            return _buffsById.TryGetValue(buffId, out config);
        }

        public bool TryGetEffect(int effectId, out BattleEffectConfig config)
        {
            EnsureMaps();
            return _effectsById.TryGetValue(effectId, out config);
        }

        public bool TryGetSkillId(GameplayAbilityDefinition ability, out int skillId)
        {
            skillId = 0;
            EnsureMaps();
            return ability != null && _skillIdsByAbility.TryGetValue(ability, out skillId);
        }

        public bool TryGetEffectId(GameplayEffectDefinition effect, out int effectId)
        {
            effectId = 0;
            EnsureMaps();
            return effect != null && _effectIdsByEffect.TryGetValue(effect, out effectId);
        }

        public bool TryGetBuffId(GameplayEffectDefinition effect, out int buffId)
        {
            buffId = 0;
            EnsureMaps();
            return effect != null && _buffIdsByEffect.TryGetValue(effect, out buffId);
        }

        public bool TryGetBuffId(int effectId, out int buffId)
        {
            buffId = 0;
            EnsureMaps();
            return effectId > 0 && _buffIdsByEffectId.TryGetValue(effectId, out buffId);
        }

        public void GetValidationErrors(List<string> results)
        {
            if (results == null)
                return;

            EnsureMaps();
            results.Clear();
            results.AddRange(_validationErrors);
        }

        public void RegisterSkill(int skillId, GameplayAbilityDefinition ability, bool isBasicAttack = false)
        {
            skills.Add(new BattleSkillConfig
            {
                SkillId = skillId,
                Ability = ability,
                IsBasicAttack = isBasicAttack,
            });
            InvalidateMaps();
        }

        public void RegisterBuff(int buffId, GameplayEffectDefinition effect, bool isDebuff = false, bool canDispel = true)
        {
            buffs.Add(new BattleBuffConfig
            {
                BuffId = buffId,
                Effect = effect,
                IsDebuff = isDebuff,
                CanDispel = canDispel,
            });
            InvalidateMaps();
        }

        public void RegisterEffect(int effectId, GameplayEffectDefinition effect)
        {
            effects.Add(new BattleEffectConfig
            {
                EffectId = effectId,
                Effect = effect,
            });
            InvalidateMaps();
        }

        private void OnValidate()
        {
            InvalidateMaps();
        }

        private void InvalidateMaps()
        {
            _skillsById = null;
            _effectsById = null;
            _buffsById = null;
            _skillIdsByAbility = null;
            _effectIdsByEffect = null;
            _buffIdsByEffect = null;
            _buffIdsByEffectId = null;
            _validationErrors = null;
        }

        private void EnsureMaps()
        {
            if (_skillsById != null)
                return;

            _skillsById = new Dictionary<int, BattleSkillConfig>();
            _effectsById = new Dictionary<int, BattleEffectConfig>();
            _buffsById = new Dictionary<int, BattleBuffConfig>();
            _skillIdsByAbility = new Dictionary<GameplayAbilityDefinition, int>();
            _effectIdsByEffect = new Dictionary<GameplayEffectDefinition, int>();
            _buffIdsByEffect = new Dictionary<GameplayEffectDefinition, int>();
            _buffIdsByEffectId = new Dictionary<int, int>();
            _validationErrors = new List<string>();

            for (int i = 0; i < skills.Count; i++)
                AddSkill(skills[i], i);
            for (int i = 0; i < effects.Count; i++)
                AddEffect(effects[i], i);
            for (int i = 0; i < buffs.Count; i++)
                AddBuff(buffs[i], i);
        }

        private void AddSkill(BattleSkillConfig config, int index)
        {
            if (config == null || config.SkillId <= 0 || config.Ability == null)
            {
                _validationErrors.Add($"Skill entry {index} requires a positive SkillId and an Ability.");
                return;
            }

            if (_skillsById.ContainsKey(config.SkillId) || _skillIdsByAbility.ContainsKey(config.Ability))
            {
                _validationErrors.Add($"Skill entry {index} duplicates a business SkillId or Ability reference.");
                return;
            }

            _skillsById.Add(config.SkillId, config);
            _skillIdsByAbility.Add(config.Ability, config.SkillId);
        }

        private void AddEffect(BattleEffectConfig config, int index)
        {
            if (config == null || config.EffectId <= 0 || config.Effect == null)
            {
                _validationErrors.Add($"Effect entry {index} requires a positive EffectId and an Effect.");
                return;
            }

            if (_effectsById.ContainsKey(config.EffectId) || _effectIdsByEffect.ContainsKey(config.Effect))
            {
                _validationErrors.Add($"Effect entry {index} duplicates a business EffectId or Effect reference.");
                return;
            }

            _effectsById.Add(config.EffectId, config);
            _effectIdsByEffect.Add(config.Effect, config.EffectId);
        }

        private void AddBuff(BattleBuffConfig config, int index)
        {
            if (config == null || config.BuffId <= 0 || config.Effect == null || config.Effect.EffectId <= 0)
            {
                _validationErrors.Add($"Buff entry {index} requires a positive BuffId and an Effect with a positive EffectId.");
                return;
            }

            if (_buffsById.ContainsKey(config.BuffId) ||
                _buffIdsByEffect.ContainsKey(config.Effect) ||
                _buffIdsByEffectId.ContainsKey(config.Effect.EffectId))
            {
                _validationErrors.Add($"Buff entry {index} duplicates a business BuffId or Effect reference.");
                return;
            }

            _buffsById.Add(config.BuffId, config);
            _buffIdsByEffect.Add(config.Effect, config.BuffId);
            _buffIdsByEffectId.Add(config.Effect.EffectId, config.BuffId);
        }
    }
}
