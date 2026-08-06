using BattleFoundation;

namespace BattleCommon
{
    /// <summary>
    /// 表现层绑定契约：L2 逻辑层通过此接口向 L3 表现层下发单向指令（R3 决策 1）。
    ///
    /// 设计原则：
    /// - L2 CombatActor 只持纯逻辑数据（Position/Rotation 为逻辑真相），不持 GameObject/Transform。
    /// - L3 ActorViewBinder 实现此接口，持有 GameObject/Animator/Animancer/NavMeshAgent，
    ///   每帧或按事件把逻辑状态投影到表现，单向不回流。
    /// - GameObject 的创建/池化/销毁由 L3 ViewBinder 负责，L2 不再自销 Object.Destroy。
    ///
    /// 与 IBattlePresentationSink 的边界：
    /// - IActorViewBinding 是 per-actor 视图指令（变换同步、动画播放、生命周期）。
    /// - IBattlePresentationSink 是 battle 级事件通知（OnDamageHit/OnActorDied 等），由 S9 定义。
    ///
    /// 本接口仅定义契约，CombatActor 的实际解耦在 S4，ActorViewBinder 实现在 S7。
    /// </summary>
    public interface IActorViewBinding
    {
        /// <summary>
        /// 把逻辑层 Position/Rotation 投影到表现层 Transform（单向，不回流）。
        /// 替代 CombatActor.Position/Rotation 直接读写 Transform。
        /// </summary>
        void SyncTransform(in Float3 position, in Float4 rotation);

        /// <summary>
        /// 销毁表现视图（GameObject）。由 CombatActorSystem 在实体销毁时调用，
        /// 替代 CombatActor.OnDispose 中的 Object.Destroy(GameObject)。
        /// </summary>
        void DestroyView();

        /// <summary>
        /// 回收到对象池时清理表现侧缓存（Animancer/Director/Clip 引用）。
        /// 由 CombatActorSystem 在实体回收时调用，替代 CombatActor.DeactivateForPool 的缓存清理。
        /// </summary>
        void OnRecycle();

        /// <summary>
        /// 播放受击表现（闪白）。替代 ActorPresentationComponent.PlayHitFlash。
        /// </summary>
        void PlayHit();

        /// <summary>
        /// 播放死亡表现（动画 + 渐隐序列）。替代 ActorPresentationComponent.StartDeathPresentation。
        /// </summary>
        void PlayDeath();

        /// <summary>
        /// 显式开始死亡渐隐。由 DeathAbility 在播放完死亡 Timeline 后调用，
        /// 替代 CombatActor.BeginDeathFadeOut / IAbilityAnimationProvider.BeginDeathFadeOut。
        /// </summary>
        /// <param name="duration">渐隐时长（秒）。</param>
        void BeginDeathFadeOut(float duration);

        /// <summary>
        /// 播放技能动画。替代 CombatAbilityComponent 中直接 Animancer.Play(clip) 的调用。
        /// 逻辑层不再出现 Animancer.Play，由 L3 ViewBinder 查找片段并播放。
        /// </summary>
        /// <param name="abilityId">能力 Id，用于查找对应动画片段。</param>
        /// <param name="duration">预期施放时长（秒）。</param>
        void PlaySkill(int abilityId, float duration);
    }
}
