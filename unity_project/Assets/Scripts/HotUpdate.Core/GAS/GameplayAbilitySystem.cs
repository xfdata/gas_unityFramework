using System;
using System.Collections.Generic;

namespace GAS
{
    public class GameplayAbilitySystem
    {
        private GameplayEffectRuntime effectRuntime;

        private GameplayAbilityRuntime abilityRuntime;

        private GameplayDefinitionCatalog definitionCatalog;

        private bool deterministicMode;

        private IGameplayEffectRuntimeContext runtimeContext;

        private bool isInitialized;

#if UNITY_EDITOR
        private static readonly List<GameplayAbilitySystem> _editorInstances = new List<GameplayAbilitySystem>();
        internal static IReadOnlyList<GameplayAbilitySystem> EditorInstances => _editorInstances;
#endif

        public GameplayAbilitySystem(
            long entityId,
            IGameplayAttributeOwner attributeOwner,
            IGameplayEffectRuntimeContext context = null,
            GameplayDefinitionCatalog catalog = null,
            IGameplayCueManager cueManager = null)
        {
            Initialize(entityId, attributeOwner, context, catalog, cueManager);
        }

        public long EntityId => effectRuntime != null ? effectRuntime.EntityId : 0;
        public GameplayEffectRuntime Effects => effectRuntime;
        public GameplayAbilityRuntime Abilities => abilityRuntime;
        public GameplayDefinitionCatalog DefinitionCatalog => definitionCatalog;
        public IGameplayEffectRuntimeContext RuntimeContext => runtimeContext ?? effectRuntime?.RuntimeContext;
        public GameplayTagContainer OwnedTags => effectRuntime?.OwnedTags;
        public IGameplayAttributeOwner AttributeOwner => effectRuntime?.AttributeOwner;
        public IReadOnlyList<ActiveGameplayEffect> ActiveEffects => effectRuntime?.ActiveEffects;
        public IReadOnlyList<GameplayAbilitySpec> ActiveAbilities => abilityRuntime?.ActiveAbilities;
        public IReadOnlyList<GameplayEffectEvent> RecordedEvents => RuntimeContext?.Events;
        public bool IsInitialized => isInitialized;

        public bool DeterministicMode
        {
            get => deterministicMode;
            set
            {
                deterministicMode = value;
                if (effectRuntime != null) effectRuntime.DeterministicMode = value;
            }
        }

        private void Initialize(
            long entityId,
            IGameplayAttributeOwner attributeOwner,
            IGameplayEffectRuntimeContext context = null,
            GameplayDefinitionCatalog catalog = null,
            IGameplayCueManager cueManager = null)
        {
            if (isInitialized)
                Dispose();

            if (effectRuntime == null)
            {
                effectRuntime = new GameplayEffectRuntime();
                abilityRuntime = new GameplayAbilityRuntime();
            }

            runtimeContext = context ?? runtimeContext ?? new DefaultGameplayEffectRuntimeContext();

            if (catalog != null)
            {
                definitionCatalog = catalog;
            }

            if (cueManager != null)
            {
                effectRuntime.SetCueManager(cueManager);
            }

            effectRuntime.DeterministicMode = deterministicMode;
            effectRuntime.Initialize(entityId, attributeOwner, runtimeContext);
            abilityRuntime.Initialize(effectRuntime);
            isInitialized = true;

#if UNITY_EDITOR
            if (!_editorInstances.Contains(this))
                _editorInstances.Add(this);
#endif
        }

        public void SetCueManager(IGameplayCueManager cueManager)
        {
            if (effectRuntime == null)
                throw new InvalidOperationException(
                    "GameplayAbilitySystem is not initialized. Call Initialize() first.");

            effectRuntime.SetCueManager(cueManager);
        }

        public virtual void Dispose()
        {
#if UNITY_EDITOR
            _editorInstances.Remove(this);
#endif
            abilityRuntime?.Dispose();
            effectRuntime?.Dispose();
            isInitialized = false;
        }

