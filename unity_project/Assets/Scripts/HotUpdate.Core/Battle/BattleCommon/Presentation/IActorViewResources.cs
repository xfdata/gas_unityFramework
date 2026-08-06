using Animancer;
using UnityEngine;
using UnityEngine.Playables;

namespace BattleCommon
{
    /// <summary>
    /// R3-S7: 表现层资源访问接口（过渡期）。
    ///
    /// 设计原则：
    /// - IActorViewBinding 是 L2→L3 的单向指令契约（SyncTransform/PlayHit/DestroyView）。
    /// - IActorViewResources 是 L3 资源 getter，供过渡期表现组件（ActorPresentationComponent/
    ///   ActorAnimationComponent）访问 Unity 类型。
    /// - S8/S9 表现组件完全迁移到 ActorViewBinder 内部后，此接口移除。
    ///
    /// 边界：
    /// - 逻辑层（CombatActor/CombatAbilityComponent）禁止使用此接口。
    /// - 仅表现组件（EntityComponent 体系中残留的 presentation component）可使用。
    /// </summary>
    public interface IActorViewResources
    {
        GameObject GameObject { get; }
        Transform Transform { get; }
        Animator Animator { get; }
        AnimancerComponent Animancer { get; }
        PlayableDirector Director { get; }
    }
}
