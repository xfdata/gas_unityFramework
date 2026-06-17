using GAS;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 闃插尽濉旈厤缃甋criptableObject銆?
    /// 瀹氫箟闃插尽濉旂被鍨嬨€佹敾鍑诲睘鎬с€佸缓閫犳秷鑰椼€佸崌绾ч摼銆?
    /// </summary>
    [CreateAssetMenu(fileName = "TowerConfig", menuName = "TowerDefense/Tower Config", order = 140)]
    public class TowerConfig : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _towerName = "Tower";
        [SerializeField] private ETDTowerType _towerType = ETDTowerType.ArrowTower;
        [Tooltip("濉旂殑鏄剧ず绛夌骇锛?=鍩虹锛?=涓骇锛?=楂樼骇锛?")]
        [SerializeField] private int _towerLevel = 1;

        [Header("Visual")]
        [SerializeField] private GameObject _prefab;

        [Header("Placement")]
        [Tooltip("寤洪€犳秷鑰楅噾甯?")]
        [SerializeField] private int _buildCost = 50;
        [Tooltip("鍗囩骇鍒颁笅涓€绾ч渶瑕佺殑閲戝竵")]
        [SerializeField] private int _upgradeCost = 75;

        [Header("Combat")]
        [Tooltip("鏀诲嚮璺濈")]
        [SerializeField] private float _attackRange = 5f;
        [Tooltip("鏀诲嚮闂撮殧锛堢锛?")]
        [SerializeField] private float _attackInterval = 1f;
        [Tooltip("鍗曟鏀诲嚮浼ゅ")]
        [SerializeField] private float _attackDamage = 20f;
        [Tooltip("鏄惁绌块€忔敾鍑伙紙娉曞锛?")]
        [SerializeField] private bool _isPiercing;
        [Tooltip("AOE鐖嗙偢鍗婂緞锛?=闈濧OE锛?")]
        [SerializeField] private float _aoeRadius;
        [Tooltip("鍛戒腑鏂藉姞鍑忛€熺櫨鍒嗘瘮锛堝啺濉旓級锛?=鏃犲噺閫?")]
        [SerializeField] private float _slowPercent;

        [Header("Projectile")]
        [Tooltip("鏀诲嚮鎶€鑳斤紙RemoteAttackAbilityDefinition锛夛紝閫氳繃GAS婵€娲诲彂灏勬姇灏勭墿")]
        [SerializeField] private RemoteAttackAbilityDefinition _attackAbility;
        [Tooltip("鎶曞皠鐗╁畾涔夛紙绠/鐐浣跨敤鎶涚墿绾挎垨鐩寸嚎椋炶锛?")]
        [SerializeField] private RangedProjectileDefinition _projectileDefinition;
        [Tooltip("绱㈡晫绛栫暐")]
        [SerializeField] private ETDTargetPriority _targetPriority = ETDTargetPriority.MostProgressed;

        [Header("Special Effects")]
        [Tooltip("鍑忛€?鎺у埗鏁堟灉锛堝啺濉旂敤锛夛紝鍛戒腑鍚庢柦鍔犲埌鐩爣銆侱urationPolicy=Duration鏃舵寔缁敓鏁堛€?")]
        [SerializeField] private GameplayEffectDefinition _slowEffect;

        [Header("Upgrade")]
        [Tooltip("鍗囩骇鍚庣殑閰嶇疆锛坣ull=涓嶅彲鍗囩骇锛?")]
        [SerializeField] private TowerConfig _upgradeConfig;

        public string TowerName => _towerName;
        public ETDTowerType TowerType => _towerType;
        public int TowerLevel => _towerLevel;
        public GameObject Prefab => _prefab;
        public int BuildCost => _buildCost;
        public int UpgradeCost => _upgradeCost;
        public float AttackRange => _attackRange;
        public float AttackInterval => _attackInterval;
        public float AttackDamage => _attackDamage;
        public bool IsPiercing => _isPiercing;
        public float AoeRadius => _aoeRadius;
        public float SlowPercent => _slowPercent;
        public RangedProjectileDefinition ProjectileDefinition => _projectileDefinition;
        public RemoteAttackAbilityDefinition AttackAbility => _attackAbility;
        public ETDTargetPriority TargetPriority => _targetPriority;
        public GameplayEffectDefinition SlowEffect => _slowEffect;
        public TowerConfig UpgradeConfig => _upgradeConfig;
        public bool CanUpgrade => _upgradeConfig != null;
    }
}
