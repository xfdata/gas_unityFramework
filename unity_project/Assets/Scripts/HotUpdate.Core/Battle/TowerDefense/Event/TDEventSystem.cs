using BattleFoundation;
using Framework;

namespace TowerDefense
{
    /// <summary>
    /// TD战斗事件系统 — 收集关键战斗数据，对接BattleEventBus和回放。
    /// 
    /// 职责：
    /// - 监听所有TD事件，聚合统计数据
    /// - 提供UI查询接口（击杀数、波次、剩余血量等）
    /// - 已通过BattleRecorder/BattlePlayback兼容回放
    ///   （事件由EventBus自动记录在FrameRecordData中）
    /// </summary>
    public class TDEventSystem : IBattleSystem
    {
        private IBattleContext _context;

        // ===== 统计数据 =====
        public int TotalEnemyKilled { get; private set; }
        public int TotalEnemySpawned { get; private set; }
        public int TotalEnemyReachedEnd { get; private set; }
        public int TowersBuilt { get; private set; }
        public int TowersUpgraded { get; private set; }
        public int CurrentWaveIndex { get; private set; }
        public int TotalWaves { get; private set; }
        public float TotalDamageToCity { get; private set; }
        public float TotalDamageByTowers { get; private set; }
        public int BossesKilled { get; private set; }
        public int SkillCastCount { get; private set; }
        public int TargetSwitchCount { get; private set; }
        public int ChoiceCount { get; private set; }          // Phase 5: 强化选择次数
        public int CompletedWaves { get; private set; }       // Phase 5: 已完成的波次数

        /// <summary>
        /// 主城当前血量百分比
        /// </summary>
        public float CityHpPercent
        {
            get
            {
                var mainCitySystem = _context?.GetSystem<MainCitySystem>();
                return mainCitySystem?.MainCity?.Health?.HPPercent ?? 1f;
            }
        }

        public void Initialize(IBattleContext context)
        {
            _context = context;
            ResetStats();

            // 订阅TD事件
            var eb = context.EventBus;
            eb.On<TDEnemyActor>(TDEventIds.EnemySpawned, OnEnemySpawned);
            eb.On<EnemyKilledEvent>(TDEventIds.EnemyKilled, OnEnemyKilled);
            eb.On<EnemyReachedEndEvent>(TDEventIds.EnemyReachedEnd, OnEnemyReachedEnd);
            eb.On<TowerActor>(TDEventIds.TowerBuilt, OnTowerBuilt);
            eb.On<TowerActor>(TDEventIds.TowerUpgraded, OnTowerUpgraded);
            eb.On<MainCityDamagedEvent>(TDEventIds.MainCityDamaged, OnCityDamaged);
            eb.On<int>(TDEventIds.WaveStarted, OnWaveStarted);
            eb.On<int>(TDEventIds.WaveCleared, OnWaveCleared);
            eb.On<TDEnemyActor>(TDEventIds.BossSpawned, OnBossSpawned);
            eb.On<TowerAttackEvent>(TDEventIds.TowerAttack, OnTowerAttack);
            eb.On<ProjectileHitEvent>(TDEventIds.ProjectileHit, OnProjectileHit);
            eb.On<TowerSkillCastEvent>(TDEventIds.TowerSkillCast, OnTowerSkillCast);
            eb.On<TowerTargetSwitchEvent>(TDEventIds.TowerTargetSwitch, OnTowerTargetSwitch);
            // Phase 5: Roguelike 事件
            eb.On<int>(TDEventIds.WaveCompleted, OnWaveCompleted);
            eb.On<RoguelikeChoiceStartEvent>(TDEventIds.RoguelikeChoiceStart, OnRoguelikeChoiceStart);
            eb.On<ChoiceSelectedEvent>(TDEventIds.RoguelikeChoiceSelected, OnRoguelikeChoiceSelected);
        }

        public void Start() { }

        public void Update(float deltaTime) { }

        public void LateUpdate(float deltaTime) { }

        /// <summary>
        /// 设置总波次数（由WaveManager注入）
        /// </summary>
        public void SetTotalWaves(int total)
        {
            TotalWaves = total;
        }

        /// <summary>
        /// 获取存活敌人数量
        /// </summary>
        public int GetAliveEnemyCount()
        {
            return _context?.EntityManager?.AliveCountByCamp(EEntityCamp.Enemy) ?? 0;
        }

