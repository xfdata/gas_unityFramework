using BattleFoundation;

namespace TowerDefense
{
    /// <summary>
    /// 波次生成命令 — 通过BattleEngine命令队列生成一波敌人。
    /// 确保在命令执行阶段（System.Update之前）完成实体创建。
    /// </summary>
    public class WaveSpawnerCommand : BattleCommand
    {
        private TDEnemyConfig _config;
        private WaypointPath _path;
        private int _count;
        private int _enemyIdCounter;
        private bool _isBoss;

        public WaveSpawnerCommand() { }

        public void Setup(TDEnemyConfig config, WaypointPath path, int count, bool isBoss = false)
        {
            _config = config;
            _path = path;
            _count = count;
            _isBoss = isBoss;
            SourceEntityId = 0;
            TargetEntityId = 0;
        }

        protected override byte GetCommandTypeId() => 10; // TD类别

        protected override void OnExecute(BattleEngine engine)
        {
            if (_config == null || _path == null) return;

            var tdEngine = engine as TDBattleEngine;
            var factory = tdEngine?.EnemyFactory;
            if (factory == null) return;

            var pathStart = _path.Waypoints.Length > 0 ? _path.Waypoints[0] : UnityEngine.Vector3.zero;

            // 批量生成敌人（用命令队列确保顺序）
            for (int i = 0; i < _count; i++)
            {
                var enemy = factory.Allocate(_config, _path, pathStart);
                if (enemy != null)
                {
                    _enemyIdCounter++;
                    SourceEntityId = enemy.Id;
                }
            }
        }

        public override void Reset()
        {
            base.Reset();
            _config = null;
            _path = null;
            _count = 0;
            _enemyIdCounter = 0;
            _isBoss = false;
        }
    }
}
