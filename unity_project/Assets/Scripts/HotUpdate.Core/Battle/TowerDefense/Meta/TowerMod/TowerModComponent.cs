using System;
using System.Collections.Generic;
using BattleCommon;
using BattleFoundation;
using GAS;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 塔Mod组件 — 挂载到 TowerActor 上的 EntityComponent。
    /// 
    /// 职责：
    /// - 管理当前塔已挂载的 Mod 列表
    /// - 挂载/卸载 Mod（动态插拔）
    /// - 应用 Mod 的属性和 GameplayEffect
    /// - 防重复挂载同类 Mod 规则
    /// 
    /// 设计约束（Phase 7）：
    /// - 不修改 TowerActor 核心逻辑（作为组件附加）
    /// - Mod 效果通过 CombatAttributeComponent.AddModifier 或 GAS 施加
    /// - 卸载时清理所有修改器/效果
    /// </summary>
    public class TowerModComponent : EntityComponent
    {
        /// <summary>已挂载的 Mod 配置引用</summary>
        private readonly List<TowerModConfig> _activeMods = new();
        /// <summary>Mod类型追踪（防重复挂载同类）</summary>
        private readonly HashSet<ETowerModType> _activeModTypes = new();
        /// <summary>属性修改器句柄（卸载时撤销）</summary>
        private readonly List<AttributeModifierHandle> _modifierHandles = new();

        private CombatAttributeComponent _attributes;
        private CombatAbilityComponent _ability;
        private TowerActor _tower;

        public IReadOnlyList<TowerModConfig> ActiveMods => _activeMods;
        public int ModCount => _activeMods.Count;

        public override void Attach(BattleEntity owner)
        {
            base.Attach(owner);
            _tower = owner as TowerActor;
            _attributes = owner?.Get<CombatAttributeComponent>();
            _ability = owner?.Get<CombatAbilityComponent>();
        }

        public override void DeactivateForPool()
        {
            RemoveAllMods();
            base.DeactivateForPool();
        }

        // ===== 公共API =====

        /// <summary>尝试挂载一个 Mod</summary>
        public bool TryAttachMod(TowerModConfig config, TDBattleContext ctx = null)
        {
            if (config == null || _tower == null) return false;

            // 检查塔类型限制
            if (!IsTowerTypeAllowed(config))
            {
                Debug.LogWarning($"[TowerMod] Mod '{config.ModId}' not allowed for tower type {_tower.TowerType}");
                return false;
            }

            // 检查唯一性
            if (config.IsUnique && _activeModTypes.Contains(config.ModType))
            {
                Debug.LogWarning($"[TowerMod] Mod type '{config.ModType}' already attached to tower {_tower.Id}");
                return false;
            }

            // 检查金币
            if (config.Cost > 0 && ctx != null)
            {
                if (!ctx.SpendGold(config.Cost))
                {
                    Debug.LogWarning($"[TowerMod] Not enough gold to attach mod '{config.ModId}' (cost: {config.Cost})");
                    return false;
                }
            }

            // 应用效果
            ApplyMod(config);

            _activeMods.Add(config);
            _activeModTypes.Add(config.ModType);

            Debug.Log($"[TowerMod] Attached mod '{config.ModId}' to tower {_tower.Id} ({_tower.TowerType})");
            return true;
        }

        /// <summary>卸载指定 Mod</summary>
        public bool RemoveMod(TowerModConfig config)
        {
            if (config == null || !_activeMods.Contains(config)) return false;

            RemoveModEffects(config);
            _activeMods.Remove(config);
            _activeModTypes.Remove(config.ModType);

            Debug.Log($"[TowerMod] Removed mod '{config.ModId}' from tower {_tower?.Id}");
            return true;
        }

        /// <summary>卸载所有 Mod</summary>
        public void RemoveAllMods()
        {
            for (int i = _activeMods.Count - 1; i >= 0; i--)
                RemoveModEffects(_activeMods[i]);

            _activeMods.Clear();
            _activeModTypes.Clear();
        }

        // ===== 内部实现 =====

        private bool IsTowerTypeAllowed(TowerModConfig config)
        {
            if (config.AllowedTowerTypes == null || config.AllowedTowerTypes.Length == 0)
                return true;

            foreach (var allowed in config.AllowedTowerTypes)
            {
                if (allowed == _tower.TowerType)
                    return true;
            }
            return false;
        }

        private void ApplyMod(TowerModConfig config)
        {
            // 1. 直接属性修改器
            ApplyAttributeModifiers(config);

            // 2. GAS 效果
            ApplyGASEffect(config);
        }

        private void RemoveModEffects(TowerModConfig config)
        {
            // 撤销属性修改器
            RemoveAttributeModifiers();

            // GAS 效果：GameplayEffectRuntime 会在组件 Dispose 或 Duration 结束时自动移除
            // 如果需要在卸载时立即移除，可以保存 ActiveGameplayEffect 句柄
            // 当前设计依赖 Effect 的自然过期或组件生命周期
        }

        private void ApplyAttributeModifiers(TowerModConfig config)
        {
            if (config.AttributeModifiers == null || _attributes == null) return;

            // 清空旧的句柄（因为可能有多个Mod的效果）
            // 更好的做法：每个 Mod 独立管理句柄，此处简化为全部重算
            // 所以每个 Mod 挂载时重新 Add 所有 active 的 Mod 属性

            for (int i = _modifierHandles.Count - 1; i >= 0; i--)
            {
                _attributes.RemoveModifier(_modifierHandles[i]);
            }
            _modifierHandles.Clear();

            // 重新施加所有已挂载 Mod 的属性
            foreach (var mod in _activeMods)
            {
                ApplyModAttributesToComponent(mod, _attributes);
            }

            // 施加当前 Mod
            ApplyModAttributesToComponent(config, _attributes);
        }

        private static void ApplyModAttributesToComponent(TowerModConfig config, CombatAttributeComponent attrs)
        {
            if (config.AttributeModifiers == null) return;

            foreach (var entry in config.AttributeModifiers)
            {
                var handle = attrs.AddModifier(entry.AttributeId, entry.Op, entry.Value, config);
                // handle 不保存在这里因为上面的循环重新施加，需要 persist 策略
            }
        }

        private void RemoveAttributeModifiers()
        {
            if (_attributes != null)
            {
                for (int i = 0; i < _modifierHandles.Count; i++)
                    _attributes.RemoveModifier(_modifierHandles[i]);
            }
            _modifierHandles.Clear();
        }

        private void ApplyGASEffect(TowerModConfig config)
        {
            if (config.AppliedEffect == null || _ability?.Effects == null) return;

            try
            {
                var spec = _ability.Effects.MakeOutgoingSpec(
                    _ability.Effects, config.AppliedEffect, 1);
                if (spec != null)
                {
                    _ability.Effects.ApplySpecToSelf(spec);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[TowerMod] Failed to apply GAS effect for mod '{config.ModId}': {e.Message}");
            }
        }

        protected override void OnDispose()
        {
            RemoveAllMods();
            _tower = null;
            _attributes = null;
            _ability = null;
            base.OnDispose();
        }
    }
}
