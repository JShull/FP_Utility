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
            Dictionary<FPMeshNavigationTags, int> tagCounts = new();
            if (records != null)
            {
                for (int i = 0; i < records.Count; i++)
                {
                    if (records[i].HasEndpoint)
                    {
                        mapped++;
                    }

                    if (lookup.TryResolvePrimaryTag(records[i], out FPMeshNavigationTags tag))
                    {
                        tagCounts.TryGetValue(tag, out int count);
                        tagCounts[tag] = count + 1;
                    }
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Lookup Debug", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Mesh Vertices", records == null ? "0" : records.Count.ToString());
            EditorGUILayout.LabelField("Mapped Data Vertices", mapped.ToString());
            bool hasGeneratedMesh = lookup.TryResolveGeneratedMesh(out Mesh generatedMesh);
            EditorGUILayout.LabelField("Generated Mesh", generatedMesh == null ? "None" : generatedMesh.name);
            DrawTagCounts(tagCounts);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Point Selection", EditorStyles.boldLabel);
            int maxVertex = records == null || records.Count == 0 ? -1 : records.Count - 1;
            int selectedIndex = Mathf.Clamp(lookup.SelectedDebugMeshVertexIndex, -1, maxVertex);
            EditorGUI.BeginChangeCheck();
            selectedIndex = EditorGUILayout.IntSlider("Mesh Vertex Index", selectedIndex, -1, Mathf.Max(0, maxVertex));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(lookup, "Select Surface Lookup Vertex");
                lookup.SetSelectedDebugMeshVertexIndex(selectedIndex);
                EditorUtility.SetDirty(lookup);
                SceneView.RepaintAll();
            }

            using (new EditorGUI.DisabledScope(records == null || selectedIndex < 0 || selectedIndex >= records.Count))
            {
                if (GUILayout.Button("Frame Selected Vertex"))
                {
                    FrameRecord(lookup, records[selectedIndex]);
                }
            }

            using (new EditorGUI.DisabledScope(selectedIndex < 0))
            {
                if (GUILayout.Button("Clear Selected Vertex"))
                {
                    Undo.RecordObject(lookup, "Clear Surface Lookup Vertex Selection");
                    lookup.SetSelectedDebugMeshVertexIndex(-1);
                    EditorUtility.SetDirty(lookup);
                    SceneView.RepaintAll();
                }
            }

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

        private static void DrawTagCounts(Dictionary<FPMeshNavigationTags, int> tagCounts)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Primary Tag Counts", EditorStyles.boldLabel);
            if (tagCounts == null || tagCounts.Count == 0)
            {
                EditorGUILayout.LabelField("No mapped tag data");
                return;
            }

            FPMeshNavigationTags[] tags =
            {
                FPMeshNavigationTags.Ground,
                FPMeshNavigationTags.Wall,
                FPMeshNavigationTags.Tree,
                FPMeshNavigationTags.Water,
                FPMeshNavigationTags.Air,
                FPMeshNavigationTags.Underground,
                FPMeshNavigationTags.Home,
                FPMeshNavigationTags.Food,
                FPMeshNavigationTags.Hazard,
                FPMeshNavigationTags.CustomA,
                FPMeshNavigationTags.CustomB
            };

            for (int i = 0; i < tags.Length; i++)
            {
                if (!tagCounts.TryGetValue(tags[i], out int count))
                {
                    continue;
                }

                Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                Rect swatch = new Rect(rect.x, rect.y + 2f, 12f, rect.height - 4f);
                EditorGUI.DrawRect(swatch, FPMeshGeneratedSurfaceLookup.GetTagDebugColor(tags[i]));
                EditorGUI.LabelField(new Rect(rect.x + 18f, rect.y, rect.width - 18f, rect.height), tags[i].ToString(), count.ToString());
            }
        }

        private static void FrameRecord(FPMeshGeneratedSurfaceLookup lookup, FPMeshGeneratedSurfaceVertexLookupRecord record)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                return;
            }

            Vector3 world = lookup.GetCurrentMeshVertexWorldPosition(record);
            Camera camera = sceneView.camera;
            float size = Mathf.Max(0.2f, lookup.ResolveDebugPointSize(world, camera) * 12f);
            sceneView.Frame(new Bounds(world, Vector3.one * size), false);
            SceneView.RepaintAll();
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
            SceneView sceneView = SceneView.currentDrawingSceneView;
            Camera sceneCamera = sceneView == null ? null : sceneView.camera;
            TrySelectDebugPointAtMouse(lookup, sceneCamera);

            for (int i = 0; i < lookup.VertexLookup.Count; i++)
            {
                FPMeshGeneratedSurfaceVertexLookupRecord record = lookup.VertexLookup[i];
                Vector3 meshWorld = lookup.GetCurrentMeshVertexWorldPosition(record);
                float pointSize = lookup.ResolveDebugPointSize(meshWorld, sceneCamera);
                bool resolved = lookup.TryResolveEndpointWorldPosition(record, out Vector3 dataWorld);
                float distance = Vector3.Distance(meshWorld, dataWorld);
                bool aligned = record.HasEndpoint && resolved && distance <= tolerance;

                Color recordColor = lookup.GetDebugColor(record);
                Handles.color = recordColor;

                if (record.MeshVertexIndex == lookup.SelectedDebugMeshVertexIndex)
                {
                    Vector3 forward = GetSceneViewForward();
                    Handles.color = Color.white;
                    Handles.DrawWireDisc(meshWorld, forward, pointSize * 2.8f);
                    Handles.color = Color.yellow;
                    Handles.DrawWireDisc(meshWorld, forward, pointSize * 3.5f);
                }

                if (record.HasEndpoint)
                {
                    Handles.color = recordColor;
                    Handles.DrawAAPolyLine(2f, meshWorld, dataWorld);
                }

                if (!lookup.DrawDebugLabels)
                {
                    continue;
                }

                string label = record.HasEndpoint
                    ? $"MV{record.MeshVertexIndex} -> S{record.SurfaceIndex}"
                    : $"MV{record.MeshVertexIndex}";
                if (lookup.TryResolvePrimaryTag(record, out FPMeshNavigationTags tag))
                {
                    label += $" {tag}";
                }

                if (record.HasEndpoint && !aligned)
                {
                    label += $" ({distance:0.###})";
                }

                Vector3 labelOffset = sceneCamera != null
                    ? sceneCamera.transform.up * pointSize * 1.5f
                    : Vector3.up * pointSize * 1.5f;
                Handles.Label(meshWorld + labelOffset, label, labelStyle);
            }

            Handles.color = previous;
        }

        private static void TrySelectDebugPointAtMouse(FPMeshGeneratedSurfaceLookup lookup, Camera sceneCamera)
        {
            Event current = Event.current;
            if (current.type != EventType.MouseDown || current.button != 0 || current.alt || lookup.VertexLookup == null)
            {
                return;
            }

            int bestIndex = -1;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < lookup.VertexLookup.Count; i++)
            {
                FPMeshGeneratedSurfaceVertexLookupRecord record = lookup.VertexLookup[i];
                Vector3 meshWorld = lookup.GetCurrentMeshVertexWorldPosition(record);
                if (!TryGetDebugPointPickRadius(lookup, sceneCamera, meshWorld, out float pickRadius))
                {
                    continue;
                }

                Vector2 guiPoint = HandleUtility.WorldToGUIPoint(meshWorld);
                float distance = Vector2.Distance(current.mousePosition, guiPoint);
                if (distance > pickRadius || distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                bestIndex = record.MeshVertexIndex;
            }

            if (bestIndex < 0)
            {
                return;
            }

            Undo.RecordObject(lookup, "Select Surface Lookup Vertex");
            lookup.SetSelectedDebugMeshVertexIndex(bestIndex);
            EditorUtility.SetDirty(lookup);
            SceneView.RepaintAll();
            current.Use();
        }

        private static bool TryGetDebugPointPickRadius(FPMeshGeneratedSurfaceLookup lookup, Camera sceneCamera, Vector3 world, out float radius)
        {
            radius = 0f;
            if (sceneCamera == null)
            {
                radius = 12f;
                return true;
            }

            Vector3 viewport = sceneCamera.WorldToViewportPoint(world);
            if (viewport.z <= 0.001f)
            {
                return false;
            }

            float pointSize = lookup.ResolveDebugPointSize(world, sceneCamera);
            Vector3 offsetWorld = world + (sceneCamera.transform.right * pointSize);
            Vector2 center = HandleUtility.WorldToGUIPoint(world);
            Vector2 edge = HandleUtility.WorldToGUIPoint(offsetWorld);
            radius = Mathf.Clamp(Vector2.Distance(center, edge) * 1.8f, 8f, 42f);
            return true;
        }

        private static Vector3 GetSceneViewForward()
        {
            SceneView sceneView = SceneView.currentDrawingSceneView;
            return sceneView != null && sceneView.camera != null ? sceneView.camera.transform.forward : Vector3.forward;
        }
    }
}
