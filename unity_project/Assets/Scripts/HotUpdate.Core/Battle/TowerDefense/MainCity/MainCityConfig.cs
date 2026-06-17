using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 涓诲煄閰嶇疆ScriptableObject锛圥hase 2 瀹屽杽锛夈€?
    /// </summary>
    [CreateAssetMenu(fileName = "MainCityConfig", menuName = "TowerDefense/Main City Config", order = 130)]
    public class MainCityConfig : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _cityName = "Main City";

        [Header("Combat")]
        [Tooltip("涓诲煄鏈€澶х敓鍛藉€?")]
        [SerializeField] private float _maxHp = 100f;
        [Tooltip("涓诲煄闃插尽鍔?")]
        [SerializeField] private float _defense = 0f;

        [Header("Visual")]
        [Tooltip("涓诲煄棰勫埗浣?")]
        [SerializeField] private GameObject _prefab;

        public string CityName => _cityName;
        public float MaxHp => _maxHp;
        public float Defense => _defense;
        public GameObject Prefab => _prefab;
    }
}
