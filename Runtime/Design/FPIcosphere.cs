namespace FuzzPhyte.Utility
{
    using System.Collections.Generic;
    using UnityEngine;
    public class FPIcosphere
    {
        public class TriangleIndices
        {
            public int v1, v2, v3;

            public TriangleIndices(int v1, int v2, int v3)
            {
                this.v1 = v1;
                this.v2 = v2;
                this.v3 = v3;
            }
        }
        private float radius;
        private int numDivisions;
        private List<Vector3> vertices;

        public FPIcosphere(float radius, int numDivisions, bool bottomHalf)
        {
            this.radius = radius;
            this.numDivisions = numDivisions;
            vertices = new List<Vector3>();

            GenerateIcosphere(bottomHalf);
        }

        public List<Vector3> GetVertices()
        {
            return vertices;
        }

        protected void GenerateIcosphere(bool createBottomHalf)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;

            vertices.Add(new Vector3(-1, t, 0).normalized * radius);
            vertices.Add(new Vector3(1, t, 0).normalized * radius);
            vertices.Add(new Vector3(-1, -t, 0).normalized * radius);
            vertices.Add(new Vector3(1, -t, 0).normalized * radius);

            vertices.Add(new Vector3(0, -1, t).normalized * radius);
            vertices.Add(new Vector3(0, 1, t).normalized * radius);
            vertices.Add(new Vector3(0, -1, -t).normalized * radius);
            vertices.Add(new Vector3(0, 1, -t).normalized * radius);

            vertices.Add(new Vector3(t, 0, -1).normalized * radius);
            vertices.Add(new Vector3(t, 0, 1).normalized * radius);
            vertices.Add(new Vector3(-t, 0, -1).normalized * radius);
            vertices.Add(new Vector3(-t, 0, 1).normalized * radius);

            List<TriangleIndices> faces = new List<TriangleIndices>
            {
                new TriangleIndices(0, 11, 5),
                new TriangleIndices(0, 5, 1),
                new TriangleIndices(0, 1, 7),
                new TriangleIndices(0, 7, 10),
                new TriangleIndices(0, 10, 11),
                new TriangleIndices(1, 5, 9),
                new TriangleIndices(5, 11, 4),
                new TriangleIndices(11, 10, 2),
                new TriangleIndices(10, 7, 6),
                new TriangleIndices(7, 1, 8),
                new TriangleIndices(3, 9, 4),
                new TriangleIndices(3, 4, 2),
                new TriangleIndices(3, 2, 6),
                new TriangleIndices(3, 6, 8),
                new TriangleIndices(3, 8, 9),
                new TriangleIndices(4, 9, 5),
                new TriangleIndices(2, 4, 11),
                new TriangleIndices(6, 2, 10),
                new TriangleIndices(8, 6, 7),
                new TriangleIndices(9, 8, 1)
            };

            // Subdivide triangles
            /*
            for (int i = 0; i < numDivisions; i++)
            {
                List<TriangleIndices> newFaces = new List<TriangleIndices>();
                foreach (TriangleIndices face in faces)
                {
                    int a = GetMiddlePoint(face.v1, face.v2);
                    int b = GetMiddlePoint(face.v2, face.v3);
                    int c = GetMiddlePoint(face.v3, face.v1);

                    if (createBottomHalf)
                    {
                        if (vertices[face.v1].y < 0 && vertices[face.v2].y < 0 && vertices[face.v3].y < 0)
                        {
                            newFaces.Add(new TriangleIndices(face.v1, a, c));
                            newFaces.Add(new TriangleIndices(face.v2, b, a));
                            newFaces.Add(new TriangleIndices(face.v3, c, b));
                            newFaces.Add(new TriangleIndices(a, b, c));
                        }
                    }
                    else
                    {
                        if (vertices[face.v1].y >= 0 && vertices[face.v2].y >= 0 && vertices[face.v3].y >= 0)
                        {
                            newFaces.Add(new TriangleIndices(face.v1, a, c));
                            newFaces.Add(new TriangleIndices(face.v2, b, a));
                            newFaces.Add(new TriangleIndices(face.v3, c, b));
                            newFaces.Add(new TriangleIndices(a, b, c));
                        }
                    }
                }
                faces = newFaces;
            }
            */

            for (int i = 0; i < numDivisions; i++)
            {
                List<TriangleIndices> newFaces = new();
                foreach (var face in faces)
                {
                    int a = GetMiddlePoint(face.v1, face.v2);
                    int b = GetMiddlePoint(face.v2, face.v3);
                    int c = GetMiddlePoint(face.v3, face.v1);

                    newFaces.Add(new TriangleIndices(face.v1, a, c));
                    newFaces.Add(new TriangleIndices(face.v2, b, a));
                    newFaces.Add(new TriangleIndices(face.v3, c, b));
                    newFaces.Add(new TriangleIndices(a, b, c));
                }
                faces = newFaces;
            }

            // Now filter based on createBottomHalf
            faces = faces.FindAll(face =>
            {
                Vector3 v1 = vertices[face.v1];
                Vector3 v2 = vertices[face.v2];
                Vector3 v3 = vertices[face.v3];

                return createBottomHalf
                    ? (v1.y < 0 && v2.y < 0 && v3.y < 0)
                    : (v1.y >= 0 && v2.y >= 0 && v3.y >= 0);
            });

        }

        private Dictionary<long, int> middlePointIndexCache = new Dictionary<long, int>();

        private int GetMiddlePoint(int index1, int index2)
        {
            long smallerIndex = Mathf.Min(index1, index2);
            long greaterIndex = Mathf.Max(index1, index2);
            long key = (smallerIndex << 32) + greaterIndex;

            int ret;
            if (middlePointIndexCache.TryGetValue(key, out ret))
            {
                return ret;
            }

            Vector3 point1 = vertices[index1];
            Vector3 point2 = vertices[index2];
            Vector3 middle = Vector3.Lerp(point1, point2, 0.5f).normalized * radius;

            ret = vertices.Count;
            vertices.Add(middle);

            middlePointIndexCache.Add(key, ret);
            return ret;
        }
    }
}
