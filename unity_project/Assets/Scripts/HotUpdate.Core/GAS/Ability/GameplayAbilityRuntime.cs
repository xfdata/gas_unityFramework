using System;
using System.Collections.Generic;

namespace GAS
{
    public class GameplayAbilityRuntime
    {
        private GameplayEffectRuntime effectRuntime;

        private HashSet<GameplayAbilityDefinition> grantedAbilities = new HashSet<GameplayAbilityDefinition>();

        private readonly List<GameplayAbilitySpec> activeAbilities = new List<GameplayAbilitySpec>();

        // Reference-counted because several active abilities can block the same tag at the same time.
        private readonly Dictionary<GameplayTag, int> blockedAbilityTagCounts = new Dictionary<GameplayTag, int>();

        private bool suppressRuntimeEvents;

        public GameplayAbilityRuntime()
        {
        }

        public GameplayAbilityRuntime(GameplayEffectRuntime effectRuntime)
        {
            Initialize(effectRuntime);
        }

        public GameplayEffectRuntime EffectRuntime => effectRuntime;
        public IGameplayEffectRuntimeContext RuntimeContext => effectRuntime.RuntimeContext;
        public IReadOnlyCollection<GameplayAbilityDefinition> GrantedAbilities => grantedAbilities;
        public IReadOnlyList<GameplayAbilitySpec> ActiveAbilities => activeAbilities;

        public int[] CaptureGrantedAbilityIds()
        {
            if (grantedAbilities.Count == 0)
                return Array.Empty<int>();

            var abilityIds = new int[grantedAbilities.Count];
            int index = 0;

            foreach (var ability in grantedAbilities)
            {
                abilityIds[index++] = ability != null ? ability.AbilityId : 0;
            }

            return abilityIds;
        }

        public GameplayAbilitySpecState[] CaptureActiveAbilityStates()
        {
            if (activeAbilities.Count == 0)
                return Array.Empty<GameplayAbilitySpecState>();

            var states = new GameplayAbilitySpecState[activeAbilities.Count];

            for (int i = 0; i < activeAbilities.Count; i++)
            {
                var spec = activeAbilities[i];
                states[i] = new GameplayAbilitySpecState
                {
                    AbilitySpecId = spec.AbilitySpecId,
                    AbilityId = spec.Ability != null ? spec.Ability.AbilityId : 0,
                    SourceEntityId = spec.SourceEntityId,
                    TargetEntityId = spec.TargetEntityId,
                    Level = spec.Level,
                    IsActive = spec.IsActive,
                    IsEnded = spec.IsEnded,
                    EndReason = spec.EndReason,
                    ActiveTasks = CaptureTaskStates(spec),
                };
            }

            return states;
        }

        public virtual void Initialize(GameplayEffectRuntime effectRuntime)
        {
            this.effectRuntime = effectRuntime;
        }

        public virtual void Dispose()
        {
            for (int i = activeAbilities.Count - 1; i >= 0; i--)
                activeAbilities[i].EndAbility(GameplayAbilityEndReason.Cancelled);

            grantedAbilities.Clear();
            blockedAbilityTagCounts.Clear();
            effectRuntime = null;
        }

        public virtual bool GrantAbility(GameplayAbilityDefinition ability)
        {
            if (ability == null)
                return false;

            return grantedAbilities.Add(ability);
        }

        public virtual bool RevokeAbility(GameplayAbilityDefinition ability, bool cancelActive = true)
        {
            if (ability == null)
                return false;

            bool removed = grantedAbilities.Remove(ability);

            if (cancelActive)
            {
                CancelAbility(ability);
            }

            return removed;
        }

        public bool HasAbility(GameplayAbilityDefinition ability)
        {
            return ability != null && grantedAbilities.Contains(ability);
        }

