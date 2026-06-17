using System;
using BattleFoundation;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// Meta（局外）→ Run（局内）桥梁。
    /// 
    /// 职责：
    /// - 读取 MetaTalentManager 中的所有天赋效果
    /// - 将天赋效果转换为局内数值（初始金币/主城血量/塔属性等）
    /// - 在 BattleEngine.OnBeforeBattleStart 时注入到 BattleContext
    /// 
    /// Meta vs Run 严格分离：
    /// - 局外：MetaTalentManager + MetaSaveData（不依赖 BattleEngine）
    /// - 桥梁：MetaToRunBridge（轻量转换层）
    /// - 局内：TDBattleContext + Tower/Enemy/Balance（不直接访问 Meta）
    /// 
    /// 数据流：TalentTree → MetaTalentManager → MetaToRunBridge → TDBattleContext → Systems
    /// </summary>
    public static class MetaToRunBridge
    {
        /// <summary>
        /// 将局外天赋效果注入到局内 BattleContext。
        /// 在 TDBattleEngine.OnBeforeBattleStart 中调用。
        /// </summary>
        public static void ApplyToBattleContext(TDBattleContext ctx, TowerDefenseGlobalConfig tdConfig)
        {
            if (ctx == null)
            {
                Debug.LogError("[MetaToRunBridge] BattleContext is null!");
                return;
            }

            try
            {
                var mgr = MetaTalentManager.Instance;
                if (mgr.Config == null)
                {
                    Debug.LogWarning("[MetaToRunBridge] MetaTalentManager not initialized, skipping meta injection.");
                    return;
                }

                var effects = mgr.GetAllEffects();
                if (effects.Count == 0)
                {
                    Debug.Log("[MetaToRunBridge] No talent effects to apply.");
                    return;
                }

                // 创建 Meta 注入数据容器（挂在 Context 上）
                var injection = new MetaInjectionData(ctx);
                ctx.SetService<IMetaInjection>(injection);

                // 应用各项效果
                foreach (var kvp in effects)
                {
                    injection.Apply(kvp.Key, kvp.Value, tdConfig);
                }

                Debug.Log($"[MetaToRunBridge] Applied {effects.Count} talent effects to run. " +
                         $"Gold={ctx.PlayerGold}, CityHP modifier={injection.MainCityHPBonusPercent:F0}%");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MetaToRunBridge] Failed to apply meta effects: {e.Message}");
            }
        }

        /// <summary>
        /// 获取指定天赋类型的百分比加成（供局内系统查询）
        /// </summary>
        public static float GetTalentBonus(IBattleContext ctx, ETalentType type)
        {
            if (ctx is TDBattleContext tdCtx)
            {
                var injection = tdCtx.GetService<IMetaInjection>();
                return injection?.GetBonus(type) ?? 0f;
            }
            return 0f;
        }
    }

    /// <summary>
    /// Meta→Run 注入数据接口（局内系统查询用）
    /// </summary>
    public interface IMetaInjection
    {
        float GetBonus(ETalentType type);
    }

    /// <summary>
    /// Meta→Run 注入数据容器。
    /// 挂载到 TDBattleContext 上，作为 Service 存在。
    /// </summary>
    public class MetaInjectionData : IMetaInjection
    {
        private readonly float[] _bonuses;
        private readonly int _numTypes;

        public float StartingGoldMultiplier { get; private set; } = 1f;
        public float MainCityHPBonusPercent { get; private set; }
        public float TowerAttackBonusPercent { get; private set; }
        public float ArrowTowerSpeedBonusPercent { get; private set; }
        public float CannonTowerRangeBonusPercent { get; private set; }
        public float IceTowerSlowBonusPercent { get; private set; }
        public float KillGoldBonusPercent { get; private set; }
        public float BuildCostReductionPercent { get; private set; }

        public MetaInjectionData(TDBattleContext ctx)
        {
            _numTypes = Enum.GetValues(typeof(ETalentType)).Length;
            _bonuses = new float[_numTypes];
        }

        /// <summary>应用单条天赋效果到对应字段</summary>
        public void Apply(ETalentType type, float value, TowerDefenseGlobalConfig tdConfig)
        {
            _bonuses[(int)type] = value;

            switch (type)
            {
                case ETalentType.StartingGoldBonus:
                    StartingGoldMultiplier = 1f + value / 100f;
                    break;

                case ETalentType.MainCityHPBonus:
                    MainCityHPBonusPercent = value;
                    break;

                case ETalentType.TowerAttackBonus:
                    TowerAttackBonusPercent = value;
                    break;

                case ETalentType.ArrowTowerAttackSpeed:
                    ArrowTowerSpeedBonusPercent = value;
                    break;

                case ETalentType.CannonTowerRange:
                    CannonTowerRangeBonusPercent = value;
                    break;

                case ETalentType.IceTowerSlowBonus:
                    IceTowerSlowBonusPercent = value;
                    break;

                case ETalentType.KillGoldBonus:
                    KillGoldBonusPercent = value;
                    break;

                case ETalentType.BuildCostReduction:
                    BuildCostReductionPercent = value;
                    break;
            }
        }

        public float GetBonus(ETalentType type)
        {
            int idx = (int)type;
            return idx >= 0 && idx < _numTypes ? _bonuses[idx] : 0f;
        }
    }

    /// <summary>
    /// TDBattleContext 扩展：Service 机制（轻量，非侵入 BattleContext 核心）
    /// </summary>
    public static class TDBattleContextServiceExtensions
    {
        private static readonly System.Collections.Generic.Dictionary<Type, object> _services =
            new System.Collections.Generic.Dictionary<Type, object>();

        public static void SetService<T>(this TDBattleContext ctx, T service) where T : class
        {
            _services[typeof(T)] = service;
        }

        public static T GetService<T>(this TDBattleContext ctx) where T : class
        {
            _services.TryGetValue(typeof(T), out var service);
            return service as T;
        }

        public static void ClearServices(this TDBattleContext ctx)
        {
            _services.Clear();
        }
    }
}
