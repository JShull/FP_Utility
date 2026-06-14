namespace FuzzPhyte.Utility.Editor.MeshTools
{
    using FuzzPhyte.Utility.MeshTools;
    using UnityEditor;
    using UnityEngine;

    [CustomPropertyDrawer(typeof(FPMeshSurfaceEdgeEndpoint))]
    public class FPMeshSurfaceEdgeEndpointDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return (EditorGUIUtility.singleLineHeight * 3f) + (EditorGUIUtility.standardVerticalSpacing * 2f);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty kind = property.FindPropertyRelative("Kind");
            SerializedProperty sourceMeshIndex = property.FindPropertyRelative("SourceMeshIndex");
            SerializedProperty vertexIndex = property.FindPropertyRelative("VertexIndex");
            SerializedProperty generatedPointIndex = property.FindPropertyRelative("GeneratedPointIndex");

            Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(line, kind, label);

            EditorGUI.indentLevel++;
            line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            FPMeshSurfacePointKind endpointKind = (FPMeshSurfacePointKind)kind.enumValueIndex;
            if (endpointKind == FPMeshSurfacePointKind.GeneratedPoint)
            {
                EditorGUI.PropertyField(line, generatedPointIndex);
                line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                EditorGUI.LabelField(line, GUIContent.none);
            }
            else
            {
                EditorGUI.PropertyField(line, sourceMeshIndex);
                line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                EditorGUI.PropertyField(line, vertexIndex);
            }

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }
    }
}
