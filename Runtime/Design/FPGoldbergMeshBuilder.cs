namespace FuzzPhyte.Utility
{
    using UnityEngine;
    using System.Collections.Generic;
    public class FPGoldbergMeshBuilder : MonoBehaviour,IFPOnStartSetup
    {
        [Header("Goldberg Parameters")]
        public int m = 2;
        public int n = 1;
        public float radius = 4f;
        [SerializeField]protected bool generateOnStart = true;
        [SerializeField] protected bool useVertexColors = false;
        [SerializeField]protected MeshFilter meshFilter;
        [SerializeField]protected MeshRenderer meshRenderer;
        public delegate void GoldbergMeshBuilder(MeshFilter meshGO);
        public event GoldbergMeshBuilder OnMeshGeneratedEvent;
        public bool SetupStart { get => generateOnStart; set => generateOnStart=value; }

        public void Start()
        {
            if (SetupStart)
            {
                GenerateMesh();
            }
        }
        /// <summary>
        /// Generate Mesh with passed parameters
        /// </summary>
        /// <param name="rad"></param>
        /// <param name="theM"></param>
        /// <param name="theN"></param>
        public void GenerateMesh(float rad,int theM,int theN)
        {
            m = theM;
            n = theN;
            radius = rad;
            GenerateMesh();
        }
        [ContextMenu("Editor Test Generate Goldberg Mesh")]
        public void GenerateMesh()
        {
            if(meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
                if (meshFilter == null)
                {
                    Debug.LogError($"Missing a mesh filter, just adding one here for you");
                    meshFilter = gameObject.AddComponent<MeshFilter>();
                }
            }
            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
                if (meshRenderer == null)
                {
                    Debug.LogError($"Missing a mesh renderer, just adding one here for you");
                    meshRenderer = gameObject.AddComponent<MeshRenderer>();
                }
            }
            
            // Build a base icosahedron
            FPIcosahedron icosa = new FPIcosahedron(radius);
            List<Vector3> baseVertices = icosa.Vertices;
            List<int[]> baseTriangles = icosa.Faces;

            // Subdivide and project points
            Dictionary<long, int> vertexLookup = new();
            List<Vector3> vertices = new();
            List<int> triangles = new();
            List<Vector2> uvs = new();
            List<Color> colors = new();

            foreach (var face in baseTriangles)
            {
                SubdivideTriangle(
                    baseVertices[face[0]],
                    baseVertices[face[1]],
                    baseVertices[face[2]],
                    m, n, radius,
                    vertices, triangles,
                    vertexLookup,uvs,colors
                );
            }

            // Build final mesh
            Mesh mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.uv = uvs.ToArray();
            if (useVertexColors)
            {
                mesh.colors = colors.ToArray();
            }
            mesh.RecalculateNormals();


            meshFilter.mesh = mesh;
            OnMeshGeneratedEvent?.Invoke(meshFilter);
        }

        protected void SubdivideTriangle(
            Vector3 v0, Vector3 v1, Vector3 v2,
            int m, int n, float radius,
            List<Vector3> vertices, List<int> triangles,
            Dictionary<long, int> vertexLookup, List<Vector2>uvs,List<Color>colors)
        {
            int divisions = m + n;
            for (int i = 0; i <= divisions; i++)
            {
                for (int j = 0; j <= divisions - i; j++)
                {
                    Vector3 point =
                        (v0 * (divisions - i - j) +
                         v1 * i +
                         v2 * j) / divisions;

                    point = point.normalized * radius;
                    int index = GetOrAddVertex(point, vertices, vertexLookup);

                    while (uvs.Count <= index)
                        uvs.Add(new Vector2(point.x, point.z));

                    if (useVertexColors)
                    {
                        while (colors.Count <= index)
                            colors.Add(Color.Lerp(Color.red, Color.blue, point.y / (2f * radius) + 0.5f));
                    }

                    if (i + j < divisions)
                    {
                        int a = index;
                        int b = GetOrAddVertex(
                            ((v0 * (divisions - i - j - 1) + v1 * (i + 1) + v2 * j) / divisions).normalized * radius, vertices, vertexLookup);
                        int c = GetOrAddVertex(
                            ((v0 * (divisions - i - j - 1) + v1 * i + v2 * (j + 1)) / divisions).normalized * radius, vertices, vertexLookup);
                        triangles.Add(a);
                        triangles.Add(b);
                        triangles.Add(c);

                        if (i + j < divisions - 1)
                        {
                            int d = GetOrAddVertex(
                                ((v0 * (divisions - i - j - 2) + v1 * (i + 1) + v2 * (j + 1)) / divisions).normalized * radius, vertices, vertexLookup);
                            triangles.Add(b);
                            triangles.Add(d);
                            triangles.Add(c);
                        }
                    }
                }
            }

        }
        protected int GetOrAddVertex(Vector3 vertex, List<Vector3> vertices, Dictionary<long, int> lookup)
        {
            long hash = HashVector(vertex);
            if (lookup.TryGetValue(hash, out int index))
                return index;

            index = vertices.Count;
            vertices.Add(vertex);
            lookup.Add(hash, index);
            return index;
        }

        protected long HashVector(Vector3 v)
        {
            int x = Mathf.RoundToInt(v.x * 10000);
            int y = Mathf.RoundToInt(v.y * 10000);
            int z = Mathf.RoundToInt(v.z * 10000);
            return ((long)x << 40) + ((long)y << 20) + z;
        }
    }
}
