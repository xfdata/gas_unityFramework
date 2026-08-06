using GAS;
using BattleFoundation;

namespace BattleCommon
{
    /// <summary>
    /// R3-S9: 战斗表现层薄契约门面（L2 定义，L3 实现）。
    ///
    /// 设计原则（03_refactor_layering_proposal.md §三.4 决策 4）：
    /// - 薄契约门面：框架定义契约，应用层解释数据。
    /// - 不引入 Binder/Shell/Handle/投影管线（整套投影-快照改造不适合现状）。
    /// - 收敛 F4 痛点：表现事件三条并行通路（GameplayCue / ActorPresentationComponent 直接订阅 GAS /
    ///   CombatHealthComponent.OnDeath C# 事件）收敛为单一通路。
    /// - 接入成本为零：现有分发链路不动，仅新增只读出口。
    ///
    /// 与 IActorViewBinding 的边界：
    /// - IActorViewBinding 是 per-actor 指令契约（SyncTransform/PlayHit/DestroyView）。
    /// - IBattlePresentationSink 是 battle 级事件通知（OnActorSpawned/OnDamageDealt/OnCueTriggered）。
    /// - 两者互补：Sink 通知"发生了什么"，ViewBinding 指令"做什么"。
    ///
    /// 实现注意：
    /// - 所有方法为 void，实现方自行决定同步/异步/丢弃。
    /// - 事件载荷为 readonly struct，零 GC。
    /// - 实现方不应在方法内反查逻辑层（CombatActor/BattleContext），仅基于载荷决策表现。
    /// </summary>
    public interface IBattlePresentationSink
    {
        /// <summary>Actor 创建（对应 CombatActorEventIds.ActorSpawned）。</summary>
        void OnActorSpawned(in ActorSpawnedEvent evt);

        /// <summary>Actor 死亡/销毁（对应 CombatActorEventIds.ActorDied，携带 DeathReason/killerId）。</summary>
        void OnActorDied(in ActorDiedEvent evt);

        /// <summary>伤害结算完成（基于 DamageResult 完整溯源，R2 已建立）。</summary>
        void OnDamageDealt(in DamageDealtPresentation evt);

        /// <summary>属性变化（对应 GAS AttributeChanged 事件）。</summary>
        void OnAttributeChanged(in AttributeChangedPresentation evt);

        /// <summary>技能激活（对应 GAS AbilityActivated 事件）。</summary>
        void OnAbilityActivated(in AbilityPresentation evt);

        /// <summary>技能结束（对应 GAS AbilityEnded 事件）。</summary>
        void OnAbilityEnded(in AbilityPresentation evt);

        /// <summary>Cue 触发（对应 GAS CueTriggered，替代 IGameplayCueManager 并行通路）。</summary>
        void OnCueTriggered(in CuePresentation evt);
    }

    // ===== 事件载荷结构（readonly struct，零 GC）=====

    /// <summary>伤害结算表现载荷（基于 DamageResult 提取表现所需字段）。</summary>
    public readonly struct DamageDealtPresentation
    {
        public readonly int TargetEntityId;
        public readonly int SourceEntityId;
        public readonly float FinalDamage;
        public readonly float ShieldCost;
        public readonly float BlockedDamage;
        public readonly bool IsCritical;
        public readonly bool WasKilled;
        public readonly Float3 Position;

        public DamageDealtPresentation(
            int targetEntityId, int sourceEntityId,
            float finalDamage, float shieldCost, float blockedDamage,
            bool isCritical, bool wasKilled, in Float3 position)
        {
            TargetEntityId = targetEntityId;
            SourceEntityId = sourceEntityId;
            FinalDamage = finalDamage;
            ShieldCost = shieldCost;
            BlockedDamage = blockedDamage;
            IsCritical = isCritical;
            WasKilled = wasKilled;
            Position = position;
        }
    }

    /// <summary>属性变化表现载荷。</summary>
    public readonly struct AttributeChangedPresentation
    {
        public readonly int EntityId;
        public readonly int AttributeId;
        public readonly float OldValue;
        public readonly float NewValue;
        public readonly float Delta;

        public AttributeChangedPresentation(int entityId, int attributeId, float oldValue, float newValue, float delta)
        {
            EntityId = entityId;
            AttributeId = attributeId;
            OldValue = oldValue;
            NewValue = newValue;
            Delta = delta;
        }
    }

    /// <summary>技能生命周期表现载荷。</summary>
    public readonly struct AbilityPresentation
    {
        public readonly int EntityId;
        public readonly int AbilityId;
        public readonly int AbilitySpecId;

        public AbilityPresentation(int entityId, int abilityId, int abilitySpecId)
        {
            EntityId = entityId;
            AbilityId = abilityId;
            AbilitySpecId = abilitySpecId;
        }
    }

    /// <summary>Cue 触发表现载荷。</summary>
    public readonly struct CuePresentation
    {
        public readonly int TargetEntityId;
        public readonly int SourceEntityId;
        public readonly GameplayTag CueTag;
        public readonly float Magnitude;
        public readonly Float3 Position;

        public CuePresentation(int targetEntityId, int sourceEntityId, GameplayTag cueTag, float magnitude, in Float3 position)
        {
            TargetEntityId = targetEntityId;
            SourceEntityId = sourceEntityId;
            CueTag = cueTag;
            Magnitude = magnitude;
            Position = position;
        }
    }
}
