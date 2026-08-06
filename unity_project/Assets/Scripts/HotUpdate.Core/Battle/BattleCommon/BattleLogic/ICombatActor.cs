using BattleFoundation;

namespace BattleCommon
{
    /// <summary>
    /// CombatActor 的逻辑接口，供 L2 纯逻辑层使用，打破对 Assembly-CSharp 中 CombatActor 具体类的循环引用。
    /// CombatActor（Assembly-CSharp）实现此接口，L2 通过接口访问。
    /// </summary>
    public interface ICombatActor
    {
        T Get<T>() where T : class, IEntityComponent;
        bool IsAlive { get; }
    }
}
