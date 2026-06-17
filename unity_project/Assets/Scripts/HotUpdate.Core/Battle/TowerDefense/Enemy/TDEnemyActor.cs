using BattleCommon;
using BattleFoundation;
using GAS;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// TD敌方Actor，继承CombatActor。
    /// 
    /// 复用：
    /// - CombatAttributeComponent：属性（hp, atk, def, moveSpeed）
    /// - CombatHealthComponent：生命值/死亡
    /// - CombatAbilityComponent：GAS Buff/效果
    /// - CombatMovementComponent：NavMesh移动（备选方案）
    /// 
    /// 新增：
    /// - PathFollowerComponent：路径跟随（主移动方式）
    /// - TDEnemyConfig引用：配置数据
    /// </summary>
    public class TDEnemyActor : CombatActor
    {
        private TDEnemyConfig _config;
        private PathFollowerComponent _pathFollower;

        public TDEnemyConfig Config => _config;
        public PathFollowerComponent PathFollower => _pathFollower ??= Get<PathFollowerComponent>();
        public ETDEnemyType EnemyType => _config?.EnemyType ?? ETDEnemyType.Normal;
        public bool IsBoss => _config?.IsBoss ?? false;

        /// <summary>
        /// 初始化敌人（来自对象池或新建）
        /// </summary>
        public void InitEnemy(TDEnemyConfig config, WaypointPath path, Vector3 spawnPosition)
        {
            _config = config;
            SetEntityType(config.IsBoss ? EEntityType.Boss : EEntityType.Monster);

            // 初始化路径跟随组件
            _pathFollower = Get<PathFollowerComponent>();
            if (_pathFollower == null)
                _pathFollower = AddComponent<PathFollowerComponent>();
            _pathFollower.Init(path, config.GetEffectiveSpeed());
            Position = spawnPosition;

            // 初始化属性组件（复用CombatAttributeComponent的属性访问器）
            var attributes = Get<CombatAttributeComponent>();
            if (attributes != null)
            {
                attributes.HP = config.GetEffectiveHp();
                attributes.MaxHP = config.GetEffectiveHp();
                attributes.Attack = config.Atk;
                attributes.Defense = config.Def;
                attributes.MoveSpeed = config.GetEffectiveSpeed();
            }

            HitRadius = config.HitRadius;

            // 初始化GameObject
            if (config.Prefab != null && GameObject == null)
            {
                GameObject = Object.Instantiate(config.Prefab, spawnPosition, Quaternion.identity);
                Transform = GameObject.transform;
                Animator = GameObject.GetComponentInChildren<Animator>();
            }

            if (Transform != null)
                Transform.position = spawnPosition;

            base.Initialize();
        }

        /// <summary>
        /// 获取这个敌人到达终点时对主城造成的伤害
        /// </summary>
        public int GetLeakDamage() => _config?.LeakDamage ?? 1;

        /// <summary>
        /// 获取击杀奖励金币
        /// </summary>
        public int GetKillGold() => _config?.GetEffectiveKillGold() ?? 10;

        public override void Die()
        {
            if (!IsAlive) return;
            
            // 停止攻击主城（如果正在攻击）
            var attacker = Get<CityAttackerComponent>();
            if (attacker != null && attacker.IsAttacking)
            {
                attacker.StopAttack();
            }

            base.Die();

            // 发射击杀事件（由攻击方发射，此处只需标记死亡）
            Engine?.Context?.EventBus?.Emit(TDEventIds.EnemyKilled,
                new EnemyKilledEvent(Id, 0));

            // 销毁GameObject（或回收到对象池的GameObject管理器）
            BeginDeathFadeOut(0.3f);
        }

        public override void DeactivateForPool()
        {
            _config = null;
            base.DeactivateForPool();
        }
    }
}
