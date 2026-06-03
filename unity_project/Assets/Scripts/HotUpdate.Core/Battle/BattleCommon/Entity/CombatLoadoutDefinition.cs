using System.Collections.Generic;
using GAS;
using UnityEngine;

namespace BattleCommon
{
    [CreateAssetMenu(menuName = "BattleCommon/Combat Loadout")]
    public class CombatLoadoutDefinition : ScriptableObject
    {
        [Header("Attributes")]
        public float MaxHP = 100f;
        public float Attack = 10f;
        public float Defense;
        public float MoveSpeed = 3f;
        public float AttackRange = 2f;
        public float AttackInterval = 1.5f;
        public float CritRate;
        public float CritDamage = 1.5f;
        public float DamageReduce;

        [Header("Collision")]
        public float HitRadius = 0.5f;

        [Header("Abilities")]
        public List<GameplayAbilityDefinition> Abilities = new List<GameplayAbilityDefinition>();

        [Header("AI")]
        public CombatAIProfile AIProfile;
    }
}
