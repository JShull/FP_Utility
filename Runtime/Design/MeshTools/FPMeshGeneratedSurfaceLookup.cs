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

    public class FPMeshGeneratedSurfaceLookup : MonoBehaviour
    {
        [SerializeField] protected FPMeshVertexPaintAuthoring sourceAuthoring;
        [SerializeField] protected Mesh generatedMesh;
        [SerializeField] protected List<FPMeshGeneratedSurfaceVertexLookupRecord> vertexLookup = new();
        [SerializeField] protected bool drawDebugGizmos = true;
        [SerializeField] protected bool drawDebugLabels = true;
        [SerializeField] protected float alignmentTolerance = 0.0025f;
        [SerializeField] protected float debugPointSize = 0.035f;

        public FPMeshVertexPaintAuthoring SourceAuthoring => sourceAuthoring;
        public Mesh GeneratedMesh => generatedMesh;
        public IReadOnlyList<FPMeshGeneratedSurfaceVertexLookupRecord> VertexLookup => vertexLookup;
        public bool DrawDebugGizmos => drawDebugGizmos;
        public bool DrawDebugLabels => drawDebugLabels;
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

                Gizmos.color = record.HasEndpoint
                    ? aligned ? new Color(0.25f, 1f, 0.45f, 0.95f) : new Color(1f, 0.18f, 0.1f, 0.95f)
                    : new Color(0.25f, 0.8f, 1f, 0.55f);
                Gizmos.DrawSphere(meshWorld, pointSize);

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
