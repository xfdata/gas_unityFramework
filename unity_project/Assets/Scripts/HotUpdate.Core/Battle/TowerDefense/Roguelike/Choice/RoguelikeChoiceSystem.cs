using System.Collections.Generic;
using BattleCommon;
using BattleFoundation;
using Framework;
using GAS;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 缃楀悏灏斿己鍖栭€夋嫨绯荤粺 (IBattleSystem)銆?
    /// 
    /// 鑱岃矗锛?
    /// - 鐩戝惉 WaveCompleted 浜嬩欢锛屾殏鍋?TimeScale
    /// - 浠?ChoiceConfig 姹犱腑鎸夋潈閲嶉殢鏈烘娊鍙?涓€夐」
    /// - 绛夊緟鐜╁閫夋嫨锛屾柦鍔?GameplayEffect锛屾仮澶?TimeScale 骞舵帹杩涙尝娆?
    /// 
    /// 鏁版嵁娴侊細WaveCompleted 鈫?Pause 鈫?Generate3 鈫?PlayerSelect 鈫?Apply 鈫?Resume 鈫?NextWave
    /// 
    /// 鎬ц兘锛氫粎鍦ㄦ尝娆＄粨鏉熸椂瑙﹀彂锛屼笉鍦?Update 涓墽琛岄噸璁＄畻銆?
    /// </summary>
    public class RoguelikeChoiceSystem : IBattleSystem
    {
        private const int CHOICE_COUNT = 3;

        private IBattleContext _context;
        private TDBattleContext _tdContext;
        private EntityManager _entityManager;
        private WaveManagerSystem _waveManager;
        private BattleEngine _engine;

        // 閫夋嫨鏁版嵁
        private readonly List<ChoiceConfig> _choicePool = new List<ChoiceConfig>();
        private readonly List<int> _weightedChoiceIndices = new List<int>(32);
        private readonly HashSet<int> _selectedChoiceIndices = new HashSet<int>();
        private readonly List<BattleEntity> _targetBuffer = new List<BattleEntity>(32);
        private ChoiceData[] _currentChoices = new ChoiceData[CHOICE_COUNT];
        private bool _isChoosing;

        /// <summary>褰撳墠鏄惁澶勪簬閫夋嫨闃舵</summary>
        public bool IsChoosing => _isChoosing;

        /// <summary>褰撳墠3涓€夐」锛圲I灞傛煡璇㈢敤锛?/summary>
        public IReadOnlyList<ChoiceData> CurrentChoices => _currentChoices;

        public void Initialize(IBattleContext context)
        {
            _context = context;
            _tdContext = context as TDBattleContext;
            _entityManager = context.EntityManager;
            _engine = context.Engine;

            // 鑾峰彇娉㈡绠＄悊鍣ㄥ苟璁剧疆缃楀悏灏旈€夋嫨閽╁瓙
            _waveManager = context.GetSystem<WaveManagerSystem>();
            if (_waveManager != null)
            {
                _waveManager.WaitingForRoguelikeChoice = true;
            }

            // 浠庡叏灞€閰嶇疆鍔犺浇 ChoiceConfig 姹?
            var engine = context.Engine as TDBattleEngine;
            var tdConfig = engine?.TDConfig;
            if (tdConfig != null && tdConfig.RoguelikeChoicePool != null)
            {
                foreach (var choice in tdConfig.RoguelikeChoicePool)
                {
                    if (choice != null && choice.Weight > 0)
                        _choicePool.Add(choice);
                }
            }

            // 璁㈤槄娉㈡瀹屾垚浜嬩欢
            context.EventBus.On<int>(TDEventIds.WaveCompleted, OnWaveCompleted);

            Debug.Log($"[RoguelikeChoiceSystem] Initialized with {_choicePool.Count} choice options in pool.");
        }

        public void Start() { }

        public void Update(float deltaTime)
        {
            // 閫夋嫨闃舵涓嶆墽琛屼换浣曟搷浣滐紙TimeScale=0鏃朵笉浼氬埌杩欓噷锛屼絾浣滀负瀹夊叏鎺柦锛?
        }

        public void LateUpdate(float deltaTime) { }

        // ===== 鍏叡鎺ュ彛 (UI灞傝皟鐢? =====

        /// <summary>
        /// 鐜╁閫夋嫨绗?index 涓€夐」锛?-2锛夈€?
        /// 杩斿洖鏄惁閫夋嫨鎴愬姛銆?
        /// </summary>
        public bool SelectChoice(int index)
        {
            if (!_isChoosing || index < 0 || index >= CHOICE_COUNT)
                return false;

            var choiceData = _currentChoices[index];
            if (choiceData.SourceConfig == null)
            {
                Debug.LogError($"[RoguelikeChoiceSystem] Invalid choice at index {index}");
                return false;
            }

            var config = choiceData.SourceConfig;
            int waveIndex = _waveManager?.CurrentWaveIndex ?? -1;

            // 1. 娑堣€楅噾甯侊紙濡傛湁锛?
            int costPaid = 0;
            if (!config.IsFree)
            {
                if (_tdContext == null || !_tdContext.SpendGold(config.Cost))
                {
                    Debug.LogWarning($"[RoguelikeChoiceSystem] Not enough gold for choice '{config.ChoiceId}' (need {config.Cost})");
                    return false;
                }
                costPaid = config.Cost;
            }

            // 2. 鏂藉姞 GameplayEffect
            ApplyChoiceEffect(config);

            // 3. 鍙戝皠閫夋嫨浜嬩欢
            _context.EventBus.Emit(TDEventIds.RoguelikeChoiceSelected,
                new ChoiceSelectedEvent(waveIndex, config.ChoiceId, config.Category, costPaid));

            Debug.Log($"[RoguelikeChoiceSystem] Player selected '{config.ChoiceId}' (cost: {costPaid})");

            // 4. 鎭㈠鎴樻枟
            ResumeBattle();

            return true;
        }

        // ===== 鍐呴儴閫昏緫 =====

        /// <summary>
        /// 娉㈡瀹屾垚鍥炶皟锛氭殏鍋滄垬鏂楋紝鐢熸垚閫夐」
        /// </summary>
        private void OnWaveCompleted(int waveIndex)
        {
            if (_choicePool.Count == 0)
            {
                Debug.LogWarning("[RoguelikeChoiceSystem] No choices in pool, skipping...");
                _waveManager?.ResumeNextWave();
                return;
            }

            // 鏆傚仠鎴樻枟
            if (_engine != null)
            {
                _engine.TimeScale = 0f;
            }

            _isChoosing = true;

            // 鐢熸垚3涓殢鏈洪€夐」
            GenerateChoices();

            // 鍙戝皠閫夋嫨寮€濮嬩簨浠?
            _context.EventBus.Emit(TDEventIds.RoguelikeChoiceStart,
                new RoguelikeChoiceStartEvent(waveIndex, CHOICE_COUNT));

            Debug.Log($"[RoguelikeChoiceSystem] Wave {waveIndex + 1} completed. " +
                      $"Presenting {CHOICE_COUNT} choices. Battle paused.");
        }

        /// <summary>
        /// 浠庨厤缃睜涓寜鏉冮噸闅忔満鎶藉彇3涓笉閲嶅鐨勯€夐」銆?
        /// 浣跨敤 Fisher-Yates 鍔犳潈鎶藉彇锛岀‘淇濅笉閲嶅銆?
        /// </summary>
        private void GenerateChoices()
        {
            // 鍒涘缓鍔犳潈姹犲壇鏈紙鎸夋潈閲嶅睍寮€锛?
            _weightedChoiceIndices.Clear();
            _selectedChoiceIndices.Clear();
            for (int i = 0; i < _choicePool.Count; i++)
            {
                int weight = Mathf.Max(1, _choicePool[i].Weight);
                for (int w = 0; w < weight; w++)
                    _weightedChoiceIndices.Add(i);
            }

            // Fisher-Yates 娲楃墝鍙栧墠3涓笉閲嶅绱㈠紩
            int maxAttempts = _weightedChoiceIndices.Count * 2;
            int attempts = 0;

            for (int c = 0; c < CHOICE_COUNT && attempts < maxAttempts; attempts++)
            {
                int randomIdx = UnityEngine.Random.Range(0, _weightedChoiceIndices.Count);
                int poolIdx = _weightedChoiceIndices[randomIdx];

                if (!_selectedChoiceIndices.Contains(poolIdx) && CheckPrerequisite(_choicePool[poolIdx]))
                {
                    _selectedChoiceIndices.Add(poolIdx);
                    _currentChoices[c] = new ChoiceData(_choicePool[poolIdx]);
                    c++;
                }
            }

            // 濡傛灉涓嶅3涓紙姹犲お灏忔垨鍓嶇疆鏉′欢杩囨护锛夛紝鐢ㄦ睜涓墿浣欏～鍏?
            if (_selectedChoiceIndices.Count < CHOICE_COUNT)
            {
                int c = _selectedChoiceIndices.Count;
                for (int i = 0; i < _choicePool.Count && c < CHOICE_COUNT; i++)
                {
                    if (!_selectedChoiceIndices.Contains(i))
                    {
                        _currentChoices[c] = new ChoiceData(_choicePool[i]);
                        c++;
                    }
                }
            }
        }

        /// <summary>
        /// 妫€鏌ラ€夐」鐨勫墠缃潯浠舵槸鍚︽弧瓒炽€?
        /// 渚嬪锛氶渶瑕佹垬鍦轰腑瀛樺湪 IceTower 鎵嶅嚭鐜板啺濉斿己鍖栭€夐」銆?
        /// </summary>
        private bool CheckPrerequisite(ChoiceConfig config)
        {
            if (config.RequiredTags == null || config.RequiredTags.Length == 0)
                return true;

            // 閬嶅巻鎵€鏈夊锛屾鏌ユ槸鍚︽湁鍖归厤鏍囩
            var allEntities = _entityManager.All;
            for (int i = 0; i < allEntities.Count; i++)
            {
                if (!(allEntities[i] is TowerActor tower) || !tower.IsAlive)
                    continue;

                // 绠€鍗曟爣绛惧尮閰嶏細RequiredTags[0] 鍖归厤濉旂被鍨嬪悕绉?
                foreach (var tag in config.RequiredTags)
                {
                    if (tower.Config != null &&
                        tower.Config.TowerType.ToString().Contains(tag))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 鏍规嵁閰嶇疆鏂藉姞 GameplayEffect 鍒扮洰鏍囧疄浣撱€?
        /// 渚濇嵁 EChoiceCategory 鍜?EChoiceTarget 纭畾搴旂敤鐩爣銆?
        /// </summary>
        private void ApplyChoiceEffect(ChoiceConfig config)
        {
            if (config.AppliedEffect == null)
            {
                // 鏃?GameplayEffectDefinition锛岀洿鎺ヤ慨鏀瑰睘鎬?
                ApplyDirectAttributeModifier(config);
                return;
            }

            // 鑾峰彇鐩爣瀹炰綋鍒楄〃
            var targets = ResolveTargets(config.TargetType);
            if (targets.Count == 0)
            {
                Debug.LogWarning($"[RoguelikeChoiceSystem] No targets found for choice '{config.ChoiceId}' (target: {config.TargetType})");
                return;
            }

            // 涓烘瘡涓洰鏍囨柦鍔?GameplayEffect
            foreach (var entity in targets)
            {
                ApplyEffectToEntity(config, entity);
            }

            Debug.Log($"[RoguelikeChoiceSystem] Applied effect '{config.ChoiceId}' to {targets.Count} target(s)");
        }

        /// <summary>
        /// 鐩存帴淇敼灞炴€э紙褰撴病鏈?GameplayEffectDefinition 鏃讹級銆?
        /// 閬靛惊 TowerUpgradeComponent 鐨勫凡鏈夋ā寮忋€?
        /// </summary>
        private void ApplyDirectAttributeModifier(ChoiceConfig config)
        {
            var targets = ResolveTargets(config.TargetType);
            float modifier = config.ValueModifier;

            foreach (var entity in targets)
            {
                var attrs = entity.Get<CombatAttributeComponent>();
                if (attrs == null) continue;

                switch (config.Category)
                {
                    case EChoiceCategory.TowerBuff:
                        // 濉斿己鍖栵細榛樿璁や负 ValueModifier 淇グ鏀婚€燂紙AttackInterval锛?
                        attrs.AttackInterval *= (1f / modifier);
                        break;
                    case EChoiceCategory.SkillBuff:
                        attrs.Attack *= modifier;
                        break;
                    case EChoiceCategory.AttributeBuff:
                        attrs.Attack *= modifier;
                        break;
                }
            }
        }

        /// <summary>
        /// 灏?GameplayEffect 鏂藉姞鍒板崟涓疄浣撱€?
        /// 浣跨敤瀹炰綋鐨?CombatAbilityComponent.Effects 浣滀负鏉ユ簮銆?
        /// </summary>
        private void ApplyEffectToEntity(ChoiceConfig config, BattleEntity targetEntity)
        {
            if (config.AppliedEffect == null || targetEntity == null)
                return;

            var abilityComp = targetEntity.Get<CombatAbilityComponent>();
            if (abilityComp?.Effects == null)
            {
                // 鍥為€€锛氱洿鎺ヤ慨鏀瑰睘鎬?
                ApplyDirectAttributeModifierToEntity(config, targetEntity);
                return;
            }

            // 閫氳繃 GAS 閾捐矾鏂藉姞鏁堟灉锛堣嚜鏂芥斁鍒拌嚜韬級
            var spec = abilityComp.Effects.MakeOutgoingSpec(
                abilityComp.Effects, config.AppliedEffect, 1);
            if (spec != null)
            {
                // 灏?ValueModifier 娉ㄥ叆 SetByCaller
                // 锛堝鏋?GameplayEffectExecution 浣跨敤 SetByCaller 鑾峰彇鍊硷級
                abilityComp.Effects.ApplySpecToSelf(spec);
            }
        }

        /// <summary>
        /// 鐩存帴淇敼灞炴€у埌鍗曚釜瀹炰綋锛堜綔涓?GAS 鍥為€€鏂规锛?
        /// </summary>
        private void ApplyDirectAttributeModifierToEntity(ChoiceConfig config, BattleEntity entity)
        {
            var attrs = entity.Get<CombatAttributeComponent>();
            if (attrs == null) return;

            float modifier = config.ValueModifier;
            switch (config.Category)
            {
                case EChoiceCategory.TowerBuff:
                    attrs.AttackInterval *= (1f / modifier);
                    break;
                case EChoiceCategory.SkillBuff:
                case EChoiceCategory.AttributeBuff:
                    attrs.Attack *= modifier;
                    break;
            }
        }

        /// <summary>
        /// 鏍规嵁鐩爣绫诲瀷瑙ｆ瀽鐩爣瀹炰綋鍒楄〃銆?
        /// </summary>
        private List<BattleEntity> ResolveTargets(EChoiceTarget targetType)
        {
            _targetBuffer.Clear();

            switch (targetType)
            {
                case EChoiceTarget.ArrowTower:
                case EChoiceTarget.CannonTower:
                case EChoiceTarget.MageTower:
                case EChoiceTarget.IceTower:
                    CollectTowersByType(targetType, _targetBuffer);
                    break;

                case EChoiceTarget.AllTowers:
                    CollectAllTowers(_targetBuffer);
                    break;

                case EChoiceTarget.Player:
                    CollectPlayer(_targetBuffer);
                    break;

                case EChoiceTarget.MainCity:
                    CollectMainCity(_targetBuffer);
                    break;

                case EChoiceTarget.Global:
                    // 鍏ㄥ眬鏁堟灉锛氭敹闆嗘墍鏈夊 + 鐜╁ + 涓诲煄
                    CollectAllTowers(_targetBuffer);
                    CollectPlayer(_targetBuffer);
                    CollectMainCity(_targetBuffer);
                    break;
            }

            return _targetBuffer;
        }

        private void CollectTowersByType(EChoiceTarget targetType, List<BattleEntity> result)
        {
            var towerSystem = _context.GetSystem<TowerPlacementSystem>();
            if (towerSystem?.Towers == null) return;

            var targetTowerType = ChoiceTargetToTowerType(targetType);

            for (int i = 0; i < towerSystem.Towers.Count; i++)
            {
                var tower = towerSystem.Towers[i];
                if (tower.IsAlive && tower.Config != null && tower.Config.TowerType == targetTowerType)
                    result.Add(tower);
            }
        }

        private void CollectAllTowers(List<BattleEntity> result)
        {
            var towerSystem = _context.GetSystem<TowerPlacementSystem>();
            if (towerSystem?.Towers == null) return;

            for (int i = 0; i < towerSystem.Towers.Count; i++)
            {
                if (towerSystem.Towers[i].IsAlive)
                    result.Add(towerSystem.Towers[i]);
            }
        }

        private void CollectPlayer(List<BattleEntity> result)
        {
            var playerSystem = _context.GetSystem<TDPlayerMovementSystem>();
            if (playerSystem?.Player != null && playerSystem.Player.IsAlive)
                result.Add(playerSystem.Player);
        }

        private void CollectMainCity(List<BattleEntity> result)
        {
            var mainCitySystem = _context.GetSystem<MainCitySystem>();
            if (mainCitySystem?.MainCity != null && mainCitySystem.MainCity.IsAlive)
                result.Add(mainCitySystem.MainCity);
        }

        /// <summary>
        /// 灏?EChoiceTarget 鏄犲皠鍒?ETDTowerType
        /// </summary>
        private static ETDTowerType ChoiceTargetToTowerType(EChoiceTarget target)
        {
            return target switch
            {
                EChoiceTarget.ArrowTower => ETDTowerType.ArrowTower,
                EChoiceTarget.CannonTower => ETDTowerType.CannonTower,
                EChoiceTarget.MageTower => ETDTowerType.MageTower,
                EChoiceTarget.IceTower => ETDTowerType.IceTower,
                _ => ETDTowerType.ArrowTower,
            };
        }

        /// <summary>
        /// 鎭㈠鎴樻枟锛氶噸缃甌imeScale锛屾竻闄ら€夋嫨鐘舵€侊紝鎺ㄨ繘涓嬩竴娉€?
        /// </summary>
        private void ResumeBattle()
        {
            _isChoosing = false;

            // 鎭㈠ TimeScale
            if (_engine != null)
            {
                _engine.TimeScale = 1f;
            }

            // 娓呴櫎褰撳墠閫夐」
            for (int i = 0; i < _currentChoices.Length; i++)
                _currentChoices[i] = default;

            // 鎺ㄨ繘涓嬩竴娉?
            _waveManager?.ResumeNextWave();

            Debug.Log("[RoguelikeChoiceSystem] Battle resumed.");
        }

        public void Dispose()
        {
            if (_context != null)
            {
                _context.EventBus.Off<int>(TDEventIds.WaveCompleted, OnWaveCompleted);
            }

            _choicePool.Clear();
            _weightedChoiceIndices.Clear();
            _selectedChoiceIndices.Clear();
            _targetBuffer.Clear();
            _currentChoices = null;
            _waveManager = null;
            _engine = null;
            _entityManager = null;
            _tdContext = null;
            _context = null;
        }
    }
}
