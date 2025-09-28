namespace FuzzPhyte.Utility.Interactive.Editor
{
    using UnityEditor;
    using UnityEngine;
    using FuzzPhyte.Utility.Interactive;
    using FuzzPhyte.Utility.Editor;
    using UnityEditor.IMGUI.Controls;
    [CustomEditor(typeof(FPVariantPreview))]
    public class FPVariantPreviewEditor : Editor
    {
        private int selectedColliderIndex = -1;

        public override void OnInspectorGUI()
        {
            var p = (FPVariantPreview)target;
            if (p == null || p.Equals(null)) return;
            if (p.workingColliders == null) return;
            SyncSelectedIndexFromHierarchy(p);
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
                /*
                if (GUILayout.Toggle(selectedColliderIndex == i, $"[{i}] {spec.name} ({spec.type})", "Button"))
                {
                    selectedColliderIndex = i;
                }
                */
                if (GUILayout.Button("Focus"))
                {
                    var t = p.colliderRoot.Find($"Col_{i}_{spec.type}");
                    if (t)
                    {
                        Selection.activeTransform = t;
                        FP_Utility_Editor.FocusOnObject(t.gameObject);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            //not going to use this
            if (selectedColliderIndex >= 0 && selectedColliderIndex < p.workingColliders.Count)
            {
                EditorGUILayout.Space();
                if (GUILayout.Button("Refit Box To Visual Bounds"))
                {
                    var s = p.workingColliders[selectedColliderIndex];
                    if (s.type == FPColliderType.Box && p.workingMesh)
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

            SyncSelectedIndexFromHierarchy(p);

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
                if (spec.type == FPColliderType.Box)
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
                else if (spec.type == FPColliderType.Sphere)
                {
                    // radius handle in the collider's local orientation
                    float newRadius = Handles.RadiusHandle(rot, pos, Mathf.Max(0.001f, spec.radius));
                    if (EditorGUI.EndChangeCheck())
                    {
                        spec.localPosition = pos;
                        spec.localEuler = rot.eulerAngles;
                        spec.radius = Mathf.Max(0.001f, newRadius);
                        p.workingColliders[selectedColliderIndex] = spec;
                        p.RebuildColliders();
                    }
                }else if(spec.type == FPColliderType.Capsule)
                {
                    // CapsuleBoundsHandle gives nice native gizmos (direction: 0=X,1=Y,2=Z)
                    var cbh = new CapsuleBoundsHandle(spec.direction)
                    {
                        center = pos,
                        height = Mathf.Max(spec.height, Mathf.Max(0.001f, spec.radius) * 2f),
                        radius = Mathf.Max(0.001f, spec.radius)
                    };

                    EditorGUI.BeginChangeCheck();
                    cbh.DrawHandle();
                    if (EditorGUI.EndChangeCheck())
                    {
                        spec.localPosition = cbh.center;
                        spec.height = Mathf.Max(cbh.height, cbh.radius * 2f);
                        spec.radius = Mathf.Max(0.001f, cbh.radius);
                        // direction stays what user chose in UI; change UI to switch axis if desired
                        p.workingColliders[selectedColliderIndex] = spec;
                        p.RebuildColliders();
                    }
                }else 
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
                if (spec.type == FPColliderType.Box)
                {
                    var m = Matrix4x4.TRS(spec.localPosition, Quaternion.Euler(spec.localEuler), spec.localScale == Vector3.zero ? Vector3.one : spec.localScale);
                    using (new Handles.DrawingScope(m))
                    {
                        Handles.CubeHandleCap(0, Vector3.zero, Quaternion.identity, 1f, EventType.Repaint);
                    }
                }
            }
        }

        private void SyncSelectedIndexFromHierarchy(FPVariantPreview p)
        {
            var sel = Selection.activeTransform;
            if (!sel || !p || !p.colliderRoot) return;
            if (!sel.IsChildOf(p.colliderRoot)) return;

            // Expect: "Col_{index}_{type}"
            var name = sel.name;
            if (!name.StartsWith("Col_")) return;
            var parts = name.Split('_');
            if (parts.Length >= 3 && int.TryParse(parts[1], out var idx))
                selectedColliderIndex = Mathf.Clamp(idx, 0, p.workingColliders.Count - 1);
        }
    }
}
