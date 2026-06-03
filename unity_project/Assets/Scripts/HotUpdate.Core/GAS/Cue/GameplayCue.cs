using System;
using System.Collections.Generic;
using UnityEngine;

namespace GAS
{
    public struct GameplayCuePayload
    {
        public GameplayTag CueTag;

        public GameplayEffectRuntime Source;
        public GameplayEffectRuntime Target;
        public long SourceEntityId;
        public long TargetEntityId;

        public GameplayEffectSpec Spec;
        public GameplayEffectDefinition EffectDefinition;

        public int SpecId;
        public int RuntimeEffectId;

        public float Magnitude;
        public Vector3 Position;

        public object UserData;
    }

    public abstract class GameplayCueNotify : ScriptableObject
    {
        public void HandleCue(
            GameplayCueEventType eventType,
            in GameplayCuePayload payload
        )
        {
            switch (eventType)
            {
                case GameplayCueEventType.Execute:
                    OnExecute(payload);
                    break;

                case GameplayCueEventType.OnActive:
                    OnActive(payload);
                    break;

                case GameplayCueEventType.WhileActive:
                    WhileActive(payload);
                    break;

                case GameplayCueEventType.Removed:
                    OnRemove(payload);
                    break;
            }
        }

        protected virtual void OnExecute(in GameplayCuePayload payload) { }
        protected virtual void OnActive(in GameplayCuePayload payload) { }
        protected virtual void WhileActive(in GameplayCuePayload payload) { }
        protected virtual void OnRemove(in GameplayCuePayload payload) { }
    }

    [Serializable]
    public class GameplayCueEntry
    {
        public GameplayTag CueTag;
        public GameplayCueNotify Notify;
    }

    [CreateAssetMenu(menuName = "PVE/GAS/Gameplay Cue Set")]
    public class GameplayCueSet : ScriptableObject
    {
        public List<GameplayCueEntry> Entries = new List<GameplayCueEntry>();
    }

    public class GameplayCueManager : MonoBehaviour, IGameplayCueManager
    {
        [SerializeField]
        private GameplayCueSet cueSet;

        private readonly List<GameplayCueEntry> runtimeEntries = new List<GameplayCueEntry>();

        private void Awake()
        {
            Initialize(cueSet);
        }

        public void Initialize(GameplayCueSet set)
        {
            runtimeEntries.Clear();

            if (set == null || set.Entries == null)
                return;

            for (int i = 0; i < set.Entries.Count; i++)
            {
                var entry = set.Entries[i];

                if (entry == null)
                    continue;

                if (!entry.CueTag.IsValid || entry.Notify == null)
                    continue;

                runtimeEntries.Add(entry);
            }
        }

        public void HandleCue(
            GameplayTag cueTag,
            GameplayCueEventType eventType,
            in GameplayCuePayload payload
        )
        {
            if (!cueTag.IsValid)
                return;

            for (int i = 0; i < runtimeEntries.Count; i++)
            {
                var entry = runtimeEntries[i];

                if (!cueTag.Matches(entry.CueTag))
                    continue;

                entry.Notify.HandleCue(eventType, payload);
            }
        }
    }
}
