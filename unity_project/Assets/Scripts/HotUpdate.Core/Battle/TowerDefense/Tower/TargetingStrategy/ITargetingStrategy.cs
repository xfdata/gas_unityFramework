using System.Collections.Generic;
using BattleFoundation;

namespace TowerDefense
{
    /// <summary>
    /// 目标选择策略接口 —— 策略模式，支持运行时切换和未来扩展。
    /// 
    /// 设计：
    /// - 每种策略独立实现 FindBestTarget，互不依赖
    /// - 新增策略只需实现此接口，无需修改 TDTargetingComponent
    /// - 策略由 TDTargetingComponent 在初始化时根据 TowerConfig.TargetPriority 创建
    /// </summary>
    public interface ITargetingStrategy
    {
        /// <summary>
        /// 策略名称（用于调试/事件记录/UI显示）
        /// </summary>
        string StrategyName { get; }

        /// <summary>
        /// 在候选敌人列表中寻找最佳目标。
        /// 
        /// 性能：调用方已预计算 rangeSqr，策略内不做额外的距离平方运算缓存。
        /// </summary>
        /// <param name="enemies">候选敌人列表（已按阵营过滤，调用方保证非null）</param>
        /// <param name="owner">防御塔自身（提供 Position 等上下文）</param>
        /// <param name="rangeSqr">攻击距离平方（调用方预计算）</param>
        /// <returns>最佳目标，无有效目标返回 null</returns>
        TDEnemyActor FindBestTarget(IReadOnlyList<BattleEntity> enemies, BattleEntity owner, float rangeSqr);
    }
}
