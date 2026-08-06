using BattleFoundation;
using GAS;

namespace BattleCommon
{
    /// <summary>
    /// CombatAbilityComponent 的逻辑接口，供 L2 纯逻辑层访问标签操作。
    /// 继承 IEntityComponent 以支持 Get&lt;ICombatAbilityComponent&gt;() 查询。
    /// </summary>
    public interface ICombatAbilityComponent : IEntityComponent
    {
        bool HasTag(GameplayTag tag);
        void AddTag(GameplayTag tag);
        void RemoveTag(GameplayTag tag);
    }
}
