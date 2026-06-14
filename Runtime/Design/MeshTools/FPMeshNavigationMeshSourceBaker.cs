namespace FuzzPhyte.Utility.MeshTools
{
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;

    public class FPMeshNavigationMeshSourceBaker : Baker<FPMeshNavigationMeshSource>
    {
        public override void Bake(FPMeshNavigationMeshSource authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            Bounds bounds = BuildBounds(authoring.SourceMeshes);

            AddComponent(entity, new FPMeshNavigationSurface
            {
                GraphId = authoring.GraphId,
                Tags = authoring.Tags,
                BuildMode = authoring.BuildMode,
                NavMeshArea = authoring.NavMeshArea,
                VertexWeldDistance = math.max(0f, authoring.VertexWeldDistance),
                LinkSearchRadius = math.max(0f, authoring.LinkSearchRadius),
                IncludeInNavMeshBuild = authoring.IncludeInNavMeshBuild ? (byte)1 : (byte)0,
                InvisibleRuntimeSurface = authoring.InvisibleRuntimeSurface ? (byte)1 : (byte)0
            });

            AddComponent(entity, new FPMeshNavigationMeshBounds
            {
                Center = bounds.center,
                Size = bounds.size
            });
        }

        internal static Bounds BuildBounds(MeshFilter[] meshes)
        {
            bool hasBounds = false;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

            if (meshes == null)
            {
                return bounds;
            }

            for (int i = 0; i < meshes.Length; i++)
            {
                MeshFilter meshFilter = meshes[i];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                Bounds meshBounds = meshFilter.sharedMesh.bounds;
                Vector3 center = meshFilter.transform.TransformPoint(meshBounds.center);
                Vector3 size = Vector3.Scale(meshBounds.size, meshFilter.transform.lossyScale);
                Bounds worldBounds = new Bounds(center, new Vector3(math.abs(size.x), math.abs(size.y), math.abs(size.z)));

                if (!hasBounds)
                {
                    bounds = worldBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(worldBounds);
                }
            }

            return bounds;
        }
    }

    public class FPMeshVertexPaintAuthoringBaker : Baker<FPMeshVertexPaintAuthoring>
    {
        public override void Bake(FPMeshVertexPaintAuthoring authoring)
        {
            Entity graphEntity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(graphEntity, new FPMeshGraphRoot
            {
                GraphId = authoring.GraphId,
                Tags = authoring.DefaultTags,
                NavigationMeshEntity = graphEntity
            });

            DynamicBuffer<FPMeshGraphVertexRef> vertexRefs = AddBuffer<FPMeshGraphVertexRef>(graphEntity);
            int vertexIndex = 0;

            for (int i = 0; i < authoring.PaintedVertices.Count; i++)
            {
                FPMeshPaintedVertexRecord record = authoring.PaintedVertices[i];
                CreateVertex(authoring, graphEntity, vertexRefs, vertexIndex++, record.WorldPosition, record.Normal, record.Tags);
            }

            for (int i = 0; i < authoring.GeneratedPoints.Count; i++)
            {
                FPMeshGeneratedPointRecord record = authoring.GeneratedPoints[i];
                CreateVertex(authoring, graphEntity, vertexRefs, vertexIndex++, record.WorldPosition, record.Normal, record.Tags);
            }
        }

        private void CreateVertex(
            FPMeshVertexPaintAuthoring authoring,
            Entity graphEntity,
            DynamicBuffer<FPMeshGraphVertexRef> vertexRefs,
            int vertexIndex,
            float3 position,
            float3 normal,
            FPMeshNavigationTags tags)
        {
            Entity vertexEntity = CreateAdditionalEntity(TransformUsageFlags.None, false, $"Mesh Vertex {vertexIndex}");
            AddComponent(vertexEntity, new FPMeshVertex
            {
                Address = new FPMeshVertexAddress(authoring.GraphId, vertexIndex),
                Position = position,
                Normal = normal,
                Tags = tags,
                GraphRoot = graphEntity
            });

            AddBuffer<FPMeshVertexNeighbor>(vertexEntity);
            vertexRefs.Add(new FPMeshGraphVertexRef { Vertex = vertexEntity });
        }
    }
}
