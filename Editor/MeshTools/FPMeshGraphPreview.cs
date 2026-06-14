namespace FuzzPhyte.Utility.Editor.MeshTools
{
    using System.Collections.Generic;
    using FuzzPhyte.Utility.MeshTools;
    using UnityEditor;
    using UnityEngine;

    internal static class FPMeshGraphPreview
    {
        public static readonly Color ActiveVertexColor = new Color(0.1f, 0.75f, 1f, 1f);
        public static readonly Color StoredVertexColor = new Color(1f, 0.65f, 0.1f, 1f);
        public static readonly Color GeneratedVertexColor = new Color(0.2f, 1f, 0.45f, 1f);
        public static readonly Color EdgeColor = new Color(0.1f, 0.55f, 0.9f, 0.35f);
        public static readonly Color PlaneFillColor = new Color(0.2f, 0.75f, 1f, 0.16f);
        public static readonly Color PlaneEdgeColor = new Color(0.2f, 0.85f, 1f, 0.85f);

        public static void DrawMeshPreview(
            MeshFilter meshFilter,
            ISet<int> selectedVertices,
            Color selectedColor,
            bool drawEdges,
            float vertexSize)
        {
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return;
            }

            Mesh mesh = meshFilter.sharedMesh;
            Transform transform = meshFilter.transform;
            Vector3[] vertices = mesh.vertices;

            if (drawEdges)
            {
                DrawEdges(mesh, transform);
            }

            for (int i = 0; i < vertices.Length; i++)
            {
                bool selected = selectedVertices != null && selectedVertices.Contains(i);
                Handles.color = selected ? selectedColor : new Color(1f, 1f, 1f, 0.14f);
                float size = selected ? vertexSize * 1.35f : vertexSize;
                Handles.SphereHandleCap(0, transform.TransformPoint(vertices[i]), Quaternion.identity, size, EventType.Repaint);
            }
        }

        public static void DrawPaintedRecords(IReadOnlyList<FPMeshPaintedVertexRecord> records, float vertexSize)
        {
            if (records == null)
            {
                return;
            }

            Handles.color = StoredVertexColor;
            for (int i = 0; i < records.Count; i++)
            {
                Handles.SphereHandleCap(0, records[i].WorldPosition, Quaternion.identity, vertexSize, EventType.Repaint);
            }
        }

        public static void DrawGeneratedPoints(IReadOnlyList<FPMeshGeneratedPointRecord> records, float vertexSize)
        {
            if (records == null)
            {
                return;
            }

            Handles.color = GeneratedVertexColor;
            for (int i = 0; i < records.Count; i++)
            {
                Handles.SphereHandleCap(0, records[i].WorldPosition, Quaternion.identity, vertexSize, EventType.Repaint);
            }
        }

        public static void DrawGeneratedPlane(FPMeshGeneratedPlane plane, float size)
        {
            if (!plane.IsValid)
            {
                return;
            }

            Vector3[] corners = GetPlaneCorners(plane, size);
            Handles.DrawSolidRectangleWithOutline(corners, PlaneFillColor, PlaneEdgeColor);

            Handles.color = PlaneEdgeColor;
            Handles.DrawLine(plane.Origin, plane.Origin + (plane.Normal.normalized * Mathf.Max(0.1f, size * 0.25f)));
        }

        public static void DrawPlanePicks(IReadOnlyList<Vector3> picks, float vertexSize)
        {
            if (picks == null)
            {
                return;
            }

            Handles.color = PlaneEdgeColor;
            for (int i = 0; i < picks.Count; i++)
            {
                Handles.SphereHandleCap(0, picks[i], Quaternion.identity, vertexSize, EventType.Repaint);
                if (i > 0)
                {
                    Handles.DrawLine(picks[i - 1], picks[i]);
                }
            }
        }

        private static void DrawEdges(Mesh mesh, Transform transform)
        {
            int[] triangles = mesh.triangles;
            Handles.color = EdgeColor;

            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                Vector3 a = transform.TransformPoint(mesh.vertices[triangles[i]]);
                Vector3 b = transform.TransformPoint(mesh.vertices[triangles[i + 1]]);
                Vector3 c = transform.TransformPoint(mesh.vertices[triangles[i + 2]]);
                Handles.DrawLine(a, b);
                Handles.DrawLine(b, c);
                Handles.DrawLine(c, a);
            }
        }

        private static Vector3[] GetPlaneCorners(FPMeshGeneratedPlane plane, float size)
        {
            Vector3 right = plane.Right.sqrMagnitude > 0.0001f ? plane.Right.normalized : Vector3.right;
            Vector3 forward = plane.Forward.sqrMagnitude > 0.0001f ? plane.Forward.normalized : Vector3.forward;
            return new[]
            {
                plane.Origin - (right * size) - (forward * size),
                plane.Origin - (right * size) + (forward * size),
                plane.Origin + (right * size) + (forward * size),
                plane.Origin + (right * size) - (forward * size)
            };
        }
    }

}
