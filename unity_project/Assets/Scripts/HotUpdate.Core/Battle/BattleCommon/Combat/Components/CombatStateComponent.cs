using BattleFoundation;
using GAS;

namespace BattleCommon
{
    public class CombatStateComponent : CombatComponentBase
    {
        private CombatHealthComponent _health;
        private CombatAbilityComponent _ability;
        private GameplayTag _bornInvincibleTag = GameplayTag.None;
        private GameplayTag _deadTag = GameplayTag.None;

        public bool IsDead => HasTag(_deadTag) || (_health?.IsDead ?? false);
        public bool IsAlive => !IsDead && (Owner?.IsAlive ?? false);
        public bool IsBornInvincible => IsAlive && HasTag(_bornInvincibleTag);
        public bool IsBornComplete => !HasTag(_bornInvincibleTag);
        public bool CanAct => IsAlive && IsBornComplete;
        public bool CanBeTargeted => IsAlive && !IsBornInvincible;
        public bool CanBeAttacked => CanBeTargeted;
        public bool CanTakeDamage => IsAlive && !IsBornInvincible;

        public void SetBornInvincibleTag(GameplayTag tag)
        {
            _bornInvincibleTag = tag;
        }

        public void SetDeadTag(GameplayTag tag)
        {
            _deadTag = tag;
        }

        public void BeginBorn()
        {
            AddTag(_bornInvincibleTag);
        }

        public void CompleteBorn()
        {
            RemoveTag(_bornInvincibleTag);
        }

        public void MarkDead()
        {
            CompleteBorn();
            AddTag(_deadTag);
        }

        public void ClearDead()
        {
            RemoveTag(_deadTag);
        }

        public void SyncDeathState()
        {
            if (_health != null && _health.IsDead)
                MarkDead();
            else
                ClearDead();
        }

        public override void Attach(BattleEntity owner)
        {
            base.Attach(owner);
            _health = Owner?.Get<CombatHealthComponent>();
            _ability = Owner?.Get<CombatAbilityComponent>();
        }

        public override void Initialize()
        {
            base.Initialize();
            _health = Owner?.Get<CombatHealthComponent>();
            _ability = Owner?.Get<CombatAbilityComponent>();
            CompleteBorn();
            SyncDeathState();
        }

        public override void DeactivateForPool()
        {
            CompleteBorn();
            ClearDead();
            base.DeactivateForPool();
        }

        protected override void OnDispose()
        {
            CompleteBorn();
            ClearDead();
            _health = null;
            _ability = null;
            base.OnDispose();
        }

        private bool HasTag(GameplayTag tag)
        {
            return tag.IsValid && (_ability?.HasTag(tag) ?? false);
        }

        private void AddTag(GameplayTag tag)
        {
            if (tag.IsValid)
                _ability?.AddTag(tag);
        }

        private void RemoveTag(GameplayTag tag)
        {
            if (tag.IsValid)
                _ability?.RemoveTag(tag);
        }
    }
}
