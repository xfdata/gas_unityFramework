using System.Collections.Generic;
using BattleCommon;
using BattleFoundation;
using Framework;
using GAS;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 防御塔放置系统 — 管理所有防御塔的生命周期、攻击驱动、建造与升级。
    /// 
    /// 设计：
    /// - 集中驱动所有 TowerActor 的 Targeting + Attack 组件（避免双重 Update 问题）
    /// - 处理建造/升级/出售的玩家请求
    /// - 管理可建造网格检测
    /// - 升级逻辑委托给 TowerUpgradeComponent
    /// </summary>
    public class TowerPlacementSystem : IBattleSystem
    {
        private IBattleContext _context;
        private EntityManager _entityManager;
        private TDBattleContext _tdContext;
        private TowerBuilderComponent _builder;
        private TowerDefenseGlobalConfig _tdConfig;
        private CombatProjectileSystem _projectileSystem;
        private ProjectileRuntime _projectileRuntime;

        /// <summary>
        /// 所有防御塔列表（快速遍历，避免每次查询EntityManager）
        /// </summary>
        private readonly List<TowerActor> _towers = new List<TowerActor>(32);

        /// <summary>
        /// 网格占用图（标记哪些格子已被占用）
        /// </summary>
        private readonly HashSet<Vector3> _occupiedPositions = new HashSet<Vector3>();

        public IReadOnlyList<TowerActor> Towers => _towers;
        public int TowerCount => _towers.Count;

        public void Initialize(IBattleContext context)
        {
            _context = context;
            _entityManager = context.EntityManager;
            _tdContext = context as TDBattleContext;
            _builder = new TowerBuilderComponent();

            // 从TDBattleEngine获取配置
            var engine = context.Engine as TDBattleEngine;
            _tdConfig = engine?.TDConfig;
            _builder.SetConfig(_tdConfig);

            // 获取投射物系统（供防御塔发射投射物用）
            _projectileSystem = context.GetSystem<CombatProjectileSystem>();
            _projectileRuntime = _projectileSystem?.Runtime;

            _towers.Clear();
            _occupiedPositions.Clear();
        }

        public void Start() { }

        public void Update(float deltaTime)
        {
            using (new AutoProfiler("TowerDefense.TowerPlacementSystem.Update"))
            {
                // 统一驱动所有防御塔的 Targeting + Attack 组件更新
                // Targeting 先更新（索敌），Attack 后更新（依赖索敌结果）
                for (int i = 0; i < _towers.Count; i++)
                {
                    var tower = _towers[i];
                    if (!tower.IsAlive) continue;

                    tower.Targeting?.Update(deltaTime);
                    tower.Attack?.Update(deltaTime);
                }
            }
        }

        public void LateUpdate(float deltaTime) { }

        /// <summary>
        /// 尝试建造防御塔
        /// </summary>
        public bool TryBuildTower(TowerConfig config, Vector3 worldPosition, out TowerActor tower)
        {
            tower = null;
            if (config == null || _tdContext == null) return false;

            // 金币检查
            if (!_tdContext.SpendGold(config.BuildCost))
                return false;

            // 位置检查
            if (!_builder.CanPlace(worldPosition, out Vector3 snappedPos))
            {
                _tdContext.AddGold(config.BuildCost); // 退款
                return false;
            }

            // 占用检查
            if (_occupiedPositions.Contains(snappedPos))
            {
                _tdContext.AddGold(config.BuildCost); // 退款
                return false;
            }

            // 创建防御塔（传入投射物运行时，对接GAS攻击链路）
            tower = new TowerActor();
            tower.InitTower(config, snappedPos, _projectileRuntime);
            _entityManager.AddEntity(tower);
            _towers.Add(tower);
            _occupiedPositions.Add(snappedPos);

            _context.EventBus.Emit(TDEventIds.TowerBuilt, tower);
            Debug.Log($"[TowerPlacementSystem] Tower '{config.TowerName}' built at {snappedPos}");
            return true;
        }

        /// <summary>
        /// 尝试升级防御塔（委托给 TowerUpgradeComponent）。
        /// </summary>
        public bool TryUpgradeTower(TowerActor tower)
        {
            if (tower == null || _tdContext == null) return false;
            if (!tower.Upgrade.CanUpgrade) return false;

            bool success = tower.Upgrade.TryUpgrade(_tdContext);
            if (success)
            {
                Debug.Log($"[TowerPlacementSystem] Tower '{tower.Config.TowerName}' upgraded to Lv.{tower.TowerLevel}");
            }
            return success;
        }

        /// <summary>
        /// 出售防御塔（返回一半建造费）
        /// </summary>
        public bool SellTower(TowerActor tower)
        {
            if (tower == null) return false;

            int refund = (tower.Config?.BuildCost ?? 0) / 2;
            _tdContext?.AddGold(refund);

            _occupiedPositions.Remove(tower.Position);
            _towers.Remove(tower);
            _entityManager.RemoveEntity(tower);
            tower.Dispose();

            _context.EventBus.Emit(TDEventIds.TowerSold, tower);
            Debug.Log($"[TowerPlacementSystem] Tower sold, refund: {refund}");
            return true;
        }

        public void Dispose()
        {
            for (int i = _towers.Count - 1; i >= 0; i--)
                _towers[i]?.Dispose();
            _towers.Clear();
            _occupiedPositions.Clear();
            _builder = null;
            _tdConfig = null;
            _tdContext = null;
            _projectileSystem = null;
            _projectileRuntime = null;
            _entityManager = null;
            _context = null;
        }
    }
}
