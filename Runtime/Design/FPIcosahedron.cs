namespace FuzzPhyte.Utility
{
    using System.Collections.Generic;
    using UnityEngine;
    
    public class FPIcosahedron
    {
        public List<Vector3> Vertices;
        public List<int[]> Faces;

        public FPIcosahedron(float radius)
        {
            Vertices = new();
            Faces = new();

            float t = (1f + Mathf.Sqrt(5f)) / 2f;

            Vertices.Add(new Vector3(-1, t, 0).normalized * radius);
            Vertices.Add(new Vector3(1, t, 0).normalized * radius);
            Vertices.Add(new Vector3(-1, -t, 0).normalized * radius);
            Vertices.Add(new Vector3(1, -t, 0).normalized * radius);

            Vertices.Add(new Vector3(0, -1, t).normalized * radius);
            Vertices.Add(new Vector3(0, 1, t).normalized * radius);
            Vertices.Add(new Vector3(0, -1, -t).normalized * radius);
            Vertices.Add(new Vector3(0, 1, -t).normalized * radius);

            Vertices.Add(new Vector3(t, 0, -1).normalized * radius);
            Vertices.Add(new Vector3(t, 0, 1).normalized * radius);
            Vertices.Add(new Vector3(-t, 0, -1).normalized * radius);
            Vertices.Add(new Vector3(-t, 0, 1).normalized * radius);

            Faces.Add(new int[] { 0, 11, 5 });
            Faces.Add(new int[] { 0, 5, 1 });
            Faces.Add(new int[] { 0, 1, 7 });
            Faces.Add(new int[] { 0, 7, 10 });
            Faces.Add(new int[] { 0, 10, 11 });

            Faces.Add(new int[] { 1, 5, 9 });
            Faces.Add(new int[] { 5, 11, 4 });
            Faces.Add(new int[] { 11, 10, 2 });
            Faces.Add(new int[] { 10, 7, 6 });
            Faces.Add(new int[] { 7, 1, 8 });

            Faces.Add(new int[] { 3, 9, 4 });
            Faces.Add(new int[] { 3, 4, 2 });
            Faces.Add(new int[] { 3, 2, 6 });
            Faces.Add(new int[] { 3, 6, 8 });
            Faces.Add(new int[] { 3, 8, 9 });

            Faces.Add(new int[] { 4, 9, 5 });
            Faces.Add(new int[] { 2, 4, 11 });
            Faces.Add(new int[] { 6, 2, 10 });
            Faces.Add(new int[] { 8, 6, 7 });
            Faces.Add(new int[] { 9, 8, 1 });
        }
    }
}
