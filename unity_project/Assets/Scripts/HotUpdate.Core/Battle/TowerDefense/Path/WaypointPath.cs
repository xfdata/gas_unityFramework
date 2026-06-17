using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TowerDefense
{
    /// <summary>
    /// 路径路点数据，ScriptableObject存储，编辑器可视化编辑。
    /// 挂载为Asset后拖入WaveConfig使用。
    /// </summary>
    [CreateAssetMenu(fileName = "WaypointPath", menuName = "TowerDefense/Waypoint Path", order = 100)]
    public class WaypointPath : ScriptableObject
    {
        [SerializeField]
        private Vector3[] _waypoints = new Vector3[0];

        /// <summary>
        /// 路点数组（世界坐标），不可运行时修改。
        /// </summary>
        public Vector3[] Waypoints => _waypoints;

        /// <summary>
        /// 路径总长度（缓存，避免重复计算）
        /// </summary>
        public float TotalLength
        {
            get
            {
                if (_totalLength < 0f)
                    _totalLength = CalculateLength();
                return _totalLength;
            }
        }

        private float _totalLength = -1f;

        private float CalculateLength()
        {
            if (_waypoints == null || _waypoints.Length < 2)
                return 0f;
            float length = 0f;
            for (int i = 1; i < _waypoints.Length; i++)
                length += Vector3.Distance(_waypoints[i - 1], _waypoints[i]);
            return length;
        }

        /// <summary>
        /// 获取从指定位置到终点的剩余路径长度
        /// </summary>
        public float GetRemainingLength(int fromWaypointIndex)
        {
            if (_waypoints == null || fromWaypointIndex >= _waypoints.Length)
                return 0f;

            float remaining = 0f;
            for (int i = fromWaypointIndex + 1; i < _waypoints.Length; i++)
                remaining += Vector3.Distance(_waypoints[i - 1], _waypoints[i]);
            return remaining;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_waypoints == null || _waypoints.Length < 2) return;

            Gizmos.color = Color.yellow;
            for (int i = 0; i < _waypoints.Length; i++)
            {
                Gizmos.DrawSphere(_waypoints[i], 0.3f);
                if (i > 0)
                    Gizmos.DrawLine(_waypoints[i - 1], _waypoints[i]);
            }

            // 起点绿色、终点红色
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(_waypoints[0], 0.5f);
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(_waypoints[_waypoints.Length - 1], 0.5f);
        }
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// WaypointPath自定义编辑器，支持Scene视图点击添加/删除路点
    /// </summary>
    [CustomEditor(typeof(WaypointPath))]
    public class WaypointPathEditor : Editor
    {
        private int _selectedIndex = -1;

        public override void OnInspectorGUI()
        {
            var path = (WaypointPath)target;
            serializedObject.Update();

            var waypointsProp = serializedObject.FindProperty("_waypoints");

            EditorGUILayout.LabelField($"Waypoints: {waypointsProp.arraySize}", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // 显示所有路点的坐标
            if (waypointsProp.arraySize > 0 && _selectedIndex >= 0 && _selectedIndex < waypointsProp.arraySize)
            {
                var element = waypointsProp.GetArrayElementAtIndex(_selectedIndex);
                EditorGUILayout.PropertyField(element, new GUIContent($"WP [{_selectedIndex}]"));
            }

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reverse Path"))
            {
                Undo.RecordObject(path, "Reverse Waypoint Path");
                System.Array.Reverse(path.Waypoints);
                serializedObject.Update();
            }
            if (GUILayout.Button("Clear All"))
            {
                Undo.RecordObject(path, "Clear Waypoints");
                waypointsProp.ClearArray();
                serializedObject.ApplyModifiedProperties();
            }
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            var path = (WaypointPath)target;
            var waypoints = path.Waypoints;
            if (waypoints == null) return;

            for (int i = 0; i < waypoints.Length; i++)
            {
                EditorGUI.BeginChangeCheck();
                var newPos = Handles.PositionHandle(waypoints[i], Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(path, "Move Waypoint");
                    waypoints[i] = newPos;
                    EditorUtility.SetDirty(path);
                }

                Handles.Label(waypoints[i] + Vector3.up * 0.5f, $"[{i}]");
            }

            // 绘制路径线
            if (waypoints.Length >= 2)
            {
                Handles.color = Color.yellow;
                for (int i = 0; i < waypoints.Length - 1; i++)
                {
                    Handles.DrawLine(waypoints[i], waypoints[i + 1], 2f);
                }
            }
        }
    }
#endif
}
