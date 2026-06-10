using System.Collections.Generic;
using BattleCommon;
using GAS;
using Framework;

namespace BattleCommon
{
    public class CombatGameplayCueManager : IGameplayCueManager
    {
        private readonly Dictionary<long, HashSet<int>> _activePoisonCues =
            new Dictionary<long, HashSet<int>>();

        public void HandleCue(
            GameplayTag cueTag,
            GameplayCueEventType eventType,
            in GameplayCuePayload payload)
        {
            using (new AutoProfiler("BattleCommon.GameplayCueManager.HandleCue"))
            {
                if (cueTag.Matches(CombatGameplayTags.Cue_Hit))
                {
                    HandleHitCue(eventType, payload);
                    return;
                }

                if (cueTag.Matches(CombatGameplayTags.Cue_Poison))
                {
                    HandlePoisonCue(eventType, payload);
                }
            }
        }

        private static void HandleHitCue(GameplayCueEventType eventType, in GameplayCuePayload payload)
        {
            if (eventType != GameplayCueEventType.Execute && eventType != GameplayCueEventType.OnActive)
                return;

            ResolveTarget(payload)?.Get<ActorPresentationComponent>()?.PlayHitFlash();
        }

        private void HandlePoisonCue(GameplayCueEventType eventType, in GameplayCuePayload payload)
        {
            var target = ResolveTarget(payload);
            if (target == null)
                return;

            switch (eventType)
            {
                case GameplayCueEventType.OnActive:
                    AddActivePoisonCue(payload);
                    target.Get<ActorPresentationComponent>()?.SetPoisoned(true);
                    break;

                case GameplayCueEventType.WhileActive:
                    if (HasActivePoisonCue(payload))
                    {
                        target.Get<ActorPresentationComponent>()?.SetPoisoned(true);
                    }
                    break;

                case GameplayCueEventType.Removed:
                    RemoveActivePoisonCue(payload);
                    target.Get<ActorPresentationComponent>()?.SetPoisoned(
                        HasActivePoisonCue(payload) || HasPoisonTag(target));
                    break;
            }
        }

        private void AddActivePoisonCue(in GameplayCuePayload payload)
        {
            if (payload.TargetEntityId == 0 || payload.RuntimeEffectId == 0)
                return;

            if (!_activePoisonCues.TryGetValue(payload.TargetEntityId, out var runtimeIds))
            {
                runtimeIds = new HashSet<int>();
                _activePoisonCues[payload.TargetEntityId] = runtimeIds;
            }

            runtimeIds.Add(payload.RuntimeEffectId);
        }

        private void RemoveActivePoisonCue(in GameplayCuePayload payload)
        {
            if (payload.TargetEntityId == 0 || payload.RuntimeEffectId == 0)
                return;

            if (!_activePoisonCues.TryGetValue(payload.TargetEntityId, out var runtimeIds))
                return;

            runtimeIds.Remove(payload.RuntimeEffectId);
            if (runtimeIds.Count == 0)
            {
                _activePoisonCues.Remove(payload.TargetEntityId);
            }
        }

        private bool HasActivePoisonCue(in GameplayCuePayload payload)
        {
            return payload.TargetEntityId != 0 &&
                   _activePoisonCues.TryGetValue(payload.TargetEntityId, out var runtimeIds) &&
                   runtimeIds.Count > 0;
        }

        private static bool HasPoisonTag(CombatActor target)
        {
            return target?.Get<CombatAbilityComponent>()?.HasTag(CombatGameplayTags.State_Poisoned) ?? false;
        }

        private static CombatActor ResolveTarget(in GameplayCuePayload payload)
        {
            return payload.Target?.AttributeOwner as CombatActor;
        }
    }
}