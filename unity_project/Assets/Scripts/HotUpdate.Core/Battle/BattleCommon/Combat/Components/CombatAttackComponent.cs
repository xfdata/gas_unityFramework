using System;
using BattleFoundation;
using UnityEngine;

namespace BattleCommon
{
    public class CombatAttackComponent : CombatComponentBase
    {
        private CombatAttributeComponent _attributes;
        private CombatHealthComponent _health;
        private float _attackTimer = float.MaxValue;

        public CombatActor CurrentTarget { get; private set; }

        /// <summary>
        /// Business SkillId for the normal attack. Zero preserves the legacy ability-type selection path.
        /// </summary>
        public int BasicAttackSkillId { get; set; }

        // Owner 继承自 EntityComponent，类型为 BattleEntity。需要 CombatActor 特有成员时通过 Actor 访问。
        protected CombatActor Actor => Owner as CombatActor;

        public override void Attach(BattleEntity owner)
        {
            base.Attach(owner);
            _attributes = Owner?.Get<CombatAttributeComponent>();
            _health = Owner?.Get<CombatHealthComponent>();
        }

        public override void Update(float deltaTime)
        {
            _attackTimer += deltaTime;
        }

        public bool TryAttack(CombatActor target)
        {
            if (_health == null || _health.IsDead || target == null || target == Owner || !target.IsAlive) return false;
            if (Actor?.Gameplay?.States.CanAttack() == false) return false;
            if (target.Gameplay?.States.CanBeTargeted() == false) return false;

            float range = _attributes?.AttackRange ?? 2f;
            float interval = _attributes?.AttackInterval ?? 1.5f;
            if ((Owner.Position - target.Position).sqrMagnitude > range * range || _attackTimer < interval) return false;

            CurrentTarget = target;
            bool activated;
            int skillId = ResolveBasicAttackSkillId();
            if (skillId > 0)
            {
                var result = Actor?.Gameplay?.Skills.TryCast(skillId, target);
                activated = result?.Success == true;
            }
            else
            {
                activated = Owner.Get<CombatAbilityComponent>()?.TryActivateAttackAbility(target) ?? false;
            }

            if (activated)
                _attackTimer = 0f;

            return activated;
        }

        private int ResolveBasicAttackSkillId()
        {
            if (BasicAttackSkillId > 0)
                return BasicAttackSkillId;

            var ability = Owner?.Get<CombatAbilityComponent>()?.FindGrantedAttackAbilityDefinition();
            return Actor?.GameplayCatalog?.TryGetSkillId(ability, out var skillId) == true
                ? skillId
                : 0;
        }

        public CombatActor FindTarget(Func<CombatActor, bool> filter, CombatTargetPriority priority = CombatTargetPriority.Nearest)
        {
            if (Actor?.Gameplay?.States.CanAttack() == false)
                return null;

            var query = Owner?.Engine?.Context?.GetSystem<CombatTargetQuerySystem>();
            float range = (_attributes?.AttackRange ?? 2f) * 2f;
            return query?.FindTarget(Actor, filter, priority, range);
        }

        public override void DeactivateForPool()
        {
            CurrentTarget = null;
            _attackTimer = float.MaxValue;
            base.DeactivateForPool();
        }
    }
}
