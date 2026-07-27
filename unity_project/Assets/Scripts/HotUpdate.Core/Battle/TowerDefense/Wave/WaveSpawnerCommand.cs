using BattleFoundation;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 波次生成命令 — 通过BattleEngine命令队列生成一波敌人。
    /// 敌人通过NavMesh寻路移动到主城目标位置。
    /// </summary>
    public class WaveSpawnerCommand : BattleCommand
    {
        private TDEnemyConfig _config;
        private Vector3 _targetPosition;
        private Vector3 _spawnPosition;
        private int _count;
        private int _enemyIdCounter;
        private bool _isBoss;
        private MainCityActor _cityActor;

        public WaveSpawnerCommand() { }

        public void Setup(TDEnemyConfig config, Vector3 targetPosition, Vector3 spawnPosition, int count, MainCityActor cityActor = null, bool isBoss = false)
        {
            _config = config;
            _targetPosition = targetPosition;
            _spawnPosition = spawnPosition;
            _count = count;
            _isBoss = isBoss;
            _cityActor = cityActor;
            SourceEntityId = 0;
            TargetEntityId = 0;
        }

        protected override byte GetCommandTypeId() => 10; // TD类别

        protected override void OnExecute(BattleEngine engine)
        {
            if (_config == null) return;

            var tdEngine = engine as TDBattleEngine;
            var factory = tdEngine?.EnemyFactory;
            if (factory == null) return;

            // 批量生成敌人（用命令队列确保顺序）
            for (int i = 0; i < _count; i++)
            {
                var enemy = factory.Allocate(_config, _targetPosition, _spawnPosition, _cityActor);
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
            _count = 0;
            _enemyIdCounter = 0;
            _isBoss = false;
            _cityActor = null;
        }
    }
}
