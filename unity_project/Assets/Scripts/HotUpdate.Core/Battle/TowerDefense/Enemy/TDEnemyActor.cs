using System;
using BattleCommon;
using BattleFoundation;
using GAS;
using UnityEngine;
using UnityEngine.AI;

namespace TowerDefense
{
    /// <summary>
    /// TD敌方Actor，继承CombatActor。
    /// 
    /// 复用：
    /// - CombatAttributeComponent：属性（hp, atk, def, moveSpeed）
    /// - CombatHealthComponent：生命值/死亡
    /// - CombatAbilityComponent：GAS Buff/效果
    /// - CombatMovementComponent：NavMesh移动（现为主移动方式）
    /// 
    /// 新增：
    /// - CityAttackerComponent：到达主城后持续攻击
    /// - TDEnemyConfig引用：配置数据
    /// 
    /// 已废弃：
    /// - PathFollowerComponent（不再使用固定路点路径）
    /// </summary>
    public class TDEnemyActor : CombatActor, ICombatProgressTarget
    {
        private TDEnemyConfig _config;
        private Vector3 _cityTargetPosition;
        private bool _cityReached;
        private float _cityAttackRange = 3f;
        private MainCityActor _cityActor;

        public TDEnemyConfig Config => _config;
        public ETDEnemyType EnemyType => _config?.EnemyType ?? ETDEnemyType.Normal;
        public bool IsBoss => _config?.IsBoss ?? false;

        /// <summary>
        /// 初始化敌人（NavMesh寻路到主城目标位置）
        /// </summary>
        public void InitEnemy(TDEnemyConfig config, Vector3 targetPosition, Vector3 spawnPosition, MainCityActor cityActor = null)
        {
            _config = config;
            _cityTargetPosition = targetPosition;
            _cityReached = false;
            _cityActor = cityActor;
            SetEntityType(config.IsBoss ? EEntityType.Boss : EEntityType.Monster);

            // 初始化属性组件
            var attributes = Get<CombatAttributeComponent>();
            if (attributes == null)
                attributes = AddComponent<CombatAttributeComponent>();
            attributes.HP = config.GetEffectiveHp();
            attributes.MaxHP = config.GetEffectiveHp();
            attributes.Attack = config.Atk;
            attributes.Defense = config.Def;
            attributes.MoveSpeed = config.GetEffectiveSpeed();
            attributes.AttackRange = _cityAttackRange;
            attributes.AttackInterval = config.CityAttackInterval > 0 ? config.CityAttackInterval : 1.5f;

            HitRadius = config.HitRadius;

            // 初始化GameObject
            if (config.Prefab != null && GameObject == null)
            {
                GameObject = GameObject.Instantiate(config.Prefab, spawnPosition, Quaternion.identity);
                Transform = GameObject.transform;
                Animator = GameObject.GetComponentInChildren<Animator>();
            }

            if (Transform != null)
                Transform.position = spawnPosition;
            Position = spawnPosition;

            // ----- 设置NavMeshAgent（运行时确保组件存在） -----
            var navAgent = GameObject?.GetComponent<NavMeshAgent>();
            if (navAgent == null && GameObject != null)
            {
                navAgent = GameObject.AddComponent<NavMeshAgent>();
                navAgent.radius = 0.3f;
                navAgent.height = 2f;
                navAgent.baseOffset = 0f;
                navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                BattleLog.Move($"Added NavMeshAgent to enemy prefab '{config.name}'");
            }

            // ----- 设置移动组件（NavMesh驱动） -----
            var movement = Get<CombatMovementComponent>();
            if (movement == null)
                movement = AddComponent<CombatMovementComponent>();
            if (navAgent != null)
            {
                movement.SetNavAgent(navAgent);
                BattleLog.Move($"SetNavAgent on enemy, speed={config.GetEffectiveSpeed()}, dest=({targetPosition.x:F1},{targetPosition.z:F1})");
            }
            else
            {
                BattleLog.MoveError($"NavMeshAgent is NULL for enemy '{config.name}'! Cannot move via NavMesh.");
            }

            // ----- 确保攻击组件存在 -----
            if (Get<CombatAttackComponent>() == null)
                AddComponent<CombatAttackComponent>();

            // ----- 确保能力组件存在 -----
            if (Get<CombatAbilityComponent>() == null)
                AddComponent<CombatAbilityComponent>();

            // ----- 移动到主城目标位置 -----
            movement.MoveTo(targetPosition);
            BattleLog.Pathfinding($"Enemy {Id} start NavMesh move to city at ({targetPosition.x:F1},{targetPosition.z:F1})");

            base.Initialize();
            base.Start();
        }

