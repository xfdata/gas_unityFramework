using System;
using UnityEngine;

namespace GAS
{
    public abstract class AbilityTask
    {
        public int TaskId { get; private set; }
        public GameplayAbilitySpec AbilitySpec { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsFinished { get; private set; }

        internal void Initialize(GameplayAbilitySpec spec, int taskId)
        {
            AbilitySpec = spec;
            TaskId = taskId;
        }

        public void Activate()
        {
            if (IsFinished)
                return;

            IsActive = true;
            OnActivate();
        }

        public void Tick(float deltaTime)
        {
            if (!IsActive || IsFinished)
                return;

            OnTick(deltaTime);
        }

        public void EndTask()
        {
            if (IsFinished)
                return;

            IsActive = false;
            IsFinished = true;
            OnEnd();
        }

        protected virtual void OnActivate() { }
        protected virtual void OnTick(float deltaTime) { }
        protected virtual void OnEnd() { }

        internal virtual GameplayAbilityTaskState CaptureState()
        {
            return new GameplayAbilityTaskState
            {
                AbilityTaskId = TaskId,
                Kind = GameplayAbilityTaskKind.None,
            };
        }
    }

    public class AbilityTaskWaitDelay : AbilityTask
    {
        private readonly float duration;
        private readonly Action<AbilityTaskWaitDelay> onCompleted;
        private float elapsed;

        protected float Duration => duration;
        protected float Elapsed => elapsed;
        protected float TimeLeft => Mathf.Max(0f, duration - elapsed);

        public AbilityTaskWaitDelay(float duration, Action<AbilityTaskWaitDelay> onCompleted = null)
            : this(duration, 0f, onCompleted)
        {
        }

        protected AbilityTaskWaitDelay(
            float duration,
            float elapsed,
            Action<AbilityTaskWaitDelay> onCompleted = null)
        {
            this.duration = Mathf.Max(0f, duration);
            this.elapsed = Mathf.Clamp(elapsed, 0f, this.duration);
            this.onCompleted = onCompleted;
        }

        protected override void OnActivate()
        {
            if (duration <= 0f)
            {
                Complete();
                EndTask();
            }
        }

        protected override void OnTick(float deltaTime)
        {
            elapsed += deltaTime;

            if (elapsed >= duration)
            {
                Complete();
                EndTask();
            }
        }

        internal override GameplayAbilityTaskState CaptureState()
        {
            return new GameplayAbilityTaskState
            {
                AbilityTaskId = TaskId,
                Kind = GameplayAbilityTaskKind.WaitDelay,
                TimeLeft = TimeLeft,
            };
        }

        protected virtual void OnCompleted() { }

        private void Complete()
        {
            OnCompleted();
            onCompleted?.Invoke(this);
        }
    }
}
