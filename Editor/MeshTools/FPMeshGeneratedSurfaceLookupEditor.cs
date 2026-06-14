namespace FuzzPhyte.Utility.Editor.MeshTools
{
    using System.Collections.Generic;
    using FuzzPhyte.Utility.MeshTools;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(FPMeshGeneratedSurfaceLookup))]
    public class FPMeshGeneratedSurfaceLookupEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            FPMeshGeneratedSurfaceLookup lookup = (FPMeshGeneratedSurfaceLookup)target;
            IReadOnlyList<FPMeshGeneratedSurfaceVertexLookupRecord> records = lookup.VertexLookup;
            int mapped = 0;
            if (records != null)
            {
                for (int i = 0; i < records.Count; i++)
                {
                    if (records[i].HasEndpoint)
                    {
                        mapped++;
                    }
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Lookup Debug", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Mesh Vertices", records == null ? "0" : records.Count.ToString());
            EditorGUILayout.LabelField("Mapped Data Vertices", mapped.ToString());
            bool hasGeneratedMesh = lookup.TryResolveGeneratedMesh(out Mesh generatedMesh);
            EditorGUILayout.LabelField("Generated Mesh", generatedMesh == null ? "None" : generatedMesh.name);

            using (new EditorGUI.DisabledScope(!hasGeneratedMesh))
            {
                if (GUILayout.Button("Flip Surface Normals"))
                {
                    Undo.RecordObject(lookup, "Flip Generated Surface Normals");
                    Undo.RecordObject(generatedMesh, "Flip Generated Surface Normals");
                    if (lookup.FlipSurfaceNormals())
                    {
                        EditorUtility.SetDirty(generatedMesh);
                        EditorUtility.SetDirty(lookup);
                        SceneView.RepaintAll();
                    }
                }
            }

            using (new EditorGUI.DisabledScope(lookup.SourceAuthoring == null))
            {
                if (GUILayout.Button("Select Source Authoring"))
                {
                    Selection.activeObject = lookup.SourceAuthoring;
                    EditorGUIUtility.PingObject(lookup.SourceAuthoring);
                }
            }
        }

        private void OnSceneGUI()
        {
            FPMeshGeneratedSurfaceLookup lookup = (FPMeshGeneratedSurfaceLookup)target;
            if (!lookup.DrawDebugGizmos || lookup.VertexLookup == null)
            {
                return;
            }

            Color previous = Handles.color;
            GUIStyle labelStyle = EditorStyles.whiteMiniLabel;
            float tolerance = lookup.AlignmentTolerance;

            for (int i = 0; i < lookup.VertexLookup.Count; i++)
            {
                FPMeshGeneratedSurfaceVertexLookupRecord record = lookup.VertexLookup[i];
                Vector3 meshWorld = lookup.GetCurrentMeshVertexWorldPosition(record);
                bool resolved = lookup.TryResolveEndpointWorldPosition(record, out Vector3 dataWorld);
                float distance = Vector3.Distance(meshWorld, dataWorld);
                bool aligned = record.HasEndpoint && resolved && distance <= tolerance;

                Handles.color = !record.HasEndpoint
                    ? new Color(0.25f, 0.8f, 1f, 0.55f)
                    : aligned ? new Color(0.25f, 1f, 0.45f, 0.95f) : new Color(1f, 0.18f, 0.1f, 0.95f);

                Handles.SphereHandleCap(0, meshWorld, Quaternion.identity, lookup.DebugPointSize, EventType.Repaint);
                if (record.HasEndpoint)
                {
                    Handles.DrawAAPolyLine(2f, meshWorld, dataWorld);
                }

                if (!lookup.DrawDebugLabels)
                {
                    continue;
                }

                string label = record.HasEndpoint
                    ? $"MV{record.MeshVertexIndex} -> S{record.SurfaceIndex}"
                    : $"MV{record.MeshVertexIndex}";
                if (record.HasEndpoint && !aligned)
                {
                    label += $" ({distance:0.###})";
                }

                SceneView sceneView = SceneView.currentDrawingSceneView;
                Vector3 labelOffset = sceneView != null && sceneView.camera != null
                    ? sceneView.camera.transform.up * lookup.DebugPointSize * 1.5f
                    : Vector3.up * lookup.DebugPointSize * 1.5f;
                Handles.Label(meshWorld + labelOffset, label, labelStyle);
            }

            Handles.color = previous;
        }
    }
}
