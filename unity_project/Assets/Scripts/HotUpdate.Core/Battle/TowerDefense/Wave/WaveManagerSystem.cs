using System.Collections.Generic;
using BattleFoundation;
using Framework;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 波次管理系统 — 管理波次状态机（Preparing → Spawning → Active → Cleared）。
    /// 
    /// 支持多路径：每波可以配置多个 WavePathEntry，从不同路径同时进攻。
    /// 
    /// 状态机：
    ///   Idle → Preparing(波前倒计时) → Spawning(逐个生成) → Active(等待清除) → Cleared → NextWave
    /// </summary>
    public class WaveManagerSystem : IBattleSystem
    {
        private IBattleContext _context;
        private EntityManager _entityManager;
        private TDBattleEngine _tdEngine;
        private TDEventSystem _eventSystem;

        // 波次配置
        private WaveConfig[] _waveConfigs;

        // 状态机
        public ETDWaveState State { get; private set; } = ETDWaveState.Idle;
        public int CurrentWaveIndex { get; private set; } = -1;
        public int TotalWaves => _waveConfigs?.Length ?? 0;
        public int TotalWaveCount => TotalWaves;
        public bool AllWavesCleared => CurrentWaveIndex >= TotalWaves - 1 && State == ETDWaveState.Cleared;

        // 多路径生成进度
        private class PathSpawnProgress
        {
            public WavePathEntry PathEntry;
            public int SpawnedCount;
            public float SpawnTimer;
        }
        private readonly List<PathSpawnProgress> _pathProgressList = new List<PathSpawnProgress>();
        private int _totalToSpawn;
        private float _preparingTimer;

        // 缓存的SpawnerCommand
        private WaveSpawnerCommand _cachedCommand;

        public void Initialize(IBattleContext context)
        {
            _context = context;
            _entityManager = context.EntityManager;
            _tdEngine = context.Engine as TDBattleEngine;
            _eventSystem = context.GetSystem<TDEventSystem>();
            _cachedCommand = new WaveSpawnerCommand();
        }

        public void Start() { }

        /// <summary>
        /// 开始波次序列（路径配置在WaveConfig内部）
        /// </summary>
        public void StartWaves(WaveConfig[] configs)
        {
            _waveConfigs = configs ?? System.Array.Empty<WaveConfig>();
            _eventSystem?.SetTotalWaves(TotalWaves);
            StartNextWave();
        }

        /// <summary>
        /// 开始下一波
        /// </summary>
        public void StartNextWave()
        {
            CurrentWaveIndex++;
            BattleLog.Wave($"StartNextWave: TotalWaves={TotalWaves}, CurrentWaveIndex={CurrentWaveIndex}, State(before)={State}");

            if (CurrentWaveIndex >= TotalWaves)
            {
                State = ETDWaveState.Cleared;
                BattleLog.Wave($"All waves completed! TotalWaves={TotalWaves}, CurrentWaveIndex={CurrentWaveIndex}. State={State}. No enemies spawned?");
                return;
            }

            var currentConfig = _waveConfigs[CurrentWaveIndex];
            State = ETDWaveState.Preparing;
            BattleLog.Wave($"Wave {CurrentWaveIndex + 1} loaded: name={currentConfig.WaveName}, GetTotalSpawnCount={currentConfig.GetTotalSpawnCount()}, pathEntries={currentConfig.PathEntries?.Length ?? 0}, enemyEntries={currentConfig.EnemyEntries?.Length ?? 0}");
            _preparingTimer = currentConfig.PreparationTime;
            _totalToSpawn = currentConfig.GetTotalSpawnCount();

            _context.EventBus.Emit(TDEventIds.WaveStarted, CurrentWaveIndex);
            BattleLog.Wave($"Wave {CurrentWaveIndex + 1}/{TotalWaves} preparing ({currentConfig.PreparationTime}s)...");
            
            // 初始化多路径生成进度
            InitPathProgress(currentConfig);
        }

    // 主城目标位置（取代旧版WaypointPath，敌人通过NavMesh寻路到主城）
    private Vector3 _cityTargetPosition;
    private MainCityActor _cityActor;

    /// <summary>
    /// 设置主城目标（敌人NavMesh寻路目标）
    /// </summary>
    public void SetCityTarget(Vector3 targetPosition, MainCityActor cityActor = null)
    {
        _cityTargetPosition = targetPosition;
        _cityActor = cityActor;
    }

    /// <summary>
    /// 初始化多路径生成进度（NavMesh寻路模式，不再依赖WaypointPath）
    /// </summary>
    private void InitPathProgress(WaveConfig config)
    {
        _pathProgressList.Clear();

        BattleLog.ConfigMatch($"InitPathProgress '{config.WaveName}': PathEntries.Length={config.PathEntries?.Length ?? 0}, EnemyEntries.Length={config.EnemyEntries?.Length ?? 0}, cityPos=({_cityTargetPosition.x:F1},{_cityTargetPosition.z:F1})");

        // 新配置：多路径（多波敌人同时进攻）
        if (config.PathEntries != null && config.PathEntries.Length > 0)
        {
            BattleLog.ConfigMatch("BRANCH=PathEntries (multi-spawn groups)");
            foreach (var pathEntry in config.PathEntries)
            {
                if (pathEntry == null)
                {
                    BattleLog.ConfigMatchWarning("PathEntry is null, skipping.");
                    continue;
                }
                BattleLog.ConfigMatch($"PathEntry id='{pathEntry.PathId}': EnemyEntries.Length={pathEntry.EnemyEntries?.Length ?? 0}, GetTotalCount={pathEntry.GetTotalCount()}");
                if (pathEntry.EnemyEntries == null || pathEntry.EnemyEntries.Length == 0)
                {
                    BattleLog.ConfigMatchError($"SKIP: PathEntry '{pathEntry.PathId}' EnemyEntries is EMPTY!");
                    continue;
                }
                _pathProgressList.Add(new PathSpawnProgress
                {
                    PathEntry = pathEntry,
                    SpawnedCount = 0,
                    SpawnTimer = 0f
                });
            }
        }
        // 兼容旧配置：单一路径，使用EnemyEntries构造临时PathEntry
        else if (config.EnemyEntries != null && config.EnemyEntries.Length > 0)
        {
            BattleLog.ConfigMatch($"BRANCH=EnemyEntries fallback. EnemyEntries.Count={config.EnemyEntries.Length}");
            var fallbackEntry = new WavePathEntry
            {
                PathId = "default",
                EnemyEntries = config.EnemyEntries
            };
            _pathProgressList.Add(new PathSpawnProgress
            {
                PathEntry = fallbackEntry,
                SpawnedCount = 0,
                SpawnTimer = 0f
            });
        }
        else
        {
            BattleLog.ConfigMatchError($"ERROR: Wave '{config.WaveName}' has NO PathEntries AND NO EnemyEntries!");
        }

        BattleLog.ConfigMatch($"InitPathProgress done. _pathProgressList.Count={_pathProgressList.Count} path(s) created");
    }

        public void Update(float deltaTime)
        {
            using (new AutoProfiler("TowerDefense.WaveManagerSystem.Update"))
            {
                switch (State)
                {
                    case ETDWaveState.Preparing:
                        UpdatePreparing(deltaTime);
                        break;
                    case ETDWaveState.Spawning:
                        UpdateSpawning(deltaTime);
                        break;
                    case ETDWaveState.Active:
                        UpdateActive();
                        break;
                }
            }
        }

        public void LateUpdate(float deltaTime) { }
        
        private void UpdatePreparing(float deltaTime)
        {
            _preparingTimer -= deltaTime;
            
            if (_preparingTimer <= 0f)
            {
                State = ETDWaveState.Spawning;
                // 初始化所有路径的生成计时器
                for (int i = 0; i < _pathProgressList.Count; i++)
                {
                    _pathProgressList[i].SpawnTimer = 0f; // 立即开始生成
                }
                BattleLog.Wave($"Wave {CurrentWaveIndex + 1} spawning {_totalToSpawn} enemies.");
            }
        }

        private void UpdateSpawning(float deltaTime)
        {
            bool allSpawned = true;

            // 遍历所有路径，按间隔生成敌人
            for (int i = 0; i < _pathProgressList.Count; i++)
            {
                var progress = _pathProgressList[i];
                var pathEntry = progress.PathEntry;

                if (progress.SpawnedCount >= pathEntry.GetTotalCount())
                    continue; // 该路径已生成完毕

                allSpawned = false;
                progress.SpawnTimer -= deltaTime;

                // 按间隔生成
                while (progress.SpawnTimer <= 0f && progress.SpawnedCount < pathEntry.GetTotalCount())
                {
                    SpawnEnemyFromPath(progress);
                    progress.SpawnTimer += pathEntry.GetSpawnInterval(_waveConfigs[CurrentWaveIndex].SpawnInterval);
                }
            }

            // 全部生成完毕 → 进入Active
            if (allSpawned)
            {
                State = ETDWaveState.Active;
                BattleLog.Wave($"Wave {CurrentWaveIndex + 1} active.");
            }
        }

        /// <summary>
        /// 从指定路径生成一个敌人（NavMesh寻路到主城）
        /// </summary>
        private void SpawnEnemyFromPath(PathSpawnProgress progress)
        {
            var pathEntry = progress.PathEntry;
            if (pathEntry.EnemyEntries == null || pathEntry.EnemyEntries.Length == 0)
                return;

            // 找到当前应该生成的敌人配置
            int remaining = progress.SpawnedCount;
            TDEnemyConfig configToSpawn = pathEntry.EnemyEntries[0].Config;
            
            foreach (var entry in pathEntry.EnemyEntries)
            {
                if (remaining < entry.Count)
                {
                    configToSpawn = entry.Config;
                    break;
                }
                remaining -= entry.Count;
            }

            if (configToSpawn == null)
            {
                BattleLog.SpawnWarning("SKIP spawn: configToSpawn is NULL");
                return;
            }

            // 计算生成位置（在地图边缘生成，朝向主城方向）
            Vector3 spawnPos = CalculateSpawnPosition(configToSpawn);

            // 通过命令队列生成
            var cmd = _cachedCommand;
            cmd.Reset();
            cmd.Setup(configToSpawn, _cityTargetPosition, spawnPos, 1, _cityActor, configToSpawn.IsBoss);
            _tdEngine?.EnqueueCommand(cmd);
            BattleLog.Spawn($"EnqueueCommand: enemy={configToSpawn.name}, spawn=({spawnPos.x:F1},{spawnPos.z:F1})->city=({_cityTargetPosition.x:F1},{_cityTargetPosition.z:F1}), isBoss={configToSpawn.IsBoss}");

            progress.SpawnedCount++;
        }

        /// <summary>
        /// 计算敌人生成位置（在地图边缘，面向主城方向）
        /// </summary>
        private Vector3 CalculateSpawnPosition(TDEnemyConfig config)
        {
            // 使用PathEntry中Path的路点起点（如果存在），否则在远离主城的方向生成
            // 保留对WaypointPath的兼容：如果PathEntry.Path有路点，使用第一个路点作为出生位置
            // 否则，在远离主城的随机方向生成
            Vector3 center = _cityTargetPosition;
            
            // 如果有路径引用且路径有路点，用第一个路点（向后兼容）
            // 这里pathEntry.Path可能在PathEntries分支中有值
            // 如果没有，计算随机边缘位置
            float angle = (_context?.Random?.Value ?? 0.5f) * Mathf.PI * 2f;
            float spawnDistance = 20f + (_context?.Random?.Value ?? 0f) * 10f; // 20-30单位远
            return center + new Vector3(
                Mathf.Cos(angle) * spawnDistance,
                0f,
                Mathf.Sin(angle) * spawnDistance
            );
        }

        private void UpdateActive()
        {
            // 检查当前波次敌人是否全部清除
            int aliveEnemies = _entityManager.AliveCountByCamp(EEntityCamp.Enemy);
            if (aliveEnemies <= 0)
            {
                State = ETDWaveState.Cleared;
                _context.EventBus.Emit(TDEventIds.WaveCleared, CurrentWaveIndex);
                // Phase 5: 发射 WaveCompleted 事件，供 RoquelikeChoiceSystem 拦截
                _context.EventBus.Emit(TDEventIds.WaveCompleted, CurrentWaveIndex);
                BattleLog.Wave($"Wave {CurrentWaveIndex + 1} cleared!");

                // Phase 5: 如果 RoguelikeChoiceSystem 设置了等待标志，则暂停自动推进
                if (!WaitingForRoguelikeChoice)
                    StartNextWave();
                else
                    BattleLog.Wave("Waiting for Roguelike choice before next wave...");
            }
        }

        /// <summary>
        /// 强制跳到下一波（作弊/调试用）
        /// </summary>
        public void SkipToNextWave()
        {
            var enemies = _entityManager.GetByCamp(EEntityCamp.Enemy);
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                if (enemies[i] is TDEnemyActor tdEnemy)
                    _tdEngine?.EnemyFactory?.Recycle(tdEnemy);
            }

            State = ETDWaveState.Cleared;
            StartNextWave();
        }

        // ===== Phase 5: Roguelike 选择钩子 =====

        /// <summary>
        /// 是否等待罗吉尔选择完成后再推进下一波。
        /// 由 RoguelikeChoiceSystem 在初始化时设置为 true。
        /// </summary>
        public bool WaitingForRoguelikeChoice { get; set; }

        /// <summary>
        /// 罗吉尔选择完成后，由 RoguelikeChoiceSystem 调用以恢复波次推进。
        /// </summary>
        public void ResumeNextWave()
        {
            WaitingForRoguelikeChoice = false;
            StartNextWave();
        }

        public void Dispose()
        {
            _waveConfigs = null;
            _pathProgressList.Clear();
            _cachedCommand = null;
            _eventSystem = null;
            _tdEngine = null;
            _entityManager = null;
            _context = null;
            State = ETDWaveState.Idle;
        }
    }
}
