using System;
using System.Collections.Generic;
using UnityEngine;

namespace GAS
{
    [CreateAssetMenu(menuName = "PVE/GAS/Gameplay Effect")]
    public class GameplayEffectDefinition : ScriptableObject
    {
        [Header("Identity")]
        public int EffectId;
        public GameplayTag EffectTag;

        [Header("Duration")]
        public GameplayEffectDurationPolicy DurationPolicy = GameplayEffectDurationPolicy.Instant;
        [Min(0f)] public float Duration;
        [Min(0f)] public float Period;
        public bool ExecuteOnApply;

        [Header("Stack")]
        public GameplayEffectStackPolicy StackPolicy = GameplayEffectStackPolicy.None;
        [Min(1)] public int MaxStack = 1;
        public bool RefreshDurationOnStack = true;
        public bool ReapplyModifiersOnStack = true;

        [Header("Tag Requirements")]
        public TagQuery SourceRequiredTags = new TagQuery(TagQueryOp.All);
        public TagQuery SourceBlockedTags = new TagQuery(TagQueryOp.NotAll);
        public TagQuery TargetRequiredTags = new TagQuery(TagQueryOp.All);
        public TagQuery TargetBlockedTags = new TagQuery(TagQueryOp.NotAll);

        [Header("Granted Tags")]
        public GameplayTagContainer GrantedTags = new GameplayTagContainer();

        [Header("Modifiers")]
        public List<Modifier> Modifiers = new List<Modifier>();

        [Header("Executions")]
        public List<GameplayEffectExecution> Executions = new List<GameplayEffectExecution>();

        [Header("Gameplay Cues")]
        public List<Cue> Cues = new List<Cue>();

        [Serializable]
        public class Modifier
        {
            public int AttributeId;
            public AttributeModifierOp Op = AttributeModifierOp.Add;
            public float Value;
            public bool ScaleByStack = true;
        }

        [Serializable]
        public class Cue
        {
            public GameplayTag CueTag;
            public GameplayCuePolicy Policy = GameplayCuePolicy.Static;

            public bool OnApply = true;
            public bool OnExecute = true;
            public bool OnRemove = true;
        }
    }
}