        /// <summary>
        /// 重置统计（新战斗开始时）
        /// </summary>
        public void ResetStats()
        {
            TotalEnemyKilled = 0;
            TotalEnemySpawned = 0;
            TotalEnemyReachedEnd = 0;
            TowersBuilt = 0;
            TowersUpgraded = 0;
            CurrentWaveIndex = -1;
            TotalWaves = 0;
            TotalDamageToCity = 0f;
            TotalDamageByTowers = 0f;
            BossesKilled = 0;
            SkillCastCount = 0;
            TargetSwitchCount = 0;
            ChoiceCount = 0;
            CompletedWaves = 0;
        }

        // ===== Event Handlers =====

        private void OnEnemySpawned(TDEnemyActor enemy)
        {
            TotalEnemySpawned++;
        }

        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            TotalEnemyKilled++;

            // 检查是否Boss击杀
            var entity = _context?.EntityManager?.GetById(evt.EnemyId) as TDEnemyActor;
            if (entity?.IsBoss == true)
                BossesKilled++;
        }

        private void OnEnemyReachedEnd(EnemyReachedEndEvent evt)
        {
            TotalEnemyReachedEnd++;
            TotalDamageToCity += evt.DamageToCity;
        }

        private void OnTowerBuilt(TowerActor tower) => TowersBuilt++;
        private void OnTowerUpgraded(TowerActor tower) => TowersUpgraded++;

        private void OnCityDamaged(MainCityDamagedEvent evt)
        {
            TotalDamageToCity += evt.Damage;
        }

        private void OnWaveStarted(int waveIndex)
        {
            CurrentWaveIndex = waveIndex;
        }

        private void OnWaveCleared(int waveIndex)
        {
            // WaveManager 会处理，此处记录
        }

        private void OnBossSpawned(TDEnemyActor boss)
        {
            // Boss出场记录
        }

        private void OnTowerAttack(TowerAttackEvent evt)
        {
            TotalDamageByTowers += evt.AttackDamage;
        }

        private void OnProjectileHit(ProjectileHitEvent evt)
        {
            // 投射物命中记录（伤害已通过CombatDamageExecution处理）
        }

        private void OnTowerSkillCast(TowerSkillCastEvent evt)
        {
            SkillCastCount++;
        }

        private void OnTowerTargetSwitch(TowerTargetSwitchEvent evt)
        {
            TargetSwitchCount++;
        }

        // ===== Phase 5: Roguelike 事件处理 =====

        private void OnWaveCompleted(int waveIndex)
        {
            CompletedWaves++;
        }

        private void OnRoguelikeChoiceStart(RoguelikeChoiceStartEvent evt)
        {
            // 选择面板打开时记录
        }

        private void OnRoguelikeChoiceSelected(ChoiceSelectedEvent evt)
        {
            ChoiceCount++;
        }

        public void Dispose()
        {
            if (_context != null)
            {
                var eb = _context.EventBus;
                eb.Off<TDEnemyActor>(TDEventIds.EnemySpawned, OnEnemySpawned);
                eb.Off<EnemyKilledEvent>(TDEventIds.EnemyKilled, OnEnemyKilled);
                eb.Off<EnemyReachedEndEvent>(TDEventIds.EnemyReachedEnd, OnEnemyReachedEnd);
                eb.Off<TowerActor>(TDEventIds.TowerBuilt, OnTowerBuilt);
                eb.Off<TowerActor>(TDEventIds.TowerUpgraded, OnTowerUpgraded);
                eb.Off<MainCityDamagedEvent>(TDEventIds.MainCityDamaged, OnCityDamaged);
                eb.Off<int>(TDEventIds.WaveStarted, OnWaveStarted);
                eb.Off<int>(TDEventIds.WaveCleared, OnWaveCleared);
                eb.Off<TDEnemyActor>(TDEventIds.BossSpawned, OnBossSpawned);
                eb.Off<TowerAttackEvent>(TDEventIds.TowerAttack, OnTowerAttack);
                eb.Off<ProjectileHitEvent>(TDEventIds.ProjectileHit, OnProjectileHit);
                eb.Off<TowerSkillCastEvent>(TDEventIds.TowerSkillCast, OnTowerSkillCast);
                eb.Off<TowerTargetSwitchEvent>(TDEventIds.TowerTargetSwitch, OnTowerTargetSwitch);
                eb.Off<int>(TDEventIds.WaveCompleted, OnWaveCompleted);
                eb.Off<RoguelikeChoiceStartEvent>(TDEventIds.RoguelikeChoiceStart, OnRoguelikeChoiceStart);
                eb.Off<ChoiceSelectedEvent>(TDEventIds.RoguelikeChoiceSelected, OnRoguelikeChoiceSelected);
            }
            ResetStats();
            _context = null;
        }
    }
}
