using System;
using System.Collections.Generic;
using UnityEngine;

namespace GAS
{
    [CreateAssetMenu(menuName = "PVE/GAS/Gameplay Effect")]
    public class GameplayEffectDefinition : ScriptableObject
    {
        [Header("EffectId")]
        public int EffectId;
        public GameplayTag EffectTag;

        [Header("持续时间")]
        public GameplayEffectDurationPolicy DurationPolicy = GameplayEffectDurationPolicy.Instant;
        [Min(0f)] public float Duration;
        [Min(0f)] public float Period;
        public bool ExecuteOnApply;

        [Header("堆叠")]
        public GameplayEffectStackPolicy StackPolicy = GameplayEffectStackPolicy.None;
        [Min(1)] public int MaxStack = 1;
        public bool RefreshDurationOnStack = true;
        public bool ReapplyModifiersOnStack = true;

        [Header("标签需求")]
        public TagQuery SourceRequiredTags = new TagQuery(TagQueryOp.All);
        public TagQuery SourceBlockedTags = new TagQuery(TagQueryOp.NotAll);
        public TagQuery TargetRequiredTags = new TagQuery(TagQueryOp.All);
        public TagQuery TargetBlockedTags = new TagQuery(TagQueryOp.NotAll);

        [Header("授予标签")]
        public GameplayTagContainer GrantedTags = new GameplayTagContainer();

        [Header("修饰符")]
        public List<Modifier> Modifiers = new List<Modifier>();

        [Header("执行")]
        public List<GameplayEffectExecution> Executions = new List<GameplayEffectExecution>();

        [Header("游戏提示")]
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
