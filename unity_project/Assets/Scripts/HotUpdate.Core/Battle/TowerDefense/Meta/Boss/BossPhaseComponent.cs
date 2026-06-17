using System;
using BattleCommon;
using BattleFoundation;
using GAS;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// Boss阶段组件 — 挂载到 TDEnemyActor(TDEbossActor) 上的 EntityComponent。
    /// 
    /// 职责：
    /// - 管理 BossConfig.Phases 的状态机
    /// - 按触发条件切换阶段（HP阈值 / 时间 / 死亡）
    /// - 阶段切换时施加 PhaseEnterEffect
    /// - 激活/停用阶段技能
    /// - 属性调整（DamageResist / Speed / ImmuneSlow）
    /// 
    /// 设计约束（Phase 7）：
    /// - 复用 GAS 技能系统（不自己写技能框架）
    /// - 通过 CombatAttributeComponent.AddModifier 实现属性调整
    /// - 组件不依赖 MonoBehaviour.Update
    /// </summary>
    public class BossPhaseComponent : EntityComponent
    {
        private BossConfig _bossConfig;
        private CombatAttributeComponent _attributes;
        private CombatAbilityComponent _ability;
        private CombatHealthComponent _health;
        private TDEnemyActor _boss;

        private int _currentPhaseIndex = -1;
        private float _phaseTimer;              // 用于 TimeElapsed 触发
        private AttributeModifierHandle _speedModifier;
        private AttributeModifierHandle _damageResistModifier;

        public int CurrentPhaseIndex => _currentPhaseIndex;
        public BossConfig.BossPhase CurrentPhase =>
            (_bossConfig != null && _currentPhaseIndex >= 0 && _currentPhaseIndex < _bossConfig.Phases.Length)
                ? _bossConfig.Phases[_currentPhaseIndex] : null;

        public void Init(BossConfig config)
        {
            _bossConfig = config ?? throw new ArgumentNullException(nameof(config));
            _boss = Owner as TDEnemyActor;
            _attributes = Owner?.Get<CombatAttributeComponent>();
            _ability = Owner?.Get<CombatAbilityComponent>();
            _health = Owner?.Get<CombatHealthComponent>();

            _currentPhaseIndex = -1;
            _phaseTimer = 0f;

            // 进入初始阶段（Phase 0）
            if (_bossConfig.Phases.Length > 0)
            {
                _currentPhaseIndex = 0;
                EnterPhase(0);
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            _phaseTimer = 0f;
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
            if (_bossConfig == null || _boss == null || !_boss.IsAlive) return;

            _phaseTimer += deltaTime;
            CheckPhaseTransitions();
        }

        // ===== 阶段检测 =====

        private void CheckPhaseTransitions()
        {
            if (_bossConfig.Phases.Length == 0) return;

            bool advanced = false;

            // 首先检查死亡触发
            if (!_boss.IsAlive)
            {
                TryEnterPhaseByTrigger(EBossPhaseTrigger.OnDeath);
            }

            // 从当前阶段之后的阶段中查找触发条件满足的
            for (int i = _currentPhaseIndex + 1; i < _bossConfig.Phases.Length; i++)
            {
                var phase = _bossConfig.Phases[i];
                if (IsPhaseTriggered(phase))
                {
                    EnterPhase(i);
                    advanced = true;
                    break;
                }
            }

            if (!advanced)
            {
                // 确保当前阶段技能在运行
                EnsurePhaseSkillsActive();
            }
        }

        private bool IsPhaseTriggered(BossConfig.BossPhase phase)
        {
            switch (phase.Trigger)
            {
                case EBossPhaseTrigger.HPThreshold:
                {
                    float hpPercent = _attributes != null && _attributes.MaxHP > 0
                        ? _attributes.HP / _attributes.MaxHP : 0f;
                    return hpPercent <= phase.TriggerValue && _attributes.HP > 0f;
                }

                case EBossPhaseTrigger.TimeElapsed:
                    return _phaseTimer >= phase.TriggerValue;

                case EBossPhaseTrigger.OnDeath:
                    return _boss != null && !_boss.IsAlive;

                default:
                    return false;
            }
        }

        // ===== 阶段进入 =====

        private void EnterPhase(int phaseIndex)
        {
            if (phaseIndex < 0 || phaseIndex >= _bossConfig.Phases.Length)
                return;

            var oldPhase = CurrentPhase;
            var newPhase = _bossConfig.Phases[phaseIndex];

            // 停用旧阶段技能
            DeactivatePhaseAbilities(oldPhase);

            // 清理旧阶段属性修饰
            ClearPhaseModifiers();

            _currentPhaseIndex = phaseIndex;
            Debug.Log($"[BossPhase] '{_bossConfig.DisplayName}' entering phase {phaseIndex}: '{newPhase.PhaseName}'");

            // 施加阶段进入效果
            if (newPhase.PhaseEnterEffect != null && _ability?.Effects != null)
            {
                var spec = _ability.Effects.MakeOutgoingSpec(_ability.Effects, newPhase.PhaseEnterEffect, 1);
                if (spec != null) _ability.Effects.ApplySpecToSelf(spec);
            }

            // 激活新阶段技能
            ActivatePhaseAbilities(newPhase);

            // 应用阶段属性
            ApplyPhaseAttributes(newPhase);

            _phaseTimer = 0f; // 重置阶段计时器
        }

        private void ApplyPhaseAttributes(BossConfig.BossPhase phase)
        {
            if (_attributes == null) return;

            // 移速调整
            if (Math.Abs(phase.SpeedMultiplier - 1f) > 0.001f)
            {
                _speedModifier = _attributes.AddModifier(
                    CombatAttributeIds.MoveSpeed,
                    AttributeModifierOp.Multiply,
                    phase.SpeedMultiplier,
                    this);
            }

            // 伤害抵抗（通过增加 Defense 实现）
            if (phase.DamageResist > 0f)
            {
                _damageResistModifier = _attributes.AddModifier(
                    CombatAttributeIds.Defense,
                    AttributeModifierOp.Multiply,
                    1f + phase.DamageResist,
                    this);
            }
        }

        private void ClearPhaseModifiers()
        {
            if (_attributes != null)
            {
                if (_speedModifier != null)
                {
                    _attributes.RemoveModifier(_speedModifier);
                    _speedModifier = default;
                }
                if (_damageResistModifier != null)
                {
                    _attributes.RemoveModifier(_damageResistModifier);
                    _damageResistModifier = default;
                }
            }
        }

        private void ActivatePhaseAbilities(BossConfig.BossPhase phase)
        {
            if (_ability == null || phase == null || phase.PhaseAbilities == null) return;

            foreach (var ability in phase.PhaseAbilities)
            {
                if (ability != null)
                    _ability.GrantAbility(ability);
            }
        }

        private void DeactivatePhaseAbilities(BossConfig.BossPhase phase)
        {
            // GAS 中能力移除需要遍历或通过 AbilitySpec
            // 当前简化策略：新阶段激活新能力，旧能力在移除后自然过期
            // 完整实现需要 GameplayAbilitySystem.RevokeAbility()
        }

        private void EnsurePhaseSkillsActive()
        {
            var phase = CurrentPhase;
            if (phase != null)
                ActivatePhaseAbilities(phase);
        }

        // ===== 全局Boss技能 =====

        public void GrantGlobalAbilities()
        {
            if (_ability == null || _bossConfig?.GlobalAbilities == null) return;

            foreach (var ability in _bossConfig.GlobalAbilities)
            {
                if (ability != null)
                    _ability.GrantAbility(ability);
            }
        }

        // ===== 公开查询 =====

        public bool TryEnterPhaseByTrigger(EBossPhaseTrigger trigger)
        {
            for (int i = _currentPhaseIndex + 1; i < _bossConfig.Phases.Length; i++)
            {
                if (_bossConfig.Phases[i].Trigger == trigger)
                {
                    EnterPhase(i);
                    return true;
                }
            }
            return false;
        }

        protected override void OnDispose()
        {
            ClearPhaseModifiers();
            _bossConfig = null;
            _attributes = null;
            _ability = null;
            _health = null;
            _boss = null;
            base.OnDispose();
        }
    }
}
