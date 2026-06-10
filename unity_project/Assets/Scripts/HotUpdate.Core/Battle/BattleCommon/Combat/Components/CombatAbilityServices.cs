using GAS;

namespace BattleCommon
{
    public interface ICombatAbilityServices
    {
        GameplayDefinitionCatalog AbilityCatalog { get; }
        IGameplayCueManager GameplayCueManager { get; }
        ProjectileRuntime ProjectileRuntime { get; }
    }
}
