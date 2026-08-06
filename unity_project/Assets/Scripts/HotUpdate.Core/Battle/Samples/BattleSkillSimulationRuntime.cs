using BattleCommon;
using BattleCommon.Replay;
using BattleFoundation;

namespace BattleSkillSimulation
{
    internal sealed class SimulationBattleEngine : BattleEngine
    {
        private BattlePresentationSink _presentationSink;

        // R3-S10: 暴露 PresentationSink 供外部（WorldBuilder）注入到 CombatAbilityComponent。
        public BattlePresentationSink PresentationSink => _presentationSink;

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
            // R3-S2: 注入 CombatReplayAdapter，承担 GAS AttributeSetState 的 Capture/Restore
            SetReplayAdapter(new CombatReplayAdapter());

            // R3-S9: 注入 BattlePresentationSink，订阅 ActorSpawned/ActorDied 事件。
            // Samples 使用 NullPresentationSink（无实际表现），逻辑层正常运行。
            _presentationSink = new BattlePresentationSink();
            _presentationSink.Bind(Context);

            Context.AddSystem(new CombatTargetQuerySystem());
            Context.AddSystem(new CombatActorSystem());
        }

        protected override void OnDispose()
        {
            _presentationSink?.Dispose();
            _presentationSink = null;
            base.OnDispose();
        }
    }

    internal sealed class SimulationActor : CombatActor
    {
        public string Name { get; set; }
    }
}