        public virtual GameplayAbilitySpec ActivateAbility(
            GameplayAbilityDefinition ability,
            GameplayEffectRuntime target = null,
            int level = 1,
            GameplayEventData triggerEventData = default)
        {
            if (ability == null || effectRuntime == null)
                return null;

            if (!HasAbility(ability))
            {
                RecordAbilityEvent(null, ability, target, GameplayEffectEventType.AbilityFailed);
                return null;
            }

            if (!ability.IsIgnoreBlock && IsAbilityBlocked(ability))
            {
                RecordAbilityEvent(null, ability, target, GameplayEffectEventType.AbilityFailed);
                return null;
            }

            var spec = new GameplayAbilitySpec(
                RuntimeContext.NewAbilitySpecId(),
                ability,
                this,
                effectRuntime,
                target != null ? target : effectRuntime,
                level);

            spec.TriggerEventData = triggerEventData;

            if (!ability.CanActivateAbility(spec))
            {
                RecordAbilityEvent(spec, ability, spec.Target, GameplayEffectEventType.AbilityFailed);
                return null;
            }

            if (!CanCommitAbility(spec) || !CommitAbility(spec))
            {
                RecordAbilityEvent(spec, ability, spec.Target, GameplayEffectEventType.AbilityFailed);
                return null;
            }

            CancelAbilitiesMatchingQuery(ability.CancelAbilitiesWithTag);

            spec.MarkActive();
            activeAbilities.Add(spec);
            AddActivationOwnedTags(spec);
            AddBlockedTags(ability);
            RecordAbilityEvent(spec, ability, spec.Target, GameplayEffectEventType.AbilityActivated);

            ability.ActivateAbility(spec);

            return spec;
        }

        public bool CancelAbility(GameplayAbilityDefinition ability)
        {
            bool cancelled = false;

            for (int i = activeAbilities.Count - 1; i >= 0; i--)
            {
                var spec = activeAbilities[i];

                if (spec.Ability != ability)
                    continue;

                spec.EndAbility(GameplayAbilityEndReason.Cancelled);
                cancelled = true;
            }

            return cancelled;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            for (int i = activeAbilities.Count - 1; i >= 0; i--)
            {
                var spec = activeAbilities[i];
                spec.Tick(deltaTime);
            }
        }

        internal void OnAbilityEnded(GameplayAbilitySpec spec)
        {
            if (spec == null || !activeAbilities.Remove(spec))
                return;

            RemoveActivationOwnedTags(spec);
            RemoveBlockedTags(spec.Ability);
            RecordAbilityEvent(spec, spec.Ability, spec.Target, GameplayEffectEventType.AbilityEnded);
        }

        internal void RecordAbilityTaskEvent(
            GameplayAbilitySpec spec,
            AbilityTask task,
            GameplayEffectEventType eventType)
        {
            if (suppressRuntimeEvents)
                return;

            RuntimeContext.RecordEvent(new GameplayEffectEvent
            {
                Frame = RuntimeContext.CurrentFrame,
                Type = eventType,
                SourceEntityId = spec.SourceEntityId,
                TargetEntityId = spec.TargetEntityId,
                AbilityId = spec.Ability != null ? spec.Ability.AbilityId : 0,
                AbilitySpecId = spec.AbilitySpecId,
                AbilityTaskId = task != null ? task.TaskId : 0,
            });
        }

        private bool CommitAbility(GameplayAbilitySpec spec)
        {
            if (!ApplyCommitEffects(spec, spec.Ability.CostEffects))
                return false;

            if (!ApplyCommitEffects(spec, spec.Ability.CooldownEffects))
                return false;

            RecordAbilityEvent(spec, spec.Ability, spec.Target, GameplayEffectEventType.AbilityCommitted);
            return true;
        }

        private bool CanCommitAbility(GameplayAbilitySpec spec)
        {
            return CanApplyCommitEffects(spec, spec.Ability.CostEffects) &&
                   CanApplyCommitEffects(spec, spec.Ability.CooldownEffects);
        }

        private bool CanApplyCommitEffects(
            GameplayAbilitySpec spec,
            List<GameplayEffectDefinition> effects)
        {
            if (effects == null)
                return true;

            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];

                if (effect == null)
                    continue;

                if (!spec.CanApplyGameplayEffect(effect, GameplayAbilityTargetPolicy.Self))
                    return false;
            }

