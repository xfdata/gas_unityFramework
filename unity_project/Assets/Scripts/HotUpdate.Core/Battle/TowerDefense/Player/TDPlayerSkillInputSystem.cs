using BattleCommon;
using BattleFoundation;
using Framework;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 玩家技能输入系统 — 将玩家输入映射到GAS技能激活。
    /// 
    /// 输入接口：
    /// - TryActivateSkillSlot(int slotIndex)：技能栏位
    /// - TryAttackTarget(CombatActor target)：手动指定攻击目标
    /// - SetAutoAttackTarget(CombatActor target)：设置自动攻击目标
    /// 
    /// 复用：
    /// - CombatAbilityComponent.TryActivateById / TryActivateAttackAbility
    /// - CombatAttackComponent.TryAttack
    /// </summary>
    public class TDPlayerSkillInputSystem : IBattleSystem
    {
        private IBattleContext _context;
        private EntityManager _entityManager;

        /// <summary>
        /// 玩家Actor引用
        /// </summary>
        public TDPlayerActor Player { get; set; }

        /// <summary>
        /// 当前选中的攻击目标（自动攻击用）
        /// </summary>
        public CombatActor AutoAttackTarget { get; set; }

        // 技能栏位到AbilityId的映射
        private readonly int[] _slotAbilityIds = new int[4];

        public void Initialize(IBattleContext context)
        {
            _context = context;
            _entityManager = context.EntityManager;
        }

        public void Start() { }

        /// <summary>
        /// 绑定技能到栏位
        /// </summary>
        public void BindSkillToSlot(int slotIndex, int abilityId)
        {
            if (slotIndex >= 0 && slotIndex < _slotAbilityIds.Length)
                _slotAbilityIds[slotIndex] = abilityId;
        }

        /// <summary>
        /// 尝试激活技能栏位对应的技能
        /// </summary>
        public bool TryActivateSkillSlot(int slotIndex)
        {
            if (Player == null || !Player.IsAlive) return false;
            if (slotIndex < 0 || slotIndex >= _slotAbilityIds.Length) return false;

            int abilityId = _slotAbilityIds[slotIndex];
            if (abilityId <= 0) return false;

            bool success = Player.TryActivateSkill(abilityId);
            if (success)
            {
                Debug.Log($"[TDPlayerSkillInput] Player activated skill slot {slotIndex} (abilityId={abilityId})");
            }
            return success;
        }

        /// <summary>
        /// 尝试对目标发起攻击
        /// </summary>
        public bool TryAttackTarget(CombatActor target)
        {
            if (Player == null || !Player.IsAlive || target == null) return false;
            if (!target.IsAlive) return false;

            // 检查是否在攻击范围内（通过CombatAttackComponent的门控）
            // 玩家需要先靠近目标

            // 简单攻击：移动靠近 + 攻击
            float distSqr = (Player.Position - target.Position).sqrMagnitude;
            float atkRange = Player.Get<CombatAttributeComponent>()?.AttackRange ?? 2f;

            if (distSqr > atkRange * atkRange)
            {
                // 移动到目标附近
                Player.MoveTo(target.Position);
                return false;
            }

            // 在范围内：发起攻击
            return Player.TryAttack(target);
        }

        public void Update(float deltaTime)
        {
            if (Player == null || !Player.IsAlive || deltaTime <= 0f) return;

            // 自动攻击逻辑：如果有目标，持续尝试攻击
            if (AutoAttackTarget != null && AutoAttackTarget.IsAlive && Player.AutoAttackEnabled)
            {
                TryAttackTarget(AutoAttackTarget);
            }
        }

        public void LateUpdate(float deltaTime) { }

        public void Dispose()
        {
            Player = null;
            AutoAttackTarget = null;
            _context = null;
            _entityManager = null;
        }
    }
}
