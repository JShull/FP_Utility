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
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Rendering;

    /// <summary>
    /// Editor-only runtime debug renderer for drawing gizmo-like primitives into cameras.
    /// Call the static Draw methods from Update/LateUpdate or pass a duration for retained debug marks.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(10000)]
    public sealed class FPRuntimeDebugDraw : MonoBehaviour
    {
        private enum CommandKind
        {
            Line,
            Ray,
            Point,
            Circle,
            Sphere,
            Box,
            Plane
        }

        private struct DrawCommand
        {
            public CommandKind Kind;
            public Vector3 A;
            public Vector3 B;
            public Vector3 C;
            public Quaternion Rotation;
            public Color Color;
            public float Radius;
            public int Segments;
            public bool DepthTest;
            public bool CameraRelativeSize;
            public float CameraRelativeScale;
            public Vector2 CameraRelativeSizeRange;
            public float ExpireTime;
            public int Frame;
            public bool Retained;
        }

        private const string RuntimeObjectName = "[FP Runtime Debug Draw]";
        private const int MinimumSegments = 6;
        private const float DefaultCameraRelativeScale = 0.006f;

        private static readonly List<DrawCommand> Commands = new List<DrawCommand>(512);
        private static readonly Vector3[] BoxCorners = new Vector3[8];
        private static readonly int[] BoxLineIndices =
        {
            0, 1, 1, 2, 2, 3, 3, 0,
            4, 5, 5, 6, 6, 7, 7, 4,
            0, 4, 1, 5, 2, 6, 3, 7
        };

        private static FPRuntimeDebugDraw _instance;
        private static Material _lineMaterial;

        [SerializeField] private bool drawGameCameras = true;
        [SerializeField] private bool drawSceneViewCameras = true;
        [SerializeField] private bool clearWhenNotPlaying;

        public bool DrawGameCameras
        {
            get => drawGameCameras;
            set => drawGameCameras = value;
        }

        public bool DrawSceneViewCameras
        {
            get => drawSceneViewCameras;
            set => drawSceneViewCameras = value;
        }

        public static FPRuntimeDebugDraw Instance => EnsureRenderer();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureRenderer();
        }

        private void OnEnable()
        {
            _instance = this;
            EnsureMaterial();
        }

        private void OnDisable()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            if (_lineMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_lineMaterial);
                }
                else
                {
                    DestroyImmediate(_lineMaterial);
                }

                _lineMaterial = null;
            }
        }

        private void Update()
        {
            if (!Application.isPlaying && clearWhenNotPlaying)
            {
                Commands.Clear();
                return;
            }

            PruneExpired();
        }

        private void OnRenderObject()
        {
            Camera camera = Camera.current;
            if (camera == null || !ShouldDrawCamera(camera))
            {
                return;
            }

            PruneExpired();
            if (Commands.Count == 0)
            {
                return;
            }

            Material material = EnsureMaterial();
            if (material == null)
            {
                return;
            }

            DrawDepthGroup(material, camera, true);
            DrawDepthGroup(material, camera, false);
        }

        public static void Clear()
        {
            Commands.Clear();
        }

        public static void DrawLine(Vector3 start, Vector3 end, Color color, float duration = 0f, bool depthTest = true)
        {
            Add(new DrawCommand
            {
                Kind = CommandKind.Line,
                A = start,
                B = end,
                Color = color,
                DepthTest = depthTest
            }, duration);
        }

        public static void DrawRay(Vector3 origin, Vector3 direction, Color color, float duration = 0f, bool depthTest = true)
        {
            DrawLine(origin, origin + direction, color, duration, depthTest);
        }

        public static void DrawCameraRelativeRay(
            Vector3 origin,
            Vector3 direction,
            float fallbackLength,
            Color color,
            float duration = 0f,
            bool depthTest = true,
            float cameraRelativeScale = 0.02f,
            Vector2 cameraRelativeLengthRange = default)
        {
            Vector3 safeDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector3.up;
            if (cameraRelativeLengthRange == default)
            {
                cameraRelativeLengthRange = new Vector2(fallbackLength, fallbackLength * 6f);
            }

            Add(new DrawCommand
            {
                Kind = CommandKind.Ray,
                A = origin,
                B = safeDirection,
                Color = color,
                Radius = Mathf.Max(0.0001f, fallbackLength),
                DepthTest = depthTest,
                CameraRelativeSize = true,
                CameraRelativeScale = Mathf.Max(0.0001f, cameraRelativeScale),
                CameraRelativeSizeRange = cameraRelativeLengthRange
            }, duration);
        }

        public static void DrawPoint(
            Vector3 position,
            float radius,
            Color color,
            float duration = 0f,
            bool depthTest = true,
            bool cameraRelativeSize = false)
        {
            DrawPoint(
                position,
                radius,
                color,
                duration,
                depthTest,
                cameraRelativeSize,
                DefaultCameraRelativeScale,
                new Vector2(radius, radius * 6f));
        }

        public static void DrawPoint(
            Vector3 position,
            float radius,
            Color color,
            float duration,
            bool depthTest,
            bool cameraRelativeSize,
            float cameraRelativeScale,
            Vector2 cameraRelativeSizeRange)
        {
            Add(new DrawCommand
            {
                Kind = CommandKind.Point,
                A = position,
                Color = color,
                Radius = Mathf.Max(0.0001f, radius),
                DepthTest = depthTest,
                CameraRelativeSize = cameraRelativeSize,
                CameraRelativeScale = Mathf.Max(0.0001f, cameraRelativeScale),
                CameraRelativeSizeRange = cameraRelativeSizeRange
            }, duration);
        }

        public static void DrawWireCircle(
            Vector3 center,
            Vector3 normal,
            float radius,
            Color color,
            int segments = 32,
            float duration = 0f,
            bool depthTest = true)
        {
            Add(new DrawCommand
            {
                Kind = CommandKind.Circle,
                A = center,
                B = normal,
                Color = color,
                Radius = Mathf.Max(0.0001f, radius),
                Segments = Mathf.Max(MinimumSegments, segments),
                DepthTest = depthTest
            }, duration);
        }

        public static void DrawWireSphere(
            Vector3 center,
            float radius,
            Color color,
            int segments = 32,
            float duration = 0f,
            bool depthTest = true)
        {
            Add(new DrawCommand
            {
                Kind = CommandKind.Sphere,
                A = center,
                Color = color,
                Radius = Mathf.Max(0.0001f, radius),
                Segments = Mathf.Max(MinimumSegments, segments),
                DepthTest = depthTest
            }, duration);
        }

        public static void DrawWireBox(
            Vector3 center,
            Quaternion rotation,
            Vector3 size,
            Color color,
            float duration = 0f,
            bool depthTest = true)
        {
            Add(new DrawCommand
            {
                Kind = CommandKind.Box,
                A = center,
                Rotation = rotation,
                C = size,
                Color = color,
                DepthTest = depthTest
            }, duration);
        }

        public static void DrawBounds(Bounds bounds, Color color, float duration = 0f, bool depthTest = true)
        {
            DrawWireBox(bounds.center, Quaternion.identity, bounds.size, color, duration, depthTest);
        }

        public static void DrawSelectionMarker(
            Vector3 center,
            float radius,
            Color color,
            float duration = 0f,
            bool depthTest = false)
        {
            float safeRadius = Mathf.Max(0.0001f, radius);
            Color accent = Color.Lerp(color, Color.white, 0.35f);
            DrawWireSphere(center, safeRadius, color, 32, duration, depthTest);
            DrawWireCircle(center, Vector3.up, safeRadius * 1.08f, accent, 32, duration, depthTest);
            DrawCameraRelativeRay(
                center + Vector3.up * safeRadius,
                Vector3.up,
                safeRadius * 0.35f,
                accent,
                duration,
                depthTest,
                0.018f,
                new Vector2(safeRadius * 0.2f, safeRadius * 0.85f));
        }

        public static void DrawTargetMarker(
            Vector3 center,
            Vector3 normal,
            float radius,
            Color color,
            float duration = 0f,
            bool depthTest = true)
        {
            float safeRadius = Mathf.Max(0.0001f, radius);
            Vector3 safeNormal = normal.sqrMagnitude > 0.000001f ? normal.normalized : Vector3.up;
            BuildBasis(safeNormal, out Vector3 axisA, out Vector3 axisB);

            DrawWireCircle(center, safeNormal, safeRadius, color, 32, duration, depthTest);
            DrawLine(center - axisA * safeRadius, center + axisA * safeRadius, color, duration, depthTest);
            DrawLine(center - axisB * safeRadius, center + axisB * safeRadius, color, duration, depthTest);
            DrawCameraRelativeRay(center, safeNormal, safeRadius * 0.2f, color, duration, depthTest, 0.012f, new Vector2(safeRadius * 0.12f, safeRadius * 0.45f));
        }

        public static void DrawTargetMarker(
            Vector3 center,
            float radius,
            Color color,
            float duration = 0f,
            bool depthTest = true)
        {
            DrawTargetMarker(center, Vector3.up, radius, color, duration, depthTest);
        }

        public static void DrawClickMarker(
            Vector3 center,
            Vector3 normal,
            float radius,
            Color color,
            float duration = 0.35f,
            bool depthTest = true)
        {
            float safeRadius = Mathf.Max(0.0001f, radius);
            Color outer = color;
            outer.a *= 0.65f;
            DrawTargetMarker(center, normal, safeRadius, color, duration, depthTest);
            DrawWireCircle(center, normal, safeRadius * 1.45f, outer, 40, duration, depthTest);
        }

        public static void DrawClickMarker(
            Vector3 center,
            float radius,
            Color color,
            float duration = 0.35f,
            bool depthTest = true)
        {
            DrawClickMarker(center, Vector3.up, radius, color, duration, depthTest);
        }

        public static void DrawLink(
            Vector3 start,
            Vector3 end,
            Color color,
            float duration = 0f,
            bool depthTest = true,
            bool drawEndpoints = true,
            float endpointRadius = 0.035f)
        {
            DrawLine(start, end, color, duration, depthTest);
            if (!drawEndpoints)
            {
                return;
            }

            float safeRadius = Mathf.Max(0.0001f, endpointRadius);
            DrawPoint(start, safeRadius, color, duration, depthTest, true);
            DrawPoint(end, safeRadius, color, duration, depthTest, true);
        }

        public static void DrawPlane(
            Vector3 center,
            Vector3 normal,
            Vector2 size,
            Color color,
            float normalLength = 0.25f,
            float duration = 0f,
            bool depthTest = true)
        {
            Add(new DrawCommand
            {
                Kind = CommandKind.Plane,
                A = center,
                B = normal,
                C = new Vector3(size.x, Mathf.Max(0f, normalLength), size.y),
                Color = color,
                DepthTest = depthTest
            }, duration);
        }

        public static void DrawMeshEdges(
            Mesh mesh,
            Matrix4x4 localToWorld,
            Color color,
            float duration = 0f,
            bool depthTest = true)
        {
            if (mesh == null)
            {
                return;
            }

            Vector3[] vertices = mesh.vertices;
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                MeshTopology topology = mesh.GetTopology(subMesh);
                int[] indices = mesh.GetIndices(subMesh);

                switch (topology)
                {
                    case MeshTopology.Triangles:
                        for (int i = 0; i + 2 < indices.Length; i += 3)
                        {
                            Vector3 a = localToWorld.MultiplyPoint3x4(vertices[indices[i]]);
                            Vector3 b = localToWorld.MultiplyPoint3x4(vertices[indices[i + 1]]);
                            Vector3 c = localToWorld.MultiplyPoint3x4(vertices[indices[i + 2]]);
                            DrawLine(a, b, color, duration, depthTest);
                            DrawLine(b, c, color, duration, depthTest);
                            DrawLine(c, a, color, duration, depthTest);
                        }
                        break;
                    case MeshTopology.Quads:
                        for (int i = 0; i + 3 < indices.Length; i += 4)
                        {
                            Vector3 a = localToWorld.MultiplyPoint3x4(vertices[indices[i]]);
                            Vector3 b = localToWorld.MultiplyPoint3x4(vertices[indices[i + 1]]);
                            Vector3 c = localToWorld.MultiplyPoint3x4(vertices[indices[i + 2]]);
                            Vector3 d = localToWorld.MultiplyPoint3x4(vertices[indices[i + 3]]);
                            DrawLine(a, b, color, duration, depthTest);
                            DrawLine(b, c, color, duration, depthTest);
                            DrawLine(c, d, color, duration, depthTest);
                            DrawLine(d, a, color, duration, depthTest);
                        }
                        break;
                    case MeshTopology.Lines:
                        for (int i = 0; i + 1 < indices.Length; i += 2)
                        {
                            DrawLine(
                                localToWorld.MultiplyPoint3x4(vertices[indices[i]]),
                                localToWorld.MultiplyPoint3x4(vertices[indices[i + 1]]),
                                color,
                                duration,
                                depthTest);
                        }
                        break;
                    case MeshTopology.LineStrip:
                        for (int i = 0; i + 1 < indices.Length; i++)
                        {
                            DrawLine(
                                localToWorld.MultiplyPoint3x4(vertices[indices[i]]),
                                localToWorld.MultiplyPoint3x4(vertices[indices[i + 1]]),
                                color,
                                duration,
                                depthTest);
                        }
                        break;
                    case MeshTopology.Points:
                        for (int i = 0; i < indices.Length; i++)
                        {
                            DrawPoint(localToWorld.MultiplyPoint3x4(vertices[indices[i]]), 0.025f, color, duration, depthTest, true);
                        }
                        break;
                }
            }
        }

        public static void DrawMeshVertices(
            Mesh mesh,
            Matrix4x4 localToWorld,
            float radius,
            Color color,
            float duration = 0f,
            bool depthTest = true,
            bool cameraRelativeSize = true)
        {
            if (mesh == null)
            {
                return;
            }

            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                DrawPoint(localToWorld.MultiplyPoint3x4(vertices[i]), radius, color, duration, depthTest, cameraRelativeSize);
            }
        }

        public static void DrawMeshNormals(
            Mesh mesh,
            Matrix4x4 localToWorld,
            float length,
            Color color,
            float duration = 0f,
            bool depthTest = true)
        {
            if (mesh == null)
            {
                return;
            }

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
                DrawLine(start, start + (normal * length), color, duration, depthTest);
            }
        }

        private static void Add(DrawCommand command, float duration)
        {
            EnsureRenderer();
            command.Frame = Time.frameCount;
            command.Retained = duration > 0f;
            command.ExpireTime = Time.realtimeSinceStartup + Mathf.Max(0f, duration);
            Commands.Add(command);
        }

        private static FPRuntimeDebugDraw EnsureRenderer()
        {
            if (_instance != null)
            {
                return _instance;
            }

            FPRuntimeDebugDraw existing = FindAnyObjectByType<FPRuntimeDebugDraw>(FindObjectsInactive.Include);
            if (existing != null)
            {
                _instance = existing;
                return _instance;
            }

            var debugObject = new GameObject(RuntimeObjectName)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            if (Application.isPlaying)
            {
                DontDestroyOnLoad(debugObject);
            }

            _instance = debugObject.AddComponent<FPRuntimeDebugDraw>();
            return _instance;
        }

        private static Material EnsureMaterial()
        {
            if (_lineMaterial != null)
            {
                return _lineMaterial;
            }

            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                return null;
            }

            _lineMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            _lineMaterial.SetInt("_Cull", (int)CullMode.Off);
            _lineMaterial.SetInt("_ZWrite", 0);
            return _lineMaterial;
        }

        private static void ApplyDepthMode(Material material, bool depthTest)
        {
            material.SetInt("_ZTest", depthTest ? (int)CompareFunction.LessEqual : (int)CompareFunction.Always);
        }

        private static void PruneExpired()
        {
            float now = Time.realtimeSinceStartup;
            int frame = Time.frameCount;
            for (int i = Commands.Count - 1; i >= 0; i--)
            {
                DrawCommand command = Commands[i];
                bool alive = command.Retained ? now <= command.ExpireTime : command.Frame == frame;
                if (!alive)
                {
                    Commands.RemoveAt(i);
                }
            }
        }

        private bool ShouldDrawCamera(Camera camera)
        {
            if (camera.cameraType == CameraType.SceneView)
            {
                return drawSceneViewCameras;
            }

            return drawGameCameras;
        }

        private static void DrawDepthGroup(Material material, Camera camera, bool depthTest)
        {
            ApplyDepthMode(material, depthTest);
            material.SetPass(0);

            GL.Begin(GL.LINES);
            for (int i = 0; i < Commands.Count; i++)
            {
                DrawCommand command = Commands[i];
                if (command.DepthTest != depthTest || command.Kind == CommandKind.Point)
                {
                    continue;
                }

                EmitLines(command, camera);
            }
            GL.End();

            GL.Begin(GL.QUADS);
            for (int i = 0; i < Commands.Count; i++)
            {
                DrawCommand command = Commands[i];
                if (command.DepthTest != depthTest || command.Kind != CommandKind.Point)
                {
                    continue;
                }

                EmitPoint(command, camera);
            }
            GL.End();
        }

        private static void EmitLines(DrawCommand command, Camera camera)
        {
            switch (command.Kind)
            {
                case CommandKind.Line:
                    EmitLine(command.A, command.B, command.Color);
                    break;
                case CommandKind.Ray:
                    EmitLine(command.A, command.A + command.B * ResolveCameraRelativeSize(command, camera), command.Color);
                    break;
                case CommandKind.Circle:
                    EmitCircle(command.A, command.B, command.Radius, command.Color, command.Segments);
                    break;
                case CommandKind.Sphere:
                    EmitCircle(command.A, Vector3.right, command.Radius, command.Color, command.Segments);
                    EmitCircle(command.A, Vector3.up, command.Radius, command.Color, command.Segments);
                    EmitCircle(command.A, Vector3.forward, command.Radius, command.Color, command.Segments);
                    break;
                case CommandKind.Box:
                    EmitBox(command.A, command.Rotation, command.C, command.Color);
                    break;
                case CommandKind.Plane:
                    EmitPlane(command.A, command.B, new Vector2(command.C.x, command.C.z), command.C.y, command.Color);
                    break;
            }
        }

        private static void EmitLine(Vector3 start, Vector3 end, Color color)
        {
            GL.Color(color);
            GL.Vertex(start);
            GL.Vertex(end);
        }

        private static void EmitPoint(DrawCommand command, Camera camera)
        {
            float radius = ResolvePointRadius(command, camera);
            Vector3 right = camera.transform.right * radius;
            Vector3 up = camera.transform.up * radius;
            Vector3 center = command.A;

            GL.Color(command.Color);
            GL.Vertex(center - right - up);
            GL.Vertex(center - right + up);
            GL.Vertex(center + right + up);
            GL.Vertex(center + right - up);
        }

        private static float ResolvePointRadius(DrawCommand command, Camera camera)
        {
            if (!command.CameraRelativeSize || camera == null)
            {
                return command.Radius;
            }

            return ResolveCameraRelativeSize(command, camera);
        }

        private static float ResolveCameraRelativeSize(DrawCommand command, Camera camera)
        {
            float distance = camera.orthographic
                ? camera.orthographicSize * 2f
                : Vector3.Distance(camera.transform.position, command.A);
            float relativeRadius = distance * command.CameraRelativeScale;
            float min = Mathf.Max(0.0001f, Mathf.Min(command.CameraRelativeSizeRange.x, command.CameraRelativeSizeRange.y));
            float max = Mathf.Max(min, Mathf.Max(command.CameraRelativeSizeRange.x, command.CameraRelativeSizeRange.y));
            return Mathf.Clamp(relativeRadius, min, max);
        }

        private static void EmitCircle(Vector3 center, Vector3 normal, float radius, Color color, int segments)
        {
            BuildBasis(normal, out Vector3 axisA, out Vector3 axisB);

            GL.Color(color);
            Vector3 previous = center + (axisA * radius);
            for (int i = 1; i <= segments; i++)
            {
                float t = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 next = center + ((axisA * Mathf.Cos(t) + axisB * Mathf.Sin(t)) * radius);
                GL.Vertex(previous);
                GL.Vertex(next);
                previous = next;
            }
        }

        private static void EmitBox(Vector3 center, Quaternion rotation, Vector3 size, Color color)
        {
            Matrix4x4 matrix = Matrix4x4.TRS(center, rotation, size);
            BoxCorners[0] = matrix.MultiplyPoint3x4(new Vector3(-0.5f, -0.5f, -0.5f));
            BoxCorners[1] = matrix.MultiplyPoint3x4(new Vector3(0.5f, -0.5f, -0.5f));
            BoxCorners[2] = matrix.MultiplyPoint3x4(new Vector3(0.5f, -0.5f, 0.5f));
            BoxCorners[3] = matrix.MultiplyPoint3x4(new Vector3(-0.5f, -0.5f, 0.5f));
            BoxCorners[4] = matrix.MultiplyPoint3x4(new Vector3(-0.5f, 0.5f, -0.5f));
            BoxCorners[5] = matrix.MultiplyPoint3x4(new Vector3(0.5f, 0.5f, -0.5f));
            BoxCorners[6] = matrix.MultiplyPoint3x4(new Vector3(0.5f, 0.5f, 0.5f));
            BoxCorners[7] = matrix.MultiplyPoint3x4(new Vector3(-0.5f, 0.5f, 0.5f));

            GL.Color(color);
            for (int i = 0; i + 1 < BoxLineIndices.Length; i += 2)
            {
                GL.Vertex(BoxCorners[BoxLineIndices[i]]);
                GL.Vertex(BoxCorners[BoxLineIndices[i + 1]]);
            }
        }

        private static void EmitPlane(Vector3 center, Vector3 normal, Vector2 size, float normalLength, Color color)
        {
            BuildBasis(normal, out Vector3 axisA, out Vector3 axisB);
            Vector3 halfA = axisA * Mathf.Max(0.0001f, size.x * 0.5f);
            Vector3 halfB = axisB * Mathf.Max(0.0001f, size.y * 0.5f);
            Vector3 p0 = center - halfA - halfB;
            Vector3 p1 = center - halfA + halfB;
            Vector3 p2 = center + halfA + halfB;
            Vector3 p3 = center + halfA - halfB;

            GL.Color(color);
            GL.Vertex(p0);
            GL.Vertex(p1);
            GL.Vertex(p1);
            GL.Vertex(p2);
            GL.Vertex(p2);
            GL.Vertex(p3);
            GL.Vertex(p3);
            GL.Vertex(p0);
            if (normalLength > 0f)
            {
                GL.Vertex(center);
                GL.Vertex(center + normal.normalized * normalLength);
            }
        }

        private static void BuildBasis(Vector3 normal, out Vector3 axisA, out Vector3 axisB)
        {
            Vector3 safeNormal = normal.sqrMagnitude > 0.000001f ? normal.normalized : Vector3.up;
            axisA = Vector3.Cross(safeNormal, Vector3.up);
            if (axisA.sqrMagnitude <= 0.000001f)
            {
                axisA = Vector3.Cross(safeNormal, Vector3.right);
            }

            axisA.Normalize();
            axisB = Vector3.Cross(safeNormal, axisA).normalized;
        }
    }
}
#else
namespace FuzzPhyte.Utility.DebugTools
{
    using System.Diagnostics;
    using UnityEngine;

