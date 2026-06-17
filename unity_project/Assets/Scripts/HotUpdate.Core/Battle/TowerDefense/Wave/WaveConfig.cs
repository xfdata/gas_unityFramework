using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 娉㈡閰嶇疆ScriptableObject锛圥hase 6 瀹屽杽锛夈€?
    /// 瀹氫箟涓€娉㈡晫浜虹殑缁勬垚銆佺敓鎴愰棿闅斿拰娉㈠墠寤惰繜銆?
    /// </summary>
    [CreateAssetMenu(fileName = "WaveConfig", menuName = "TowerDefense/Wave Config", order = 120)]
    public class WaveConfig : ScriptableObject
    {
        [Header("Wave Identity")]
        [SerializeField] private string _waveName = "Wave 1";
        [Tooltip("鏄惁涓築oss娉?")]
        [SerializeField] private bool _isBossWave;

        [Header("Timing")]
        [Tooltip("娉㈠墠鍑嗗鏃堕棿锛堢锛?")]
        [SerializeField] private float _preparationTime = 3f;
        [Tooltip("鏁屼汉閫愪釜鐢熸垚鐨勯棿闅旓紙绉掞級")]
        [SerializeField] private float _spawnInterval = 1f;

    [Header("Enemies")]
    [Tooltip("鏈尝鏁屼汉閰嶇疆鍒楄〃锛堟敮鎸佹贩鍚堝嚭鍏碉級")]
    [SerializeField] private WaveEnemyEntry[] _enemyEntries;
    
    [Header("Paths (Multi-Path Support)")]
    [Tooltip("鏈尝璺緞閰嶇疆鍒楄〃锛堟敮鎸佸璺緞鍚屾椂杩涙敾锛?")]
    [SerializeField] private WavePathEntry[] _pathEntries;

    public string WaveName => _waveName;
    public bool IsBossWave => _isBossWave;
    public float PreparationTime => _preparationTime;
    public float SpawnInterval => _spawnInterval;
    public WaveEnemyEntry[] EnemyEntries => _enemyEntries;
    public WavePathEntry[] PathEntries => _pathEntries;

    /// <summary>
    /// 鑾峰彇鏈尝鎬荤敓鎴愭暟锛堥亶鍘嗘墍鏈夎矾寰勬潯鐩級
    /// </summary>
    public int GetTotalSpawnCount()
    {
        int total = 0;
        
        // 鍏煎鏃ч厤缃細鍗曚竴璺緞
        if (_enemyEntries != null && _enemyEntries.Length > 0)
        {
            foreach (var entry in _enemyEntries)
            {
                if (entry != null)
                    total += entry.Count;
            }
        }
        
        // 鏂伴厤缃細澶氳矾寰?
        if (_pathEntries != null && _pathEntries.Length > 0)
        {
            foreach (var pathEntry in _pathEntries)
            {
                if (pathEntry != null)
                    total += pathEntry.GetTotalCount();
            }
        }
        
        return total;
    }
}
}
