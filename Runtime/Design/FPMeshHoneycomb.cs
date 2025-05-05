namespace FuzzPhyte.Utility
{
    using UnityEngine;
    using System.Collections.Generic;
    public class FPMeshHoneycomb : MonoBehaviour, IFPOnStartSetup
    {
        [SerializeField] protected float radius = 5;
        [SerializeField] protected int numDivisions = 5;
        [SerializeField] protected float hexagonSize = 1f;
        [SerializeField] protected bool createBottomHalf = false;
        [SerializeField] protected bool flipNormals = false;
        [SerializeField] protected bool useGoldberg = false;
        [SerializeField] protected int goldbergM = 2;
        [SerializeField] protected int goldbergN = 1;

        public MeshFilter MeshFilter;
        private Mesh mesh;
        private List<Vector3> vertices;
        private List<int> triangles;
        [SerializeField] protected bool setupOnStart;
        public bool SetupStart { get => setupOnStart; set => setupOnStart=value; }

        public virtual void Start()
        {
            if (SetupStart)
            {
                CreateBuildHoneycombIcosphere();
            }
        }
        [ContextMenu("Generate estimate for HexagonSize")]
        public void HexagonSizeCalculator()
        {
            int t = goldbergM * goldbergM + goldbergM * goldbergN + goldbergN * goldbergN;
            int count = t * 10;
            float shrinkFactor = 1.2f;
            hexagonSize = (2f * Mathf.PI * radius) / (count* shrinkFactor);
        }
        [ContextMenu("Generate Smart Estimate for HexagonSize")]
        public void HexagonSizeAreaCalculator()
        {
            int t = goldbergM * goldbergM + goldbergM * goldbergN + goldbergN * goldbergN;
            int count = t * 10;

            float surfaceArea = 4f * Mathf.PI * radius * radius;
            float tileArea = surfaceArea / count;

            // Area of regular hexagon: (3√3 / 2) * s² → solve for s
            hexagonSize = Mathf.Sqrt((2f * tileArea) / (3f * Mathf.Sqrt(3f))) * 0.95f; // Add slight shrink
        }
        public void CreateBuildHoneycombIcosphere()
        {
            mesh = new Mesh();
            vertices = new List<Vector3>();
            triangles = new List<int>();
            MeshFilter.mesh = mesh;
            if (useGoldberg)
            {
                //List<Vector3> goldbergVertices = FPGoldbergGenerator.GenerateVertices(radius, goldbergM, goldbergN);
                var faces = FPGoldbergGenerator.GenerateGoldbergFaces(radius,goldbergM,goldbergN);
                foreach (var face in faces)
                {
                    CreatePolygon(face.center, face.sides);
                    //CreateHexagon(vertex);
                }
            }
            else
            {
                CreateHoneycombIcosphere();
            }
            UpdateMesh();

        }
        void CreateHoneycombSphere()
        {
            Vector3[] directions = {
                Vector3.forward,
                Vector3.right,
                Vector3.back,
                Vector3.left
            };

            float angleStart = createBottomHalf ? 0.5f * Mathf.PI : 0;
            float angleEnd = createBottomHalf ? Mathf.PI : 0.5f * Mathf.PI;

            for (int i = 0; i <= numDivisions; i++)
            {
                for (int j = 0; j <= numDivisions; j++)
                {
                    float angleA = Mathf.Lerp(angleStart, angleEnd, i / (float)numDivisions);
                    float angleB = (j / (float)numDivisions) * 2 * Mathf.PI;

                    Vector3 pointOnSphere = new Vector3(
                        Mathf.Sin(angleA) * Mathf.Cos(angleB),
                        Mathf.Cos(angleA),
                        Mathf.Sin(angleA) * Mathf.Sin(angleB)
                    );

                    //CreateHexagon(pointOnSphere * radius);
                    CreatePolygon(pointOnSphere*radius, 6);
                }
            }
        }
        protected virtual void CreateHoneycombIcosphere()
        {
            FPIcosphere icosphere = new FPIcosphere(radius, numDivisions, createBottomHalf);
            List<Vector3> icosphereVertices = icosphere.GetVertices();

            foreach (Vector3 vertex in icosphereVertices)
            {
                CreatePolygon(vertex, 6);
            }
        }

        protected virtual void CreateHexagon(Vector3 center)
        {
            /*
            int vertexIndex = vertices.Count;

            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, center);

            for (int i = 0; i < 6; i++)
            {
                float angle = (i / 6f) * 2 * Mathf.PI;
                Vector3 localPoint = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * hexagonSize;
                Vector3 pointOnHexagon = center + rotation * localPoint;
                vertices.Add(pointOnHexagon);

                if (i < 4)
                {
                    triangles.Add(vertexIndex);
                    triangles.Add(vertexIndex + i + 1);
                    triangles.Add(vertexIndex + i + 2);
                }
            }
            */
            int centerIndex = vertices.Count;
            vertices.Add(center); // Explicitly add center point

            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, center);

            for (int i = 0; i < 6; i++)
            {
                float angle = (i / 6f) * 2 * Mathf.PI;
                Vector3 localPoint = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * hexagonSize;
                vertices.Add(center + rotation * localPoint);
            }

            // Add triangle fan
            for (int i = 0; i < 6; i++)
            {
                int outer1 = centerIndex + 1 + i;
                int outer2 = centerIndex + 1 + ((i + 1) % 6);

                triangles.Add(centerIndex);
                triangles.Add(outer1);
                triangles.Add(outer2);
            }
        }

        protected virtual void CreatePolygon(Vector3 center, int sides)
        {
            int centerIndex = vertices.Count;
            vertices.Add(center);

            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, center);

            for (int i = 0; i < sides; i++)
            {
                float angle = (i / (float)sides) * 2 * Mathf.PI;
                Vector3 localPoint = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * hexagonSize;
                vertices.Add(center + rotation * localPoint);
            }

            for (int i = 0; i < sides; i++)
            {
                int outer1 = centerIndex + 1 + i;
                int outer2 = centerIndex + 1 + ((i + 1) % sides);

                triangles.Add(centerIndex);
                triangles.Add(outer1);
                triangles.Add(outer2);
            }
#if UNITY_EDITOR
            Debug.DrawRay(center, rotation * Vector3.forward * 0.1f, Color.green, 10f);
#endif
        }
        protected virtual void UpdateMesh()
        {
            mesh.Clear();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();

            if (flipNormals)
            {
                Vector3[] normals = mesh.normals;
                for (int i = 0; i < normals.Length; i++)
                {
                    normals[i] = -normals[i];
                }
                mesh.normals = normals;

                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    int[] tris = mesh.GetTriangles(i);
                    for (int j = 0; j < tris.Length; j += 3)
                    {
                        int temp = tris[j];
                        tris[j] = tris[j + 2];
                        tris[j + 2] = temp;
                    }
                    mesh.SetTriangles(tris, i);
                }
            }
        }

    }
}
