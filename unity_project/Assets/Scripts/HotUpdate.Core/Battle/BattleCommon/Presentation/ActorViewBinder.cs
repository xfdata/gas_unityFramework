using Animancer;
using BattleFoundation;
using UnityEngine;
using UnityEngine.Playables;

namespace BattleCommon
{
    /// <summary>
    /// R3-S7: Actor 表现层绑定器。
    ///
    /// 职责：
    /// - 持有 Unity 表现资源（GameObject/Transform/Animator/Animancer/Director）。
    /// - 实现 IActorViewBinding：接收 L2 逻辑层的单向指令（SyncTransform/DestroyView 等）。
    /// - 实现 IActorViewResources：过渡期供残留表现组件访问 Unity 资源。
    ///
    /// 生命周期：
    /// - 由 CombatActorSystem 或上层工厂在 spawn 时创建并绑定到 CombatActor.ViewBinding。
    /// - DestroyView 由 CombatActor.OnDispose 委托调用，销毁 GameObject。
    /// - OnRecycle 由 CombatActor.DeactivateForPool 委托调用，清理缓存。
    ///
    /// 注意：
    /// - S7 阶段 PlayHit/PlayDeath/BeginDeathFadeOut/PlaySkill 为空实现或最小实现，
    ///   实际表现逻辑仍在 ActorPresentationComponent/ActorAnimationComponent 中。
    /// - S8/S9 将这些逻辑迁移到 ActorViewBinder 内部，届时移除 IActorViewResources。
    /// </summary>
    public class ActorViewBinder : IActorViewBinding, IActorViewResources
    {
        private GameObject _gameObject;
        private Transform _transform;
        private Animator _animator;
        private AnimancerComponent _animancer;
        private PlayableDirector _director;
        private readonly bool _destroyWithActor;

        public ActorViewBinder(GameObject gameObject, bool destroyWithActor = true)
        {
            _gameObject = gameObject;
            _destroyWithActor = destroyWithActor;
            if (gameObject != null)
            {
                _transform = gameObject.transform;
                _animator = gameObject.GetComponentInChildren<Animator>();
                _animancer = gameObject.GetComponentInChildren<AnimancerComponent>();
                _director = gameObject.GetComponentInChildren<PlayableDirector>();
            }
        }

        // ===== IActorViewResources =====

        public GameObject GameObject => _gameObject;
        public Transform Transform => _transform;
        public Animator Animator => _animator;
        public AnimancerComponent Animancer => _animancer;
        public PlayableDirector Director => _director;

        // ===== IActorViewBinding =====

        public void SyncTransform(in Float3 position, in Float4 rotation)
        {
            if (_transform == null) return;
            _transform.position = new Vector3(position.x, position.y, position.z);
            _transform.rotation = new Quaternion(rotation.x, rotation.y, rotation.z, rotation.w);
        }

        public void DestroyView()
        {
            if (_gameObject != null && _destroyWithActor)
                Object.Destroy(_gameObject);

            _gameObject = null;
            _transform = null;
            _animator = null;
            _animancer = null;
            _director = null;
        }

        public void OnRecycle()
        {
            // R3-S7: 清理表现侧缓存引用。
            // 不销毁 GameObject（由 DestroyView 负责）。
            _animancer = null;
            _director = null;
        }

        public void PlayHit()
        {
            // R3-S7: 暂由 ActorPresentationComponent.PlayHitFlash 处理。
            // S8 迁移 CombatShaderController 到此方法内部后启用。
        }

        public void PlayDeath()
        {
            // R3-S7: 暂由 ActorPresentationComponent.StartDeathPresentation 处理。
            // S8 迁移死亡表现逻辑到此方法内部后启用。
        }

        public void BeginDeathFadeOut(float duration)
        {
            // R3-S7: 暂由 ActorPresentationComponent.BeginDeathFadeOut 处理。
            // S8 迁移后启用。
        }

        public void PlaySkill(int abilityId, float duration)
        {
            // R3-S7: 暂由 CombatAbilityComponent 直接 Animancer.Play 处理。
            // S8 迁移动画播放逻辑到此方法内部后启用。
        }
    }
}
