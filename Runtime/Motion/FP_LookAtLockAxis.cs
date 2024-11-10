namespace FuzzPhyte.Utility
{
    using System.Collections;
    using UnityEngine;
    /// <summary>
    /// Can be used in a few ways
    /// Generally used to lock the rotation of an object to a target object and run forever
    /// You could use it once, just make sure to call StartMotion() and SetupMotion() and have loop = false
    /// </summary>
    public class FP_LookAtLockAxis : FP_MotionBase
    {
        [Space]
        [Header("Look At Lock Axis Settings")]
        [SerializeField]
        protected Transform targetAligned;

        [SerializeField]
        [Tooltip("1 = allow rotation, 0 = lock rotation to start")]
        private Vector3 _zeroOutAxis = new Vector3(1, 1, 1); // Controls which axes to zero out      
        private Quaternion _originalRot;

        protected override void Start()
        {
            //need an original rotation 
            _originalRot = targetObject.rotation;
            base.Start();
        }
        public void SetupLockAxis(Transform objectToRotate, Transform objectToRotateTowards, bool startImmediately = false)
        {
            targetObject = objectToRotate;
            targetAligned = objectToRotateTowards;
            _originalRot = targetObject.rotation;
            EndMotion();
            SetupMotion();
            if (startImmediately)
            {
                StartMotion();
            }
        }
        #region Interface Requirements

        public override void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if(targetObject != null && targetAligned != null && UnityEditor.Selection.activeGameObject == this.gameObject)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(targetObject.position, targetAligned.position);
                Gizmos.DrawWireSphere(targetAligned.position, 0.2f);
            }
#endif
        }
        protected override IEnumerator MotionRoutine()
        {
            do
            {
                if (!isPaused)
                {
                    Vector3 dirToTarget = (targetAligned.position - targetObject.position).normalized;

                    // Get the desired rotation towards the target
                    Quaternion lookRotation = Quaternion.LookRotation(-dirToTarget, Vector3.up);

                    // Extract the euler angles of the new rotation
                    Vector3 lookEulerAngles = lookRotation.eulerAngles;

                    // Preserve the locked axes by blending original rotation on those axes
                    float xRot = _zeroOutAxis.x == 1 ? lookEulerAngles.x : _originalRot.eulerAngles.x;
                    float yRot = _zeroOutAxis.y == 1 ? lookEulerAngles.y : _originalRot.eulerAngles.y;
                    float zRot = _zeroOutAxis.z == 1 ? lookEulerAngles.z : _originalRot.eulerAngles.z;

                    // Apply the modified rotation with locked axes
                    targetObject.rotation = Quaternion.Euler(xRot, yRot, zRot);
                }
                yield return null;
            }
            while (loop);
        }
        #endregion
    }
}
