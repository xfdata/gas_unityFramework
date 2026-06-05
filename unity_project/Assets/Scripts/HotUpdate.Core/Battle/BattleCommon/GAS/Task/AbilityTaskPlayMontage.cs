using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace GAS
{
    // Presentation bridge usable by combat abilities in multiple gameplay modes.
    public interface IAbilityAnimationProvider
    {
        AnimancerComponent Animancer { get; }
        PlayableDirector Director { get; }
        ClipTransition GetAbilityMontage(GameplayAbilityDefinition ability);
        TimelineAsset GetAbilityTimeline(GameplayAbilityDefinition ability);
        void BeginDeathFadeOut(float duration);
    }

    public class AbilityTaskPlayMontage : AbilityTask
    {
        private const string EnableCollisionEvent = "EnableCollision";
        private const string DisableCollisionEvent = "DisableCollision";

        private readonly ClipTransition clip;
        private readonly float fadeDuration;
        private readonly Action<AbilityTaskPlayMontage> onCompleted;

        private AnimancerComponent animancer;
        private AnimancerState state;
        private Action onEnd;
        private Action enableCollisionCallback;
        private Action disableCollisionCallback;
        private bool isEnableCollisionCallbackSet;
        private bool isDisableCollisionCallbackSet;
        private readonly Dictionary<string, Action> namedCallbacks = new Dictionary<string, Action>();

        public event Action OnEnableCollision;
        public event Action OnDisableCollision;

        public AbilityTaskPlayMontage(ClipTransition clip, float fadeDuration, Action<AbilityTaskPlayMontage> onCompleted = null)
        {
            this.clip = clip;
            this.fadeDuration = fadeDuration;
            this.onCompleted = onCompleted;
        }

        protected override void OnActivate()
        {
            var provider = AbilitySpec?.Source?.AttributeOwner as IAbilityAnimationProvider;
            if (provider == null || clip == null)
            {
                EndTask();
                return;
            }

            animancer = provider.Animancer;
            if (animancer == null)
            {
                EndTask();
                return;
            }

            state = animancer.Play(clip, fadeDuration);

            onEnd = () =>
            {
                onCompleted?.Invoke(this);
                EndTask();
            };

            enableCollisionCallback = () => OnEnableCollision?.Invoke();
            disableCollisionCallback = () => OnDisableCollision?.Invoke();

            state.Events.OnEnd = onEnd;
            isEnableCollisionCallbackSet = TrySetCallback(EnableCollisionEvent, enableCollisionCallback);
            isDisableCollisionCallbackSet = TrySetCallback(DisableCollisionEvent, disableCollisionCallback);
        }

        public bool TryRegisterEvent(string eventName, Action callback)
        {
            if (string.IsNullOrEmpty(eventName) || callback == null || state == null)
                return false;

            if (!TrySetCallback(eventName, callback))
                return false;

            namedCallbacks[eventName] = callback;
            return true;
        }

        protected override void OnEnd()
        {
            if (state != null)
            {
                state.Events.OnEnd = null;

                if (isEnableCollisionCallbackSet)
                {
                    state.Events.RemoveCallback(EnableCollisionEvent, enableCollisionCallback);
                }

                if (isDisableCollisionCallbackSet)
                {
                    state.Events.RemoveCallback(DisableCollisionEvent, disableCollisionCallback);
                }

                foreach (var pair in namedCallbacks)
                {
                    state.Events.RemoveCallback(pair.Key, pair.Value);
                }
            }

            onEnd = null;
            enableCollisionCallback = null;
            disableCollisionCallback = null;
            isEnableCollisionCallbackSet = false;
            isDisableCollisionCallbackSet = false;
            namedCallbacks.Clear();
            OnEnableCollision = null;
            OnDisableCollision = null;
        }

        private bool TrySetCallback(string eventName, Action callback)
        {
            if (state.Events.IndexOf(eventName) < 0)
                return false;

            state.Events.SetCallback(eventName, callback);
            return true;
        }
    }
}
