using System;
using System.Collections.Generic;

namespace GAS
{
    public interface IGameplayEffectRuntimeContext
    {
        int CurrentFrame { get; }
        float DeltaTime { get; }
        IReadOnlyList<GameplayEffectEvent> Events { get; }

        System.Random Random { get; }

        int NewSpecId();
        int NewRuntimeEffectId();
        int NewAbilitySpecId();
        int NewAbilityTaskId();
        int NewProjectileId();
        void EnsureNextIds(
            int nextSpecId,
            int nextRuntimeEffectId,
            int nextAbilitySpecId,
            int nextAbilityTaskId,
            int nextProjectileId);

        void RegisterEntity(long entityId, IGameplayEffectRuntime runtime);
        void UnregisterEntity(long entityId, IGameplayEffectRuntime runtime);
        IGameplayEffectRuntime ResolveEntity(long entityId);

        void BeginTick(float deltaTime);
        void EndTick();
        void RecordEvent(in GameplayEffectEvent gameplayEvent);
        void ClearEvents();

        void Subscribe(GameplayEffectEventType type, Action<GameplayEffectEvent> handler);
        void Unsubscribe(GameplayEffectEventType type, Action<GameplayEffectEvent> handler);
    }

    public class DefaultGameplayEffectRuntimeContext : IGameplayEffectRuntimeContext
    {
        private readonly List<GameplayEffectEvent> events = new List<GameplayEffectEvent>();
        private readonly Dictionary<long, IGameplayEffectRuntime> entities =
            new Dictionary<long, IGameplayEffectRuntime>();

        private readonly Dictionary<GameplayEffectEventType, List<Action<GameplayEffectEvent>>> subscribers =
            new Dictionary<GameplayEffectEventType, List<Action<GameplayEffectEvent>>>();

        private int nextSpecId = 1;
        private int nextRuntimeEffectId = 1;
        private int nextAbilitySpecId = 1;
        private int nextAbilityTaskId = 1;
        private int nextProjectileId = 1;
        private System.Random random;

        public int CurrentFrame { get; private set; }
        public float DeltaTime { get; private set; }
        public IReadOnlyList<GameplayEffectEvent> Events => events;
        public System.Random Random => random ?? (random = new System.Random());

        public int NewSpecId()
        {
            return nextSpecId++;
        }

        public int NewRuntimeEffectId()
        {
            return nextRuntimeEffectId++;
        }

        public int NewAbilitySpecId()
        {
            return nextAbilitySpecId++;
        }

        public int NewAbilityTaskId()
        {
            return nextAbilityTaskId++;
        }

        public int NewProjectileId()
        {
            return nextProjectileId++;
        }

        public void EnsureNextIds(
            int nextSpecId,
            int nextRuntimeEffectId,
            int nextAbilitySpecId,
            int nextAbilityTaskId,
            int nextProjectileId)
        {
            this.nextSpecId = Math.Max(this.nextSpecId, nextSpecId);
            this.nextRuntimeEffectId = Math.Max(this.nextRuntimeEffectId, nextRuntimeEffectId);
            this.nextAbilitySpecId = Math.Max(this.nextAbilitySpecId, nextAbilitySpecId);
            this.nextAbilityTaskId = Math.Max(this.nextAbilityTaskId, nextAbilityTaskId);
            this.nextProjectileId = Math.Max(this.nextProjectileId, nextProjectileId);
        }

        public void RegisterEntity(long entityId, IGameplayEffectRuntime runtime)
        {
            if (entityId == 0 || runtime == null)
                return;

            entities[entityId] = runtime;
        }

        public void UnregisterEntity(long entityId, IGameplayEffectRuntime runtime)
        {
            if (entityId == 0)
                return;

            if (!entities.TryGetValue(entityId, out var existing) || existing != runtime)
                return;

            entities.Remove(entityId);
        }

        public IGameplayEffectRuntime ResolveEntity(long entityId)
        {
            if (entityId == 0)
                return null;

            return entities.TryGetValue(entityId, out var runtime) ? runtime : null;
        }

        public void BeginTick(float deltaTime)
        {
            DeltaTime = deltaTime;
            CurrentFrame++;
            events.Clear();
        }

        public void EndTick()
        {
            DeltaTime = 0f;
        }

        public void RecordEvent(in GameplayEffectEvent gameplayEvent)
        {
            events.Add(gameplayEvent);
            NotifySubscribers(gameplayEvent);
        }

        public void ClearEvents()
        {
            events.Clear();
        }

        public void Subscribe(GameplayEffectEventType type, Action<GameplayEffectEvent> handler)
        {
            if (handler == null)
                return;

            if (!subscribers.TryGetValue(type, out var handlers))
            {
                handlers = new List<Action<GameplayEffectEvent>>();
                subscribers[type] = handlers;
            }

            if (handlers.Contains(handler))
                return;

            handlers.Add(handler);
        }

        public void Unsubscribe(GameplayEffectEventType type, Action<GameplayEffectEvent> handler)
        {
            if (handler == null)
                return;

            if (subscribers.TryGetValue(type, out var handlers))
            {
                handlers.Remove(handler);

                if (handlers.Count == 0)
                {
                    subscribers.Remove(type);
                }
            }
        }

        public void InitRandom(int seed)
        {
            random = new System.Random(seed);
        }

        public void RestoreFrame(int frame)
        {
            CurrentFrame = Math.Max(0, frame);
        }

        private void NotifySubscribers(GameplayEffectEvent gameplayEvent)
        {
            if (!subscribers.TryGetValue(gameplayEvent.Type, out var handlers))
                return;

            var snapshot = handlers.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                snapshot[i]?.Invoke(gameplayEvent);
            }
        }
    }
}
