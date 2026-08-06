using BattleFoundation;
using GAS;

namespace BattleCommon.Replay
{
    /// <summary>
    /// R3-S2: IBattleReplayAdapter 默认实现。
    /// 承担 BF 层无法直接引用的 GAS 类型操作（AttributeSetState 的 Capture/Restore），
    /// 解除 BF→GAS 编译期依赖。属性状态以 object 在 BF 层透传，运行时为 AttributeSetState。
    /// </summary>
    public class CombatReplayAdapter : IBattleReplayAdapter
    {
        public void CaptureEntity(BattleEntity entity, EntitySnapshot snapshot)
        {
            // 默认无额外捕获，EntitySnapshot.Capture 已完成基础字段填充
        }

        public BattleEntity CreateEntity(EntitySnapshot snapshot, BattleContext context)
        {
            // 默认不创建实体，由上层（BattleEngine/EntityManager）处理
            return null;
        }

        public void ApplyEntity(BattleEntity entity, EntitySnapshot snapshot)
        {
            // 默认无额外应用，EntitySnapshot.ApplyBaseState 已完成基础字段恢复
        }

        public void RemoveEntity(BattleEntity entity, BattleContext context)
        {
            context?.EntityManager?.RemoveEntity(entity);
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
