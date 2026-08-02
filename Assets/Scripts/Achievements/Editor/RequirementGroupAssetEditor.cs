using UnityEditor;
using UnityEngine;

namespace CUBE.Achievements.Editor
{
    [CustomEditor(typeof(RequirementGroupAsset))]
    public class RequirementGroupAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var group = (RequirementGroupAsset)target;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Requirement Group Asset: {group.Operator.ToString().ToUpper()}", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            DrawDefaultInspector();

            if (group.SubRequirements != null && group.SubRequirements.Count > 0)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Summary Preview", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                for (int i = 0; i < group.SubRequirements.Count; i++)
                {
                    var req = group.SubRequirements[i];
                    string summary = req != null ? req.EditorSummary : "(null)";
                    EditorGUILayout.LabelField($"[{i}] {summary}");
                }
                EditorGUI.indentLevel--;
            }
        }
    }
}
