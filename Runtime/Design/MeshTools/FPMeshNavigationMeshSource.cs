namespace FuzzPhyte.Utility.MeshTools
{
    using System;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;

    [Flags]
    public enum FPMeshNavigationTags
    {
        None = 0,
        Ground = 1 << 0,
        Wall = 1 << 1,
        Tree = 1 << 2,
        Water = 1 << 3,
        Air = 1 << 4,
        Underground = 1 << 5,
        Home = 1 << 6,
        Food = 1 << 7,
        Hazard = 1 << 8,
        CustomA = 1 << 9,
        CustomB = 1 << 10
    }

    public enum FPMeshNavigationBuildMode
    {
        SeparateSurfaces = 0,
        CombinedInvisibleMesh = 1
    }

    public enum FPMeshVertexLinkKind
    {
        MeshTopology = 0,
        WeldedVertex = 1,
        OffMeshLink = 2,
        NavMeshProjection = 3,
        Painted = 4,
        Generated = 5
    }

    [Serializable]
    public struct FPMeshVertexAddress : IEquatable<FPMeshVertexAddress>
    {
        public int GraphId;
        public int VertexIndex;

        public FPMeshVertexAddress(int graphId, int vertexIndex)
        {
            GraphId = graphId;
            VertexIndex = vertexIndex;
        }

        public bool IsValid => GraphId >= 0 && VertexIndex >= 0;

        public bool Equals(FPMeshVertexAddress other)
        {
            return GraphId == other.GraphId && VertexIndex == other.VertexIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is FPMeshVertexAddress other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (GraphId * 397) ^ VertexIndex;
            }
        }
    }

    public class FPMeshNavigationMeshSource : MonoBehaviour
    {
        [SerializeField] protected int graphId;
        [SerializeField] protected FPMeshNavigationTags tags = FPMeshNavigationTags.Ground;
        [SerializeField] protected FPMeshNavigationBuildMode buildMode = FPMeshNavigationBuildMode.SeparateSurfaces;
        [SerializeField] protected int navMeshArea;
        [SerializeField] protected float vertexWeldDistance = 0.05f;
        [SerializeField] protected float linkSearchRadius = 0.25f;
        [SerializeField] protected bool includeInNavMeshBuild = true;
        [SerializeField] protected bool invisibleRuntimeSurface = true;
        [SerializeField] protected MeshFilter[] sourceMeshes = Array.Empty<MeshFilter>();

        public int GraphId => graphId;
        public FPMeshNavigationTags Tags => tags;
        public FPMeshNavigationBuildMode BuildMode => buildMode;
        public int NavMeshArea => navMeshArea;
        public float VertexWeldDistance => vertexWeldDistance;
        public float LinkSearchRadius => linkSearchRadius;
        public bool IncludeInNavMeshBuild => includeInNavMeshBuild;
        public bool InvisibleRuntimeSurface => invisibleRuntimeSurface;
        public MeshFilter[] SourceMeshes => sourceMeshes;
    }

    public struct FPMeshNavigationSurface : IComponentData
    {
        public int GraphId;
        public FPMeshNavigationTags Tags;
        public FPMeshNavigationBuildMode BuildMode;
        public int NavMeshArea;
        public float VertexWeldDistance;
        public float LinkSearchRadius;
        public byte IncludeInNavMeshBuild;
        public byte InvisibleRuntimeSurface;
    }

    public struct FPMeshNavigationMeshBounds : IComponentData
    {
        public float3 Center;
        public float3 Size;
    }

    public struct FPMeshGraphRoot : IComponentData
    {
        public int GraphId;
        public FPMeshNavigationTags Tags;
        public Entity NavigationMeshEntity;
    }

    public struct FPMeshVertex : IComponentData
    {
        public FPMeshVertexAddress Address;
        public float3 Position;
        public float3 Normal;
        public FPMeshNavigationTags Tags;
        public Entity GraphRoot;
    }

    public struct FPMeshVertexNeighbor : IBufferElementData
    {
        public Entity Vertex;
        public float Cost;
        public FPMeshVertexLinkKind LinkKind;
    }

    public struct FPMeshGraphVertexRef : IBufferElementData
    {
        public Entity Vertex;
    }
}
