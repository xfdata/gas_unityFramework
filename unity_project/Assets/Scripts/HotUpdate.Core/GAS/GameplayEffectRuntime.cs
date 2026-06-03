using System;
using System.Collections.Generic;

namespace GAS
{
    public class GameplayEffectRuntime : IGameplayEffectRuntime
    {
        private GameplayTagContainer ownedTags = new GameplayTagContainer();

        private IGameplayCueManager cueManager;

        private readonly List<ActiveGameplayEffect> activeEffects = new List<ActiveGameplayEffect>();

        private IGameplayEffectRuntimeContext runtimeContext;

        private bool deterministicMode;

        private bool suppressRuntimeEvents;

        public GameplayEffectRuntime()
        {
        }

        public GameplayEffectRuntime(
            long entityId,
            IGameplayAttributeOwner attributeOwner,
            IGameplayEffectRuntimeContext context = null,
            IGameplayCueManager cueManager = null)
        {
            this.cueManager = cueManager;
            Initialize(entityId, attributeOwner, context);
        }

        public long EntityId { get; set; }

        public GameplayTagContainer OwnedTags => ownedTags;

        public IGameplayAttributeOwner AttributeOwner { get; private set; }

        public IReadOnlyList<ActiveGameplayEffect> ActiveEffects => activeEffects;

        public IGameplayEffectRuntimeContext RuntimeContext => EnsureRuntimeContext();

        public IReadOnlyList<GameplayEffectEvent> RecordedEvents => RuntimeContext.Events;

        public bool DeterministicMode
        {
            get => deterministicMode;
            set => deterministicMode = value;
        }

        public ActiveGameplayEffectState[] CaptureActiveEffectStates()
        {
            if (activeEffects.Count == 0)
                return Array.Empty<ActiveGameplayEffectState>();

            var states = new ActiveGameplayEffectState[activeEffects.Count];

            for (int i = 0; i < activeEffects.Count; i++)
            {
                states[i] = activeEffects[i].CaptureState();
            }

            return states;
        }

        public virtual void Initialize(IGameplayAttributeOwner attributeOwner)
        {
            Initialize(EntityId, attributeOwner, runtimeContext);
        }

        public void Initialize(
            long entityId,
            IGameplayAttributeOwner attributeOwner,
            IGameplayEffectRuntimeContext context = null)
        {
            if (runtimeContext != null && EntityId != 0)
            {
                runtimeContext.UnregisterEntity(EntityId, this);
            }

            EntityId = entityId;
            AttributeOwner = attributeOwner;
            runtimeContext = context ?? runtimeContext ?? new DefaultGameplayEffectRuntimeContext();
            runtimeContext.RegisterEntity(EntityId, this);
        }

        public void SetCueManager(IGameplayCueManager manager)
        {
            cueManager = manager;
        }

        public void Dispose()
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
                RemoveActiveEffectAt(i);

            runtimeContext?.UnregisterEntity(EntityId, this);
        }

        public GameplayEffectSpec MakeOutgoingSpec(
            GameplayEffectRuntime target,
            GameplayEffectDefinition definition,
            int level = 1)
        {
            if (definition == null)
                return null;

            var context = EnsureRuntimeContext();
            var spec = new GameplayEffectSpec(
                definition,
                this,
                target,
                level
            )
            {
                SpecId = context.NewSpecId(),
                SourceEntityId = EntityId,
                TargetEntityId = target != null ? target.EntityId : 0,
                RuntimeContext = context,
            };

            return spec;
        }

        public GameplayEffectApplyResult ApplySpecToTarget(
            GameplayEffectSpec spec,
            GameplayEffectRuntime target
        )
        {
            if (spec == null || target == null)
                return GameplayEffectApplyResult.Failed;

            var targetSpec = spec.CloneForTarget(target);
            targetSpec.Target = target;
            targetSpec.TargetEntityId = target.EntityId;

            return target.ApplySpecToSelf(targetSpec);
        }

