using GAS;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 防御塔配置ScriptableObject。
    /// 定义防御塔类型、攻击属性、建造消耗、升级链。
    /// </summary>
    [CreateAssetMenu(fileName = "TowerConfig", menuName = "TowerDefense/Tower Config", order = 140)]
    public class TowerConfig : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _towerName = "Tower";
        [SerializeField] private ETDTowerType _towerType = ETDTowerType.ArrowTower;
        [Tooltip("塔的显示等级（1=基础，2=中级，3=高级）")]
        [SerializeField] private int _towerLevel = 1;

        [Header("Visual")]
        [SerializeField] private GameObject _prefab;

        [Header("Placement")]
        [Tooltip("建造消耗金币")]
        [SerializeField] private int _buildCost = 50;
        [Tooltip("升级到下一级需要的金币")]
        [SerializeField] private int _upgradeCost = 75;

        [Header("Combat")]
        [Tooltip("攻击距离")]
        [SerializeField] private float _attackRange = 5f;
        [Tooltip("攻击间隔（秒）")]
        [SerializeField] private float _attackInterval = 1f;
        [Tooltip("单次攻击伤害")]
        [SerializeField] private float _attackDamage = 20f;
        [Tooltip("是否穿透攻击（法塔）")]
        [SerializeField] private bool _isPiercing;
        [Tooltip("AOE爆炸半径（0=非AOE）")]
        [SerializeField] private float _aoeRadius;
        [Tooltip("命中施加减速百分比（冰塔），0=无减速")]
        [SerializeField] private float _slowPercent;

        [Header("Projectile")]
        [Tooltip("攻击技能（RemoteAttackAbilityDefinition），通过GAS激活发射投射物")]
        [SerializeField] private RemoteAttackAbilityDefinition _attackAbility;
        [Tooltip("投射物定义（箭塔/炮塔使用抛物线或直线飞行）")]
        [SerializeField] private RangedProjectileDefinition _projectileDefinition;
        [Tooltip("索敌策略")]
        [SerializeField] private ETDTargetPriority _targetPriority = ETDTargetPriority.MostProgressed;

        [Header("Special Effects")]
        [Tooltip("减速/控制效果（冰塔用），命中后施加到目标。DurationPolicy=Duration时持续生效。")]
        [SerializeField] private GameplayEffectDefinition _slowEffect;

        [Header("Upgrade")]
        [Tooltip("升级后的配置（null=不可升级）")]
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
