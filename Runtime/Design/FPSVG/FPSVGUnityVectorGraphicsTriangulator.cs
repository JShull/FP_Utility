// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using UnityEngine;

    public static class FPSVGUnityVectorGraphicsTriangulator
    {
        private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
        private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

        private static bool initialized;
        private static Type svgParserType;
        private static Type vectorUtilsType;
        private static Type tessellationOptionsType;
        private static MethodInfo importSvgMethod;
        private static MethodInfo tessellateSceneMethod;
        private static int tessellateSceneParameterCount;
        private static PropertyInfo sceneProperty;
        private static string unavailableReason;

        public static bool IsAvailable
        {
            get
            {
                EnsureInitialized();
                return string.IsNullOrWhiteSpace(unavailableReason);
            }
        }

        public static string UnavailableReason
        {
            get
            {
                EnsureInitialized();
                return unavailableReason;
            }
        }

        public static bool TryTriangulate(
            IReadOnlyList<Vector2> outerLoop,
            IReadOnlyList<List<Vector2>> holes,
            FPSVGExtruderSettings settings,
            out FPSVGTriangulation triangulation,
            out string message)
        {
            EnsureInitialized();
            triangulation = new FPSVGTriangulation();
            message = string.Empty;

            if (!string.IsNullOrWhiteSpace(unavailableReason))
            {
                message = unavailableReason;
                return false;
            }

            if (outerLoop == null || outerLoop.Count < 3)
            {
                message = "Unity Vector Graphics backend received no valid outer loop.";
                return false;
            }

            string svg = BuildTemporarySvg(outerLoop, holes);
            try
            {
                object sceneInfo = importSvgMethod.Invoke(null, new object[]
                {
                    new StringReader(svg),
                    0f,
                    1f,
                    0,
                    0,
                    false
                });
                object scene = sceneProperty.GetValue(sceneInfo, null);
                object options = CreateTessellationOptions(settings);
                object geomsObject = InvokeTessellateScene(scene, options);

                if (!(geomsObject is System.Collections.IEnumerable geometries))
                {
                    message = "Unity Vector Graphics returned no enumerable geometry.";
                    return false;
                }

                foreach (object geometry in geometries)
                {
                    AddGeometry(geometry, triangulation);
                }
            }
            catch (TargetInvocationException ex)
            {
                message = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }

            if (triangulation.Triangles.Count == 0)
            {
                message = "Unity Vector Graphics tessellated the region but produced no triangles.";
                return false;
            }

            if (holes != null)
            {
                for (int i = 0; i < holes.Count; i++)
                {
                    if (holes[i] != null && holes[i].Count >= 3)
                    {
                        triangulation.BridgedHoleIndices.Add(i);
                    }
                }
            }

            return true;
        }

        private static void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            svgParserType = FindType("Unity.VectorGraphics.SVGParser");
            vectorUtilsType = FindType("Unity.VectorGraphics.VectorUtils");
            tessellationOptionsType = FindType("Unity.VectorGraphics.VectorUtils+TessellationOptions");

            if (svgParserType == null || vectorUtilsType == null || tessellationOptionsType == null)
            {
                unavailableReason = "Unity Vector Graphics backend requires the com.unity.vectorgraphics package. The built-in com.unity.modules.vectorgraphics module is not enough.";
                return;
            }

            importSvgMethod = FindImportSvgMethod(svgParserType);
            tessellateSceneMethod = FindTessellateSceneMethod(vectorUtilsType, tessellationOptionsType);
            if (importSvgMethod == null || tessellateSceneMethod == null)
            {
                unavailableReason = "Unity Vector Graphics backend could not find the expected SVGParser.ImportSVG or VectorUtils.TessellateScene API.";
                return;
            }

            sceneProperty = importSvgMethod.ReturnType.GetProperty("Scene", PublicInstance);
            if (sceneProperty == null)
            {
                unavailableReason = "Unity Vector Graphics backend could not read SVGParser.SceneInfo.Scene.";
            }
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static MethodInfo FindImportSvgMethod(Type type)
        {
            MethodInfo[] methods = type.GetMethods(PublicStatic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name != "ImportSVG")
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 6 &&
                    typeof(TextReader).IsAssignableFrom(parameters[0].ParameterType) &&
                    parameters[1].ParameterType == typeof(float))
                {
                    return method;
                }
            }

            return null;
        }

        private static MethodInfo FindTessellateSceneMethod(Type type, Type optionsType)
        {
            MethodInfo[] methods = type.GetMethods(PublicStatic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name != "TessellateScene")
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if ((parameters.Length == 2 || parameters.Length == 3) && parameters[1].ParameterType == optionsType)
                {
                    tessellateSceneParameterCount = parameters.Length;
                    return method;
                }
            }

            return null;
        }

        private static object InvokeTessellateScene(object scene, object options)
        {
            if (tessellateSceneParameterCount == 2)
            {
                return tessellateSceneMethod.Invoke(null, new[] { scene, options });
            }

            return tessellateSceneMethod.Invoke(null, new[] { scene, options, null });
        }

        private static object CreateTessellationOptions(FPSVGExtruderSettings settings)
        {
            object options = Activator.CreateInstance(tessellationOptionsType);
            SetOption(options, "StepDistance", float.MaxValue);
            SetOption(options, "MaxCordDeviation", float.MaxValue);
            SetOption(options, "MaxTanAngleDeviation", float.MaxValue);
            SetOption(options, "SamplingStepSize", Mathf.Clamp01(1f / Mathf.Max(1f, settings.PathSampleDistance)));
            return options;
        }

        private static void SetOption(object options, string propertyName, float value)
        {
            PropertyInfo property = tessellationOptionsType.GetProperty(propertyName, PublicInstance);
            if (property != null && property.CanWrite)
            {
                property.SetValue(options, value, null);
            }
        }

        private static string BuildTemporarySvg(IReadOnlyList<Vector2> outerLoop, IReadOnlyList<List<Vector2>> holes)
        {
            CalculateBounds(outerLoop, holes, out Vector2 min, out Vector2 max);
            float width = Mathf.Max(0.001f, max.x - min.x);
            float height = Mathf.Max(0.001f, max.y - min.y);

            var builder = new StringBuilder(outerLoop.Count * 18);
            AppendLoopPath(builder, CopyWithWinding(outerLoop, true));
            if (holes != null)
            {
                for (int i = 0; i < holes.Count; i++)
                {
                    if (holes[i] != null && holes[i].Count >= 3)
                    {
                        AppendLoopPath(builder, CopyWithWinding(holes[i], false));
                    }
                }
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{0} {1} {2} {3}\"><path fill=\"#ffffff\" fill-rule=\"evenodd\" d=\"{4}\"/></svg>",
                min.x,
                min.y,
                width,
                height,
                builder);
        }

        private static List<Vector2> CopyWithWinding(IReadOnlyList<Vector2> loop, bool counterClockwise)
        {
            var copy = new List<Vector2>(loop);
            bool isCounterClockwise = FPSVGRegionDetector.SignedArea(copy) > 0f;
            if (isCounterClockwise != counterClockwise)
            {
                copy.Reverse();
            }

            return copy;
        }

        private static void AppendLoopPath(StringBuilder builder, IReadOnlyList<Vector2> loop)
        {
            builder.Append("M ");
            AppendPoint(builder, loop[0]);
            for (int i = 1; i < loop.Count; i++)
            {
                builder.Append(" L ");
                AppendPoint(builder, loop[i]);
            }

            builder.Append(" Z ");
        }

        private static void AppendPoint(StringBuilder builder, Vector2 point)
        {
            builder.Append(point.x.ToString("R", CultureInfo.InvariantCulture));
            builder.Append(' ');
            builder.Append(point.y.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void CalculateBounds(
            IReadOnlyList<Vector2> outerLoop,
            IReadOnlyList<List<Vector2>> holes,
            out Vector2 min,
            out Vector2 max)
        {
            min = outerLoop[0];
            max = outerLoop[0];
            IncludeBounds(outerLoop, ref min, ref max);
            if (holes == null)
            {
                return;
            }

            for (int i = 0; i < holes.Count; i++)
            {
                IncludeBounds(holes[i], ref min, ref max);
            }
        }

        private static void IncludeBounds(IReadOnlyList<Vector2> loop, ref Vector2 min, ref Vector2 max)
        {
            if (loop == null)
            {
                return;
            }

            for (int i = 0; i < loop.Count; i++)
            {
                min = Vector2.Min(min, loop[i]);
                max = Vector2.Max(max, loop[i]);
            }
        }

        private static void AddGeometry(object geometry, FPSVGTriangulation triangulation)
        {
            Vector2[] geometryVertices = GetField<Vector2[]>(geometry, "Vertices");
            ushort[] geometryIndices = GetField<ushort[]>(geometry, "Indices");
            object transform = GetField<object>(geometry, "WorldTransform");
            if (geometryVertices == null || geometryIndices == null)
            {
                return;
            }

            int startIndex = triangulation.Vertices.Count;
            for (int i = 0; i < geometryVertices.Length; i++)
            {
                triangulation.Vertices.Add(TransformPoint(transform, geometryVertices[i]));
            }

            for (int i = 0; i + 2 < geometryIndices.Length; i += 3)
            {
                int a = startIndex + geometryIndices[i];
                int b = startIndex + geometryIndices[i + 1];
                int c = startIndex + geometryIndices[i + 2];
                AddCounterClockwiseTriangle(triangulation, a, b, c);
            }
        }

        private static T GetField<T>(object instance, string fieldName)
        {
            if (instance == null)
            {
                return default;
            }

            FieldInfo field = instance.GetType().GetField(fieldName, PublicInstance);
            if (field == null)
            {
                return default;
            }

            object value = field.GetValue(instance);
            return value is T typedValue ? typedValue : default;
        }

        private static Vector2 TransformPoint(object transform, Vector2 point)
        {
            if (transform == null)
            {
                return point;
            }

            Type type = transform.GetType();
            FieldInfo m00Field = type.GetField("m00", PublicInstance);
            FieldInfo m01Field = type.GetField("m01", PublicInstance);
            FieldInfo m02Field = type.GetField("m02", PublicInstance);
            FieldInfo m10Field = type.GetField("m10", PublicInstance);
            FieldInfo m11Field = type.GetField("m11", PublicInstance);
            FieldInfo m12Field = type.GetField("m12", PublicInstance);
            if (m00Field == null || m01Field == null || m02Field == null || m10Field == null || m11Field == null || m12Field == null)
            {
                return point;
            }

            float m00 = (float)m00Field.GetValue(transform);
            float m01 = (float)m01Field.GetValue(transform);
            float m02 = (float)m02Field.GetValue(transform);
            float m10 = (float)m10Field.GetValue(transform);
            float m11 = (float)m11Field.GetValue(transform);
            float m12 = (float)m12Field.GetValue(transform);
            return new Vector2(
                (m00 * point.x) + (m01 * point.y) + m02,
                (m10 * point.x) + (m11 * point.y) + m12);
        }

        private static void AddCounterClockwiseTriangle(FPSVGTriangulation triangulation, int a, int b, int c)
        {
            Vector2 pa = triangulation.Vertices[a];
            Vector2 pb = triangulation.Vertices[b];
            Vector2 pc = triangulation.Vertices[c];
            float cross = ((pb.x - pa.x) * (pc.y - pa.y)) - ((pb.y - pa.y) * (pc.x - pa.x));
            if (cross < 0f)
            {
                triangulation.Triangles.Add(a);
                triangulation.Triangles.Add(c);
                triangulation.Triangles.Add(b);
                return;
            }

            triangulation.Triangles.Add(a);
            triangulation.Triangles.Add(b);
            triangulation.Triangles.Add(c);
        }
    }
}
