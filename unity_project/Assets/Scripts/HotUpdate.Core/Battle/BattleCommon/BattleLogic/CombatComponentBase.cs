using BattleFoundation;

namespace BattleCommon
{
    /// <summary>
    /// 战斗组件基类。Owner 继承自 EntityComponent，类型为 BattleEntity。
    /// L2 纯逻辑层通过 Owner.Get&lt;ICombatXxxComponent&gt;() 访问接口。
    /// Assembly-CSharp 子类通过 (Owner as CombatActor) 访问 CombatActor 特有成员。
    /// </summary>
    public abstract class CombatComponentBase : EntityComponent
    {
    }
}
