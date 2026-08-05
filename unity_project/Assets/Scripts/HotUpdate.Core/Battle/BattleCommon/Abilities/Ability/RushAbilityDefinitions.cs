using System;
using BattleCommon;
using GAS;
using UnityEngine;

/// <summary>
/// Rush 模式专用的 SetByCaller 键值常量。
/// 用于在 GameplayEffectSpec 中传递距离、难度等参数。
/// </summary>
public static class RushSetByCallerKeys
{
    public const int Distance = 100;
    public const int DifficultyMultiplier = 101;
    public const int AdvanceSpeed = 102;
}

namespace GAS
{
    /// <summary>
    /// 跑酷推进 Ability。
    /// 代表英雄处于"自动向前跑酷"状态。
    /// 激活时施加一个 Infinite GameplayEffect，该 Effect 授予 State.Moving 标签。
    /// RushMode 在每个英雄上检查此标签来判断是否应该推进。
    /// 
    /// 回放：此 Ability 的激活状态由 GAS 的 CaptureState/RestoreState 自动处理。
    /// </summary>
    [CreateAssetMenu(menuName = "GAS/Rush/Advance Ability")]
    public class RushAdvanceAbilityDefinition : GameplayAbilityDefinition
    {
        [Header("Rush Advance")]
        [Tooltip("激活时对自身施加的效果（如授予 State.Moving 标签）")]
        public GameplayEffectDefinition SelfAdvanceEffect;

        [Tooltip("推进速度（由 RushMode 读取，非 GAS 直接使用）")]
        public float AdvanceSpeed = 3f;

        public override void ActivateAbility(GameplayAbilitySpec spec)
        {
            if (SelfAdvanceEffect != null)
            {
                spec.ApplyGameplayEffect(SelfAdvanceEffect, GameplayAbilityTargetPolicy.Source);
            }
        }

        public override bool CanActivateAbility(GameplayAbilitySpec spec)
        {
            if (spec.Source?.OwnedTags?.HasTag(CombatGameplayTags.State_Moving) == true)
                return false;

            return base.CanActivateAbility(spec);
        }
    }

    [CreateAssetMenu(menuName = "GAS/Rush/Dash Ability")]
    public class RushDashAbilityDefinition : GameplayAbilityDefinition
    {
        [Header("Rush Dash")]
        [Tooltip("冲刺速度倍率")]
        public float SpeedMultiplier = 2f;

        [Tooltip("冲刺持续时间（秒）")]
        public float Duration = 2f;

        [Tooltip("冲刺时施加的效果")]
        public GameplayEffectDefinition DashEffect;

        public override void ActivateAbility(GameplayAbilitySpec spec)
        {
            ApplyConfiguredEffects(spec);

            if (DashEffect != null)
            {
                spec.ApplyGameplayEffect(DashEffect, GameplayAbilityTargetPolicy.Source);
            }

            if (!spec.HasActiveTasks)
            {
                spec.EndAbility(GameplayAbilityEndReason.Completed);
            }
        }
    }

    [CreateAssetMenu(menuName = "GAS/Rush/Difficulty Scaling Execution")]
    public class RushDifficultyScalingExecution : GameplayEffectExecution
    {
        public override void Execute(GameplayEffectSpec spec)
        {
            if (spec == null) return;

            var target = spec.Target;
            if (target == null) return;

            var attrSet = (target.AttributeOwner as IGameplayAttributeSetProvider)?.AttributeSet;
            if (attrSet == null) return;

            float distance = spec.GetSetByCaller(RushSetByCallerKeys.Distance, 0f);
            float difficultyMult = spec.GetSetByCaller(RushSetByCallerKeys.DifficultyMultiplier, 1f);

            if (difficultyMult <= 1f) return;

            float maxHP = attrSet.GetAttribute(CombatAttributeIds.MaxHP);
            float hp = attrSet.GetAttribute(CombatAttributeIds.HP);
            float attack = attrSet.GetAttribute(CombatAttributeIds.Attack);

            float newMaxHP = maxHP * difficultyMult;
            float hpRatio = maxHP > 0 ? hp / maxHP : 1f;

            attrSet.SetBaseValue(CombatAttributeIds.MaxHP, newMaxHP);
            attrSet.SetBaseValue(CombatAttributeIds.HP, newMaxHP * hpRatio);
            attrSet.SetBaseValue(CombatAttributeIds.Attack, attack * difficultyMult);
        }
    }
}