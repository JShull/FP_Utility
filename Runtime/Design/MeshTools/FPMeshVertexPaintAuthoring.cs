namespace FuzzPhyte.Utility.MeshTools
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    [Serializable]
    public struct FPMeshPaintedVertexRecord
    {
        public int SurfaceIndex;
        public int SourceMeshIndex;
        public int VertexIndex;
        public Vector3 LocalPosition;
        public Vector3 WorldPosition;
        public Vector3 Normal;
        public FPMeshNavigationTags Tags;
    }

    [Serializable]
    public struct FPMeshPaintedLinkRecord
    {
        public int StartRecordIndex;
        public int EndRecordIndex;
        public float Cost;
        public FPMeshVertexLinkKind LinkKind;
    }

    [Serializable]
    public struct FPMeshGeneratedPlane
    {
        public bool IsValid;
        public bool HasAnchorPoints;
        public Vector3 Origin;
        public Vector3 Right;
        public Vector3 Forward;
        public Vector3 Normal;
        public Color DisplayColor;
        public FPMeshSurfaceEdgeEndpoint AnchorA;
        public FPMeshSurfaceEdgeEndpoint AnchorB;
        public FPMeshSurfaceEdgeEndpoint AnchorC;
    }

    [Serializable]
    public struct FPMeshGeneratedPointRecord
    {
        public int SurfaceIndex;
        public Vector3 WorldPosition;
        public Vector3 Normal;
        public FPMeshNavigationTags Tags;
    }

    public enum FPMeshSurfacePointKind
    {
        SourceVertex = 0,
        GeneratedPoint = 1
    }

    [Serializable]
    public struct FPMeshSurfaceEdgeEndpoint : IEquatable<FPMeshSurfaceEdgeEndpoint>
    {
        public FPMeshSurfacePointKind Kind;
        public int SourceMeshIndex;
        public int VertexIndex;
        public int GeneratedPointIndex;

        public static FPMeshSurfaceEdgeEndpoint Source(int sourceMeshIndex, int vertexIndex)
        {
            return new FPMeshSurfaceEdgeEndpoint
            {
                Kind = FPMeshSurfacePointKind.SourceVertex,
                SourceMeshIndex = sourceMeshIndex,
                VertexIndex = vertexIndex,
                GeneratedPointIndex = -1
            };
        }

        public static FPMeshSurfaceEdgeEndpoint Generated(int generatedPointIndex)
        {
            return new FPMeshSurfaceEdgeEndpoint
            {
                Kind = FPMeshSurfacePointKind.GeneratedPoint,
                SourceMeshIndex = -1,
                VertexIndex = -1,
                GeneratedPointIndex = generatedPointIndex
            };
        }

        public bool Equals(FPMeshSurfaceEdgeEndpoint other)
        {
            return Kind == other.Kind &&
                SourceMeshIndex == other.SourceMeshIndex &&
                VertexIndex == other.VertexIndex &&
                GeneratedPointIndex == other.GeneratedPointIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is FPMeshSurfaceEdgeEndpoint other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (int)Kind;
                hash = (hash * 31) + SourceMeshIndex;
                hash = (hash * 31) + VertexIndex;
                hash = (hash * 31) + GeneratedPointIndex;
                return hash;
            }
        }

        public static bool operator ==(FPMeshSurfaceEdgeEndpoint left, FPMeshSurfaceEdgeEndpoint right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(FPMeshSurfaceEdgeEndpoint left, FPMeshSurfaceEdgeEndpoint right)
        {
            return !left.Equals(right);
        }
    }

    [Serializable]
    public struct FPMeshGeneratedEdgeRecord
    {
        public FPMeshSurfaceEdgeEndpoint Start;
        public FPMeshSurfaceEdgeEndpoint End;
        [HideInInspector] public int StartPointIndex;
        [HideInInspector] public int EndPointIndex;

        public static FPMeshGeneratedEdgeRecord Create(FPMeshSurfaceEdgeEndpoint start, FPMeshSurfaceEdgeEndpoint end)
        {
            return new FPMeshGeneratedEdgeRecord
            {
                Start = start,
                End = end,
                StartPointIndex = -1,
                EndPointIndex = -1
            };
        }
    }

    [Serializable]
    public struct FPMeshGeneratedTriangleRecord
    {
        public FPMeshSurfaceEdgeEndpoint A;
        public FPMeshSurfaceEdgeEndpoint B;
        public FPMeshSurfaceEdgeEndpoint C;

        public static FPMeshGeneratedTriangleRecord Create(
            FPMeshSurfaceEdgeEndpoint a,
            FPMeshSurfaceEdgeEndpoint b,
            FPMeshSurfaceEdgeEndpoint c)
        {
            return new FPMeshGeneratedTriangleRecord
            {
                A = a,
                B = b,
                C = c
            };
        }
    }

    public class FPMeshVertexPaintAuthoring : MonoBehaviour
    {
        [SerializeField] protected int graphId;
        [SerializeField] protected int nextSurfaceIndex;
        [SerializeField] protected FPMeshNavigationTags defaultTags = FPMeshNavigationTags.Ground;
        [SerializeField] protected MeshFilter[] sourceMeshes = Array.Empty<MeshFilter>();
        [SerializeField] protected int activeMeshIndex;
        [SerializeField] protected FPMeshGeneratedPlane generatedPlane;
        [SerializeField] protected List<FPMeshGeneratedPlane> generatedPlanes = new();
        [SerializeField] protected int selectedGeneratedPlaneIndex = -1;
        [SerializeField] protected List<FPMeshPaintedVertexRecord> paintedVertices = new();
        [SerializeField] protected List<FPMeshPaintedLinkRecord> paintedLinks = new();
        [SerializeField] protected List<FPMeshGeneratedPointRecord> generatedPoints = new();
        [SerializeField] protected List<FPMeshGeneratedEdgeRecord> generatedEdges = new();
        [SerializeField] protected List<FPMeshGeneratedTriangleRecord> generatedTriangles = new();
        [SerializeField] protected bool hasSelectedSurfacePoint;
        [SerializeField] protected FPMeshSurfaceEdgeEndpoint selectedSurfacePoint;

        public int GraphId => graphId;
        public int NextSurfaceIndex => nextSurfaceIndex;
        public FPMeshNavigationTags DefaultTags => defaultTags;
        public IReadOnlyList<MeshFilter> SourceMeshes => sourceMeshes;
        public int ActiveMeshIndex => activeMeshIndex;
        public FPMeshGeneratedPlane GeneratedPlane => HasSelectedGeneratedPlane ? generatedPlanes[selectedGeneratedPlaneIndex] : generatedPlane;
        public IReadOnlyList<FPMeshGeneratedPlane> GeneratedPlanes => generatedPlanes;
        public int SelectedGeneratedPlaneIndex => selectedGeneratedPlaneIndex;
        public bool HasSelectedGeneratedPlane => selectedGeneratedPlaneIndex >= 0 &&
            selectedGeneratedPlaneIndex < generatedPlanes.Count &&
            generatedPlanes[selectedGeneratedPlaneIndex].IsValid;
        public IReadOnlyList<FPMeshPaintedVertexRecord> PaintedVertices => paintedVertices;
        public IReadOnlyList<FPMeshPaintedLinkRecord> PaintedLinks => paintedLinks;
        public IReadOnlyList<FPMeshGeneratedPointRecord> GeneratedPoints => generatedPoints;
        public IReadOnlyList<FPMeshGeneratedEdgeRecord> GeneratedEdges => generatedEdges;
        public IReadOnlyList<FPMeshGeneratedTriangleRecord> GeneratedTriangles => generatedTriangles;
        public bool HasSelectedSurfacePoint => hasSelectedSurfacePoint;
        public FPMeshSurfaceEdgeEndpoint SelectedSurfacePoint => selectedSurfacePoint;

        public void SetSourceMeshes(IReadOnlyList<MeshFilter> meshes, int activeIndex)
        {
            if (meshes == null)
            {
                sourceMeshes = Array.Empty<MeshFilter>();
                activeMeshIndex = 0;
                return;
            }

            sourceMeshes = new MeshFilter[meshes.Count];
            for (int i = 0; i < meshes.Count; i++)
            {
                sourceMeshes[i] = meshes[i];
            }

            activeMeshIndex = Mathf.Clamp(activeIndex, 0, Mathf.Max(0, sourceMeshes.Length - 1));
        }

        public void SetDefaultTags(FPMeshNavigationTags tags)
        {
            defaultTags = tags;
        }

        public void SetPaintedVertices(IEnumerable<FPMeshPaintedVertexRecord> records)
        {
            paintedVertices.Clear();
            if (records == null)
            {
                return;
            }

            paintedVertices.AddRange(records);
            NormalizeSurfaceIndices();
        }

        public void SetPaintedLinks(IEnumerable<FPMeshPaintedLinkRecord> records)
        {
            paintedLinks.Clear();
            if (records == null)
            {
                return;
            }

            paintedLinks.AddRange(records);
        }

        public void SetGeneratedPlane(FPMeshGeneratedPlane plane)
        {
            generatedPlane = plane;
            if (selectedGeneratedPlaneIndex >= 0 && selectedGeneratedPlaneIndex < generatedPlanes.Count)
            {
                generatedPlanes[selectedGeneratedPlaneIndex] = plane;
            }
        }

        public int AddGeneratedPlane(FPMeshGeneratedPlane plane)
        {
            generatedPlanes.Add(plane);
            selectedGeneratedPlaneIndex = generatedPlanes.Count - 1;
            generatedPlane = plane;
            return selectedGeneratedPlaneIndex;
        }

        public bool SetSelectedGeneratedPlaneIndex(int index)
        {
            if (index < 0 || index >= generatedPlanes.Count || !generatedPlanes[index].IsValid)
            {
                selectedGeneratedPlaneIndex = -1;
                generatedPlane = default;
                return false;
            }

            selectedGeneratedPlaneIndex = index;
            generatedPlane = generatedPlanes[index];
            return true;
        }

        public bool RemoveGeneratedPlaneAt(int index)
        {
            if (index < 0 || index >= generatedPlanes.Count)
            {
                return false;
            }

            generatedPlanes.RemoveAt(index);
            if (generatedPlanes.Count == 0)
            {
                selectedGeneratedPlaneIndex = -1;
                generatedPlane = default;
                return true;
            }

            selectedGeneratedPlaneIndex = Mathf.Clamp(index, 0, generatedPlanes.Count - 1);
            generatedPlane = generatedPlanes[selectedGeneratedPlaneIndex];
            return true;
        }

        public void AddGeneratedPoint(FPMeshGeneratedPointRecord point)
        {
            if (point.SurfaceIndex < 0 || SurfaceIndexInUse(point.SurfaceIndex))
            {
                point.SurfaceIndex = ReserveSurfaceIndex();
            }
            else
            {
                EnsureNextSurfaceIndexPast(point.SurfaceIndex);
            }

            generatedPoints.Add(point);
            SetSelectedSurfacePoint(FPMeshSurfaceEdgeEndpoint.Generated(generatedPoints.Count - 1));
        }

        public bool RemoveGeneratedPointAt(int index)
        {
            if (index < 0 || index >= generatedPoints.Count)
            {
                return false;
            }

            generatedPoints.RemoveAt(index);
            if (hasSelectedSurfacePoint && ReferencesGeneratedPoint(selectedSurfacePoint, index))
            {
                ClearSelectedSurfacePoint();
            }
            else if (hasSelectedSurfacePoint)
            {
                selectedSurfacePoint = DecrementGeneratedEndpointAfterRemove(selectedSurfacePoint, index);
            }

            for (int i = generatedEdges.Count - 1; i >= 0; i--)
            {
                FPMeshGeneratedEdgeRecord edge = generatedEdges[i];
                FPMeshSurfaceEdgeEndpoint start = ResolveStartEndpoint(edge);
                FPMeshSurfaceEdgeEndpoint end = ResolveEndEndpoint(edge);
                if (ReferencesGeneratedPoint(start, index) || ReferencesGeneratedPoint(end, index))
                {
                    generatedEdges.RemoveAt(i);
                    RemoveGeneratedTrianglesUsingEdge(start, end);
                    continue;
                }

                if (start.Kind == FPMeshSurfacePointKind.GeneratedPoint && start.GeneratedPointIndex > index)
                {
                    start.GeneratedPointIndex--;
                }

                if (end.Kind == FPMeshSurfacePointKind.GeneratedPoint && end.GeneratedPointIndex > index)
                {
                    end.GeneratedPointIndex--;
                }

                generatedEdges[i] = FPMeshGeneratedEdgeRecord.Create(start, end);
            }

            for (int i = generatedTriangles.Count - 1; i >= 0; i--)
            {
                FPMeshGeneratedTriangleRecord triangle = generatedTriangles[i];
                if (ReferencesGeneratedPoint(triangle.A, index) ||
                    ReferencesGeneratedPoint(triangle.B, index) ||
                    ReferencesGeneratedPoint(triangle.C, index))
                {
                    generatedTriangles.RemoveAt(i);
                    continue;
                }

                triangle.A = DecrementGeneratedEndpointAfterRemove(triangle.A, index);
                triangle.B = DecrementGeneratedEndpointAfterRemove(triangle.B, index);
                triangle.C = DecrementGeneratedEndpointAfterRemove(triangle.C, index);
                generatedTriangles[i] = triangle;
            }

            return true;
        }

        public bool AddGeneratedEdge(FPMeshGeneratedEdgeRecord edge)
        {
            FPMeshSurfaceEdgeEndpoint start = ResolveStartEndpoint(edge);
            FPMeshSurfaceEdgeEndpoint end = ResolveEndEndpoint(edge);
            if (!IsValidEdgeEndpoint(start) || !IsValidEdgeEndpoint(end) || start == end)
            {
                return false;
            }

            for (int i = 0; i < generatedEdges.Count; i++)
            {
                FPMeshGeneratedEdgeRecord current = generatedEdges[i];
                FPMeshSurfaceEdgeEndpoint currentStart = ResolveStartEndpoint(current);
                FPMeshSurfaceEdgeEndpoint currentEnd = ResolveEndEndpoint(current);
                if ((currentStart == start && currentEnd == end) ||
                    (currentStart == end && currentEnd == start))
                {
                    return false;
                }
            }

            generatedEdges.Add(FPMeshGeneratedEdgeRecord.Create(start, end));
            return true;
        }

        public bool RemoveGeneratedEdgeAt(int index)
        {
            if (index < 0 || index >= generatedEdges.Count)
            {
                return false;
            }

            FPMeshSurfaceEdgeEndpoint start = ResolveStartEndpoint(generatedEdges[index]);
            FPMeshSurfaceEdgeEndpoint end = ResolveEndEndpoint(generatedEdges[index]);
            generatedEdges.RemoveAt(index);
            RemoveGeneratedTrianglesUsingEdge(start, end);
            return true;
        }

        public void ClearGeneratedEdges()
        {
            generatedEdges.Clear();
            generatedTriangles.Clear();
        }

        public bool AddGeneratedTriangle(FPMeshGeneratedTriangleRecord triangle)
        {
            if (!IsValidTriangle(triangle) ||
                !HasGeneratedEdge(triangle.A, triangle.B) ||
                !HasGeneratedEdge(triangle.B, triangle.C) ||
                !HasGeneratedEdge(triangle.C, triangle.A))
            {
                return false;
            }

            for (int i = 0; i < generatedTriangles.Count; i++)
            {
                if (TrianglesMatch(generatedTriangles[i], triangle))
                {
                    return false;
                }
            }

            generatedTriangles.Add(triangle);
            return true;
        }

        public bool RemoveGeneratedTriangleAt(int index)
        {
            if (index < 0 || index >= generatedTriangles.Count)
            {
                return false;
            }

            generatedTriangles.RemoveAt(index);
            return true;
        }

        public void ClearGeneratedTriangles()
        {
            generatedTriangles.Clear();
        }

        public void ClearTransientGeneratedPoints()
        {
            generatedPoints.Clear();
            generatedEdges.Clear();
            generatedTriangles.Clear();
            ClearSelectedSurfacePoint();
        }

        public void ClearPaintedData()
        {
            paintedVertices.Clear();
            paintedLinks.Clear();
            generatedPoints.Clear();
            generatedEdges.Clear();
            generatedTriangles.Clear();
            generatedPlane = default;
            generatedPlanes.Clear();
            selectedGeneratedPlaneIndex = -1;
            nextSurfaceIndex = 0;
            ClearSelectedSurfacePoint();
        }

        public int ReserveSurfaceIndex()
        {
            return nextSurfaceIndex++;
        }

        public void EnsureNextSurfaceIndexPast(int surfaceIndex)
        {
            if (surfaceIndex >= nextSurfaceIndex)
            {
                nextSurfaceIndex = surfaceIndex + 1;
            }
        }

        public bool TryGetSurfaceIndex(FPMeshSurfaceEdgeEndpoint endpoint, out int surfaceIndex)
        {
            if (endpoint.Kind == FPMeshSurfacePointKind.GeneratedPoint)
            {
                if (endpoint.GeneratedPointIndex >= 0 && endpoint.GeneratedPointIndex < generatedPoints.Count)
                {
                    surfaceIndex = generatedPoints[endpoint.GeneratedPointIndex].SurfaceIndex;
                    return surfaceIndex >= 0;
                }

                surfaceIndex = -1;
                return false;
            }

            for (int i = 0; i < paintedVertices.Count; i++)
            {
                FPMeshPaintedVertexRecord record = paintedVertices[i];
                if (record.SourceMeshIndex == endpoint.SourceMeshIndex && record.VertexIndex == endpoint.VertexIndex)
                {
                    surfaceIndex = record.SurfaceIndex;
                    return surfaceIndex >= 0;
                }
            }

            surfaceIndex = -1;
            return false;
        }

        public void NormalizeSurfaceIndices()
        {
            HashSet<int> used = new();
            for (int i = 0; i < paintedVertices.Count; i++)
            {
                FPMeshPaintedVertexRecord record = paintedVertices[i];
                if (record.SurfaceIndex < 0 || used.Contains(record.SurfaceIndex))
                {
                    record.SurfaceIndex = ReserveUnusedSurfaceIndex(used);
                    paintedVertices[i] = record;
                }
                else
                {
                    used.Add(record.SurfaceIndex);
                    EnsureNextSurfaceIndexPast(record.SurfaceIndex);
                }
            }

            for (int i = 0; i < generatedPoints.Count; i++)
            {
                FPMeshGeneratedPointRecord point = generatedPoints[i];
                if (point.SurfaceIndex < 0 || used.Contains(point.SurfaceIndex))
                {
                    point.SurfaceIndex = ReserveUnusedSurfaceIndex(used);
                    generatedPoints[i] = point;
                }
                else
                {
                    used.Add(point.SurfaceIndex);
                    EnsureNextSurfaceIndexPast(point.SurfaceIndex);
                }
            }
        }

        public void SetSelectedSurfacePoint(FPMeshSurfaceEdgeEndpoint endpoint)
        {
            selectedSurfacePoint = endpoint;
            hasSelectedSurfacePoint = true;
        }

        public void ClearSelectedSurfacePoint()
        {
            selectedSurfacePoint = default;
            hasSelectedSurfacePoint = false;
        }

        public FPMeshSurfaceEdgeEndpoint ResolveStartEndpoint(FPMeshGeneratedEdgeRecord edge)
        {
            return edge.StartPointIndex >= 0 && edge.EndPointIndex >= 0
                ? FPMeshSurfaceEdgeEndpoint.Generated(edge.StartPointIndex)
                : edge.Start;
        }

        public FPMeshSurfaceEdgeEndpoint ResolveEndEndpoint(FPMeshGeneratedEdgeRecord edge)
        {
            return edge.StartPointIndex >= 0 && edge.EndPointIndex >= 0
                ? FPMeshSurfaceEdgeEndpoint.Generated(edge.EndPointIndex)
                : edge.End;
        }

        private bool IsValidEdgeEndpoint(FPMeshSurfaceEdgeEndpoint endpoint)
        {
            if (endpoint.Kind == FPMeshSurfacePointKind.GeneratedPoint)
            {
                return endpoint.GeneratedPointIndex >= 0 && endpoint.GeneratedPointIndex < generatedPoints.Count;
            }

            if (endpoint.SourceMeshIndex < 0 || endpoint.SourceMeshIndex >= sourceMeshes.Length)
            {
                return false;
            }

            MeshFilter sourceMesh = sourceMeshes[endpoint.SourceMeshIndex];
            return sourceMesh != null &&
                sourceMesh.sharedMesh != null &&
                endpoint.VertexIndex >= 0 &&
                endpoint.VertexIndex < sourceMesh.sharedMesh.vertexCount;
        }

        private static bool ReferencesGeneratedPoint(FPMeshSurfaceEdgeEndpoint endpoint, int generatedPointIndex)
        {
            return endpoint.Kind == FPMeshSurfacePointKind.GeneratedPoint &&
                endpoint.GeneratedPointIndex == generatedPointIndex;
        }

        private static FPMeshSurfaceEdgeEndpoint DecrementGeneratedEndpointAfterRemove(FPMeshSurfaceEdgeEndpoint endpoint, int removedIndex)
        {
            if (endpoint.Kind == FPMeshSurfacePointKind.GeneratedPoint && endpoint.GeneratedPointIndex > removedIndex)
            {
                endpoint.GeneratedPointIndex--;
            }

            return endpoint;
        }

        private int ReserveUnusedSurfaceIndex(HashSet<int> used)
        {
            while (used.Contains(nextSurfaceIndex))
            {
                nextSurfaceIndex++;
            }

            int surfaceIndex = nextSurfaceIndex++;
            used.Add(surfaceIndex);
            return surfaceIndex;
        }

        private bool SurfaceIndexInUse(int surfaceIndex)
        {
            for (int i = 0; i < paintedVertices.Count; i++)
            {
                if (paintedVertices[i].SurfaceIndex == surfaceIndex)
                {
                    return true;
                }
            }

            for (int i = 0; i < generatedPoints.Count; i++)
            {
                if (generatedPoints[i].SurfaceIndex == surfaceIndex)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsValidTriangle(FPMeshGeneratedTriangleRecord triangle)
        {
            return IsValidEdgeEndpoint(triangle.A) &&
                IsValidEdgeEndpoint(triangle.B) &&
                IsValidEdgeEndpoint(triangle.C) &&
                triangle.A != triangle.B &&
                triangle.B != triangle.C &&
                triangle.C != triangle.A;
        }

        private bool HasGeneratedEdge(FPMeshSurfaceEdgeEndpoint a, FPMeshSurfaceEdgeEndpoint b)
        {
            for (int i = 0; i < generatedEdges.Count; i++)
            {
                FPMeshSurfaceEdgeEndpoint start = ResolveStartEndpoint(generatedEdges[i]);
                FPMeshSurfaceEdgeEndpoint end = ResolveEndEndpoint(generatedEdges[i]);
                if ((start == a && end == b) || (start == b && end == a))
                {
                    return true;
                }
            }

            return false;
        }

        private void RemoveGeneratedTrianglesUsingEdge(FPMeshSurfaceEdgeEndpoint a, FPMeshSurfaceEdgeEndpoint b)
        {
            for (int i = generatedTriangles.Count - 1; i >= 0; i--)
            {
                FPMeshGeneratedTriangleRecord triangle = generatedTriangles[i];
                if (TriangleContainsEdge(triangle, a, b))
                {
                    generatedTriangles.RemoveAt(i);
                }
            }
        }

        private static bool TriangleContainsEdge(FPMeshGeneratedTriangleRecord triangle, FPMeshSurfaceEdgeEndpoint a, FPMeshSurfaceEdgeEndpoint b)
        {
            return EndpointPairMatches(triangle.A, triangle.B, a, b) ||
                EndpointPairMatches(triangle.B, triangle.C, a, b) ||
                EndpointPairMatches(triangle.C, triangle.A, a, b);
        }

        private static bool TrianglesMatch(FPMeshGeneratedTriangleRecord left, FPMeshGeneratedTriangleRecord right)
        {
            return TriangleContainsEndpoint(left, right.A) &&
                TriangleContainsEndpoint(left, right.B) &&
                TriangleContainsEndpoint(left, right.C);
        }

        private static bool TriangleContainsEndpoint(FPMeshGeneratedTriangleRecord triangle, FPMeshSurfaceEdgeEndpoint endpoint)
        {
            return triangle.A == endpoint || triangle.B == endpoint || triangle.C == endpoint;
        }

        private static bool EndpointPairMatches(
            FPMeshSurfaceEdgeEndpoint leftA,
            FPMeshSurfaceEdgeEndpoint leftB,
            FPMeshSurfaceEdgeEndpoint rightA,
            FPMeshSurfaceEdgeEndpoint rightB)
        {
            return (leftA == rightA && leftB == rightB) ||
                (leftA == rightB && leftB == rightA);
        }
    }

}