    /// <summary>
    /// Player-build no-op API for editor-only runtime debug drawing.
    /// </summary>
    public sealed class FPRuntimeDebugDraw
    {
        private static readonly FPRuntimeDebugDraw NoOpInstance = new FPRuntimeDebugDraw();

        private FPRuntimeDebugDraw()
        {
        }

        public bool DrawGameCameras { get; set; }
        public bool DrawSceneViewCameras { get; set; }

        public static FPRuntimeDebugDraw Instance => NoOpInstance;

        [Conditional("UNITY_EDITOR")]
        public static void Clear()
        {
        }

        [Conditional("UNITY_EDITOR")]
        public static void DrawLine(Vector3 start, Vector3 end, Color color, float duration = 0f, bool depthTest = true)
        {
        }

        [Conditional("UNITY_EDITOR")]
        public static void DrawRay(Vector3 origin, Vector3 direction, Color color, float duration = 0f, bool depthTest = true)
        {
        }

        [Conditional("UNITY_EDITOR")]
        public static void DrawCameraRelativeRay(
            Vector3 origin,
            Vector3 direction,
            float fallbackLength,
            Color color,
            float duration = 0f,
            bool depthTest = true,
            float cameraRelativeScale = 0.02f,
            Vector2 cameraRelativeLengthRange = default)
        {
        }

