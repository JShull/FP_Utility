namespace FuzzPhyte.Utility
{
    using UnityEngine;
    /// <summary>
    /// Helps for rotating a child object with a parent object in conditions of moving/rotating one 
    /// but don't want to parent/child the relationship
    /// </summary>
    public class RotateChildParent : MonoBehaviour
    {
        public bool ActiveFollow = true;
        public Transform ReferenceTransform; // The transform to follow and rotate with
        public Vector3 PositionOffset; // Offset from the reference transform's position
        public Vector3 RotationOffset; // Offset from the reference transform's rotation
        protected virtual void Start()
        {
            if (ReferenceTransform == null)
            {
                Debug.LogWarning("Reference Transform is not assigned.");
                return;
            }

            // Calculate the initial position offset
            PositionOffset = Quaternion.Inverse(ReferenceTransform.rotation) * (transform.position - ReferenceTransform.position);
            // Calculate the initial rotation offset
            RotationOffset = (Quaternion.Inverse(ReferenceTransform.rotation) * transform.rotation).eulerAngles;
        }
        protected virtual void Update()
        {
            if (!ActiveFollow)
            {
                return;
            }
            if (ReferenceTransform == null)
            {
                Debug.LogWarning("Reference Transform is not assigned.");
                return;
            }

            // Calculate the new position with offset
            Vector3 offsetPosition = ReferenceTransform.position + ReferenceTransform.rotation * PositionOffset;

            // Apply the calculated position to this transform
            transform.position = offsetPosition;

            // Calculate the new rotation with offset
            Quaternion offsetRotation = ReferenceTransform.rotation * Quaternion.Euler(RotationOffset);

            // Apply the calculated rotation to this transform
            transform.rotation = offsetRotation;
        }
    }
}
