using System;

namespace BattleFoundation
{
    [Serializable]
    public class BattleRuntimeSettings
    {
        public EBattleTickMode TickMode = EBattleTickMode.RealTime;
        public float FrameSyncStep = 0.033333f;
        public float InitialTimeScale = 1f;
        public bool EnableReplay = true;
        public int RandomSeed;

        public BattleRuntimeSettings Clone()
        {
            return new BattleRuntimeSettings
            {
                TickMode = TickMode,
                FrameSyncStep = FrameSyncStep,
                InitialTimeScale = InitialTimeScale,
                EnableReplay = EnableReplay,
                RandomSeed = RandomSeed,
            };
        }
    }
}
