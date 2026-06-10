using BattleFoundation;

namespace BattleCommon
{
    public abstract class CombatComponentBase : EntityComponent
    {
        public new CombatActor Owner => base.Owner as CombatActor;
    }
}
