using System;
using UnityEngine;

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

    [CreateAssetMenu(menuName = "BattleFoundation/Battle Config")]
    public class BattleFoundationConfig : ScriptableObject
    {
        [Header("Tick Mode")]
        [SerializeField]
        private EBattleTickMode _tickMode = EBattleTickMode.RealTime;

        [Header("Frame Sync")]
        [SerializeField]
        [Tooltip("帧同步固定步长（秒），通常为 1/30")]
        [Min(0.01f)]
        private float _frameSyncStep = 0.033333f;

        [Header("Replay")]
        [SerializeField]
        private bool _enableReplay = true;

        [Header("Time")]
        [SerializeField]
        [Tooltip("全局时间缩放，1=正常速度")]
        [Range(0f, 10f)]
        private float _globalTimeScale = 1f;

        [Header("Random")]
        [SerializeField]
        [Tooltip("随机种子，0=随机生成")]
        private int _randomSeed;

        [Header("Entity Pool")]
        [SerializeField]
        [Tooltip("是否启用实体对象池")]
        private bool _enableEntityPool = true;

        [SerializeField]
        [Tooltip("对象池初始容量")]
        [Min(0)]
        private int _poolInitialCapacity = 20;

        [Header("Debug")]
        [SerializeField]
        private bool _enableDebugLog;

        [SerializeField]
        private bool _enableGizmos;

        public EBattleTickMode TickMode => _tickMode;
        public float FrameSyncStep => _frameSyncStep;
        public bool EnableReplay => _enableReplay;
        public float GlobalTimeScale => _globalTimeScale;
        public int RandomSeed => _randomSeed;
        public bool EnableEntityPool => _enableEntityPool;
        public int PoolInitialCapacity => _poolInitialCapacity;
        public bool EnableDebugLog => _enableDebugLog;
        public bool EnableGizmos => _enableGizmos;

        public BattleRuntimeSettings CreateRuntimeSettings()
        {
            return new BattleRuntimeSettings
            {
                TickMode = _tickMode,
                FrameSyncStep = _frameSyncStep,
                InitialTimeScale = _globalTimeScale,
                EnableReplay = _enableReplay,
                RandomSeed = _randomSeed,
            };
        }
    }
}
