using BattleFoundation;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// 路径跟随组件，挂载于TDEnemyActor。
    /// 沿WaypointPath逐个路点移动，到达终点时标记ReachedEnd。
    /// 
    /// 性能优化：
    /// - 不自行Update，由PathFollowerSystem统一驱动
    /// - 方向向量预缓存，避免每帧normalize
    /// - 使用sqrMagnitude判定而非magnitude
    /// </summary>
    public class PathFollowerComponent : EntityComponent
    {
        public WaypointPath Path { get; private set; }
        public int CurrentWaypointIndex { get; private set; }
        public float Speed { get; set; } = 3f;
        public bool ReachedEnd => Path == null || CurrentWaypointIndex >= Path.Waypoints.Length;
        
        /// <summary>
        /// 是否已经触发了到达终点事件（防止重复触发）
        /// </summary>
        public bool EndEventTriggered { get; set; }
        /// <summary>
        /// 归一化的路径进度 [0, 1]
        /// </summary>
        public float Progress01
        {
            get
            {
                if (Path == null || Path.Waypoints.Length <= 1)
                    return ReachedEnd ? 1f : 0f;
                return (float)CurrentWaypointIndex / (Path.Waypoints.Length - 1);
            }
        }

        // 预缓存当前段方向，避免重复计算
        private Vector3 _currentDirection;
        private float _currentSegmentLength;
        private float _traveledInSegment;

        /// <summary>
        /// 初始化路径跟随
        /// </summary>
        public void Init(WaypointPath path, float speed)
        {
            Path = path;
            Speed = Mathf.Max(0.1f, speed);
            ResetFollower();
        }

        /// <summary>
        /// 重置到路径起点
        /// </summary>
        public void ResetFollower()
        {
            CurrentWaypointIndex = 0;
            EndEventTriggered = false;
            _traveledInSegment = 0f;
            _currentSegmentLength = 0f;
            _currentDirection = Vector3.zero;

            if (Path != null && Path.Waypoints.Length > 0 && Owner != null)
            {
                Owner.Position = Path.Waypoints[0];
                CacheCurrentSegment();
            }
        }

        /// <summary>
        /// 每帧Tick，由PathFollowerSystem调用
        /// </summary>
        public void Tick(float deltaTime, Transform unityTransform)
        {
            if (ReachedEnd || deltaTime <= 0f)
                return;

            // 死亡时停止移动
            if (Owner != null && !Owner.IsAlive)
                return;

            float remaining = Speed * deltaTime;

            while (remaining > 0f && !ReachedEnd)
            {
                // 更新当前段缓存
                float segRemaining = _currentSegmentLength - _traveledInSegment;
                if (remaining >= segRemaining)
                {
                    // 到达当前路点
                    remaining -= segRemaining;
                    _traveledInSegment = _currentSegmentLength;

                    if (unityTransform != null)
                        unityTransform.position = Path.Waypoints[CurrentWaypointIndex];
                    if (Owner != null)
                        Owner.Position = Path.Waypoints[CurrentWaypointIndex];

                    CurrentWaypointIndex++;

                    if (ReachedEnd)
                        break;

                    CacheCurrentSegment();
                }
                else
                {
                    // 在当前段内移动
                    _traveledInSegment += remaining;
                    Vector3 pos = Path.Waypoints[CurrentWaypointIndex] - _currentDirection * (_currentSegmentLength - _traveledInSegment);
                    if (unityTransform != null)
                        unityTransform.position = pos;
                    if (Owner != null)
                        Owner.Position = pos;
                    remaining = 0f;
                }
            }
        }

        private void CacheCurrentSegment()
        {
            if (Path == null || CurrentWaypointIndex >= Path.Waypoints.Length - 1)
                return;

            Vector3 from = Path.Waypoints[CurrentWaypointIndex];
            Vector3 to = Path.Waypoints[CurrentWaypointIndex + 1];
            _currentDirection = to - from;
            _currentSegmentLength = _currentDirection.magnitude;
            if (_currentSegmentLength > 0.0001f)
                _currentDirection /= _currentSegmentLength;
            _traveledInSegment = 0f;
        }

        public override void DeactivateForPool()
        {
            Path = null;
            CurrentWaypointIndex = 0;
            EndEventTriggered = false;
            Speed = 3f;
            _currentDirection = Vector3.zero;
            _currentSegmentLength = 0f;
            _traveledInSegment = 0f;
            base.DeactivateForPool();
        }
    }
}