        public virtual GameplayEffectApplyResult ApplySpecToSelf(GameplayEffectSpec spec)
        {
            if (spec == null || spec.Asset == null)
                return GameplayEffectApplyResult.Failed;

            var context = EnsureRuntimeContext();

            spec.Target = this;
            spec.TargetEntityId = EntityId;
            spec.RuntimeContext = context;

            if (spec.Source != null)
            {
                spec.SourceEntityId = spec.Source.EntityId;
            }

            if (spec.SpecId == 0)
            {
                spec.SpecId = context.NewSpecId();
            }

            if (!CanApply(spec))
                return GameplayEffectApplyResult.Failed;

            var asset = spec.Asset;

            if (asset.DurationPolicy == GameplayEffectDurationPolicy.Instant)
            {
                ExecuteEffect(spec, 0);
                SendCues(spec, GameplayCueEventType.Execute, 0, 0f);
                return GameplayEffectApplyResult.InstantEffect();
            }

            var existing = FindStackableEffect(spec);

            if (existing != null)
            {
                ApplyStack(existing, spec);
                SendCues(existing.Spec, GameplayCueEventType.OnActive, existing.RuntimeEffectId, 0f);
                return GameplayEffectApplyResult.ActiveEffect(existing.RuntimeEffectId);
            }

            var runtimeEffectId = context.NewRuntimeEffectId();
            var active = new ActiveGameplayEffect(
                runtimeEffectId,
                spec,
                asset.DurationPolicy == GameplayEffectDurationPolicy.Duration
                    ? spec.Duration
                    : float.PositiveInfinity,
                spec.Period
            );

            activeEffects.Add(active);

            RecordEffectEvent(active.Spec, GameplayEffectEventType.EffectApplied, active.RuntimeEffectId);
            AddGrantedTags(active);
            AddPersistentModifiers(active);

            if (asset.ExecuteOnApply)
            {
                ExecuteEffect(spec, active.RuntimeEffectId);
                SendCues(spec, GameplayCueEventType.Execute, active.RuntimeEffectId, 0f);
            }

            SendCues(spec, GameplayCueEventType.OnActive, active.RuntimeEffectId, 0f);

            return GameplayEffectApplyResult.ActiveEffect(runtimeEffectId);
        }

        public virtual bool CanApplySpecToSelf(GameplayEffectSpec spec)
        {
            if (spec == null || spec.Asset == null)
                return false;

            spec.Target = this;
            spec.TargetEntityId = EntityId;

            if (spec.Source != null)
            {
                spec.SourceEntityId = spec.Source.EntityId;
            }

            return CanApply(spec);
        }

        public bool RemoveActiveGameplayEffect(int runtimeId)
        {
            if (runtimeId == 0)
                return false;

            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                if (activeEffects[i].RuntimeEffectId != runtimeId)
                    continue;

                RemoveActiveEffectAt(i);
                return true;
            }

