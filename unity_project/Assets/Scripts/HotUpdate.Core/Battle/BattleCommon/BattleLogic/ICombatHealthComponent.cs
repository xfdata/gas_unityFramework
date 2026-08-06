using BattleFoundation;

namespace BattleCommon
{
    /// <summary>
    /// CombatHealthComponent 的逻辑接口，供 L2 纯逻辑层访问生命值状态。
    /// 继承 IEntityComponent 以支持 Get&lt;ICombatHealthComponent&gt;() 查询。
    /// </summary>
    public interface ICombatHealthComponent : IEntityComponent
    {
        bool IsDead { get; }
        bool IsAlive { get; }
        float HP { get; }
        float MaxHP { get; }
    }
}
