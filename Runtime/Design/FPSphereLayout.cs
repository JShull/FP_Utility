namespace FuzzPhyte.Utility
{
    using System.Collections.Generic;
    using UnityEngine;
    public class FPSphereLayout : MonoBehaviour
    {
        public float radius = 1.0f;
        public float startTheta = 0.0f;
        public float endTheta = 180.0f;
        public float startPhi = 0.0f;
        public float endPhi = 360.0f;
        [SerializeField] private int latitudeBands = 6; // vertical rings (theta divisions)
        [SerializeField] private int itemsPerBand = 12; // default count for middle band
        public List<GameObject> PotentialItems = new List<GameObject>();

        [ContextMenu("Test in Editor")]
        public void SphereEditorTest()
        {
            RunSphereLayout(PotentialItems, this.transform.position);
        }
        public void RunSphereLayout(List<GameObject> listItems, Vector3 pivotPt)
        {
            SphereLayout(listItems,pivotPt);
        }
        public void RunSphereLayoutChildren(float rad, Transform aTransformParent)
        {
            radius = rad;
            RunSphereLayoutChildren(aTransformParent);
        }
        public void RunSphereLayoutChildren(float rad, Transform aTransformParent,float StartTheta, float EndTheta)
        {
            radius = rad;
            startTheta = StartTheta;
            endTheta = EndTheta;
            RunSphereLayoutChildren(aTransformParent);
        }
        public void RunSphereLayoutChildren(float rad, Transform aTransformParent,float StartTheta, float EndTheta,float StartPhi,float EndPhi)
        {
            radius = rad;
            startTheta = StartTheta;
            endTheta = EndTheta;
            startPhi = StartPhi;
            endPhi = EndPhi;
            RunSphereLayoutChildren(aTransformParent);
        }
        
        public void RunSphereLayoutChildren(Transform theTransform)
        {
            PotentialItems.Clear();
            
            foreach (Transform child in theTransform)
            {
                PotentialItems.Add(child.gameObject);
            }
            SphereLayout(PotentialItems, theTransform.position);
        }
        protected void SphereLayout(List<GameObject> items, Vector3 thePivotTransform)
        {
            if (items.Count == 0 || latitudeBands < 2) return;

            float thetaStartRad = Mathf.Deg2Rad * startTheta;
            float thetaEndRad = Mathf.Deg2Rad * endTheta;
            float phiStartRad = Mathf.Deg2Rad * startPhi;
            float phiEndRad = Mathf.Deg2Rad * endPhi;

            List<Vector3> points = new List<Vector3>();
            int totalItems = items.Count;
            int placed = 0;

            for (int i = 0; i < latitudeBands && placed < totalItems; i++)
            {
                float t = (float)i / (latitudeBands - 1);
                float theta = Mathf.Lerp(thetaStartRad, thetaEndRad, t);

                int bandCount = Mathf.RoundToInt(Mathf.Sin(theta) * latitudeBands * 2);
                bandCount = Mathf.Max(1, bandCount);

                for (int j = 0; j < bandCount && placed < totalItems; j++)
                {
                    float phi = Mathf.Lerp(phiStartRad, phiEndRad, (float)j / bandCount);

                    float x = radius * Mathf.Sin(theta) * Mathf.Cos(phi);
                    float y = radius * Mathf.Cos(theta);
                    float z = radius * Mathf.Sin(theta) * Mathf.Sin(phi);

                    items[placed].transform.position = new Vector3(x, y, z) + thePivotTransform;
                    items[placed].transform.LookAt(thePivotTransform);
                    placed++;
                }
            }

            Debug.Log($"Placed {placed}/{items.Count} items over {latitudeBands} latitudes.");
        }
    }
}