            return false;
        }

        public int RemoveActiveGameplayEffectsByTag(GameplayTag effectTag, bool includeChildren = true)
        {
            if (!effectTag.IsValid)
                return 0;

            int count = 0;

            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                var assetTag = activeEffects[i].Definition.EffectTag;

                bool matched = includeChildren
                    ? assetTag.Matches(effectTag)
                    : assetTag == effectTag;

                if (!matched)
                    continue;

                RemoveActiveEffectAt(i);
                count++;
            }

            return count;
        }

        public bool HasActiveGameplayEffect(GameplayTag effectTag, bool includeChildren = true)
        {
            if (!effectTag.IsValid)
                return false;

            for (int i = 0; i < activeEffects.Count; i++)
            {
                var assetTag = activeEffects[i].Definition.EffectTag;

                bool matched = includeChildren
                    ? assetTag.Matches(effectTag)
                    : assetTag == effectTag;

                if (matched)
                    return true;
            }

            return false;
        }

        public ActiveGameplayEffect GetActiveEffect(int runtimeEffectId)
        {
            if (runtimeEffectId == 0)
                return null;

            for (int i = 0; i < activeEffects.Count; i++)
            {
                if (activeEffects[i].RuntimeEffectId == runtimeEffectId)
                    return activeEffects[i];
            }

            return null;
        }

        public ActiveGameplayEffect GetActiveEffectByTag(GameplayTag effectTag, bool includeChildren = true)
        {
            if (!effectTag.IsValid)
                return null;

            for (int i = 0; i < activeEffects.Count; i++)
            {
                var assetTag = activeEffects[i].Definition.EffectTag;

                bool matched = includeChildren
                    ? assetTag.Matches(effectTag)
                    : assetTag == effectTag;

                if (matched)
                    return activeEffects[i];
            }

            return null;
        }

        public int GetActiveEffectStackCount(int runtimeEffectId)
        {
            var effect = GetActiveEffect(runtimeEffectId);
            return effect != null ? effect.Stack : 0;
        }

        public float GetActiveEffectTimeRemaining(int runtimeEffectId)
        {
            var effect = GetActiveEffect(runtimeEffectId);
            return effect != null ? effect.TimeLeft : 0f;
        }

        public void Tick(float deltaTime)
        {
            Tick(deltaTime, true);
        }

        public virtual void Tick(float deltaTime, bool advanceRuntimeFrame)
        {
            if (deltaTime <= 0f)
                return;

            var context = EnsureRuntimeContext();

            if (advanceRuntimeFrame)
            {
                context.BeginTick(deltaTime);
            }

            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                var active = activeEffects[i];
                var asset = active.Definition;
                var spec = active.Spec;
                float effectDeltaTime = deltaTime;

                if (asset.DurationPolicy == GameplayEffectDurationPolicy.Duration)
                {
                    effectDeltaTime = active.TimeLeft > 0f
                        ? Math.Min(deltaTime, active.TimeLeft)
                        : 0f;
                    active.TimeLeft -= deltaTime;
                }

                if (spec.Period > 0f && effectDeltaTime > 0f)
                {
                    active.PeriodLeft -= effectDeltaTime;

                    int periodLoops = 0;
                    const int maxPeriodLoopsPerTick = 32;

                    while (active.PeriodLeft <= 0f && periodLoops < maxPeriodLoopsPerTick)
                    {
                        active.PeriodLeft += spec.Period;
                        periodLoops++;

                        ExecuteEffect(spec, active.RuntimeEffectId);
                        SendCues(spec, GameplayCueEventType.Execute, active.RuntimeEffectId, 0f);
                    }
                }

                SendCues(spec, GameplayCueEventType.WhileActive, active.RuntimeEffectId, 0f);

                if (active.IsExpired)
                {
                    RemoveActiveEffectAt(i);
                }
            }

            if (advanceRuntimeFrame)
            {
                context.EndTick();
            }
        }

        public int NextRandom(int minValue, int maxValue)
        {
            return RuntimeContext.Random.Next(minValue, maxValue);
        }

        public int NextRandom(int maxValue)
        {
            return RuntimeContext.Random.Next(maxValue);
        }

        public float NextRandomFloat()
        {
            return (float)RuntimeContext.Random.NextDouble();
        }

        public float NextRandomFloat(float minValue, float maxValue)
        {
            return minValue + (float)RuntimeContext.Random.NextDouble() * (maxValue - minValue);
        }

        public void ApplyAttributeBaseValue(
            GameplayEffectSpec spec,
            int attributeId,
            float delta)
        {
            if (AttributeOwner == null)
                return;

            float oldValue = AttributeOwner.GetAttribute(attributeId);
            AttributeOwner.AddAttributeBaseValue(attributeId, delta);
            float newValue = AttributeOwner.GetAttribute(attributeId);

            RecordEffectEvent(
                spec,
                GameplayEffectEventType.AttributeChanged,
                spec != null ? spec.RuntimeEffectId : 0,
                attributeId,
                oldValue,
                newValue,
                newValue - oldValue);
        }

        protected virtual bool CanApply(GameplayEffectSpec spec)
        {
            var asset = spec.Asset;

            GameplayTagContainer sourceTags = spec.Source != null
                ? spec.Source.OwnedTags
                : null;

            GameplayTagContainer targetTags = spec.Target != null
                ? spec.Target.OwnedTags
                : ownedTags;

            if (asset.SourceRequiredTags != null &&
                !asset.SourceRequiredTags.Match(sourceTags))
                return false;

            if (asset.SourceBlockedTags != null &&
                !asset.SourceBlockedTags.Match(sourceTags))
                return false;

            if (asset.TargetRequiredTags != null &&
                !asset.TargetRequiredTags.Match(targetTags))
                return false;

            if (asset.TargetBlockedTags != null &&
                !asset.TargetBlockedTags.Match(targetTags))
                return false;

            return true;
        }

        private ActiveGameplayEffect FindStackableEffect(GameplayEffectSpec newSpec)
        {
            var asset = newSpec.Asset;

            if (asset.MaxStack <= 1 || asset.StackPolicy == GameplayEffectStackPolicy.None)
                return null;

            for (int i = 0; i < activeEffects.Count; i++)
            {
                var active = activeEffects[i];

                if (active.Definition != asset)
                    continue;

                switch (asset.StackPolicy)
                {
                    case GameplayEffectStackPolicy.StackByTarget:
                        return active;

                    case GameplayEffectStackPolicy.StackBySource:
                        if (newSpec.SourceEntityId != 0 &&
                            active.Spec.SourceEntityId == newSpec.SourceEntityId)
                            return active;
                        if (active.Spec.Source == newSpec.Source && newSpec.Source != null)
                            return active;
                        break;
                }
            }

            return null;
        }

        private void ApplyStack(
            ActiveGameplayEffect active,
            GameplayEffectSpec incomingSpec
        )
        {
            var asset = active.Definition;
            int oldStack = active.Stack;

            if (active.Stack < asset.MaxStack)
            {
                active.Stack++;
                active.Spec.Stack = active.Stack;

                RecordEffectEvent(
                    active.Spec,
                    GameplayEffectEventType.EffectStackChanged,
                    active.RuntimeEffectId,
                    0,
                    oldStack,
                    active.Stack,
                    active.Stack - oldStack);

                if (asset.ReapplyModifiersOnStack)
                {
                    RemovePersistentModifiers(active);
                    AddPersistentModifiers(active);
                }
            }

            if (asset.RefreshDurationOnStack &&
                asset.DurationPolicy == GameplayEffectDurationPolicy.Duration)
            {
                active.TimeLeft = incomingSpec.Duration;
            }

            active.Spec.CopyDynamicValuesFrom(incomingSpec, false);
        }

        private void ExecuteEffect(GameplayEffectSpec spec, int runtimeEffectId)
        {
            spec.RuntimeEffectId = runtimeEffectId;

            RecordEffectEvent(spec, GameplayEffectEventType.EffectExecuted, runtimeEffectId);
            ApplyInstantModifiers(spec);

            var executions = spec.Asset.Executions;

            if (executions == null)
                return;

            for (int i = 0; i < executions.Count; i++)
            {
                var execution = executions[i];

                if (execution == null)
                    continue;

                execution.Execute(spec);
            }
        }

        private void ApplyInstantModifiers(GameplayEffectSpec spec)
        {
            var asset = spec.Asset;

            if (asset.DurationPolicy != GameplayEffectDurationPolicy.Instant)
                return;

            if (AttributeOwner == null)
                return;

            var modifiers = asset.Modifiers;

            if (modifiers == null)
                return;

            for (int i = 0; i < modifiers.Count; i++)
            {
                ApplyInstantModifier(spec, modifiers[i]);
            }
        }

        private void ApplyInstantModifier(
            GameplayEffectSpec spec,
            GameplayEffectDefinition.Modifier modifier
        )
        {
            if (modifier == null || AttributeOwner == null)
                return;

            float value = modifier.Value;

            if (modifier.ScaleByStack)
            {
                value *= spec.Stack;
            }

            switch (modifier.Op)
            {
                case AttributeModifierOp.Add:
                    ApplyAttributeBaseValue(spec, modifier.AttributeId, value);
                    break;

                case AttributeModifierOp.Multiply:
                {
                    float current = AttributeOwner.GetAttribute(modifier.AttributeId);
                    float delta = current * (value - 1f);
                    ApplyAttributeBaseValue(spec, modifier.AttributeId, delta);
                    break;
                }

                case AttributeModifierOp.Override:
                {
                    float current = AttributeOwner.GetAttribute(modifier.AttributeId);
                    float delta = value - current;
                    ApplyAttributeBaseValue(spec, modifier.AttributeId, delta);
                    break;
                }
            }
        }

        private void AddPersistentModifiers(ActiveGameplayEffect active)
        {
            if (AttributeOwner == null)
                return;

            var modifiers = active.Definition.Modifiers;

            if (modifiers == null)
                return;

            for (int i = 0; i < modifiers.Count; i++)
            {
                var modifier = modifiers[i];

                if (modifier == null)
                    continue;

                float value = modifier.Value;

                if (modifier.ScaleByStack)
                {
                    value *= active.Stack;
                }

                var handle = AttributeOwner.AddModifier(
                    modifier.AttributeId,
                    modifier.Op,
                    value,
                    active
                );

                if (handle.IsValid)
                {
                    active.ModifierHandles.Add(handle);
                    RecordEffectEvent(
                        active.Spec,
                        GameplayEffectEventType.ModifierAdded,
                        active.RuntimeEffectId,
                        modifier.AttributeId,
                        0f,
                        value,
                        value);
                }
            }
        }

        private void RemovePersistentModifiers(ActiveGameplayEffect active)
        {
            if (AttributeOwner == null)
                return;

            for (int i = 0; i < active.ModifierHandles.Count; i++)
            {
                RecordEffectEvent(
                    active.Spec,
                    GameplayEffectEventType.ModifierRemoved,
                    active.RuntimeEffectId);
                AttributeOwner.RemoveModifier(active.ModifierHandles[i]);
            }

            active.ModifierHandles.Clear();
        }

        private void AddGrantedTags(ActiveGameplayEffect active)
        {
            var tags = active.Definition.GrantedTags;

            if (tags == null || tags.Tags == null)
                return;

            for (int i = 0; i < tags.Tags.Count; i++)
            {
                var tag = tags.Tags[i];

                RecordEffectEvent(
                    active.Spec,
                    GameplayEffectEventType.TagAdded,
                    active.RuntimeEffectId,
                    tag);
                ownedTags.AddTag(tag);
            }
        }

        private void RemoveGrantedTags(ActiveGameplayEffect active)
        {
            var tags = active.Definition.GrantedTags;

            if (tags == null || tags.Tags == null)
                return;

            for (int i = 0; i < tags.Tags.Count; i++)
            {
                var tag = tags.Tags[i];

                RecordEffectEvent(
                    active.Spec,
                    GameplayEffectEventType.TagRemoved,
                    active.RuntimeEffectId,
                    tag);
                ownedTags.RemoveTag(tag);
            }
        }

        private void RemoveActiveEffectAt(int index)
        {
            var active = activeEffects[index];

            RemovePersistentModifiers(active);
            RemoveGrantedTags(active);

            SendCues(active.Spec, GameplayCueEventType.Removed, active.RuntimeEffectId, 0f);
            RecordEffectEvent(active.Spec, GameplayEffectEventType.EffectRemoved, active.RuntimeEffectId);

            if (deterministicMode)
            {
                activeEffects.RemoveAt(index);
                return;
            }

            int last = activeEffects.Count - 1;
            if (index == last)
            {
                activeEffects.RemoveAt(last);
                return;
            }

            activeEffects[index] = activeEffects[last];
            activeEffects.RemoveAt(last);
        }

        private bool BeginSuppressRuntimeEvents()
        {
            bool previous = suppressRuntimeEvents;
            suppressRuntimeEvents = true;
            return previous;
        }

        private void EndSuppressRuntimeEvents(bool previous)
        {
            suppressRuntimeEvents = previous;
        }

        private void SendCues(
            GameplayEffectSpec spec,
            GameplayCueEventType eventType,
            int runtimeId,
            float magnitude
        )
        {
            if (suppressRuntimeEvents)
                return;

            var asset = spec.Asset;

            if (asset.Cues == null)
                return;

            for (int i = 0; i < asset.Cues.Count; i++)
            {
                var cue = asset.Cues[i];

                if (cue == null || !cue.CueTag.IsValid)
                    continue;

                if (!ShouldTriggerCue(cue, eventType))
                    continue;

                RecordCueEvent(spec, runtimeId, cue.CueTag, magnitude);

                if (cueManager == null)
                    continue;

                var payload = new GameplayCuePayload
                {
                    CueTag = cue.CueTag,
                    Source = spec.Source,
                    Target = spec.Target,
                    SourceEntityId = spec.SourceEntityId,
                    TargetEntityId = spec.TargetEntityId,
                    Spec = spec,
                    EffectDefinition = asset,
                    SpecId = spec.SpecId,
                    RuntimeEffectId = runtimeId,
                    Magnitude = magnitude,
                    Position = spec.Position,
                    UserData = spec.UserData,
                };

                cueManager.HandleCue(cue.CueTag, eventType, payload);
            }
        }

        private static bool ShouldTriggerCue(
            GameplayEffectDefinition.Cue cue,
            GameplayCueEventType eventType
        )
        {
            if (cue.Policy == GameplayCuePolicy.Static &&
                eventType != GameplayCueEventType.Execute)
            {
                return false;
            }

            if (cue.Policy == GameplayCuePolicy.Active &&
                eventType == GameplayCueEventType.Execute)
            {
                return false;
            }

            switch (eventType)
            {
                case GameplayCueEventType.OnActive:
                    return cue.OnApply;

                case GameplayCueEventType.Execute:
                    return cue.OnExecute;

                case GameplayCueEventType.Removed:
                    return cue.OnRemove;

                case GameplayCueEventType.WhileActive:
                    return true;

                default:
                    return false;
            }
        }

        private IGameplayEffectRuntimeContext EnsureRuntimeContext()
        {
            if (runtimeContext == null)
            {
                runtimeContext = new DefaultGameplayEffectRuntimeContext();
            }

            return runtimeContext;
        }

        private void RecordEffectEvent(
            GameplayEffectSpec spec,
            GameplayEffectEventType type,
            int runtimeEffectId,
            int attributeId = 0,
            float oldValue = 0f,
            float newValue = 0f,
            float delta = 0f)
        {
            if (suppressRuntimeEvents)
                return;

            RuntimeContext.RecordEvent(new GameplayEffectEvent
            {
                Frame = RuntimeContext.CurrentFrame,
                Type = type,
                SourceEntityId = spec != null ? spec.SourceEntityId : 0,
                TargetEntityId = spec != null ? spec.TargetEntityId : EntityId,
                EffectId = spec != null && spec.Asset != null ? spec.Asset.EffectId : 0,
                SpecId = spec != null ? spec.SpecId : 0,
                RuntimeEffectId = runtimeEffectId,
                AttributeId = attributeId,
                OldValue = oldValue,
                NewValue = newValue,
                Delta = delta,
                Position = spec != null ? spec.Position : default,
            });
        }

        private void RecordEffectEvent(
            GameplayEffectSpec spec,
            GameplayEffectEventType type,
            int runtimeEffectId,
            GameplayTag gameplayTag)
        {
            if (suppressRuntimeEvents)
                return;

            var gameplayEvent = new GameplayEffectEvent
            {
                Frame = RuntimeContext.CurrentFrame,
                Type = type,
                SourceEntityId = spec != null ? spec.SourceEntityId : 0,
                TargetEntityId = spec != null ? spec.TargetEntityId : EntityId,
                EffectId = spec != null && spec.Asset != null ? spec.Asset.EffectId : 0,
                SpecId = spec != null ? spec.SpecId : 0,
                RuntimeEffectId = runtimeEffectId,
                GameplayTag = gameplayTag,
                Position = spec != null ? spec.Position : default,
            };

            RuntimeContext.RecordEvent(gameplayEvent);
        }

        private void RecordCueEvent(
            GameplayEffectSpec spec,
            int runtimeEffectId,
            GameplayTag cueTag,
            float magnitude)
        {
            if (suppressRuntimeEvents)
                return;

            RuntimeContext.RecordEvent(new GameplayEffectEvent
            {
                Frame = RuntimeContext.CurrentFrame,
                Type = GameplayEffectEventType.CueTriggered,
                SourceEntityId = spec != null ? spec.SourceEntityId : 0,
                TargetEntityId = spec != null ? spec.TargetEntityId : EntityId,
                EffectId = spec != null && spec.Asset != null ? spec.Asset.EffectId : 0,
                SpecId = spec != null ? spec.SpecId : 0,
                RuntimeEffectId = runtimeEffectId,
                CueTag = cueTag,
                Position = spec != null ? spec.Position : default,
                Magnitude = magnitude,
            });
        }

        public virtual void RestoreActiveEffects(ActiveGameplayEffectState[] states, GameplayDefinitionCatalog catalog)
        {
            bool previousSuppressState = BeginSuppressRuntimeEvents();

            try
            {
                for (int i = activeEffects.Count - 1; i >= 0; i--)
                {
                    RemoveActiveEffectAt(i);
                }

                if (states == null || states.Length == 0)
                    return;

                for (int i = 0; i < states.Length; i++)
                {
                    var state = states[i];
                    var definition = catalog != null ? catalog.GetEffect(state.EffectId) : null;

                    if (definition == null)
                    {
                        RecordRestoreSkippedEvent(state);
                        continue;
                    }

                    var source = ResolveRuntime(state.SourceEntityId);
                    var target = ResolveRuntime(state.TargetEntityId);

                    if (target != this)
                        continue;

                    var spec = new GameplayEffectSpec(definition, source, target, state.Level)
                    {
                        SpecId = state.SpecId,
                        SourceEntityId = state.SourceEntityId,
                        TargetEntityId = state.TargetEntityId,
                        Duration = state.Duration,
                        Period = state.Period,
                        Stack = state.Stack,
                    };

                    var active = new ActiveGameplayEffect(
                        state.RuntimeEffectId,
                        spec,
                        state.DurationPolicy == GameplayEffectDurationPolicy.Duration
                            ? state.TimeLeft
                            : float.PositiveInfinity,
                        state.PeriodLeft);

                    active.Stack = state.Stack;

                    activeEffects.Add(active);
                    AddGrantedTags(active);
                    AddPersistentModifiers(active);
                }
            }
            finally
            {
                EndSuppressRuntimeEvents(previousSuppressState);
            }
        }

        private GameplayEffectRuntime ResolveRuntime(long entityId)
        {
            if (entityId == 0)
                return null;

            return RuntimeContext.ResolveEntity(entityId) as GameplayEffectRuntime;
        }

        private void RecordRestoreSkippedEvent(ActiveGameplayEffectState state)
        {
            RuntimeContext.RecordEvent(new GameplayEffectEvent
            {
                Frame = RuntimeContext.CurrentFrame,
                Type = GameplayEffectEventType.RestoreEffectSkipped,
                SourceEntityId = state.SourceEntityId,
                TargetEntityId = state.TargetEntityId != 0 ? state.TargetEntityId : EntityId,
                EffectId = state.EffectId,
                SpecId = state.SpecId,
                RuntimeEffectId = state.RuntimeEffectId,
            });
        }
    }
}
