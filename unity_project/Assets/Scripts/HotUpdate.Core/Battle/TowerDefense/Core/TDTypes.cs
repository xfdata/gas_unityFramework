namespace TowerDefense
{
    /// <summary>
    /// TD专属战斗事件ID，在BattleEventIds基础上扩展（6000+段）
    /// </summary>
    public static class TDEventIds
    {
        public const int WaveStarted       = 6001;
        public const int WaveCleared       = 6002;
        public const int EnemySpawned      = 6003;
        public const int EnemyKilled       = 6004;
        public const int EnemyReachedEnd   = 6005;
        public const int TowerBuilt        = 6006;
        public const int TowerUpgraded     = 6007;
        public const int TowerSold         = 6008;
        public const int MainCityDamaged   = 6009;
        public const int MainCityDestroyed = 6010;
        public const int BossSpawned       = 6011;
        public const int PlayerGoldChanged = 6012;
        public const int AllWavesCleared   = 6013; // 所有波次清除，触发胜利
        public const int TowerAttack       = 6014; // 防御塔发射攻击
        public const int ProjectileHit     = 6015; // 投射物命中目标
        public const int TowerSkillCast    = 6016; // 防御塔成功施放技能
        public const int TowerTargetSwitch = 6017; // 防御塔切换目标

        // === Phase 5: Roguelike构筑 ===
        public const int WaveCompleted          = 6018; // 波次彻底清除（在选择阶段前触发）
        public const int RoguelikeChoiceStart   = 6019; // 强化选择面板打开
        public const int RoguelikeChoiceSelected = 6020; // 玩家做出强化选择

        // === Phase 6: UI接入与游戏循环 ===
        public const int BattlePhaseChanged     = 6021; // 战斗阶段切换
        public const int UIRequestBuildTower    = 6022; // UI请求建造防御塔
        public const int UIRequestStartWave     = 6023; // UI请求开始下一波
        public const int UISelectRoguelikeOption = 6024; // UI提交罗吉尔强化选择

        // === Phase 7: 商业前置系统 (Meta Progression) ===
        public const int MetaInjectionApplied   = 6025; // Meta天赋注入完成
        public const int TowerModAttached       = 6026; // 塔插件挂载
        public const int TowerModRemoved        = 6027; // 塔插件卸载
        public const int BossPhaseChanged       = 6028; // Boss阶段切换
        public const int BossKilled             = 6029; // Boss被击杀
    }

    // ===== Phase 6: 战斗阶段 =====

    /// <summary>
    /// 战斗阶段枚举。UI通过监听 BattlePhaseChanged 事件切换界面。
    /// Phase 6 完善了完整的游戏循环。
    /// </summary>
    public enum EBattlePhase
    {
        /// <summary>战前准备（可建塔）</summary>
        Prepare,
        /// <summary>战斗中（NPC进攻）</summary>
        Combat,
        /// <summary>波次结束（结算中）</summary>
        WaveEnd,
        /// <summary>罗吉尔强化选择</summary>
        Choice,
        /// <summary>战斗胜利</summary>
        Victory,
        /// <summary>战斗失败</summary>
        Defeat,
    }

    // ===== Phase 6: UI事件结构 =====

    /// <summary>
    /// 战斗阶段切换事件（struct，栈分配，零GC）</summary>
    public readonly struct BattlePhaseChangedEvent
    {
        public readonly EBattlePhase PreviousPhase;
        public readonly EBattlePhase CurrentPhase;

        public BattlePhaseChangedEvent(EBattlePhase previous, EBattlePhase current)
        {
            PreviousPhase = previous;
            CurrentPhase = current;
        }
    }

    /// <summary>
    /// UI请求建造防御塔事件（struct，栈分配，零GC）
    /// UI层发射此事件，GameFlowSystem/TowerPlacementSystem消费。
    /// </summary>
    public readonly struct UIRequestBuildTowerEvent
    {
        public readonly ETDTowerType TowerType;

        public UIRequestBuildTowerEvent(ETDTowerType towerType)
        {
            TowerType = towerType;
        }
    }

    /// <summary>
    /// UI请求开始波次事件（struct，栈分配，零GC）</summary>
    public readonly struct UIRequestStartWaveEvent
    {
        /// <summary>空结构体，仅作信号。可扩展携带波次索引。</summary>
    }

    /// <summary>
    /// UI提交罗吉尔选择事件（struct，栈分配，零GC）
    /// UI层发射此事件，RoguelikeChoiceSystem消费。
    /// </summary>
    public readonly struct UISelectRoguelikeOptionEvent
    {
        /// <summary>选项索引（0-2）</summary>
        public readonly int OptionIndex;

        public UISelectRoguelikeOptionEvent(int optionIndex)
        {
            OptionIndex = optionIndex;
        }
    }

    // ===== Phase 5: Roguelike 枚举 =====

    /// <summary>
    /// 强化选择类别
    /// </summary>
    public enum EChoiceCategory
    {
        /// <summary>塔强化类（攻速/范围/减速等）</summary>
        TowerBuff,
        /// <summary>技能强化类（伤害/冷却/附加效果）</summary>
        SkillBuff,
        /// <summary>属性强化类（攻击力/主城回血/金币掉落）</summary>
        AttributeBuff,
    }

    /// <summary>
    /// 强化选择目标过滤
    /// </summary>
    public enum EChoiceTarget
    {
        ArrowTower,
        CannonTower,
        MageTower,
        IceTower,
        AllTowers,
        Player,
        MainCity,
        Global,
    }

    // ===== Phase 5: Roguelike 事件结构 =====

    /// <summary>
    /// 强化选择开始事件（struct，栈分配，零GC）
    /// </summary>
    public readonly struct RoguelikeChoiceStartEvent
    {
        public readonly int WaveIndex;
        public readonly int ChoiceCount; // 固定3

        public RoguelikeChoiceStartEvent(int waveIndex, int choiceCount)
        {
            WaveIndex = waveIndex;
            ChoiceCount = choiceCount;
        }
    }

    /// <summary>
    /// 强化选择提交事件（struct，栈分配，零GC）
    /// </summary>
    public readonly struct ChoiceSelectedEvent
    {
        public readonly int WaveIndex;
        public readonly string ChoiceId;
        public readonly EChoiceCategory Category;
        public readonly int CostPaid;

        public ChoiceSelectedEvent(int waveIndex, string choiceId,
            EChoiceCategory category, int costPaid)
        {
            WaveIndex = waveIndex;
            ChoiceId = choiceId;
            Category = category;
            CostPaid = costPaid;
        }
    }

    /// <summary>
    /// 波次状态机
    /// </summary>
    public enum ETDWaveState
    {
        Idle,         // 未开始
        Preparing,    // 波前准备（倒计时）
        Spawning,     // 正在生成敌人
        Active,       // 敌人正在行动
        Cleared,      // 当前波次清除
    }

    /// <summary>
    /// 防御塔索敌策略
    /// </summary>
    public enum ETDTargetPriority
    {
        /// <summary>最近目标（距离最短）</summary>
        Nearest,
        /// <summary>沿路径进度最大（最靠近主城）</summary>
        MostProgressed,
        /// <summary>优先锁定Boss（回退到MostProgressed）</summary>
        PriorityBoss,
        /// <summary>最远目标（路径进度最小）</summary>
        FarthestProgress,
        /// <summary>血量最低</summary>
        LowestHP,
    }

    /// <summary>
    /// 防御塔类型
    /// </summary>
    public enum ETDTowerType
    {
        None = 0,
        ArrowTower,   // 箭塔（单目标、高攻速）
        CannonTower,  // 炮塔（AOE、低攻速）
        MageTower,    // 法塔（穿透、Debuff）
        IceTower,     // 冰塔（减速）
    }

    /// <summary>
    /// 敌人类型
    /// </summary>
    public enum ETDEnemyType
    {
        Normal  = 0,
        Fast    = 1,
        Tanky   = 2,
        Boss    = 3,
        Flyer   = 4,  // 飞行敌人（无视地面阻挡）
    }

    /// <summary>
    /// 城市攻击者状态
    /// </summary>
    public enum ECityAttackerState
    {
        Idle,           // 未开始攻击
        Attacking,      // 正在攻击主城
        Stopped,        // 攻击已停止（主城被摧毁或敌人被击杀）
    }

    /// <summary>
    /// 敌人到达终点事件（struct，栈分配，零GC）
    /// </summary>
    public readonly struct EnemyReachedEndEvent
    {
        public readonly int EnemyId;
        public readonly int DamageToCity;
        public readonly bool ShouldAttackCity; // 是否应该持续攻击主城（而非一次性伤害）

        public EnemyReachedEndEvent(int enemyId, int damageToCity, bool shouldAttackCity = true)
        {
            EnemyId = enemyId;
            DamageToCity = damageToCity;
            ShouldAttackCity = shouldAttackCity;
        }
    }

    /// <summary>
    /// 敌人被击杀事件
    /// </summary>
    public readonly struct EnemyKilledEvent
    {
        public readonly int EnemyId;
        public readonly int KillerId;  // 防御塔或玩家ID

        public EnemyKilledEvent(int enemyId, int killerId)
        {
            EnemyId = enemyId;
            KillerId = killerId;
        }
    }

    /// <summary>
    /// 金币变化事件
    /// </summary>
    public readonly struct PlayerGoldChangedEvent
    {
        public readonly int PreviousGold;
        public readonly int CurrentGold;
        public readonly int Delta;

        public PlayerGoldChangedEvent(int previous, int current, int delta)
        {
            PreviousGold = previous;
            CurrentGold = current;
            Delta = delta;
        }
    }

    /// <summary>
    /// 主城受伤事件
    /// </summary>
    public readonly struct MainCityDamagedEvent
    {
        public readonly int CityId;
        public readonly float Damage;
        public readonly float RemainingHp;
        public readonly float MaxHp;

        public MainCityDamagedEvent(int cityId, float damage, float remainingHp, float maxHp)
        {
            CityId = cityId;
            Damage = damage;
            RemainingHp = remainingHp;
            MaxHp = maxHp;
        }
    }

    /// <summary>
    /// 主城被摧毁事件
    /// </summary>
    public readonly struct MainCityDestroyedEvent
    {
        public readonly int CityId;

        public MainCityDestroyedEvent(int cityId)
        {
            CityId = cityId;
        }
    }

    /// <summary>
    /// 防御塔攻击事件（发射投射物时）
    /// </summary>
    public readonly struct TowerAttackEvent
    {
        public readonly int TowerId;
        public readonly int TargetId;
        public readonly float AttackDamage;
        public readonly ETDTowerType TowerType;

        public TowerAttackEvent(int towerId, int targetId, float attackDamage, ETDTowerType towerType)
        {
            TowerId = towerId;
            TargetId = targetId;
            AttackDamage = attackDamage;
            TowerType = towerType;
        }
    }

    /// <summary>
    /// 防御塔技能施放事件（GAS链路成功激活时）
    /// </summary>
    public readonly struct TowerSkillCastEvent
    {
        public readonly int TowerId;
        public readonly int TargetId;
        public readonly int AbilityId;
        public readonly ETDTowerType TowerType;

        public TowerSkillCastEvent(int towerId, int targetId, int abilityId, ETDTowerType towerType)
        {
            TowerId = towerId;
            TargetId = targetId;
            AbilityId = abilityId;
            TowerType = towerType;
        }
    }

    /// <summary>
    /// 防御塔切换目标事件
    /// </summary>
    public readonly struct TowerTargetSwitchEvent
    {
        public readonly int TowerId;
        public readonly int PreviousTargetId; // -1 表示之前无目标
        public readonly int NewTargetId;      // -1 表示丢失目标
        public readonly string StrategyName;

        public TowerTargetSwitchEvent(int towerId, int prevTargetId, int newTargetId, string strategyName)
        {
            TowerId = towerId;
            PreviousTargetId = prevTargetId;
            NewTargetId = newTargetId;
            StrategyName = strategyName;
        }
    }

    /// <summary>
    /// 投射物命中事件
    /// </summary>
    public readonly struct ProjectileHitEvent
    {
        public readonly int ProjectileId;
        public readonly int SourceId;   // 发射者（防御塔）ID
        public readonly int TargetId;   // 被命中目标ID
        public readonly float Damage;
        public readonly UnityEngine.Vector3 HitPosition;

        public ProjectileHitEvent(int projectileId, int sourceId, int targetId, float damage, UnityEngine.Vector3 hitPosition)
        {
            ProjectileId = projectileId;
            SourceId = sourceId;
            TargetId = targetId;
            Damage = damage;
            HitPosition = hitPosition;
        }
    }
}
