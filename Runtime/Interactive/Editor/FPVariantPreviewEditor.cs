namespace FuzzPhyte.Utility.Interactive.Editor
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using FuzzPhyte.Utility.Interactive;

    [CustomEditor(typeof(FPVariantPreview))]
    public class FPVariantPreviewEditor : Editor
    {
        private int selectedColliderIndex = -1;

        public override void OnInspectorGUI()
        {
            var p = (FPVariantPreview)target;
            EditorGUILayout.LabelField("Variant Preview", EditorStyles.boldLabel);
            if (p.workingMesh == null)
                EditorGUILayout.HelpBox("No visual mesh assigned.", MessageType.Warning);
            // Select collider index
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Colliders", EditorStyles.boldLabel);
            for (int i = 0; i < p.workingColliders.Count; i++)
            {
                var spec = p.workingColliders[i];
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Toggle(selectedColliderIndex == i, $"[{i}] {spec.name} ({spec.type})", "Button"))
                    selectedColliderIndex = i;
                if (GUILayout.Button("Focus"))
                {
                    var t = p.visualsRoot.Find($"Col_{i}_{spec.type}");
                    if (t) Selection.activeTransform = t;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (selectedColliderIndex >= 0 && selectedColliderIndex < p.workingColliders.Count)
            {
                EditorGUILayout.Space();
                if (GUILayout.Button("Refit Box To Visual Bounds"))
                {
                    var s = p.workingColliders[selectedColliderIndex];
                    if (s.type == FPVariantColliderType.Box && p.workingMesh)
                    {
                        var b = p.workingMesh.bounds; // local
                        s.localPosition = b.center;
                        s.localScale = b.size;
                        s.localEuler = Vector3.zero;
                        p.workingColliders[selectedColliderIndex] = s;
                        p.RebuildColliders();
                    }
                }
            }
        }

        private void OnSceneGUI()
        {
            var p = (FPVariantPreview)target;
            if (p == null || p.visualsRoot == null) return;


            if (selectedColliderIndex < 0 || selectedColliderIndex >= p.workingColliders.Count) return;
            var spec = p.workingColliders[selectedColliderIndex];


            var handleMatrix = p.visualsRoot.localToWorldMatrix;
            using (new Handles.DrawingScope(handleMatrix))
            {
                var pos = spec.localPosition;
                var rot = Quaternion.Euler(spec.localEuler);


                EditorGUI.BeginChangeCheck();
                pos = Handles.PositionHandle(pos, rot);
                rot = Handles.RotationHandle(rot, pos);
                if (spec.type == FPVariantColliderType.Box)
                {
                    var size = spec.localScale == Vector3.zero ? Vector3.one : spec.localScale;
                    var newSize = Handles.ScaleHandle(size, pos, rot, HandleUtility.GetHandleSize(pos));
                    if (EditorGUI.EndChangeCheck())
                    {
                        spec.localPosition = pos;
                        spec.localEuler = rot.eulerAngles;
                        spec.localScale = newSize;
                        p.workingColliders[selectedColliderIndex] = spec;
                        p.RebuildColliders();
                    }
                }
                else
                {
                    if (EditorGUI.EndChangeCheck())
                    {
                        spec.localPosition = pos;
                        spec.localEuler = rot.eulerAngles;
                        p.workingColliders[selectedColliderIndex] = spec;
                        p.RebuildColliders();
                    }
                }


                // Simple gizmo draw for selected collider
                Handles.color = new Color(0f, 1f, 1f, 0.15f);
                if (spec.type == FPVariantColliderType.Box)
                {
                    var m = Matrix4x4.TRS(spec.localPosition, Quaternion.Euler(spec.localEuler), spec.localScale == Vector3.zero ? Vector3.one : spec.localScale);
                    using (new Handles.DrawingScope(m))
                    {
                        Handles.CubeHandleCap(0, Vector3.zero, Quaternion.identity, 1f, EventType.Repaint);
                    }
                }
            }
        }
    }
}
