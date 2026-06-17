using UnityEngine;

namespace TowerDefense
{
    [DisallowMultipleComponent]
    [AddComponentMenu("TowerDefense/Tower Defense Scene Config")]
    public sealed class TowerDefenseSceneConfig : MonoBehaviour
    {
        [SerializeField]
        private TowerDefenseGlobalConfig _globalConfig;

        public static TowerDefenseSceneConfig Current { get; private set; }

        public TowerDefenseGlobalConfig GlobalConfig => _globalConfig;

        private void OnEnable()
        {
            if (Current != null && Current != this)
                Debug.LogWarning("[TowerDefenseSceneConfig] Multiple scene configs found, using the latest enabled one.");

            Current = this;
        }

        private void OnDisable()
        {
            if (Current == this)
                Current = null;
        }
    }
}
