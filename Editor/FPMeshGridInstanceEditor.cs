namespace FuzzPhyte.Utility.Editor
{
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(FPMeshGridInstance))]
    public class FPMeshGridInstanceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();

            var instance = (FPMeshGridInstance)target;
            using (new EditorGUI.DisabledScope(instance.DataAsset == null))
            {
                if (GUILayout.Button("Regenerate Mesh"))
                {
                    RegenerateInstance(instance);
                }
            }

            if (instance.DataAsset == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign an FPMeshGridData asset to enable regeneration from stored grid and heightmap settings.",
                    MessageType.Info);
            }
        }

        [MenuItem("GameObject/FuzzPhyte/Rendering/Regenerate Selected Mesh Grid", false, 21)]
        private static void RegenerateSelectedMeshGrid()
        {
            var instances = Selection.GetFiltered<FPMeshGridInstance>(SelectionMode.Editable | SelectionMode.TopLevel);
            for (int i = 0; i < instances.Length; i++)
            {
                RegenerateInstance(instances[i]);
            }
        }

        [MenuItem("GameObject/FuzzPhyte/Rendering/Regenerate Selected Mesh Grid", true)]
        private static bool ValidateRegenerateSelectedMeshGrid()
        {
            return Selection.GetFiltered<FPMeshGridInstance>(SelectionMode.Editable | SelectionMode.TopLevel).Length > 0;
        }

        private static void RegenerateInstance(FPMeshGridInstance instance)
        {
            if (instance == null)
            {
                return;
            }

            Undo.RecordObject(instance, "Regenerate Mesh Grid");
            instance.Regenerate();
            EditorUtility.SetDirty(instance);
        }
    }
}
