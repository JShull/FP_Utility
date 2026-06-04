// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor
{
    using UnityEditor;
    using UnityEngine;

    internal enum FPMeshPreviewProjection
    {
        Perspective = 0,
        Orthographic = 1
    }

    /// <summary>
    /// Shared editor preview helpers for mesh tools that render and orbit meshes in an EditorWindow.
    /// </summary>
    internal static class FPMeshPreviewEditorUtility
    {
        public const float DefaultFieldOfView = 30f;
        public const float DefaultOrbitSensitivity = 0.4f;
        public static Color PreviewMeshColor => new Color(0.62f, 0.83f, 1f, 1f);
        public static Color VertexOverlayColor => FP_Utility_Editor.WarningColor;
        public static Color EdgeOverlayColor => Color.white;

        public static Quaternion ApplyUnityStyleOrbit(Quaternion currentRotation, Vector2 delta, bool invertOrbit, float sensitivity = DefaultOrbitSensitivity)
        {
            Vector3 forward = currentRotation * Vector3.forward;
            float direction = invertOrbit ? -1f : 1f;
            Quaternion yaw = Quaternion.AngleAxis(delta.x * sensitivity * direction, Vector3.up);
            forward = yaw * forward;

            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude <= 0.00001f)
            {
                right = currentRotation * Vector3.right;
            }

            right.Normalize();
            Quaternion pitch = Quaternion.AngleAxis(delta.y * sensitivity * direction, right);
            Vector3 nextForward = pitch * forward;

            float upDot = Mathf.Abs(Vector3.Dot(nextForward.normalized, Vector3.up));
            if (upDot > 0.98f)
            {
                nextForward = forward;
            }

            return Quaternion.LookRotation(nextForward.normalized, Vector3.up);
        }

        public static float CalculateFitDistance(Bounds bounds, Rect previewRect, float fieldOfView = DefaultFieldOfView)
        {
            float radius = Mathf.Max(0.1f, bounds.extents.magnitude);
            float verticalFov = fieldOfView * Mathf.Deg2Rad;
            float aspect = Mathf.Max(0.1f, previewRect.width / Mathf.Max(1f, previewRect.height));
            float horizontalFov = 2f * Mathf.Atan(Mathf.Tan(verticalFov * 0.5f) * aspect);
            float limitingFov = Mathf.Min(verticalFov, horizontalFov);
            return radius / Mathf.Max(0.01f, Mathf.Sin(limitingFov * 0.5f));
        }

        public static float CalculateOrthographicSize(Bounds bounds, Rect previewRect, Quaternion cameraRotation)
        {
            float aspect = Mathf.Max(0.1f, previewRect.width / Mathf.Max(1f, previewRect.height));
            Quaternion worldToCamera = Quaternion.Inverse(cameraRotation);
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector3 center = bounds.center;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, max.y, max.z)
            };

            float horizontalExtent = 0.1f;
            float verticalExtent = 0.1f;
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 cameraLocal = worldToCamera * (corners[i] - center);
                horizontalExtent = Mathf.Max(horizontalExtent, Mathf.Abs(cameraLocal.x));
                verticalExtent = Mathf.Max(verticalExtent, Mathf.Abs(cameraLocal.y));
            }

            return Mathf.Max(verticalExtent, horizontalExtent / aspect);
        }

        public static FPMeshPreviewProjection DrawProjectionPopup(FPMeshPreviewProjection projection)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            Rect labelRect = new Rect(rect.x, rect.y, 104f, rect.height);
            Rect popupRect = new Rect(labelRect.xMax + 4f, rect.y, Mathf.Max(1f, rect.xMax - labelRect.xMax - 4f), rect.height);
            EditorGUI.LabelField(labelRect, "Projection");
            return (FPMeshPreviewProjection)EditorGUI.EnumPopup(popupRect, projection);
        }

        public static bool DrawInvertCameraOrbitToggle(bool invertCameraOrbit)
        {
            return EditorGUILayout.Toggle("Invert Camera Orbit", invertCameraOrbit);
        }

        public static bool DrawShowVerticesToggle(bool showVertices)
        {
            return EditorGUILayout.Toggle("Show Vertices", showVertices);
        }

        public static bool DrawShowEdgesToggle(bool showEdges)
        {
            return EditorGUILayout.Toggle("Show Edges", showEdges);
        }

        public static bool DrawRightAlignedToggle(string label, bool value)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            Rect labelRect = new Rect(rect.x, rect.y, Mathf.Max(1f, rect.width - 24f), rect.height);
            Rect toggleRect = new Rect(rect.xMax - 18f, rect.y, 18f, rect.height);
            EditorGUI.LabelField(labelRect, label);
            return EditorGUI.Toggle(toggleRect, value);
        }

        public static void DrawMeshVertexOverlay(Camera camera, Rect previewRect, Mesh mesh, Matrix4x4 matrix, Color color, float pointRadius = 2.5f, int maxDrawnVertices = 12000)
        {
            if (camera == null || mesh == null || !mesh.isReadable || Event.current.type != EventType.Repaint)
            {
                return;
            }

            Vector3[] vertices = mesh.vertices;
            if (vertices == null || vertices.Length == 0)
            {
                return;
            }

            int stride = Mathf.Max(1, Mathf.CeilToInt(vertices.Length / (float)Mathf.Max(1, maxDrawnVertices)));
            Handles.BeginGUI();
            GUI.BeginClip(previewRect);
            Color previousColor = Handles.color;
            Handles.color = color;

            for (int i = 0; i < vertices.Length; i += stride)
            {
                Vector3 world = matrix.MultiplyPoint3x4(vertices[i]);
                Vector3 viewport = camera.WorldToViewportPoint(world);
                if (viewport.z <= 0.001f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
                {
                    continue;
                }

                Vector3 point = new Vector3(viewport.x * previewRect.width, (1f - viewport.y) * previewRect.height, 0f);
                Handles.DrawSolidDisc(point, Vector3.forward, pointRadius);
            }

            Handles.color = previousColor;
            GUI.EndClip();
            Handles.EndGUI();
        }

        public static void DrawMeshEdgeOverlay(Camera camera, Rect previewRect, Mesh mesh, Matrix4x4 matrix, Color color, float thickness = 1.5f, int maxDrawnTriangles = 12000)
        {
            if (camera == null || mesh == null || !mesh.isReadable || Event.current.type != EventType.Repaint)
            {
                return;
            }

            Vector3[] vertices = mesh.vertices;
            if (vertices == null || vertices.Length == 0)
            {
                return;
            }

            Handles.BeginGUI();
            GUI.BeginClip(previewRect);
            Color previousColor = Handles.color;
            Handles.color = color;

            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                if (mesh.GetTopology(subMesh) != MeshTopology.Triangles)
                {
                    continue;
                }

                int[] indices = mesh.GetIndices(subMesh);
                if (indices == null || indices.Length < 3)
                {
                    continue;
                }

                int triangleCount = indices.Length / 3;
                int stride = Mathf.Max(1, Mathf.CeilToInt(triangleCount / (float)Mathf.Max(1, maxDrawnTriangles)));
                for (int triangle = 0; triangle < triangleCount; triangle += stride)
                {
                    int index = triangle * 3;
                    DrawProjectedEdge(camera, previewRect, vertices, indices[index], indices[index + 1], matrix, thickness);
                    DrawProjectedEdge(camera, previewRect, vertices, indices[index + 1], indices[index + 2], matrix, thickness);
                    DrawProjectedEdge(camera, previewRect, vertices, indices[index + 2], indices[index], matrix, thickness);
                }
            }

            Handles.color = previousColor;
            GUI.EndClip();
            Handles.EndGUI();
        }

        public static void DrawWorldUpIndicator(Rect previewRect, Camera camera)
        {
            DrawUpIndicator(previewRect, camera, Vector3.up, "World Y+");
        }

        public static void DrawSceneOrientationGizmo(Rect previewRect, Camera camera, FPMeshPreviewProjection projection)
        {
            if (camera == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            const float width = 96f;
            const float height = 88f;
            Rect gizmoRect = new Rect(previewRect.xMax - width - 8f, previewRect.y + 8f, width, height);
            if (gizmoRect.x < previewRect.x + 8f)
            {
                return;
            }

            GUI.Box(gizmoRect, GUIContent.none, EditorStyles.helpBox);

            Vector2 center = new Vector2(gizmoRect.x + (gizmoRect.width * 0.5f), gizmoRect.y + 38f);
            AxisGizmoDirection[] axes =
            {
                new AxisGizmoDirection("x", Vector3.right, new Color(0.9f, 0.22f, 0.18f, 1f)),
                new AxisGizmoDirection("y", Vector3.up, new Color(0.35f, 0.78f, 0.25f, 1f)),
                new AxisGizmoDirection("z", Vector3.forward, new Color(0.25f, 0.48f, 0.95f, 1f))
            };

            for (int i = 0; i < axes.Length - 1; i++)
            {
                for (int j = i + 1; j < axes.Length; j++)
                {
                    float firstDepth = camera.transform.InverseTransformDirection(axes[i].Direction).z;
                    float secondDepth = camera.transform.InverseTransformDirection(axes[j].Direction).z;
                    if (firstDepth > secondDepth)
                    {
                        AxisGizmoDirection swap = axes[i];
                        axes[i] = axes[j];
                        axes[j] = swap;
                    }
                }
            }

            Vector2[] labelPositions = new Vector2[axes.Length];
            Handles.BeginGUI();
            Color previousColor = Handles.color;
            for (int i = 0; i < axes.Length; i++)
            {
                Vector3 cameraLocal = camera.transform.InverseTransformDirection(axes[i].Direction);
                Vector2 screenDirection = new Vector2(cameraLocal.x, -cameraLocal.y);
                bool hasScreenDirection = screenDirection.sqrMagnitude > 0.0001f;
                if (!hasScreenDirection)
                {
                    screenDirection = Vector2.up;
                }

                screenDirection.Normalize();
                float depthFade = Mathf.Lerp(0.55f, 1f, Mathf.InverseLerp(-1f, 1f, -cameraLocal.z));
                float length = Mathf.Lerp(13f, 28f, Mathf.Clamp01(new Vector2(cameraLocal.x, cameraLocal.y).magnitude));
                Vector2 tip = center + (screenDirection * length);
                labelPositions[i] = tip;

                Color axisColor = axes[i].Color;
                axisColor.a = depthFade;
                Handles.color = axisColor;
                Vector3 center3 = new Vector3(center.x, center.y, 0f);
                Vector3 tip3 = new Vector3(tip.x, tip.y, 0f);
                Handles.DrawAAPolyLine(3f, center3, tip3);
                Handles.DrawSolidDisc(tip3, Vector3.forward, hasScreenDirection ? 5f : 6f);
            }

            Handles.color = previousColor;
            Handles.EndGUI();

            for (int i = 0; i < axes.Length; i++)
            {
                Rect labelRect = new Rect(labelPositions[i].x - 5f, labelPositions[i].y - 8f, 10f, 14f);
                GUI.Label(labelRect, axes[i].Label, EditorStyles.whiteMiniLabel);
            }

            string projectionLabel = projection == FPMeshPreviewProjection.Orthographic ? "<Ortho" : "<Persp";
            GUI.Label(new Rect(gizmoRect.x + 4f, gizmoRect.yMax - 18f, gizmoRect.width - 8f, 16f), projectionLabel, EditorStyles.centeredGreyMiniLabel);
        }

        public static void DrawUpIndicator(Rect previewRect, Camera camera, Vector3 upDirection, string label)
        {
            if (camera == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            const float width = 86f;
            const float height = 66f;
            Rect indicatorRect = new Rect(previewRect.x + 8f, previewRect.yMax - height - 8f, width, height);
            if (indicatorRect.y < previewRect.y + 8f)
            {
                return;
            }

            GUI.Box(indicatorRect, GUIContent.none, EditorStyles.helpBox);
            GUI.Label(new Rect(indicatorRect.x + 4f, indicatorRect.y + 4f, indicatorRect.width - 8f, 16f), label, EditorStyles.centeredGreyMiniLabel);

            if (upDirection.sqrMagnitude <= 0.0001f)
            {
                upDirection = Vector3.up;
            }

            Vector3 cameraLocalUp = camera.transform.InverseTransformDirection(upDirection.normalized);
            Vector2 direction = new Vector2(cameraLocalUp.x, -cameraLocalUp.y);
            bool hasScreenDirection = direction.sqrMagnitude > 0.0001f;
            if (!hasScreenDirection)
            {
                direction = Vector2.up;
            }

            direction.Normalize();
            Vector2 center = new Vector2(indicatorRect.x + (indicatorRect.width * 0.5f), indicatorRect.y + 40f);
            Vector2 tip = center + (direction * 18f);
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            Vector2 arrowLeft = tip - (direction * 7f) + (perpendicular * 4f);
            Vector2 arrowRight = tip - (direction * 7f) - (perpendicular * 4f);
            Vector3 center3 = new Vector3(center.x, center.y, 0f);
            Vector3 tip3 = new Vector3(tip.x, tip.y, 0f);
            Vector3 arrowLeft3 = new Vector3(arrowLeft.x, arrowLeft.y, 0f);
            Vector3 arrowRight3 = new Vector3(arrowRight.x, arrowRight.y, 0f);

            Handles.BeginGUI();
            Color previousColor = Handles.color;
            Handles.color = VertexOverlayColor;

            if (hasScreenDirection)
            {
                Handles.DrawAAPolyLine(3f, center3, tip3);
                Handles.DrawAAConvexPolygon(tip3, arrowLeft3, arrowRight3);
            }
            else
            {
                Handles.DrawSolidDisc(center3, Vector3.forward, 4f);
                Handles.DrawWireDisc(center3, Vector3.forward, 9f);
            }

            Handles.color = previousColor;
            Handles.EndGUI();
        }

        private static void DrawProjectedEdge(Camera camera, Rect previewRect, Vector3[] vertices, int firstIndex, int secondIndex, Matrix4x4 matrix, float thickness)
        {
            if (firstIndex < 0 || secondIndex < 0 || firstIndex >= vertices.Length || secondIndex >= vertices.Length)
            {
                return;
            }

            if (!TryProjectPreviewPoint(camera, previewRect, matrix.MultiplyPoint3x4(vertices[firstIndex]), out Vector3 first) ||
                !TryProjectPreviewPoint(camera, previewRect, matrix.MultiplyPoint3x4(vertices[secondIndex]), out Vector3 second))
            {
                return;
            }

            Handles.DrawAAPolyLine(thickness, first, second);
        }

        private static bool TryProjectPreviewPoint(Camera camera, Rect previewRect, Vector3 world, out Vector3 point)
        {
            Vector3 viewport = camera.WorldToViewportPoint(world);
            if (viewport.z <= 0.001f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
            {
                point = Vector3.zero;
                return false;
            }

            point = new Vector3(viewport.x * previewRect.width, (1f - viewport.y) * previewRect.height, 0f);
            return true;
        }

        public static void DrawSectionDivider()
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 8f);
            rect.x += 2f;
            rect.y += 3f;
            rect.width = Mathf.Max(1f, rect.width - 4f);
            rect.height = 2f;
            EditorGUI.DrawRect(rect, FP_Utility_Editor.WarningColor);
        }

        private struct AxisGizmoDirection
        {
            public readonly string Label;
            public readonly Vector3 Direction;
            public readonly Color Color;

            public AxisGizmoDirection(string label, Vector3 direction, Color color)
            {
                Label = label;
                Direction = direction;
                Color = color;
            }
        }
    }
}
