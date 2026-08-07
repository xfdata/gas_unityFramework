using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BattleCommon.Editor
{
    [CustomEditor(typeof(BattleUnitGameplayConfig))]
    public sealed class BattleUnitGameplayConfigEditor : UnityEditor.Editor
    {
        private readonly List<string> _validationErrors = new List<string>();

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var config = target as BattleUnitGameplayConfig;
            config?.GetValidationErrors(_validationErrors);
            if (_validationErrors.Count == 0)
            {
                EditorGUILayout.HelpBox("配置有效。", MessageType.Info);
                return;
            }

            for (int i = 0; i < _validationErrors.Count; i++)
                EditorGUILayout.HelpBox(_validationErrors[i], MessageType.Error);
        }
    }
}