            return true;
        }

        private bool ApplyCommitEffects(
            GameplayAbilitySpec spec,
            List<GameplayEffectDefinition> effects)
        {
            if (effects == null)
                return true;

            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];

                if (effect == null)
                    continue;

                var result = spec.ApplyGameplayEffect(effect, GameplayAbilityTargetPolicy.Self);
                if (!result.Success)
                    return false;
            }

            return true;
        }

        private void RecordAbilityEvent(
            GameplayAbilitySpec spec,
            GameplayAbilityDefinition ability,
            GameplayEffectRuntime target,
            GameplayEffectEventType eventType)
        {
            if (suppressRuntimeEvents)
                return;

            RuntimeContext.RecordEvent(new GameplayEffectEvent
            {
                Frame = RuntimeContext.CurrentFrame,
                Type = eventType,
                SourceEntityId = spec != null ? spec.SourceEntityId : effectRuntime != null ? effectRuntime.EntityId : 0,
                TargetEntityId = spec != null ? spec.TargetEntityId : target != null ? target.EntityId : 0,
                AbilityId = ability != null ? ability.AbilityId : 0,
                AbilitySpecId = spec != null ? spec.AbilitySpecId : 0,
            });
        }

        public void HandleGameplayEvent(GameplayTag eventTag, GameplayEventData eventData)
        {
            if (!eventTag.IsValid)
                return;

            foreach (var ability in grantedAbilities)
            {
                if (ability == null || ability.AbilityTriggers == null)
                    continue;

                for (int i = 0; i < ability.AbilityTriggers.Count; i++)
                {
                    if (eventTag.Matches(ability.AbilityTriggers[i]))
                    {
                        ActivateAbility(ability, null, 1, eventData);
                        break;
                    }
                }
            }
        }

        private bool IsAbilityBlocked(GameplayAbilityDefinition ability)
        {
            if (blockedAbilityTagCounts.Count == 0 || ability == null || !ability.AbilityTag.IsValid)
                return false;

            foreach (var pair in blockedAbilityTagCounts)
            {
                if (pair.Value > 0 && ability.AbilityTag.Matches(pair.Key))
                    return true;
            }

            return false;
        }

        private void CancelAbilitiesMatchingQuery(TagQuery query)
        {
            if (query == null)
                return;

            for (int i = activeAbilities.Count - 1; i >= 0; i--)
            {
                var spec = activeAbilities[i];
                var abilityTag = spec.Ability != null ? spec.Ability.AbilityTag : GameplayTag.None;

                if (!abilityTag.IsValid)
                    continue;

                if (query.Match(abilityTag))
                {
                    spec.EndAbility(GameplayAbilityEndReason.Cancelled);
                }
            }
        }

        private void AddActivationOwnedTags(GameplayAbilitySpec spec)
        {
            if (spec?.Ability?.ActivationOwnedTags == null)
                return;

            var sourceTags = spec.Source?.OwnedTags;
            if (sourceTags != null)
                sourceTags.AddTags(spec.Ability.ActivationOwnedTags);
        }

        private void RemoveActivationOwnedTags(GameplayAbilitySpec spec)
        {
            if (spec?.Ability?.ActivationOwnedTags == null)
                return;

            var sourceTags = spec.Source?.OwnedTags;
            if (sourceTags != null)
                sourceTags.RemoveTags(spec.Ability.ActivationOwnedTags);
        }

        private void AddBlockedTags(GameplayAbilityDefinition ability)
        {
            if (ability?.BlockAbilitiesWithTag?.Nodes == null)
                return;

            for (int i = 0; i < ability.BlockAbilitiesWithTag.Nodes.Count; i++)
            {
                var tag = ability.BlockAbilitiesWithTag.Nodes[i];
                if (!tag.IsValid)
                    continue;

                blockedAbilityTagCounts.TryGetValue(tag, out int count);
                blockedAbilityTagCounts[tag] = count + 1;
            }
        }

        private void RemoveBlockedTags(GameplayAbilityDefinition ability)
        {
            if (ability?.BlockAbilitiesWithTag?.Nodes == null)
                return;

            for (int i = 0; i < ability.BlockAbilitiesWithTag.Nodes.Count; i++)
            {
                var tag = ability.BlockAbilitiesWithTag.Nodes[i];
                if (!tag.IsValid)
                    continue;

                if (!blockedAbilityTagCounts.TryGetValue(tag, out int count))
                    continue;

                if (count <= 1)
                    blockedAbilityTagCounts.Remove(tag);
                else
                    blockedAbilityTagCounts[tag] = count - 1;
            }
        }

        public virtual void RestoreGrantedAbilities(int[] abilityIds, GameplayDefinitionCatalog catalog)
        {
            grantedAbilities.Clear();

            if (abilityIds == null || abilityIds.Length == 0)
                return;

            for (int i = 0; i < abilityIds.Length; i++)
            {
                var ability = catalog != null ? catalog.GetAbility(abilityIds[i]) : null;

                if (ability != null)
                    grantedAbilities.Add(ability);
            }
        }

        public virtual void RestoreActiveAbilities(GameplayAbilitySpecState[] states, GameplayDefinitionCatalog catalog)
        {
            bool previousSuppressState = suppressRuntimeEvents;
            suppressRuntimeEvents = true;

            try
            {
                for (int i = activeAbilities.Count - 1; i >= 0; i--)
                    activeAbilities[i].EndAbility(GameplayAbilityEndReason.Cancelled);

                blockedAbilityTagCounts.Clear();

                if (states == null || states.Length == 0)
                    return;

                for (int i = 0; i < states.Length; i++)
                {
                    var state = states[i];
                    var ability = catalog != null ? catalog.GetAbility(state.AbilityId) : null;

                    if (ability == null)
                    {
                        RecordRestoreSkippedEvent(state);
                        continue;
                    }

                    if (state.IsEnded)
                    {
                        continue;
                    }

                    var source = ResolveRuntime(state.SourceEntityId);
                    var target = ResolveRuntime(state.TargetEntityId);
                    var spec = new GameplayAbilitySpec(
                        state.AbilitySpecId,
                        ability,
                        this,
                        source,
                        target,
                        state.Level);
                    spec.SetAuthorityEntityIds(state.SourceEntityId, state.TargetEntityId);

                    if (state.IsActive)
                    {
                        spec.MarkActive();
                    }

                    activeAbilities.Add(spec);
                    if (spec.IsActive)
                    {
                        AddActivationOwnedTags(spec);
                        AddBlockedTags(ability);
                        ability.RestoreAbilityTasks(spec, state.ActiveTasks);
                    }
                }
            }
            finally
            {
                suppressRuntimeEvents = previousSuppressState;
            }
        }

        private static GameplayAbilityTaskState[] CaptureTaskStates(GameplayAbilitySpec spec)
        {
            if (spec == null || !spec.HasActiveTasks)
                return Array.Empty<GameplayAbilityTaskState>();

            var tasks = spec.ActiveTasks;
            var states = new GameplayAbilityTaskState[tasks.Count];

            for (int i = 0; i < tasks.Count; i++)
            {
                states[i] = tasks[i] != null
                    ? tasks[i].CaptureState()
                    : default;
            }

            return states;
        }

        private GameplayEffectRuntime ResolveRuntime(long entityId)
        {
            if (entityId == 0 || effectRuntime == null)
                return null;

            return RuntimeContext.ResolveEntity(entityId) as GameplayEffectRuntime;
        }

        private void RecordRestoreSkippedEvent(GameplayAbilitySpecState state)
        {
            RuntimeContext.RecordEvent(new GameplayEffectEvent
            {
                Frame = RuntimeContext.CurrentFrame,
                Type = GameplayEffectEventType.RestoreAbilitySkipped,
                SourceEntityId = state.SourceEntityId,
                TargetEntityId = state.TargetEntityId,
                AbilityId = state.AbilityId,
                AbilitySpecId = state.AbilitySpecId,
            });
        }
    }
}
