using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 鏁屾柟鍗曚綅閰嶇疆锛孲criptableObject銆?
    /// 瀹氫箟鏁屼汉鐨勫熀纭€灞炴€с€佸瑙傘€佺Щ鍔ㄩ€熷害銆佸嚮鏉€濂栧姳绛夈€?
    /// </summary>
    [CreateAssetMenu(fileName = "TDEnemyConfig", menuName = "TowerDefense/Enemy Config", order = 110)]
    public class TDEnemyConfig : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _displayName = "Enemy";
        [SerializeField] private ETDEnemyType _enemyType = ETDEnemyType.Normal;

        [Header("Prefab")]
        [SerializeField] private GameObject _prefab;

        [Header("Movement")]
        [Tooltip("娌胯矾寰勭Щ鍔ㄩ€熷害锛坢/s锛?")]
        [SerializeField] private float _moveSpeed = 3f;

        [Header("Combat Attributes")]
        [SerializeField] private float _maxHp = 100f;
        [SerializeField] private float _atk = 10f;
        [SerializeField] private float _def = 0f;
        [SerializeField] private float _hitRadius = 0.5f;

        [Header("Reward")]
        [Tooltip("鍑绘潃濂栧姳閲戝竵")]
        [SerializeField] private int _killGold = 10;
        [Tooltip("鍒拌揪缁堢偣瀵逛富鍩庨€犳垚鐨勪激瀹?")]
        [SerializeField] private int _leakDamage = 1;

        [Header("City Attack Settings")]
        [Tooltip("鍒拌揪缁堢偣鍚庢敾鍑讳富鍩庣殑闂撮殧锛堢锛夛紝0鎴栬礋鏁拌〃绀哄彧閫犳垚涓€娆′激瀹筹紙涓嶆寔缁敾鍑伙級")]
        [SerializeField] private float _cityAttackInterval = 0f;
        [Tooltip("鍒拌揪缁堢偣鍚庢槸鍚︽寔缁敾鍑讳富鍩庯紙鍚﹀垯鍙€犳垚涓€娆′激瀹筹級")]
        [SerializeField] private bool _canAttackCity = true;

        [Header("Boss Settings")]
        [SerializeField] private bool _isBoss;
        [SerializeField] private float _bossHpMultiplier = 3f;
        [SerializeField] private float _bossSpeedMultiplier = 0.7f;
        [SerializeField] private int _bossKillGold = 100;

        public string DisplayName => _displayName;
        public ETDEnemyType EnemyType => _enemyType;
        public GameObject Prefab => _prefab;
        public float MoveSpeed => _moveSpeed;
        public float MaxHp => _maxHp;
        public float Atk => _atk;
        public float Def => _def;
        public float HitRadius => _hitRadius;
        public int KillGold => _killGold;
        public int LeakDamage => _leakDamage;
        public bool IsBoss => _isBoss;
        public float CityAttackInterval => _cityAttackInterval;
        public bool CanAttackCity => _canAttackCity;

        /// <summary>
        /// 鑾峰彇瀹為檯HP锛圔oss搴旂敤鍔犳垚锛?
        /// </summary>
        public float GetEffectiveHp() => IsBoss ? _maxHp * Mathf.Max(1f, _bossHpMultiplier) : _maxHp;

        /// <summary>
        /// 鑾峰彇瀹為檯绉诲姩閫熷害锛圔oss搴旂敤璋冩暣锛?
        /// </summary>
        public float GetEffectiveSpeed() => IsBoss ? _moveSpeed * Mathf.Max(0.1f, _bossSpeedMultiplier) : _moveSpeed;

        /// <summary>
        /// 鑾峰彇瀹為檯鍑绘潃閲戝竵
        /// </summary>
        public int GetEffectiveKillGold() => IsBoss ? _bossKillGold : _killGold;
    }
}
