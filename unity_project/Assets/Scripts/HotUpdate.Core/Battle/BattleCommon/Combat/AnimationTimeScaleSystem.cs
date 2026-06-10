using System;
using System.Collections.Generic;
using Animancer;
using Framework;
using UnityEngine;
using BattleFoundation;
using UnityEngine.Playables;

namespace BattleCommon
{

public interface IAnimationTimeScaleServices
{
    bool IsAnimationPaused { get; }
    float AnimationPlaybackScale { get; }
    void ForEachAnimationActor(Action<CombatActor> action);
}

public class AnimationTimeScaleSystem : IBattleSystem
{
    private IAnimationTimeScaleServices _services;
    private readonly HashSet<GameObject> _trackedRoots = new HashSet<GameObject>();
    private readonly Dictionary<Animator, float> _animatorBaseSpeeds = new Dictionary<Animator, float>();
    private readonly Dictionary<AnimancerComponent, float> _animancerBaseSpeeds = new Dictionary<AnimancerComponent, float>();
    private readonly Dictionary<PlayableDirector, float[]> _directorBaseSpeeds = new Dictionary<PlayableDirector, float[]>();
    private readonly List<PlayableDirector> _directorBuffer = new List<PlayableDirector>();
    private float _lastAppliedScale = float.NaN;

    public void Initialize(IBattleContext context)
    {
        _services = context as IAnimationTimeScaleServices;
    }

    public void Start()
    {
        ApplyTimeScale(true);
    }

    public void Update(float deltaTime)
    {
        using (new AutoProfiler("BattleCommon.AnimationTimeScale.Update"))
        {
            ApplyTimeScale();
        }
    }

    public void LateUpdate(float deltaTime)
    {
        ApplyTimeScale();
    }

    public void ApplyTimeScale(bool force = false)
    {
        TrackActiveActors();

        float timeScale = GetCurrentTimeScale();
        if (!force && Mathf.Approximately(_lastAppliedScale, timeScale))
        {
            ApplyDirectors(timeScale);
            return;
        }

        _lastAppliedScale = timeScale;
        ApplyAnimators(timeScale);
        ApplyAnimancers(timeScale);
        ApplyDirectors(timeScale);
    }

    public void Dispose()
    {
        _services = null;
        _trackedRoots.Clear();
        _animatorBaseSpeeds.Clear();
        _animancerBaseSpeeds.Clear();
        _directorBaseSpeeds.Clear();
        _directorBuffer.Clear();
        _lastAppliedScale = float.NaN;
    }

    private float GetCurrentTimeScale()
    {
        if (_services == null || _services.IsAnimationPaused)
            return 0f;

        return Mathf.Max(0f, _services.AnimationPlaybackScale);
    }

    private void TrackActiveActors()
    {
        _services?.ForEachAnimationActor(TrackActor);
    }

    private void TrackActor(CombatActor actor)
    {
        var root = actor?.GameObject;
        if (root == null || !_trackedRoots.Add(root))
            return;

        TrackAnimators(root);
        TrackAnimancers(root);
        TrackDirectors(root);
    }

    private void TrackAnimators(GameObject root)
    {
        var animators = root.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            var animator = animators[i];
            if (animator != null && !_animatorBaseSpeeds.ContainsKey(animator))
            {
                _animatorBaseSpeeds.Add(animator, animator.speed);
            }
        }
    }

    private void TrackAnimancers(GameObject root)
    {
        var animancers = root.GetComponentsInChildren<AnimancerComponent>(true);
        for (int i = 0; i < animancers.Length; i++)
        {
            var animancer = animancers[i];
            if (animancer != null && !_animancerBaseSpeeds.ContainsKey(animancer))
            {
                _animancerBaseSpeeds.Add(animancer, animancer.Playable.Speed);
            }
        }
    }

    private void TrackDirectors(GameObject root)
    {
        var directors = root.GetComponentsInChildren<PlayableDirector>(true);
        for (int i = 0; i < directors.Length; i++)
        {
            var director = directors[i];
            if (director != null && !_directorBaseSpeeds.ContainsKey(director))
            {
                _directorBaseSpeeds.Add(director, null);
            }
        }
    }

    private void ApplyAnimators(float timeScale)
    {
        foreach (var pair in _animatorBaseSpeeds)
        {
            if (pair.Key == null)
                continue;

            pair.Key.speed = pair.Value * timeScale;
        }
    }

    private void ApplyAnimancers(float timeScale)
    {
        foreach (var pair in _animancerBaseSpeeds)
        {
            if (pair.Key == null)
                continue;

            pair.Key.Playable.Speed = pair.Value * timeScale;
        }
    }

    private void ApplyDirectors(float timeScale)
    {
        _directorBuffer.Clear();
        foreach (var pair in _directorBaseSpeeds)
        {
            _directorBuffer.Add(pair.Key);
        }

        for (int directorIndex = 0; directorIndex < _directorBuffer.Count; directorIndex++)
        {
            var director = _directorBuffer[directorIndex];
            if (director == null || !director.playableGraph.IsValid())
                continue;

            var graph = director.playableGraph;
            int rootCount = graph.GetRootPlayableCount();
            if (rootCount <= 0)
                continue;

            var baseSpeeds = _directorBaseSpeeds[director];
            if (baseSpeeds == null || baseSpeeds.Length != rootCount)
            {
                baseSpeeds = new float[rootCount];
                for (int i = 0; i < rootCount; i++)
                {
                    baseSpeeds[i] = (float)graph.GetRootPlayable(i).GetSpeed();
                }

                _directorBaseSpeeds[director] = baseSpeeds;
            }

            for (int i = 0; i < rootCount; i++)
            {
                graph.GetRootPlayable(i).SetSpeed(baseSpeeds[i] * timeScale);
            }
        }
    }
}

}
