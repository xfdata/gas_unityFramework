using BattleCommon;
using BattleFoundation;

namespace BattleSkillSimulation
{
    internal sealed class SimulationBattleEngine : BattleEngine
    {
        protected override BattleRuntimeSettings CreateRuntimeSettings()
        {
            return new BattleRuntimeSettings
            {
                TickMode = EBattleTickMode.RealTime,
                EnableReplay = false,
                InitialTimeScale = 1f,
                RandomSeed = 20260602,
            };
        }

        protected override void OnInitialize()
        {
            Context.AddSystem(new CombatTargetQuerySystem());
            Context.AddSystem(new CombatActorSystem());
        }
    }

    internal sealed class SimulationActor : CombatActor
    {
        public string Name { get; set; }
    }
}
