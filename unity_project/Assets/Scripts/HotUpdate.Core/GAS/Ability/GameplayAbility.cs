using System;
using System.Collections.Generic;
using UnityEngine;

namespace GAS
{
    [CreateAssetMenu(menuName = "PVE/GAS/Gameplay Ability")]
    public class GameplayAbilityDefinition : ScriptableObject
    {
        [Header("AbilityId")]
        public int AbilityId;
        public GameplayTag AbilityTag;

        [Header("需求")]
        public TagQuery SourceRequiredTags = new TagQuery(TagQueryOp.All);
        public TagQuery SourceBlockedTags = new TagQuery(TagQueryOp.NotAll);
        public TagQuery TargetRequiredTags = new TagQuery(TagQueryOp.All);
        public TagQuery TargetBlockedTags = new TagQuery(TagQueryOp.NotAll);

        [Header("激活策略")]
        public TagQuery ActivationRequiredTags = new TagQuery(TagQueryOp.All);
        public TagQuery ActivationBlockedTags = new TagQuery(TagQueryOp.NotAll);
        public GameplayTagContainer ActivationOwnedTags = new GameplayTagContainer();
        public TagQuery CancelAbilitiesWithTag = new TagQuery(TagQueryOp.Any);
        public TagQuery BlockAbilitiesWithTag = new TagQuery(TagQueryOp.Any);
        public bool IsIgnoreBlock;

        [Header("事件触发")]
        public List<GameplayTag> AbilityTriggers = new List<GameplayTag>();

        [Header("提交")]
        public List<GameplayEffectDefinition> CostEffects = new List<GameplayEffectDefinition>();
        public List<GameplayEffectDefinition> CooldownEffects = new List<GameplayEffectDefinition>();

        [Header("激活时的效果")]
        public List<EffectApplication> EffectsOnActivate = new List<EffectApplication>();

        [Header("延迟效果")]
        public List<DelayedEffectApplication> DelayedEffects = new List<DelayedEffectApplication>();

        [Serializable]
        public class EffectApplication
        {
            public GameplayEffectDefinition Effect;
            public GameplayAbilityTargetPolicy TargetPolicy = GameplayAbilityTargetPolicy.Target;
            public bool EndAbilityAfterApply;
        }

        [Serializable]
        public class DelayedEffectApplication
        {
            [Min(0f)] public float Delay;
            public GameplayEffectDefinition Effect;
            public GameplayAbilityTargetPolicy TargetPolicy = GameplayAbilityTargetPolicy.Target;
            public bool EndAbilityAfterApply = true;
        }

        public virtual bool CanActivateAbility(GameplayAbilitySpec spec)
        {
            if (spec == null)
                return false;

            if (!Matches(SourceRequiredTags, spec.Source))
                return false;

            if (!Matches(SourceBlockedTags, spec.Source))
                return false;

            if (!Matches(TargetRequiredTags, spec.Target))
                return false;

            if (!Matches(TargetBlockedTags, spec.Target))
                return false;

            if (!Matches(ActivationRequiredTags, spec.Source))
                return false;

            if (!Matches(ActivationBlockedTags, spec.Source))
                return false;

            return true;
        }

        public virtual void ActivateAbility(GameplayAbilitySpec spec)
        {
            ApplyConfiguredEffects(spec);
            StartDelayedEffects(spec);

            if (!spec.HasActiveTasks)
            {
                spec.EndAbility(GameplayAbilityEndReason.Completed);
            }
        }

        protected void ApplyConfiguredEffects(GameplayAbilitySpec spec)
        {
            if (EffectsOnActivate == null)
                return;

            for (int i = 0; i < EffectsOnActivate.Count; i++)
            {
                var application = EffectsOnActivate[i];

                if (application == null || application.Effect == null)
                    continue;

                spec.ApplyGameplayEffect(application.Effect, application.TargetPolicy);

                if (application.EndAbilityAfterApply)
                {
                    spec.EndAbility(GameplayAbilityEndReason.Completed);
                    return;
                }
            }
        }

        protected void StartDelayedEffects(GameplayAbilitySpec spec)
        {
            if (DelayedEffects == null)
                return;

            for (int i = 0; i < DelayedEffects.Count; i++)
            {
                var application = DelayedEffects[i];

                if (application == null || application.Effect == null)
                    continue;

                spec.AddTask(new AbilityTaskDelayedEffect(i, application));
            }
        }