        public virtual GameplayAbilitySystemState CaptureState()
        {
            EnsureInitialized();

            var tags = effectRuntime.OwnedTags != null
                ? effectRuntime.OwnedTags.Tags
                : null;
            var activeGrantedTagCounts = CaptureActiveEffectGrantedTagCounts();
            var activeAbilityOwnedTagCounts = CaptureActiveAbilityOwnedTagCounts();
            var capturedTags = tags != null && tags.Count > 0
                ? new List<GameplayTag>(tags.Count)
                : null;

            if (tags != null)
            {
                for (int i = 0; i < tags.Count; i++)
                {
                    var tag = tags[i];
                    int activeGrantedCount = 0;
                    if (activeGrantedTagCounts != null)
                    {
                        activeGrantedTagCounts.TryGetValue(tag, out activeGrantedCount);
                    }

                    int activeAbilityOwnedCount = 0;
                    if (activeAbilityOwnedTagCounts != null)
                    {
                        activeAbilityOwnedTagCounts.TryGetValue(tag, out activeAbilityOwnedCount);
                    }

                    if (effectRuntime.OwnedTags.GetTagCount(tag) > activeGrantedCount + activeAbilityOwnedCount)
                    {
                        capturedTags.Add(tag);
                    }
                }
            }

            return new GameplayAbilitySystemState
            {
                Frame = RuntimeContext.CurrentFrame,
                EntityId = EntityId,
                OwnedTags = capturedTags != null && capturedTags.Count > 0
                    ? capturedTags.ToArray()
                    : Array.Empty<GameplayTag>(),
                AttributeSet = CaptureAttributeSetState(),
                GrantedAbilityIds = abilityRuntime.CaptureGrantedAbilityIds(),
                ActiveAbilities = abilityRuntime.CaptureActiveAbilityStates(),
                ActiveEffects = effectRuntime.CaptureActiveEffectStates(),
            };
        }

        public virtual void RestoreState(GameplayAbilitySystemState state)
        {
            EnsureInitialized();

            RestoreRuntimeFrame(state.Frame);
            effectRuntime.Initialize(state.EntityId, effectRuntime.AttributeOwner, RuntimeContext);
            abilityRuntime.RestoreActiveAbilities(null, definitionCatalog);
            effectRuntime.RestoreActiveEffects(null, definitionCatalog);
            RestoreAttributeSetState(state.AttributeSet);

            var ownedTags = effectRuntime.OwnedTags;
            if (ownedTags != null)
            {
                ownedTags.Clear();

                if (state.OwnedTags != null)
                {
                    for (int i = 0; i < state.OwnedTags.Length; i++)
                    {
                        ownedTags.AddTag(state.OwnedTags[i]);
                    }
                }
            }

            effectRuntime.RestoreActiveEffects(state.ActiveEffects, definitionCatalog);
            abilityRuntime.RestoreGrantedAbilities(state.GrantedAbilityIds, definitionCatalog);
            abilityRuntime.RestoreActiveAbilities(state.ActiveAbilities, definitionCatalog);
            EnsureRuntimeIds(state);
        }

        public void SetDefinitionCatalog(GameplayDefinitionCatalog catalog)
        {
            definitionCatalog = catalog;
        }

        public bool GrantAbility(GameplayAbilityDefinition ability)
        {
            EnsureInitialized();
            return abilityRuntime.GrantAbility(ability);
        }

        public bool GrantAbility(int abilityId)
        {
            var ability = GetAbilityDefinition(abilityId);
            return GrantAbility(ability);
        }

        public bool RevokeAbility(GameplayAbilityDefinition ability, bool cancelActive = true)
        {
            EnsureInitialized();
            return abilityRuntime.RevokeAbility(ability, cancelActive);
        }

        public bool RevokeAbility(int abilityId, bool cancelActive = true)
        {
            var ability = GetAbilityDefinition(abilityId);
            return RevokeAbility(ability, cancelActive);
        }

        public GameplayAbilitySpec ActivateAbility(
            GameplayAbilityDefinition ability,
            GameplayAbilitySystem target = null,
            int level = 1)
        {
            EnsureInitialized();
            return abilityRuntime.ActivateAbility(
                ability,
                target != null ? target.Effects : null,
                level);
        }

        public GameplayAbilitySpec ActivateAbility(
            int abilityId,
            GameplayAbilitySystem target = null,
            int level = 1)
        {
            var ability = GetAbilityDefinition(abilityId);
            return ActivateAbility(ability, target, level);
        }