        [Conditional("UNITY_EDITOR")]
        public static void DrawPoint(Vector3 position, float radius, Color color, float duration = 0f, bool depthTest = true, bool cameraRelativeSize = false)
        {
        }

        [Conditional("UNITY_EDITOR")]
        public static void DrawPoint(
            Vector3 position,
            float radius,
            Color color,
            float duration,
            bool depthTest,
            bool cameraRelativeSize,
            float cameraRelativeScale,
            Vector2 cameraRelativeSizeRange)
        {
        }

        [Conditional("UNITY_EDITOR")]
        public static void DrawWireCircle(Vector3 center, Vector3 normal, float radius, Color color, int segments = 32, float duration = 0f, bool depthTest = true)
        {
        }

        [Conditional("UNITY_EDITOR")]
        public static void DrawWireSphere(Vector3 center, float radius, Color color, int segments = 32, float duration = 0f, bool depthTest = true)
        {
        }

        [Conditional("UNITY_EDITOR")]
        public static void DrawWireBox(Vector3 center, Quaternion rotation, Vector3 size, Color color, float duration = 0f, bool depthTest = true)
        {
        }

        [Conditional("UNITY_EDITOR")]
        public static void DrawBounds(Bounds bounds, Color color, float duration = 0f, bool depthTest = true)
        {
        }