        internal virtual void RestoreAbilityTasks(
            GameplayAbilitySpec spec,
            GameplayAbilityTaskState[] taskStates)
        {
            if (spec == null || taskStates == null || DelayedEffects == null)
                return;

            for (int i = 0; i < taskStates.Length; i++)
            {
                var state = taskStates[i];
                if (state.Kind != GameplayAbilityTaskKind.DelayedEffect)
                    continue;

                if (state.DefinitionIndex < 0 || state.DefinitionIndex >= DelayedEffects.Count)
                    continue;

                var application = DelayedEffects[state.DefinitionIndex];
                if (application == null || application.Effect == null)
                    continue;

                spec.RestoreTask(
                    new AbilityTaskDelayedEffect(state.DefinitionIndex, application, state.TimeLeft),
                    state.AbilityTaskId);
            }
        }

        private static bool Matches(TagQuery query, GameplayEffectRuntime runtime)
        {
            return query == null || query.Match(runtime != null ? runtime.OwnedTags : null);
        }
    }

    internal class AbilityTaskDelayedEffect : AbilityTaskWaitDelay
    {
        private readonly int definitionIndex;
        private readonly GameplayAbilityDefinition.DelayedEffectApplication application;

        public AbilityTaskDelayedEffect(
            int definitionIndex,
            GameplayAbilityDefinition.DelayedEffectApplication application)
            : base(application != null ? application.Delay : 0f)
        {
            this.definitionIndex = definitionIndex;
            this.application = application;
        }

        public AbilityTaskDelayedEffect(
            int definitionIndex,
            GameplayAbilityDefinition.DelayedEffectApplication application,
            float timeLeft)
            : base(
                application != null ? application.Delay : 0f,
                application != null ? Mathf.Max(0f, application.Delay - timeLeft) : 0f)
        {
            this.definitionIndex = definitionIndex;
            this.application = application;
        }

        protected override void OnCompleted()
        {
            var spec = AbilitySpec;
            if (spec == null || spec.IsEnded || application == null || application.Effect == null)
                return;

            spec.ApplyGameplayEffect(application.Effect, application.TargetPolicy);

            if (application.EndAbilityAfterApply)
            {
                spec.EndAbility(GameplayAbilityEndReason.Completed);
            }
        }

        internal override GameplayAbilityTaskState CaptureState()
        {
            return new GameplayAbilityTaskState
            {
                AbilityTaskId = TaskId,
                Kind = GameplayAbilityTaskKind.DelayedEffect,
                DefinitionIndex = definitionIndex,
                TimeLeft = TimeLeft,
            };
        }
    }

    public class GameplayAbilitySpec
    {
        private readonly List<AbilityTask> activeTasks = new List<AbilityTask>();

        public int AbilitySpecId { get; }
        public GameplayAbilityDefinition Ability { get; }
        public GameplayAbilityRuntime AbilityRuntime { get; }
        // 运行时引用是客户端缓存。下面的实体ID是权威标识，用于同步/回放。
        public GameplayEffectRuntime Source { get; }
        public GameplayEffectRuntime Target { get; private set; }
        public int Level { get; }
        public bool IsActive { get; private set; }
        public bool IsEnded { get; private set; }
        public GameplayAbilityEndReason EndReason { get; private set; }
        public GameplayEventData TriggerEventData { get; internal set; }

        public long SourceEntityId { get; private set; }
        public long TargetEntityId { get; private set; }
        public bool HasActiveTasks => activeTasks.Count > 0;
        public IReadOnlyList<AbilityTask> ActiveTasks => activeTasks;
        public IGameplayEffectRuntimeContext RuntimeContext => AbilityRuntime.RuntimeContext;

        internal GameplayAbilitySpec(
            int abilitySpecId,
            GameplayAbilityDefinition ability,
            GameplayAbilityRuntime abilityRuntime,
            GameplayEffectRuntime source,
            GameplayEffectRuntime target,
            int level)
        {
            AbilitySpecId = abilitySpecId;
            Ability = ability;
            AbilityRuntime = abilityRuntime;
            Source = source;
            Target = target;
            Level = level;
            SourceEntityId = source != null ? source.EntityId : 0;
            TargetEntityId = target != null ? target.EntityId : 0;
        }

        internal void MarkActive()
        {
            IsActive = true;
        }

        internal void MarkInactive()
        {
            IsActive = false;
        }

        internal void RestoreEndedState(GameplayAbilityEndReason reason)
        {
            EndReason = reason;
            IsEnded = true;
            IsActive = false;
        }

