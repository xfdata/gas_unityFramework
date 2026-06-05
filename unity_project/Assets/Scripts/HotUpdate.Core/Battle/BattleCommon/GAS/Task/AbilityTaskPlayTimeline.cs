using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace GAS
{
    public class AbilityTaskPlayTimeline : AbilityTask
    {
        private const string EnableCollisionEvent = "EnableCollision";
        private const string DisableCollisionEvent = "DisableCollision";

        private readonly TimelineAsset timelineAsset;
        private readonly Action<AbilityTaskPlayTimeline> onCompleted;

        private PlayableDirector director;
        private readonly List<TimelineEvent> events = new List<TimelineEvent>();
        private readonly HashSet<TimelineEvent> firedEvents = new HashSet<TimelineEvent>();
        private readonly Dictionary<string, Action> callbacks = new Dictionary<string, Action>();
        private bool isPlayBackEnded;
        private Action enableCollisionCallback;
        private Action disableCollisionCallback;

        public event Action OnEnableCollision;
        public event Action OnDisableCollision;

        private struct TimelineEvent
        {
            public string Name;
            public double Time;
        }

        public AbilityTaskPlayTimeline(
            TimelineAsset timelineAsset,
            Action<AbilityTaskPlayTimeline> onCompleted = null)
        {
            this.timelineAsset = timelineAsset;
            this.onCompleted = onCompleted;
        }

        protected override void OnActivate()
        {
            var provider = AbilitySpec?.Source?.AttributeOwner as IAbilityAnimationProvider;
            if (provider == null || timelineAsset == null)
            {
                EndTask();
                return;
            }

            director = provider.Director;
            if (director == null)
            {
                EndTask();
                return;
            }

            CollectEvents(timelineAsset);
            director.stopped += OnDirectorStopped;

            director.playableAsset = timelineAsset;
            director.time = 0;

            enableCollisionCallback = () => OnEnableCollision?.Invoke();
            disableCollisionCallback = () => OnDisableCollision?.Invoke();

            director.Play();
        }

        public bool TryRegisterEvent(string eventName, Action callback)
        {
            if (string.IsNullOrEmpty(eventName) || callback == null || director == null)
                return false;

            bool found = false;
            foreach (var evt in events)
            {
                if (evt.Name == eventName)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                return false;

            callbacks[eventName] = callback;
            return true;
        }

        protected override void OnTick(float deltaTime)
        {
            if (director == null || isPlayBackEnded)
                return;

            double currentTime = director.time;

            // Check marker-based events
            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                if (firedEvents.Contains(evt))
                    continue;

                if (currentTime >= evt.Time)
                {
                    firedEvents.Add(evt);
                    FireEvent(evt.Name);
                }
            }

            // Check playback ended (timeline reached its duration)
            if (currentTime >= director.duration && director.duration > 0)
            {
                isPlayBackEnded = true;
                OnPlaybackCompleted();
            }
        }

        protected override void OnEnd()
        {
            if (director != null)
            {
                director.stopped -= OnDirectorStopped;
            }

            isPlayBackEnded = false;
            enableCollisionCallback = null;
            disableCollisionCallback = null;
            callbacks.Clear();
            firedEvents.Clear();
            events.Clear();
            OnEnableCollision = null;
            OnDisableCollision = null;
        }

        private void OnDirectorStopped(PlayableDirector d)
        {
            if (!isPlayBackEnded)
            {
                isPlayBackEnded = true;
                OnPlaybackCompleted();
            }
        }

        private void OnPlaybackCompleted()
        {
            onCompleted?.Invoke(this);
            EndTask();
        }

        private void FireEvent(string eventName)
        {
            if (eventName == EnableCollisionEvent)
            {
                enableCollisionCallback?.Invoke();
            }
            else if (eventName == DisableCollisionEvent)
            {
                disableCollisionCallback?.Invoke();
            }

            if (callbacks.TryGetValue(eventName, out var callback))
            {
                callback?.Invoke();
            }
        }

        private void CollectEvents(TimelineAsset asset)
        {
            if (asset == null)
                return;

            // Collect from output tracks
            foreach (var track in asset.GetOutputTracks())
            {
                CollectFromTrack(track);
            }

            // Collect from top-level marker track
            if (asset.markerTrack != null)
            {
                CollectMarkers(asset.markerTrack.GetMarkers());
            }
        }

        private void CollectFromTrack(TrackAsset track)
        {
            if (track == null)
                return;

            CollectMarkers(track.GetMarkers());

            foreach (var childTrack in track.GetChildTracks())
            {
                CollectFromTrack(childTrack);
            }
        }

        private void CollectMarkers(IEnumerable<IMarker> markerList)
        {
            if (markerList == null)
                return;

            foreach (var marker in markerList)
            {
                if (marker is SignalEmitter signalEmitter && signalEmitter.asset != null)
                {
                    events.Add(new TimelineEvent
                    {
                        Name = signalEmitter.asset.name,
                        Time = marker.time
                    });
                }
            }
        }
    }
}
