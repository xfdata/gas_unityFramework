using System;
using BattleFoundation;
using GAS;

namespace BattleCommon.Replay
{
    /// <summary>
    /// Battle-mode factory for replay entity identity and custom state.
    /// The factory must return a detached, fully configured CombatActor.
    /// </summary>
    public interface ICombatReplayEntityFactory
    {
        void Capture(CombatActor actor, EntitySnapshot snapshot);
        CombatActor Create(EntitySnapshot snapshot, BattleContext context);
        void Apply(CombatActor actor, EntitySnapshot snapshot);
    }
    /// <summary>
    /// R3-S2: IBattleReplayAdapter 默认实现。
    /// 承担 BF 层无法直接引用的 GAS 类型操作（AttributeSetState 的 Capture/Restore），
    /// 解除 BF→GAS 编译期依赖。属性状态以 object 在 BF 层透传，运行时为 AttributeSetState。
    /// </summary>
    public class CombatReplayAdapter : IBattleReplayAdapter
    {
        private readonly ICombatReplayEntityFactory entityFactory;

        public CombatReplayAdapter(ICombatReplayEntityFactory entityFactory = null)
        {
            this.entityFactory = entityFactory;
        }

        public void CaptureEntity(BattleEntity entity, EntitySnapshot snapshot)
        {
            if (entity is CombatActor actor)
                entityFactory?.Capture(actor, snapshot);
        }

        public BattleEntity CreateEntity(EntitySnapshot snapshot, BattleContext context)
        {
            if (snapshot == null || context == null || entityFactory == null)
                return null;

            var actor = entityFactory.Create(snapshot, context);
            if (actor == null)
                return null;

            var actorSystem = context.GetSystem<CombatActorSystem>();
            if (actorSystem == null)
            {
                actor.Dispose();
                throw new InvalidOperationException(
                    "Combat replay requires CombatActorSystem to create dynamic actors.");
            }

            actor.SetId(snapshot.EntityId);
            actor.SetCamp(snapshot.Camp);
            actor.SetEntityType(snapshot.EntityType);
            actorSystem.Spawn(actor);
            return actor;
        }

        public void ApplyEntity(BattleEntity entity, EntitySnapshot snapshot)
        {
            if (entity is CombatActor actor)
                entityFactory?.Apply(actor, snapshot);
        }

        public void RemoveEntity(BattleEntity entity, BattleContext context)
        {
            if (entity is CombatActor actor)
            {
                var actorSystem = context?.GetSystem<CombatActorSystem>();
                if (actorSystem != null)
                {
                    actorSystem.Despawn(actor, DeathReason.SceneCleanup);
                    return;
                }
            }

            context?.EntityManager?.RemoveEntity(entity);
            entity?.Dispose();
        }

        /// <summary>
        /// 从 entity 的 GAS AttributeSet 捕获完整状态（baseValues + modifiers + nextModifierId）。
        /// 返回 object 避免 BF 层引用 GAS 类型；运行时为 AttributeSetState。
        /// </summary>
        public object CaptureAttributes(BattleEntity entity)
        {
            var attrSet = (entity as IGameplayAttributeSetProvider)?.AttributeSet;
            if (attrSet == null)
                return null;

            return attrSet.CaptureState(includeModifiers: true);
        }

        /// <summary>
        /// 恢复 entity 的 GAS AttributeSet 状态（含 modifiers，确保回放后属性与 modifiers 一致）。
        /// 不触发变更通知，避免回放期间重复触发死亡判定/UI 刷新。
        /// </summary>
        public void ApplyAttributes(BattleEntity entity, object state)
        {
            if (state == null)
                return;

            var attrSet = (entity as IGameplayAttributeSetProvider)?.AttributeSet;
            if (attrSet == null)
                return;

            if (state is AttributeSetState attributeSetState)
                attrSet.RestoreState(attributeSetState, notifyChanges: false);
        }
    }
}
