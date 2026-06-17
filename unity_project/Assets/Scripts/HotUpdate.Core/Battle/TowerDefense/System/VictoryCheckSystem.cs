using BattleFoundation;
using Framework;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 胜利条件检查系统 — 每帧检查胜利条件。
    /// 
    /// 胜利条件：
    /// 1. 所有波次已生成完毕（WaveManagerSystem.State == ETDWaveState.Cleared 且 CurrentWaveIndex >= TotalWaves - 1）
    /// 2. 场上无存活敌人（EntityManager.AliveCountByCamp(Enemy) == 0）
    /// 
    /// 使用方案B：独立System，解耦胜利条件检查与波次管理。
    /// </summary>
    public class VictoryCheckSystem : IBattleSystem
    {
        private IBattleContext _context;
        private EntityManager _entityManager;
        private WaveManagerSystem _waveManager;
        private bool _victoryTriggered;

        public void Initialize(IBattleContext context)
        {
            _context = context;
            _entityManager = context.EntityManager;
            _waveManager = context.GetSystem<WaveManagerSystem>();
            _victoryTriggered = false;
        }

        public void Start() { }

        public void Update(float deltaTime)
        {
            if (_victoryTriggered)
                return;

            // 检查胜利条件
            if (CheckVictoryCondition())
            {
                _victoryTriggered = true;
                TriggerVictory();
            }
        }

        public void LateUpdate(float deltaTime) { }

        /// <summary>
        /// 检查胜利条件
        /// </summary>
        private bool CheckVictoryCondition()
        {
            // 条件1：所有波次已清除
            if (_waveManager == null || !_waveManager.AllWavesCleared)
                return false;

            // 条件2：场上无存活敌人
            int aliveEnemies = _entityManager.AliveCountByCamp(EEntityCamp.Enemy);
            if (aliveEnemies > 0)
                return false;

            return true;
        }

        /// <summary>
        /// 触发胜利
        /// </summary>
        private void TriggerVictory()
        {
            Debug.Log("[VictoryCheckSystem] Victory condition met! Triggering battle win...");
            
            // 发射胜利事件
            _context.EventBus.Emit(TDEventIds.AllWavesCleared, 0); // 参数暂无意义
            
            // 触发战斗胜利（通过 BattleRule）
            // 注意：实际胜利判定应由 AllWavesClearedRule 处理
            // 这里仅发射事件，由 Rule 监听并触发
        }

        public void Dispose()
        {
            _waveManager = null;
            _entityManager = null;
            _context = null;
            _victoryTriggered = false;
        }
    }
}
