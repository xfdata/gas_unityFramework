using Animancer;

namespace BattleCommon
{
    /// <summary>
    /// 战斗单位动画配置接口，定义角色所需的动画片段。
    /// 具体实现（如 UnitConfig）放在 PVE/NewPVE 层，BattleCommon 仅通过接口访问。
    /// </summary>
    public interface IUnitAnimationConfig
    {
        ClipTransition SpawnAnim { get; }
        ClipTransition IdleAnim { get; }
        ClipTransition DeathAnim { get; }
        ClipTransition AttackAnim { get; }
    }
}