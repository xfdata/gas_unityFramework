using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 防御塔建造逻辑组件 — 处理放置检测、资源消耗验证。
    /// 由TowerPlacementSystem驱动。
    /// </summary>
    public class TowerBuilderComponent
    {
        private TowerDefenseGlobalConfig _tdConfig;

        public void SetConfig(TowerDefenseGlobalConfig config)
        {
            _tdConfig = config;
        }

        /// <summary>
        /// 检查指定位置是否可以建造防御塔
        /// </summary>
        public bool CanPlace(Vector3 worldPosition, out Vector3 snappedPosition)
        {
            snappedPosition = GetGridSnapPosition(worldPosition);

            if (_tdConfig == null) return false;

            float halfGrid = _tdConfig.PlacementGridSize * 0.5f;

            // 检测是否有阻挡（不可建造区域）
            if (_tdConfig.BlockedLayerMask != 0)
            {
                if (Physics.CheckSphere(snappedPosition, halfGrid, _tdConfig.BlockedLayerMask))
                    return false;
            }

            // 检测是否在地面
            float castHeight = 10f;
            if (Physics.Raycast(
                snappedPosition + Vector3.up * castHeight,
                Vector3.down,
                out RaycastHit hit,
                castHeight * 2f,
                _tdConfig.PlacementLayerMask))
            {
                snappedPosition = hit.point;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 将世界坐标吸附到建造网格
        /// </summary>
        public Vector3 GetGridSnapPosition(Vector3 worldPosition)
        {
            if (_tdConfig == null) return worldPosition;

            float gridSize = Mathf.Max(0.1f, _tdConfig.PlacementGridSize);
            return new Vector3(
                Mathf.Round(worldPosition.x / gridSize) * gridSize,
                worldPosition.y,
                Mathf.Round(worldPosition.z / gridSize) * gridSize
            );
        }
    }
}