        public GameplayEffectApplyResult ApplyEffectToSelf(
            GameplayEffectDefinition effect,
            int level = 1)
        {
            EnsureInitialized();

            if (effect == null)
                return GameplayEffectApplyResult.Failed;

            var spec = effectRuntime.MakeOutgoingSpec(effectRuntime, effect, level);
            return effectRuntime.ApplySpecToSelf(spec);
        }

        public GameplayEffectApplyResult ApplyEffectToSelf(
            int effectId,
            int level = 1)
        {
            var effect = GetEffectDefinition(effectId);
            return ApplyEffectToSelf(effect, level);
        }

        public GameplayEffectApplyResult ApplyEffectToTarget(
            GameplayEffectDefinition effect,
            GameplayAbilitySystem target,
            int level = 1)
        {
            EnsureInitialized();

            if (effect == null || target == null || target.Effects == null)
                return GameplayEffectApplyResult.Failed;

            var spec = effectRuntime.MakeOutgoingSpec(target.Effects, effect, level);
            return effectRuntime.ApplySpecToTarget(spec, target.Effects);
        }

        public GameplayEffectApplyResult ApplyEffectToTarget(
            int effectId,
            GameplayAbilitySystem target,
            int level = 1)
        {
            var effect = GetEffectDefinition(effectId);
            return ApplyEffectToTarget(effect, target, level);
        }

        public bool RemoveActiveEffect(int runtimeEffectId)
        {
            EnsureInitialized();
            return effectRuntime.RemoveActiveGameplayEffect(runtimeEffectId);
        }

        public int RemoveActiveEffectsByTag(GameplayTag effectTag, bool includeChildren = true)
        {
            EnsureInitialized();
            return effectRuntime.RemoveActiveGameplayEffectsByTag(effectTag, includeChildren);
        }

        public void SendGameplayEvent(GameplayTag eventTag, GameplayEventData eventData)
        {
            EnsureInitialized();
            eventData.EventTag = eventTag;
            abilityRuntime.HandleGameplayEvent(eventTag, eventData);
        }

        public bool HasActiveEffect(GameplayTag effectTag, bool includeChildren = true)
        {
            if (effectRuntime == null)
                return false;

            return effectRuntime.HasActiveGameplayEffect(effectTag, includeChildren);
        }

        public ActiveGameplayEffect GetActiveEffect(int runtimeEffectId)
        {
            if (effectRuntime == null)
                return null;

            return effectRuntime.GetActiveEffect(runtimeEffectId);
        }

        public ActiveGameplayEffect GetActiveEffectByTag(GameplayTag effectTag, bool includeChildren = true)
        {
            if (effectRuntime == null)
                return null;

            return effectRuntime.GetActiveEffectByTag(effectTag, includeChildren);
        }

        public int GetActiveEffectStackCount(int runtimeEffectId)
        {
            if (effectRuntime == null)
                return 0;

            return effectRuntime.GetActiveEffectStackCount(runtimeEffectId);
        }

        public float GetActiveEffectTimeRemaining(int runtimeEffectId)
        {
            if (effectRuntime == null)
                return 0f;

            return effectRuntime.GetActiveEffectTimeRemaining(runtimeEffectId);
        }

        public bool HasAbility(GameplayAbilityDefinition ability)
        {
            if (abilityRuntime == null)
                return false;

            return abilityRuntime.HasAbility(ability);
        }

        public bool HasAbility(int abilityId)
        {
            var ability = GetAbilityDefinition(abilityId);
            return ability != null && HasAbility(ability);
        }

        public void Tick(float deltaTime)
        {
            Tick(deltaTime, true);
        }

        public virtual void Tick(float deltaTime, bool advanceRuntimeFrame)
        {
            if (deltaTime <= 0f)
                return;

            EnsureInitialized();

            if (advanceRuntimeFrame)
            {
                RuntimeContext.BeginTick(deltaTime);
            }

            effectRuntime.Tick(deltaTime, false);
            abilityRuntime.Tick(deltaTime);

            if (advanceRuntimeFrame)
            {
                RuntimeContext.EndTick();
            }
        }

        public void TickFixed(float fixedDeltaTime)
        {
            Tick(fixedDeltaTime, true);
        }

        public void InitDeterministicRandom(int seed)
        {
            var ctx = RuntimeContext;

            if (ctx is DefaultGameplayEffectRuntimeContext defaultCtx)
            {
                defaultCtx.InitRandom(seed);
            }
        }

