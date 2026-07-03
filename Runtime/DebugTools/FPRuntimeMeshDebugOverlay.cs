// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

#if UNITY_EDITOR
namespace FuzzPhyte.Utility.DebugTools
{
    using UnityEngine;

    /// <summary>
    /// Editor-only runtime mesh overlay that renders vertices, edges, normals, and bounds into cameras.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class FPRuntimeMeshDebugOverlay : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private Mesh overrideMesh;

        [Header("Visibility")]
        [SerializeField] private bool drawInEditMode;
        [SerializeField] private bool drawEdges = true;
        [SerializeField] private bool drawVertices = true;
        [SerializeField] private bool drawNormals;
        [SerializeField] private bool drawBounds;
        [SerializeField] private bool depthTest = true;

        [Header("Colors")]
        [SerializeField] private Color edgeColor = new Color(0.1f, 0.55f, 0.9f, 0.45f);
        [SerializeField] private Color vertexColor = new Color(0.15f, 0.85f, 1f, 0.9f);
        [SerializeField] private Color normalColor = new Color(0.9f, 0.95f, 1f, 0.7f);
        [SerializeField] private Color boundsColor = new Color(1f, 0.86f, 0.18f, 0.75f);

        [Header("Camera Relative Handles")]
        [SerializeField] private bool cameraRelativeVertexSize = true;
        [SerializeField] private float vertexRadius = 0.025f;
        [SerializeField] private float cameraRelativeVertexScale = 0.006f;
        [SerializeField] private Vector2 cameraRelativeVertexSizeRange = new Vector2(0.015f, 0.16f);
        [SerializeField] private float normalLength = 0.08f;
        [SerializeField] private bool cameraRelativeNormalLength = true;
        [SerializeField] private float cameraRelativeNormalScale = 0.02f;
        [SerializeField] private Vector2 cameraRelativeNormalLengthRange = new Vector2(0.04f, 0.4f);

        public MeshFilter MeshFilter
        {
            get => meshFilter;
            set => meshFilter = value;
        }

        public Mesh OverrideMesh
        {
            get => overrideMesh;
            set => overrideMesh = value;
        }

        public bool DrawEdges
        {
            get => drawEdges;
            set => drawEdges = value;
        }

        public bool DrawVertices
        {
            get => drawVertices;
            set => drawVertices = value;
        }

        public bool DrawNormals
        {
            get => drawNormals;
            set => drawNormals = value;
        }

        public bool DrawBounds
        {
            get => drawBounds;
            set => drawBounds = value;
        }

        public bool DepthTest
        {
            get => depthTest;
            set => depthTest = value;
        }

        private void Reset()
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        private void OnEnable()
        {
            _ = FPRuntimeDebugDraw.Instance;
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying && !drawInEditMode)
            {
                return;
            }

            DrawOverlay();
        }

        [ContextMenu("Draw One Frame")]
        public void DrawOverlay()
        {
            Mesh mesh = ResolveMesh();
            if (mesh == null)
            {
                return;
            }

            Matrix4x4 localToWorld = ResolveLocalToWorld();

            if (drawEdges)
            {
                FPRuntimeDebugDraw.DrawMeshEdges(mesh, localToWorld, edgeColor, 0f, depthTest);
            }

            if (drawVertices)
            {
                DrawVerticesInternal(mesh, localToWorld);
            }

            if (drawNormals)
            {
                DrawNormalsInternal(mesh, localToWorld);
            }

            if (drawBounds)
            {
                Bounds bounds = mesh.bounds;
                FPRuntimeDebugDraw.DrawWireBox(
                    localToWorld.MultiplyPoint3x4(bounds.center),
                    ResolveWorldRotation(),
                    Vector3.Scale(bounds.size, Abs(ResolveWorldScale())),
                    boundsColor,
                    0f,
                    depthTest);
            }
        }

        private Mesh ResolveMesh()
        {
            if (overrideMesh != null)
            {
                return overrideMesh;
            }

            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
            }

            return meshFilter != null ? meshFilter.sharedMesh : null;
        }

        private Matrix4x4 ResolveLocalToWorld()
        {
            Transform sourceTransform = meshFilter != null && overrideMesh == null ? meshFilter.transform : transform;
            return sourceTransform.localToWorldMatrix;
        }

        private Quaternion ResolveWorldRotation()
        {
            Transform sourceTransform = meshFilter != null && overrideMesh == null ? meshFilter.transform : transform;
            return sourceTransform.rotation;
        }

        private Vector3 ResolveWorldScale()
        {
            Transform sourceTransform = meshFilter != null && overrideMesh == null ? meshFilter.transform : transform;
            return sourceTransform.lossyScale;
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private void DrawVerticesInternal(Mesh mesh, Matrix4x4 localToWorld)
        {
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                FPRuntimeDebugDraw.DrawPoint(
                    localToWorld.MultiplyPoint3x4(vertices[i]),
                    vertexRadius,
                    vertexColor,
                    0f,
                    depthTest,
                    cameraRelativeVertexSize,
                    cameraRelativeVertexScale,
                    cameraRelativeVertexSizeRange);
            }
        }

        private void DrawNormalsInternal(Mesh mesh, Matrix4x4 localToWorld)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            if (normals == null || normals.Length != vertices.Length)
            {
                return;
            }

            Matrix4x4 normalMatrix = localToWorld.inverse.transpose;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 start = localToWorld.MultiplyPoint3x4(vertices[i]);
                Vector3 normal = normalMatrix.MultiplyVector(normals[i]).normalized;
                if (cameraRelativeNormalLength)
                {
                    FPRuntimeDebugDraw.DrawCameraRelativeRay(
                        start,
                        normal,
                        normalLength,
                        normalColor,
                        0f,
                        depthTest,
                        cameraRelativeNormalScale,
                        cameraRelativeNormalLengthRange);
                }
                else
                {
                    FPRuntimeDebugDraw.DrawLine(start, start + normal * Mathf.Max(0.0001f, normalLength), normalColor, 0f, depthTest);
                }
            }
        }
    }
}
#endif
