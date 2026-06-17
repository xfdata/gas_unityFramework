using BattleFoundation;

namespace TowerDefense
{
    /// <summary>
    /// TD战斗上下文，继承BattleContext，提供TD专属服务访问。
    /// </summary>
    public class TDBattleContext : BattleContext
    {
        /// <summary>
        /// 敌人工厂（对象池管理）
        /// </summary>
        public EnemyFactory EnemyFactory { get; private set; }

        /// <summary>
        /// 波次管理器（便捷访问）
        /// </summary>
        public WaveManagerSystem WaveManager => GetSystem<WaveManagerSystem>();

        /// <summary>
        /// 防御塔放置系统（便捷访问）
        /// </summary>
        public TowerPlacementSystem TowerPlacement => GetSystem<TowerPlacementSystem>();

        /// <summary>
        /// 玩家金币数（运行时可修改）
        /// </summary>
        public int PlayerGold { get; set; }

        public override void Initialize(BattleEngine engine, BattleRuntimeSettings settings)
        {
            base.Initialize(engine, settings);
            EnemyFactory = new EnemyFactory(this);
            PlayerGold = 0;
        }

        /// <summary>
        /// 增加玩家金币并发射事件
        /// </summary>
        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            int previous = PlayerGold;
            PlayerGold += amount;
            EventBus.Emit(TDEventIds.PlayerGoldChanged,
                new PlayerGoldChangedEvent(previous, PlayerGold, amount));
        }

        /// <summary>
        /// 消耗玩家金币，返回是否成功
        /// </summary>
        public bool SpendGold(int amount)
        {
            if (PlayerGold < amount) return false;
            int previous = PlayerGold;
            PlayerGold -= amount;
            EventBus.Emit(TDEventIds.PlayerGoldChanged,
                new PlayerGoldChangedEvent(previous, PlayerGold, -amount));
            return true;
        }

        protected override void OnDispose()
        {
            EnemyFactory?.ClearPools();
            EnemyFactory = null;
            base.OnDispose();
        }
    }
}
