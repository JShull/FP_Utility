namespace FuzzPhyte.Utility
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    public class FPGoldbergGenerator
    {
        public struct GoldbergFace
        {
            public Vector3 center;
            public int sides; // 5 for pentagon, 6 for hex

            public GoldbergFace(Vector3 center, int sides)
            {
                this.center = center;
                this.sides = sides;
            }
        }
        public static List<Vector3> GenerateVertices(float radius, int m, int n)
        {

            int t = m * m + m * n + n * n;
            var ico = new FPIcosphere(radius, 1, false); // base icosahedron
            var baseVertices = ico.GetVertices();

            HashSet<Vector3> goldbergVertices = new();

            foreach (var vertex in baseVertices)
            {
                goldbergVertices.Add(vertex.normalized * radius);
            }

            // TODO: generate additional vertices via Goldberg projection rules
            // Currently stubbed for structure

            return goldbergVertices.ToList();
        }
        /// <summary>
        /// Simulated Goldberg (m,n) vertex projection using golden spiral
        /// </summary>
        public static List<GoldbergFace> GenerateGoldbergFaces(float radius, int m, int n)
        {
            int t = m * m + m * n + n * n;
            int count = t * 10;

            List<GoldbergFace> faces = new List<GoldbergFace>();
            float offset = 2.0f / count;
            float increment = Mathf.PI * (3f - Mathf.Sqrt(5f)); // golden angle

            for (int i = 0; i < count; i++)
            {
                float y = ((i * offset) - 1f) + (offset / 2f);
                float r = Mathf.Sqrt(1 - y * y);
                float phi = i * increment;

                float x = Mathf.Cos(phi) * r;
                float z = Mathf.Sin(phi) * r;

                Vector3 point = new Vector3(x, y, z) * radius;

                // For now, mark 12 evenly spaced points as pentagons (approximation)
                int sides = (i % (count / 12) == 0) ? 5 : 6;
                faces.Add(new GoldbergFace(point, sides));
            }
            return faces;
        }
    }
}
