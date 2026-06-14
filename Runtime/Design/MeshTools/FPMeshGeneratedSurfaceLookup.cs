namespace FuzzPhyte.Utility.MeshTools
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    [Serializable]
    public struct FPMeshGeneratedSurfaceVertexLookupRecord
    {
        public int MeshVertexIndex;
        public int SurfaceIndex;
        public bool HasEndpoint;
        public FPMeshSurfaceEdgeEndpoint Endpoint;
        public Vector3 LocalPosition;
        public Vector3 WorldPosition;
    }

    public enum FPMeshGeneratedSurfaceDebugColorMode
    {
        Alignment = 0,
        PrimaryTag = 1
    }

    public class FPMeshGeneratedSurfaceLookup : MonoBehaviour
    {
        [SerializeField] protected FPMeshVertexPaintAuthoring sourceAuthoring;
        [SerializeField] protected Mesh generatedMesh;
        [SerializeField] protected List<FPMeshGeneratedSurfaceVertexLookupRecord> vertexLookup = new();
        [SerializeField] protected bool drawDebugGizmos = true;
        [SerializeField] protected bool drawDebugLabels = true;
        [SerializeField] protected FPMeshGeneratedSurfaceDebugColorMode debugColorMode = FPMeshGeneratedSurfaceDebugColorMode.PrimaryTag;
        [SerializeField] protected int selectedDebugMeshVertexIndex = -1;
        [SerializeField] protected float alignmentTolerance = 0.0025f;
        [SerializeField] protected float debugPointSize = 0.035f;

        public FPMeshVertexPaintAuthoring SourceAuthoring => sourceAuthoring;
        public Mesh GeneratedMesh => generatedMesh;
        public IReadOnlyList<FPMeshGeneratedSurfaceVertexLookupRecord> VertexLookup => vertexLookup;
        public bool DrawDebugGizmos => drawDebugGizmos;
        public bool DrawDebugLabels => drawDebugLabels;
        public FPMeshGeneratedSurfaceDebugColorMode DebugColorMode => debugColorMode;
        public int SelectedDebugMeshVertexIndex => selectedDebugMeshVertexIndex;
        public float AlignmentTolerance => alignmentTolerance;
        public float DebugPointSize => debugPointSize;

        public bool TryResolveGeneratedMesh(out Mesh mesh)
        {
            if (generatedMesh != null)
            {
                mesh = generatedMesh;
                return true;
            }

            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                generatedMesh = meshFilter.sharedMesh;
                mesh = generatedMesh;
                return true;
            }

            mesh = null;
            return false;
        }

        [ContextMenu("Flip Surface Normals")]
        public bool FlipSurfaceNormals()
        {
            if (!TryResolveGeneratedMesh(out Mesh mesh))
            {
                return false;
            }

            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                int[] triangles = mesh.GetTriangles(subMesh);
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
                }

                mesh.SetTriangles(triangles, subMesh);
            }

            Vector3[] normals = mesh.normals;
            if (normals == null || normals.Length != mesh.vertexCount)
            {
                mesh.RecalculateNormals();
                normals = mesh.normals;
            }

            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = -normals[i];
            }

            mesh.normals = normals;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return true;
        }

        public void SetLookup(
            FPMeshVertexPaintAuthoring authoring,
            Mesh mesh,
            IEnumerable<FPMeshGeneratedSurfaceVertexLookupRecord> records)
        {
            sourceAuthoring = authoring;
            generatedMesh = mesh;
            vertexLookup.Clear();
            if (records != null)
            {
                vertexLookup.AddRange(records);
            }
        }

        public bool TryGetRecordForMeshVertex(int meshVertexIndex, out FPMeshGeneratedSurfaceVertexLookupRecord record)
        {
            for (int i = 0; i < vertexLookup.Count; i++)
            {
                if (vertexLookup[i].MeshVertexIndex == meshVertexIndex)
                {
                    record = vertexLookup[i];
                    return true;
                }
            }

            record = default;
            return false;
        }

        public bool TryFindBySurfaceIndex(int surfaceIndex, out FPMeshGeneratedSurfaceVertexLookupRecord record)
        {
            for (int i = 0; i < vertexLookup.Count; i++)
            {
                if (vertexLookup[i].SurfaceIndex == surfaceIndex)
                {
                    record = vertexLookup[i];
                    return true;
                }
            }

            record = default;
            return false;
        }

        public bool TryResolveEndpointWorldPosition(
            FPMeshGeneratedSurfaceVertexLookupRecord record,
            out Vector3 worldPosition)
        {
            if (!record.HasEndpoint || sourceAuthoring == null)
            {
                worldPosition = record.WorldPosition;
                return false;
            }

            if (record.Endpoint.Kind == FPMeshSurfacePointKind.GeneratedPoint)
            {
                IReadOnlyList<FPMeshGeneratedPointRecord> points = sourceAuthoring.GeneratedPoints;
                if (points != null &&
                    record.Endpoint.GeneratedPointIndex >= 0 &&
                    record.Endpoint.GeneratedPointIndex < points.Count)
                {
                    worldPosition = points[record.Endpoint.GeneratedPointIndex].WorldPosition;
                    return true;
                }

                worldPosition = record.WorldPosition;
                return false;
            }

            IReadOnlyList<MeshFilter> sourceMeshes = sourceAuthoring.SourceMeshes;
            if (sourceMeshes != null &&
                record.Endpoint.SourceMeshIndex >= 0 &&
                record.Endpoint.SourceMeshIndex < sourceMeshes.Count)
            {
                MeshFilter meshFilter = sourceMeshes[record.Endpoint.SourceMeshIndex];
                if (meshFilter != null &&
                    meshFilter.sharedMesh != null &&
                    record.Endpoint.VertexIndex >= 0 &&
                    record.Endpoint.VertexIndex < meshFilter.sharedMesh.vertexCount)
                {
                    worldPosition = meshFilter.transform.TransformPoint(meshFilter.sharedMesh.vertices[record.Endpoint.VertexIndex]);
                    return true;
                }
            }

            worldPosition = record.WorldPosition;
            return false;
        }

        public Vector3 GetCurrentMeshVertexWorldPosition(FPMeshGeneratedSurfaceVertexLookupRecord record)
        {
            return transform.TransformPoint(record.LocalPosition);
        }

        public void SetSelectedDebugMeshVertexIndex(int meshVertexIndex)
        {
            selectedDebugMeshVertexIndex = meshVertexIndex;
        }

        public bool TryResolveRecordTags(FPMeshGeneratedSurfaceVertexLookupRecord record, out FPMeshNavigationTags tags)
        {
            tags = FPMeshNavigationTags.None;
            if (!record.HasEndpoint || sourceAuthoring == null)
            {
                return false;
            }

            if (record.Endpoint.Kind == FPMeshSurfacePointKind.GeneratedPoint)
            {
                IReadOnlyList<FPMeshGeneratedPointRecord> points = sourceAuthoring.GeneratedPoints;
                if (points != null &&
                    record.Endpoint.GeneratedPointIndex >= 0 &&
                    record.Endpoint.GeneratedPointIndex < points.Count)
                {
                    tags = points[record.Endpoint.GeneratedPointIndex].Tags;
                    return tags != FPMeshNavigationTags.None;
                }

                return false;
            }

            IReadOnlyList<FPMeshPaintedVertexRecord> paintedVertices = sourceAuthoring.PaintedVertices;
            if (paintedVertices == null)
            {
                return false;
            }

            for (int i = 0; i < paintedVertices.Count; i++)
            {
                FPMeshPaintedVertexRecord painted = paintedVertices[i];
                if (painted.SourceMeshIndex == record.Endpoint.SourceMeshIndex &&
                    painted.VertexIndex == record.Endpoint.VertexIndex)
                {
                    tags = painted.Tags;
                    return tags != FPMeshNavigationTags.None;
                }
            }

            return false;
        }

        public static FPMeshNavigationTags GetPrimaryTag(FPMeshNavigationTags tags)
        {
            if (tags == FPMeshNavigationTags.None)
            {
                return FPMeshNavigationTags.None;
            }

            int value = (int)tags;
            int firstBit = value & -value;
            return (FPMeshNavigationTags)firstBit;
        }

        public bool TryResolvePrimaryTag(FPMeshGeneratedSurfaceVertexLookupRecord record, out FPMeshNavigationTags tag)
        {
            tag = FPMeshNavigationTags.None;
            if (!TryResolveRecordTags(record, out FPMeshNavigationTags tags))
            {
                return false;
            }

            tag = GetPrimaryTag(tags);
            return tag != FPMeshNavigationTags.None;
        }

        public Color GetDebugColor(FPMeshGeneratedSurfaceVertexLookupRecord record)
        {
            if (!record.HasEndpoint)
            {
                return new Color(0.25f, 0.8f, 1f, 0.55f);
            }

            if (debugColorMode == FPMeshGeneratedSurfaceDebugColorMode.PrimaryTag &&
                TryResolvePrimaryTag(record, out FPMeshNavigationTags tag))
            {
                return GetTagDebugColor(tag);
            }

            bool resolved = TryResolveEndpointWorldPosition(record, out Vector3 dataWorld);
            Vector3 meshWorld = GetCurrentMeshVertexWorldPosition(record);
            float distance = Vector3.Distance(meshWorld, dataWorld);
            bool aligned = resolved && distance <= alignmentTolerance;
            return aligned ? new Color(0.25f, 1f, 0.45f, 0.95f) : new Color(1f, 0.18f, 0.1f, 0.95f);
        }

        public static Color GetTagDebugColor(FPMeshNavigationTags tag)
        {
            switch (GetPrimaryTag(tag))
            {
                case FPMeshNavigationTags.Ground:
                    return new Color(0.25f, 1f, 0.45f, 0.95f);
                case FPMeshNavigationTags.Wall:
                    return new Color(0.35f, 0.62f, 1f, 0.95f);
                case FPMeshNavigationTags.Tree:
                    return new Color(0.1f, 0.7f, 0.22f, 0.95f);
                case FPMeshNavigationTags.Water:
                    return new Color(0.1f, 0.85f, 1f, 0.95f);
                case FPMeshNavigationTags.Air:
                    return new Color(0.86f, 0.92f, 1f, 0.95f);
                case FPMeshNavigationTags.Underground:
                    return new Color(0.48f, 0.28f, 0.14f, 0.95f);
                case FPMeshNavigationTags.Home:
                    return new Color(1f, 0.24f, 0.78f, 0.95f);
                case FPMeshNavigationTags.Food:
                    return new Color(1f, 0.86f, 0.18f, 0.95f);
                case FPMeshNavigationTags.Hazard:
                    return new Color(1f, 0.18f, 0.1f, 0.95f);
                case FPMeshNavigationTags.CustomA:
                    return new Color(0.75f, 0.35f, 1f, 0.95f);
                case FPMeshNavigationTags.CustomB:
                    return new Color(1f, 0.52f, 0.18f, 0.95f);
                default:
                    return new Color(0.75f, 0.75f, 0.75f, 0.75f);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugGizmos || vertexLookup == null)
            {
                return;
            }

            float pointSize = Mathf.Max(0.001f, debugPointSize);
            for (int i = 0; i < vertexLookup.Count; i++)
            {
                FPMeshGeneratedSurfaceVertexLookupRecord record = vertexLookup[i];
                Vector3 meshWorld = GetCurrentMeshVertexWorldPosition(record);
                bool resolved = TryResolveEndpointWorldPosition(record, out Vector3 dataWorld);
                float distance = Vector3.Distance(meshWorld, dataWorld);
                bool aligned = resolved && distance <= alignmentTolerance;

                Gizmos.color = GetDebugColor(record);
                Gizmos.DrawSphere(meshWorld, pointSize);

                if (record.MeshVertexIndex == selectedDebugMeshVertexIndex)
                {
                    Gizmos.color = Color.white;
                    Gizmos.DrawWireSphere(meshWorld, pointSize * 2.6f);
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(meshWorld, pointSize * 3.25f);
                }

                if (!record.HasEndpoint)
                {
                    continue;
                }

                Gizmos.DrawLine(meshWorld, dataWorld);
                Gizmos.color = new Color(1f, 0.86f, 0.18f, 0.95f);
                Gizmos.DrawWireSphere(dataWorld, pointSize * 1.35f);
            }
        }
    }
}
