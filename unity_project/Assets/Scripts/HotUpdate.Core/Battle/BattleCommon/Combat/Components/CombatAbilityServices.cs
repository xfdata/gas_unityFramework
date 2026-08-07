using GAS;

namespace BattleCommon
{
    public interface ICombatAbilityServices
    {
        GameplayDefinitionCatalog AbilityCatalog { get; }
        ProjectileRuntime ProjectileRuntime { get; }
    }
}
