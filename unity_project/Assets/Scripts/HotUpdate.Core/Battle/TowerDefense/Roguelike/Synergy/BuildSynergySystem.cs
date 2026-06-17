using System.Collections.Generic;
using BattleCommon;
using BattleFoundation;
using Framework;
using GAS;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// Build娴佹淳鍗忓悓绯荤粺 (IBattleSystem)銆?
    /// 
    /// 鑱岃矗锛?
    /// - 瀹炴椂缁熻鍚岀被鍨嬪鏁伴噺
    /// - 杈惧埌 SynergyConfig.RequiredCount 闃堝€兼椂鑷姩鏂藉姞 GameplayEffect 澧炵泭
    /// - 濉旇鍑哄敭/鎽ф瘉鏃惰嚜鍔ㄧЩ闄ゅ搴斿崗鍚屽鐩?
    /// - 鏁版嵁椹卞姩锛氬崗鍚岃鍒欓€氳繃 SynergyConfig ScriptableObject 閰嶇疆
    /// 
    /// 鎬ц兘锛?
    /// - 浠呭綋濉旀暟閲忓彉鍖栨椂瑙﹀彂閲嶇畻锛堝缓閫?鍑哄敭/鍗囩骇锛?
    /// - 閫氳繃浜嬩欢椹卞姩鑰岄潪姣忓抚鍏ㄩ噺鎵弿
    /// </summary>
    public class BuildSynergySystem : IBattleSystem
    {
        private IBattleContext _context;
        private EntityManager _entityManager;
        private TowerPlacementSystem _towerSystem;

        // 鍗忓悓閰嶇疆
        private SynergyConfig[] _synergyConfigs;

        // 褰撳墠鍚勭被鍨嬪鏁伴噺
        private readonly Dictionary<ETDTowerType, int> _towerCounts = new Dictionary<ETDTowerType, int>();

        // 褰撳墠婵€娲荤殑鍗忓悓鏁堟灉锛歋ynergyId 鈫?鍙楀奖鍝嶇殑濉斿垪琛?
        private readonly Dictionary<string, List<TowerActor>> _activeSynergies = new Dictionary<string, List<TowerActor>>();
        private readonly List<string> _keysToRemove = new List<string>();

        // 闃查噸澶嶆娴嬶細閬垮厤鍦ㄥ悓涓€甯у唴澶氭瑙﹀彂
        private bool _needsCheck;
        private int _lastKnownTowerCount;

        public void Initialize(IBattleContext context)
        {
            _context = context;
            _entityManager = context.EntityManager;
            _towerSystem = context.GetSystem<TowerPlacementSystem>();

            // 浠庡叏灞€閰嶇疆鍔犺浇鍗忓悓瑙勫垯
            var engine = context.Engine as TDBattleEngine;
            var tdConfig = engine?.TDConfig;
            _synergyConfigs = tdConfig?.SynergyConfigs ?? System.Array.Empty<SynergyConfig>();

            // 璁㈤槄濉斿彉鍖栦簨浠?
            var eb = context.EventBus;
            eb.On<TowerActor>(TDEventIds.TowerBuilt, OnTowerBuilt);
            eb.On<TowerActor>(TDEventIds.TowerUpgraded, OnTowerUpgraded);
            eb.On<TowerActor>(TDEventIds.TowerSold, OnTowerSold);

            ClearAll();
            RebuildTowerCounts();

            Debug.Log($"[BuildSynergySystem] Initialized with {(_synergyConfigs?.Length ?? 0)} synergy configs.");
        }

        public void Start() { }

        public void Update(float deltaTime)
        {
            if (_needsCheck && _synergyConfigs != null && _synergyConfigs.Length > 0)
            {
                RebuildTowerCounts();
                CheckSynergies();
                _needsCheck = false;
            }
        }

        public void LateUpdate(float deltaTime) { }

        // ===== 浜嬩欢鍥炶皟 =====

        private void OnTowerBuilt(TowerActor tower)
        {
            _needsCheck = true;
        }

        private void OnTowerUpgraded(TowerActor tower)
        {
            // 鍗囩骇鍙兘鏀瑰彉濉旂被鍨嬶紝闇€瑕侀噸绠?
            _needsCheck = true;
        }

        private void OnTowerSold(TowerActor tower)
        {
            // 绉婚櫎璇ュ涓婃墍鏈夋縺娲荤殑鍗忓悓澧炵泭
            RemoveEntityFromAllSynergies(tower);
            _needsCheck = true;
        }

        // ===== 鍗忓悓妫€娴嬮€昏緫 =====

        /// <summary>
        /// 閲嶆柊缁熻鍚勭被鍨嬪鏁伴噺
        /// </summary>
        private void RebuildTowerCounts()
        {
            _towerCounts.Clear();

            if (_towerSystem?.Towers == null) return;

            for (int i = 0; i < _towerSystem.Towers.Count; i++)
            {
                var tower = _towerSystem.Towers[i];
                if (!tower.IsAlive || tower.Config == null) continue;

                var type = tower.Config.TowerType;
                _towerCounts.TryGetValue(type, out int count);
                _towerCounts[type] = count + 1;
            }

            _lastKnownTowerCount = _towerSystem.TowerCount;
        }

        /// <summary>
        /// 妫€鏌ユ墍鏈夊崗鍚岃鍒欙紝婵€娲?绉婚櫎鏁堟灉
        /// </summary>
        private void CheckSynergies()
        {
            for (int i = 0; i < _synergyConfigs.Length; i++)
            {
                var config = _synergyConfigs[i];
                if (config == null) continue;

                ProcessSynergyConfig(config);
            }
        }

        /// <summary>
        /// 澶勭悊鍗曚釜鍗忓悓瑙勫垯
        /// </summary>
        private void ProcessSynergyConfig(SynergyConfig config)
        {
            if (!_towerCounts.TryGetValue(config.RequiredTowerType, out int count))
                count = 0;

            if (count >= config.RequiredCount)
            {
                // 杈惧埌闃堝€?鈫?婵€娲诲崗鍚?
                if (!_activeSynergies.ContainsKey(config.SynergyId))
                {
                    var eligibleTowers = GetEligibleTowers(config);
                    if (eligibleTowers.Count > 0)
                    {
                        ApplySynergy(config, eligibleTowers);
                    }
                }
                else if (config.IsStackable)
                {
                    // 鍫嗗彔妯″紡锛氭洿鏂版晥鏋滃己搴?
                    UpdateStackableSynergy(config, count);
                }
            }
            else
            {
                // 鏈揪鍒伴槇鍊?鈫?绉婚櫎鍗忓悓
                RemoveSynergy(config.SynergyId);
            }
        }

        /// <summary>
        /// 鑾峰彇绗﹀悎鍗忓悓鏉′欢鐨勫鍒楄〃
        /// </summary>
        private List<TowerActor> GetEligibleTowers(SynergyConfig config)
        {
            var result = new List<TowerActor>();
            if (_towerSystem?.Towers == null) return result;

            for (int i = 0; i < _towerSystem.Towers.Count; i++)
            {
                var tower = _towerSystem.Towers[i];
                if (!tower.IsAlive || tower.Config == null) continue;

                // 绫诲瀷鍖归厤
                if (tower.Config.TowerType != config.RequiredTowerType)
                    continue;

                // 棰濆鏍囩鏉′欢锛堝鏋滄湁锛?
                if (!string.IsNullOrEmpty(config.RequiredTag))
                {
                    // 绠€鍗曞尮閰嶏細妫€鏌ュ鏄惁宸叉湁姝ゆ爣绛撅紙閫氳繃 AbilityComponent锛?
                    var ability = tower.Get<CombatAbilityComponent>();
                    if (ability == null) continue;
                }

                result.Add(tower);
            }

            return result;
        }

        /// <summary>
        /// 鏂藉姞鍗忓悓澧炵泭鍒扮鍚堟潯浠剁殑濉?
        /// </summary>
        private void ApplySynergy(SynergyConfig config, List<TowerActor> towers)
        {
            if (config.BonusEffect == null) return;

            _activeSynergies[config.SynergyId] = towers;

            for (int i = 0; i < towers.Count; i++)
            {
                ApplyEffectToTower(config, towers[i]);
            }

            Debug.Log($"[BuildSynergySystem] Synergy '{config.SynergyName}' activated on {towers.Count} tower(s)!");
        }

        /// <summary>
        /// 灏嗗崗鍚屾晥鏋滄柦鍔犲埌鍗曚釜濉?
        /// </summary>
        private void ApplyEffectToTower(SynergyConfig config, TowerActor tower)
        {
            var abilityComp = tower.Get<CombatAbilityComponent>();
            if (abilityComp?.Effects == null) return;

            if (config.BonusEffect == null) return;

            var spec = abilityComp.Effects.MakeOutgoingSpec(
                abilityComp.Effects, config.BonusEffect, 1);
            if (spec != null)
            {
                abilityComp.Effects.ApplySpecToSelf(spec);
            }
        }

        /// <summary>
        /// 鏇存柊鍫嗗彔鍗忓悓鏁堟灉
        /// </summary>
        private void UpdateStackableSynergy(SynergyConfig config, int currentCount)
        {
            // 鍫嗗彔妯″紡锛氱Щ闄ゆ棫鏁堟灉锛屼互鏂扮殑鍫嗗彔鏁伴噸鏂版柦鍔?
            RemoveSynergy(config.SynergyId);
            var towers = GetEligibleTowers(config);
            if (towers.Count > 0)
            {
                ApplySynergy(config, towers);
            }
        }

        /// <summary>
        /// 绉婚櫎鎸囧畾鍗忓悓鏁堟灉
        /// </summary>
        private void RemoveSynergy(string synergyId)
        {
            if (!_activeSynergies.TryGetValue(synergyId, out var towers))
                return;

            // TODO: 瀹屾暣绉婚櫎 GameplayEffect 闇€瑕佺淮鎶?ActiveGameplayEffect 寮曠敤
            // 褰撳墠妗嗘灦涓?Infinite 鎸佺画鏃堕棿鐨勬晥鏋滃湪濉旈攢姣佹椂鑷姩绉婚櫎
            // 瀵逛簬闇€瑕佹墜鍔ㄧЩ闄ょ殑鎯呭喌锛岄渶瑕佹墿灞?GAS 妗嗘灦

            _activeSynergies.Remove(synergyId);
            Debug.Log($"[BuildSynergySystem] Synergy '{synergyId}' deactivated.");
        }

        /// <summary>
        /// 浠庢墍鏈夋縺娲荤殑鍗忓悓涓Щ闄ゆ寚瀹氬疄浣?
        /// </summary>
        private void RemoveEntityFromAllSynergies(TowerActor tower)
        {
            _keysToRemove.Clear();

            foreach (var kvp in _activeSynergies)
            {
                kvp.Value.Remove(tower);
                if (kvp.Value.Count == 0)
                    _keysToRemove.Add(kvp.Key);
            }

            foreach (var key in _keysToRemove)
            {
                _activeSynergies.Remove(key);
                Debug.Log($"[BuildSynergySystem] Synergy '{key}' removed (no eligible towers left).");
            }
            _keysToRemove.Clear();
        }

        /// <summary>
        /// 娓呴櫎鎵€鏈夌姸鎬?
        /// </summary>
        private void ClearAll()
        {
            _towerCounts.Clear();
            _activeSynergies.Clear();
            _keysToRemove.Clear();
            _needsCheck = false;
        }

        public void Dispose()
        {
            if (_context != null)
            {
                var eb = _context.EventBus;
                eb.Off<TowerActor>(TDEventIds.TowerBuilt, OnTowerBuilt);
                eb.Off<TowerActor>(TDEventIds.TowerUpgraded, OnTowerUpgraded);
                eb.Off<TowerActor>(TDEventIds.TowerSold, OnTowerSold);
            }

            ClearAll();
            _synergyConfigs = null;
            _towerSystem = null;
            _entityManager = null;
            _context = null;
        }
    }
}