        /// <summary>
        /// 每帧检查是否到达主城附近，触发攻击
        /// </summary>
        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            if (_cityReached || !IsAlive)
                return;

            float distSqr = (Position - _cityTargetPosition).sqrMagnitude;
            float rangeSqr = _cityAttackRange * _cityAttackRange;

            if (distSqr <= rangeSqr)
            {
                _cityReached = true;
                var movement = Get<CombatMovementComponent>();
                movement?.StopMove();
                BattleLog.Move($"Enemy {Id} reached city range ({Mathf.Sqrt(distSqr):F1} <= {_cityAttackRange}). Starting city attack.");

                // 挂载 CityAttackerComponent 开始攻击主城
                var attacker = Get<CityAttackerComponent>();
                if (attacker == null)
                    attacker = AddComponent<CityAttackerComponent>();

                if (_cityActor != null && _cityActor.IsAlive)
                {
                    float atkInterval = _config?.CityAttackInterval > 0 ? _config.CityAttackInterval : 1.5f;
                    int atkDamage = (int)Mathf.Max(1f, _config?.Atk ?? 10f);
                    attacker.StartAttack(_cityActor, atkInterval, atkDamage);
                    BattleLog.Attack($"Enemy {Id} started attacking city. Damage={atkDamage}, Interval={atkInterval}s");
                }
                else
                {
                    BattleLog.AttackWarning($"Enemy {Id} reached city but city actor is null or dead!");
                }
            }
        }

        /// <summary>
        /// 距离主城的"进度"（0=刚生成，1=已到主城）。用于防御塔优先攻击最靠近主城的敌人。
        /// </summary>
        public float CityProgress
        {
            get
            {
                if (_cityReached) return 1f;
                // 基于与主城距离计算进度：距离越近，进度越大
                float maxDist = 30f; // 大约最大生成距离
                float dist = Vector3.Distance(Position, _cityTargetPosition);
                return Mathf.Clamp01(1f - dist / maxDist);
            }
        }

        /// <summary>
        /// 获取这个敌人到达终点时对主城造成的伤害
        /// </summary>
        public int GetLeakDamage() => _config?.LeakDamage ?? 1;

        public float Progress => CityProgress;

        /// <summary>
        /// 获取击杀奖励金币
        /// </summary>
        public int GetKillGold() => _config?.GetEffectiveKillGold() ?? 10;

        public override void Die()
        {
            if (!IsAlive) return;
            
            // 停止移动（NavMeshAgent）
            Get<CombatMovementComponent>()?.StopMove();

            // 停止攻击主城（如果正在攻击）
            var attacker = Get<CityAttackerComponent>();
            if (attacker != null && attacker.IsAttacking)
            {
                attacker.StopAttack();
            }

            base.Die();

            // 发射击杀事件
            Engine?.Context?.EventBus?.Emit(TDEventIds.EnemyKilled,
                new EnemyKilledEvent(Id, 0));

            // 销毁GameObject（或回收到对象池的GameObject管理器）
            BeginDeathFadeOut(0.3f);
        }

        public override void DeactivateForPool()
        {
            _config = null;
            _cityTargetPosition = Vector3.zero;
            _cityReached = false;
            _cityActor = null;
            base.DeactivateForPool();
        }
    }
}
