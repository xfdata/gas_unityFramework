using BattleCommon;
using BattleFoundation;
using GAS;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// TD玩家Actor — 继承CombatActor，全功能战斗角色。
    /// 
    /// 复用CombatActor的全部能力：
    /// - CombatMovementComponent：NavMesh移动
    /// - CombatAttributeComponent：属性系统
    /// - CombatHealthComponent：血量/死亡
    /// - CombatAbilityComponent：GAS技能激活
    /// - CombatAttackComponent：攻击间隔/范围门控
    /// </summary>
    public class TDPlayerActor : CombatActor
    {
        [SerializeField]
        private float _interactionRange = 2f;

        /// <summary>
        /// 玩家手动攻击的目标（由输入系统设置）
        /// </summary>
        public CombatActor ManualAttackTarget { get; set; }

        /// <summary>
        /// 自动攻击启用标记
        /// </summary>
        public bool AutoAttackEnabled { get; set; } = true;

        public float InteractionRange => _interactionRange;

        /// <summary>
        /// 初始化玩家
        /// </summary>
        public void InitPlayer(float maxHp, float atk, float def, float moveSpeed,
            Vector3 spawnPosition, GameObject prefab = null)
        {
            SetCamp(EEntityCamp.Ally);
            SetEntityType(EEntityType.Hero);

            // 初始化属性
            var attributes = Get<CombatAttributeComponent>();
            if (attributes == null)
                attributes = AddComponent<CombatAttributeComponent>();
            attributes.MaxHP = maxHp;
            attributes.HP = maxHp;
            attributes.Attack = atk;
            attributes.Defense = def;
            attributes.MoveSpeed = moveSpeed;
            attributes.AttackRange = 2f;
            attributes.AttackInterval = 1.2f;

            // 确保必要组件存在
            if (Get<CombatHealthComponent>() == null)
                AddComponent<CombatHealthComponent>();
            if (Get<CombatAbilityComponent>() == null)
                AddComponent<CombatAbilityComponent>();
            if (Get<CombatAttackComponent>() == null)
                AddComponent<CombatAttackComponent>();
            if (Get<CombatMovementComponent>() == null)
                AddComponent<CombatMovementComponent>();

            // 初始化GameObject
            if (prefab != null && GameObject == null)
            {
                GameObject = Object.Instantiate(prefab, spawnPosition, Quaternion.identity);
                Transform = GameObject.transform;
                Animator = GameObject.GetComponentInChildren<Animator>();
            }

            Position = spawnPosition;
            if (Transform != null)
                Transform.position = spawnPosition;

            base.Initialize();
            base.Start();
        }

        /// <summary>
        /// 授予技能（GAS标准路径）
        /// </summary>
        public void GrantSkill(GameplayAbilityDefinition ability)
        {
            Get<CombatAbilityComponent>()?.GrantAbility(ability);
        }

        /// <summary>
        /// 尝试激活指定ID的技能
        /// </summary>
        public bool TryActivateSkill(int abilityId)
        {
            return Get<CombatAbilityComponent>()?.TryActivateById(abilityId) ?? false;
        }

        /// <summary>
        /// 尝试攻击目标（复用CombatAttackComponent的门控逻辑）
        /// </summary>
        public bool TryAttack(CombatActor target)
        {
            return Get<CombatAttackComponent>()?.TryAttack(target) ?? false;
        }

        public override void Die()
        {
            if (!IsAlive) return;
            base.Die();

            // 玩家死亡事件
            Engine?.Context?.EventBus?.Emit(6001, Id); // Placeholder for PlayerDeath event
        }

        public override void DeactivateForPool()
        {
            ManualAttackTarget = null;
            AutoAttackEnabled = true;
            base.DeactivateForPool();
        }
    }
}
