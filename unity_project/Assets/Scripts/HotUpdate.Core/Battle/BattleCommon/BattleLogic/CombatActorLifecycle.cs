using BattleFoundation;

namespace BattleCommon
{
    /// <summary>
    /// R3-S5: Actor 死亡/销毁原因枚举（决策 1/3）。
    /// 驱动表现差异与死亡归属，替代隐性「CanRecycle 即销毁」语义。
    /// </summary>
    public enum DeathReason
    {
        /// <summary>
        /// 被击杀：HP 归零触发死亡流程。
        /// </summary>
        Killed = 0,

        /// <summary>
        /// 超时：buff/召唤物/临时实体到期销毁。
        /// </summary>
        Timeout = 1,

        /// <summary>
        /// 回滚清理：spawn 失败回滚或快照对账时清理半成品/多余实体。
        /// </summary>
        RollbackCleanup = 2,

        /// <summary>
        /// 场景清理：战斗结束、场景卸载、强制销毁全部实体。
        /// </summary>
        SceneCleanup = 3,
    }

    /// <summary>
    /// R3-S5: Actor 死亡事件（决策 3）。
    /// 由 CombatActorSystem 在实体销毁时通过 BattleEventBus 发送，携带完整状态。
    /// 消费方（IBattlePresentationSink/日志/统计）不反查逻辑层。
    /// </summary>
    public readonly struct ActorDiedEvent
    {
        public readonly int EntityId;
        public readonly int KillerId;
        public readonly DeathReason Reason;

        public ActorDiedEvent(int entityId, int killerId, DeathReason reason)
        {
            EntityId = entityId;
            KillerId = killerId;
            Reason = reason;
        }
    }

    /// <summary>
    /// R3-S5: Actor spawn 事件。
    /// 由 CombatActorSystem 在实体创建成功后通过 BattleEventBus 发送。
    /// </summary>
    public readonly struct ActorSpawnedEvent
    {
        public readonly int EntityId;
        public readonly EEntityCamp Camp;
        public readonly EEntityType EntityType;

        public ActorSpawnedEvent(int entityId, EEntityCamp camp, EEntityType entityType)
        {
            EntityId = entityId;
            Camp = camp;
            EntityType = entityType;
        }
    }

    /// <summary>
    /// R3-S5: CombatActor 系统事件 ID 常量。
    /// 注册到 BattleEventBus 的框架事件键（int 类型，与 BattleEventIds 同体系）。
    /// ID 区间 6001-6999 留给 CombatActor 系统，避开 BattleEventIds 的 1001-5002。
    /// </summary>
    public static class CombatActorEventIds
    {
        public const int ActorSpawned = 6001;
        public const int ActorDied = 6002;
    }
}