        public void SetTarget(GameplayEffectRuntime target)
        {
            Target = target;
            TargetEntityId = target != null ? target.EntityId : 0;
        }

        internal void SetAuthorityEntityIds(long sourceEntityId, long targetEntityId)
        {
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
        }

        public GameplayEffectApplyResult ApplyGameplayEffect(
            GameplayEffectDefinition effect,
            GameplayAbilityTargetPolicy targetPolicy = GameplayAbilityTargetPolicy.Target)
        {
            var target = ResolveTarget(targetPolicy);

            if (effect == null || Source == null || target == null)
                return GameplayEffectApplyResult.Failed;

            var effectSpec = Source.MakeOutgoingSpec(target, effect, Level);
            effectSpec.SourceEntityId = SourceEntityId;
            effectSpec.TargetEntityId = target.EntityId;

            return Source.ApplySpecToTarget(effectSpec, target);
        }

        public bool CanApplyGameplayEffect(
            GameplayEffectDefinition effect,
            GameplayAbilityTargetPolicy targetPolicy = GameplayAbilityTargetPolicy.Target)
        {
            var target = ResolveTarget(targetPolicy);

            if (effect == null || Source == null || target == null)
                return false;

            var effectSpec = new GameplayEffectSpec(effect, Source, target, Level)
            {
                SourceEntityId = SourceEntityId,
                TargetEntityId = target.EntityId,
            };

            return target.CanApplySpecToSelf(effectSpec);
        }

        public T AddTask<T>(T task) where T : AbilityTask
        {
            if (task == null || IsEnded)
                return task;

            InitializeAndActivateTask(task, RuntimeContext.NewAbilityTaskId());
            return task;
        }

        internal T RestoreTask<T>(T task, int taskId) where T : AbilityTask
        {
            if (task == null || IsEnded)
                return task;

            InitializeAndActivateTask(
                task,
                taskId != 0 ? taskId : RuntimeContext.NewAbilityTaskId());
            return task;
        }

        private void InitializeAndActivateTask<T>(T task, int taskId) where T : AbilityTask
        {
            activeTasks.Add(task);
            task.Initialize(this, taskId);
            AbilityRuntime.RecordAbilityTaskEvent(this, task, GameplayEffectEventType.AbilityTaskStarted);
            task.Activate();

            if (IsEnded)
                return;

            if (task.IsFinished)
            {
                RemoveTask(task);
            }
        }

        public void EndAbility(GameplayAbilityEndReason reason)
        {
            if (IsEnded)
                return;

            EndReason = reason;
            IsEnded = true;
            IsActive = false;

            for (int i = activeTasks.Count - 1; i >= 0; i--)
            {
                var task = activeTasks[i];
                task.EndTask();
                AbilityRuntime.RecordAbilityTaskEvent(
                    this,
                    task,
                    GameplayEffectEventType.AbilityTaskEnded);
            }

            activeTasks.Clear();
            AbilityRuntime.OnAbilityEnded(this);
        }

        internal void Tick(float deltaTime)
        {
            for (int i = activeTasks.Count - 1; i >= 0; i--)
            {
                var task = activeTasks[i];
                task.Tick(deltaTime);

                if (IsEnded)
                    return;

                if (task.IsFinished)
                {
                    RemoveTaskAt(i);
                }
            }

            if (activeTasks.Count == 0 && IsActive && !IsEnded)
            {
                EndAbility(GameplayAbilityEndReason.Completed);
            }
        }

        private void RemoveTask(AbilityTask task)
        {
            activeTasks.Remove(task);
            AbilityRuntime.RecordAbilityTaskEvent(this, task, GameplayEffectEventType.AbilityTaskEnded);
        }

        private void RemoveTaskAt(int index)
        {
            var task = activeTasks[index];
            activeTasks.RemoveAt(index);
            AbilityRuntime.RecordAbilityTaskEvent(this, task, GameplayEffectEventType.AbilityTaskEnded);
        }

        private GameplayEffectRuntime ResolveTarget(GameplayAbilityTargetPolicy targetPolicy)
        {
            switch (targetPolicy)
            {
                case GameplayAbilityTargetPolicy.Self:
                    return Source;

                case GameplayAbilityTargetPolicy.Source:
                    return Source;

                case GameplayAbilityTargetPolicy.Target:
                    return Target != null ? Target : Source;

                default:
                    return Target;
            }
        }
    }
}
