namespace FuzzPhyte.Utility
{
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// GizmoDrawer for misc needs
    /// </summary>
    public static class FPGizmoDraw
    {
        /// <summary>
        /// Draw a cone by segments, return mesh and success of drawing
        /// </summary>
        /// <param name="segments">larger than 2</param>
        /// <param name="height"></param>
        /// <param name="angle"></param>
        /// <param name="bothSidedUV"></param>
        /// <returns></returns>
        public static (Mesh,bool) GenerateConeMesh(int segments, float height, float angle, bool bothSidedUV = false)
        {
            if (segments < 2)
            {
                return (null,false);
            }
            Mesh mesh = new Mesh();
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            // Cone tip (apex)
            vertices.Add(Vector3.zero); // Tip vertex at (0,0,0)
            uvs.Add(new Vector2(0.5f, 1f)); // UV for the tip

            float radius = Mathf.Tan(angle * Mathf.Deg2Rad) * height;

            // Generate base circle vertices
            for (int i = 0; i < segments; i++)
            {
                float theta = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 pos = new Vector3(Mathf.Cos(theta) * radius, Mathf.Sin(theta) * radius, height);
                vertices.Add(pos);
                uvs.Add(new Vector2((Mathf.Cos(theta) + 1) / 2, (Mathf.Sin(theta) + 1) / 2)); // Normalized UV mapping
            }

            // Generate triangles (fan from the cone tip)
            for (int i = 1; i < segments; i++)
            {
                triangles.Add(i + 1);
                triangles.Add(i);
                triangles.Add(0);
            }

            // Close the last triangle (connect last vertex to first)
            triangles.Add(1);
            triangles.Add(segments);
            triangles.Add(0);

            // Add center vertex for cap
            int centerIndex = vertices.Count;
            vertices.Add(new Vector3(0, 0, height)); // Center of the bottom circle
            uvs.Add(new Vector2(0.5f, 0.5f)); // UV for the center

            // Generate cap triangles
            for (int i = 1; i < segments; i++)
            {
                triangles.Add(centerIndex);
                triangles.Add(i);
                triangles.Add(i + 1);
            }

            // Close the cap by connecting the last vertex to the first
            triangles.Add(centerIndex);
            triangles.Add(segments);
            triangles.Add(1);

            //double UV
            if (bothSidedUV)
            {
                int triangleCount = triangles.Count;
                for (int i = 0; i < triangleCount; i += 3)
                {
                    triangles.Add(triangles[i + 2]);
                    triangles.Add(triangles[i + 1]);
                    triangles.Add(triangles[i]);
                }
            }

            // Assign mesh data
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return (mesh,true);
        }
        
    }
}
