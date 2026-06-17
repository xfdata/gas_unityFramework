using System;
using GAS;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// Boss阶段触发条件
    /// </summary>
    public enum EBossPhaseTrigger
    {
        /// <summary>HP低于指定百分比时触发</summary>
        HPThreshold,
        /// <summary>经过指定时间后触发</summary>
        TimeElapsed,
        /// <summary>被击杀后触发（复活阶段）</summary>
        OnDeath,
    }

    /// <summary>
    /// Boss配置 ScriptableObject — 定义Boss的多阶段、专属技能和特殊机制。
    /// 
    /// Boss 复用 TDEnemyActor 作为基础敌人，通过 BossPhaseComponent 扩展。
    /// 技能通过 GAS (GameplayAbilityDefinition) 驱动。
    /// 
    /// 设计约束（Phase 7）：
    /// - Boss 必须复用 BattleSkill 系统
    /// - 不允许单独写 Boss 战斗框架
    /// - Phase 必须可配置
    /// </summary>
    [CreateAssetMenu(fileName = "BossConfig", menuName = "TowerDefense/Boss Config", order = 230)]
    public class BossConfig : ScriptableObject
    {
        [Header("Identity")]
        public string BossId;
        public string DisplayName;

        [Header("Base Config")]
        [Tooltip("Boss 的基础敌人配置（继承 TDEnemyConfig 的属性）")]
        public TDEnemyConfig BaseEnemyConfig;

        [Tooltip("Boss的HP倍率（乘到 BaseEnemyConfig 的有效HP上）")]
        public float HpMultiplier = 5f;

        [Header("Phases")]
        [Tooltip("Boss阶段列表（按顺序执行）")]
        public BossPhase[] Phases = Array.Empty<BossPhase>();

        [Header("Special Skills")]
        [Tooltip("Boss全局技能（所有阶段可用）")]
        public GameplayAbilityDefinition[] GlobalAbilities = Array.Empty<GameplayAbilityDefinition>();

        [Header("Reward")]
        [Tooltip("击杀金币奖励（覆盖BaseEnemyConfig的击杀金币）")]
        public int KillGold = 500;

        [Tooltip("击杀天赋点奖励")]
        public int TalentPointReward = 5;

        /// <summary>单个Boss阶段配置</summary>
        [Serializable]
        public class BossPhase
        {
            [Tooltip("阶段名称")]
            public string PhaseName;

            [Tooltip("触发条件")]
            public EBossPhaseTrigger Trigger;

            [Tooltip("触发值（HP% = 0.3 表示 HP < 30% 时触发）")]
            [Range(0f, 1f)]
            public float TriggerValue = 0.5f;

            [Tooltip("进入此阶段时施加的 GameplayEffect")]
            public GameplayEffectDefinition PhaseEnterEffect;

            [Tooltip("此阶段激活的技能")]
            public GameplayAbilityDefinition[] PhaseAbilities = Array.Empty<GameplayAbilityDefinition>();

            [Tooltip("此阶段移速倍率")]
            public float SpeedMultiplier = 1f;

            [Tooltip("此阶段伤害抵抗")]
            [Range(0f, 0.9f)]
            public float DamageResist = 0f;

            [Tooltip("此阶段是否免疫减速")]
            public bool ImmuneSlow;
        }
    }
}