        [Conditional("UNITY_EDITOR")]
        public static void DrawSelectionMarker(Vector3 center, float radius, Color color, float duration = 0f, bool depthTest = false)
        {
        }

        [Conditional("UNITY_EDITOR")]
        public static void DrawTargetMarker(Vector3 center, Vector3 normal, float radius, Color color, float duration = 0f, bool depthTest = true)
        {
        }

        [Conditional("UNITY_EDITOR")]
        public static void DrawTargetMarker(Vector3 center, float radius, Color color, float duration = 0f, bool depthTest = true)
        {
        }

        [Conditional("UNITY_EDITOR")]
        public static void DrawClickMarker(Vector3 center, Vector3 normal, float radius, Color color, float duration = 0.35f, bool depthTest = true)
        {
        }

        [Conditional("UNITY_EDITOR")]
        public static void DrawClickMarker(Vector3 center, float radius, Color color, float duration = 0.35f, bool depthTest = true)
        {
        }

        [Conditional("UNITY_EDITOR")]
        public static void DrawLink(Vector3 start, Vector3 end, Color color, float duration = 0f, bool depthTest = true, bool drawEndpoints = true, float endpointRadius = 0.035f)
        {
        }

        [Conditional("UNITY_EDITOR")]
        public static void DrawPlane(Vector3 center, Vector3 normal, Vector2 size, Color color, float normalLength = 0.25f, float duration = 0f, bool depthTest = true)
        {
        }

        [Conditional("UNITY_EDITOR")]
        public static void DrawMeshEdges(Mesh mesh, Matrix4x4 localToWorld, Color color, float duration = 0f, bool depthTest = true)
        {
        }

        [Conditional("UNITY_EDITOR")]
        public static void DrawMeshVertices(Mesh mesh, Matrix4x4 localToWorld, float radius, Color color, float duration = 0f, bool depthTest = true, bool cameraRelativeSize = true)
        {
        }

        [Conditional("UNITY_EDITOR")]
        public static void DrawMeshNormals(Mesh mesh, Matrix4x4 localToWorld, float length, Color color, float duration = 0f, bool depthTest = true)
        {
        }
    }
}
#endif