        public GameplayEffectDefinition GetEffectDefinition(int effectId)
        {
            return definitionCatalog != null ? definitionCatalog.GetEffect(effectId) : null;
        }

        public GameplayAbilityDefinition GetAbilityDefinition(int abilityId)
        {
            return definitionCatalog != null ? definitionCatalog.GetAbility(abilityId) : null;
        }

        private void EnsureInitialized()
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException(
                    "GameplayAbilitySystem is not initialized. Call Initialize() first.");
            }
        }

        private static Dictionary<GameplayTag, int> CountTagOccurrences<T>(
            IReadOnlyList<T> items,
            Func<T, IReadOnlyList<GameplayTag>> tagSelector)
        {
            if (items == null || items.Count == 0)
                return null;

            Dictionary<GameplayTag, int> counts = null;

            for (int i = 0; i < items.Count; i++)
            {
                var tags = tagSelector(items[i]);
                if (tags == null)
                    continue;

                for (int j = 0; j < tags.Count; j++)
                {
                    var tag = tags[j];
                    if (!tag.IsValid)
                        continue;

                    if (counts == null)
                        counts = new Dictionary<GameplayTag, int>();

                    counts.TryGetValue(tag, out int count);
                    counts[tag] = count + 1;
                }
            }

            return counts;
        }

        private Dictionary<GameplayTag, int> CaptureActiveEffectGrantedTagCounts()
        {
            return CountTagOccurrences(
                effectRuntime?.ActiveEffects,
                active => active?.Definition?.GrantedTags?.Tags);
        }

        private Dictionary<GameplayTag, int> CaptureActiveAbilityOwnedTagCounts()
        {
            return CountTagOccurrences(
                abilityRuntime?.ActiveAbilities,
                spec => spec?.Ability?.ActivationOwnedTags?.Tags);
        }

        private AttributeSetState CaptureAttributeSetState()
        {
            var attributeSet = ResolveAttributeSet();
            return attributeSet != null
                ? attributeSet.CaptureState(false)
                : default;
        }

        private void RestoreAttributeSetState(AttributeSetState state)
        {
            if (!state.HasState)
                return;

            ResolveAttributeSet()?.RestoreState(state, false);
        }

        private AttributeSet ResolveAttributeSet()
        {
            var attributeOwner = effectRuntime?.AttributeOwner;

            if (attributeOwner is AttributeSet attributeSet)
                return attributeSet;

            return attributeOwner is IGameplayAttributeSetProvider provider
                ? provider.AttributeSet
                : null;
        }

        private void RestoreRuntimeFrame(int frame)
        {
            if (RuntimeContext is DefaultGameplayEffectRuntimeContext defaultContext)
            {
                defaultContext.RestoreFrame(frame);
            }
        }

        private void EnsureRuntimeIds(GameplayAbilitySystemState state)
        {
            int nextSpecId = 1;
            int nextRuntimeEffectId = 1;
            int nextAbilitySpecId = 1;
            int nextAbilityTaskId = 1;

            if (state.ActiveEffects != null)
            {
                for (int i = 0; i < state.ActiveEffects.Length; i++)
                {
                    nextSpecId = Math.Max(nextSpecId, state.ActiveEffects[i].SpecId + 1);
                    nextRuntimeEffectId = Math.Max(nextRuntimeEffectId, state.ActiveEffects[i].RuntimeEffectId + 1);
                }
            }

            if (state.ActiveAbilities != null)
            {
                for (int i = 0; i < state.ActiveAbilities.Length; i++)
                {
                    var abilityState = state.ActiveAbilities[i];
                    nextAbilitySpecId = Math.Max(nextAbilitySpecId, abilityState.AbilitySpecId + 1);

                    if (abilityState.ActiveTasks == null)
                        continue;

                    for (int j = 0; j < abilityState.ActiveTasks.Length; j++)
                    {
                        nextAbilityTaskId = Math.Max(
                            nextAbilityTaskId,
                            abilityState.ActiveTasks[j].AbilityTaskId + 1);
                    }
                }
            }

            RuntimeContext.EnsureNextIds(
                nextSpecId,
                nextRuntimeEffectId,
                nextAbilitySpecId,
                nextAbilityTaskId,
                1);
        }
    }
}
