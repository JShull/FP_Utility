using UnityEngine;

namespace FuzzPhyte.Utility
{
 
    public class FPSliceTest : MonoBehaviour
    {
        public Renderer MyMatRenderer;
        public string RefName = "_PositionIntersection";
        public string RadiusName = "_RadiusTest";
        public Transform TargetRadiusSlicer;
        [Range(0,1f)]
        public float RadiusSlice = 0.5f;

        [SerializeField]protected Material MyMat;

        public void Start()
        {
            if (MyMatRenderer != null)
            {
                MyMat = MyMatRenderer.material;
            }
            else
            {
                Debug.LogWarning("MyMatRenderer is not assigned.");
            }
        }
        public void Update()
        {
            if (MyMat == null || TargetRadiusSlicer==null)
            {
                return;
            }
            //  targetMat.SetFloat("_referenceName", alpha);

            MyMat.SetVector(RefName, TargetRadiusSlicer.transform.position);
            MyMat.SetFloat(RadiusName, RadiusSlice);
        }
    }
}
