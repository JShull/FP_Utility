namespace FuzzPhyte.Utility
{
    using System.Collections;
    using UnityEngine;
    public class FP_LookAtLockAxis : MonoBehaviour,IFPMotionController
    {
        [SerializeField]
        protected Transform _toRotate;

        [SerializeField]
        protected Transform _target;

        protected Coroutine _motionCoroutine;
        [SerializeField]
        [Tooltip("1 = allow rotation, 0 = lock rotation")]
        private Vector3 _zeroOutAxis = new Vector3(0, 1, 1); // Controls which axes to zero out
        [SerializeField] protected bool RunOnStart = true;
        private bool isPaused = true;
        private bool isActive = false;

        private Quaternion _originalRot;
        public void StartMotion()
        {
            isPaused = false;
            isActive = true;
        }
        // if we want to run on start
        protected virtual void Start()
        {
            //need an original rotation 
            _originalRot = _toRotate.rotation;

            if (RunOnStart)
            {
                SetupMotion();
                StartMotion();
            }
        }
        public void SetupLockAxis(Transform objectToRotate, Transform objectToRotateTowards, bool startImmediately = false)
        {
            _toRotate = objectToRotate;
            _target = objectToRotateTowards;
            _originalRot = _toRotate.rotation;
            EndMotion();
            SetupMotion();
            if (startImmediately)
            {
                StartMotion();
            }
        }
        #region Interface Requirements
        /// <summary>
        /// When we want to pause our motion coroutine
        /// </summary>
        public void PauseMotion()
        {
            if (_motionCoroutine != null)
            {
                isPaused = true;
            }
        }
        /// <summary>
        /// will reset and start the motion
        /// </summary>
        public void ResetMotion()
        {
            if (_motionCoroutine != null)
            {
                StopCoroutine(_motionCoroutine);
            }
            isPaused = false;
            isActive = true;
            _motionCoroutine = StartCoroutine(LoopAction());
        }
        /// <summary>
        /// for when we want to resume/ unpause
        /// </summary>
        public void ResumeMotion()
        {
            isPaused = false;
        }
        /// <summary>
        /// Setup
        /// </summary>
        public void SetupMotion()
        {
            isPaused = true;
            isActive = true;
            if (_motionCoroutine != null)
            {
                StopCoroutine(_motionCoroutine);
            }
            _motionCoroutine = StartCoroutine(LoopAction());
        }
        public void EndMotion()
        {
            isPaused = true;
            isActive = false;
        }

        public void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if(_toRotate != null && _target != null && UnityEditor.Selection.activeGameObject == this.gameObject)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(_toRotate.position, _target.position);
                Gizmos.DrawWireSphere(_target.position, 0.2f);
            }
#endif
        }
#endregion


        public void OnDisable()
        {
            if (_motionCoroutine != null)
            {
                StopCoroutine(_motionCoroutine);
            }
        }
        /*
        protected virtual void Update()
        {
            Vector3 dirToTarget = (_target.position - _toRotate.position).normalized;

            // Get the desired rotation towards the target
            Quaternion lookRotation = Quaternion.LookRotation(-dirToTarget, Vector3.up);

            // Extract the euler angles of the new rotation
            Vector3 lookEulerAngles = lookRotation.eulerAngles;

            // Preserve the locked axes by blending original rotation on those axes
            float xRot = _zeroOutAxis.x == 1 ? lookEulerAngles.x : _originalRot.eulerAngles.x;
            float yRot = _zeroOutAxis.y == 1 ? lookEulerAngles.y : _originalRot.eulerAngles.y;
            float zRot = _zeroOutAxis.z == 1 ? lookEulerAngles.z : _originalRot.eulerAngles.z;

            // Apply the modified rotation with locked axes
            _toRotate.rotation = Quaternion.Euler(xRot, yRot, zRot);
        }
        */
        private IEnumerator LoopAction()
        {
            while (isActive)
            {
                if (!isPaused)
                {
                    Vector3 dirToTarget = (_target.position - _toRotate.position).normalized;

                    // Get the desired rotation towards the target
                    Quaternion lookRotation = Quaternion.LookRotation(-dirToTarget, Vector3.up);

                    // Extract the euler angles of the new rotation
                    Vector3 lookEulerAngles = lookRotation.eulerAngles;

                    // Preserve the locked axes by blending original rotation on those axes
                    float xRot = _zeroOutAxis.x == 1 ? lookEulerAngles.x : _originalRot.eulerAngles.x;
                    float yRot = _zeroOutAxis.y == 1 ? lookEulerAngles.y : _originalRot.eulerAngles.y;
                    float zRot = _zeroOutAxis.z == 1 ? lookEulerAngles.z : _originalRot.eulerAngles.z;

                    // Apply the modified rotation with locked axes
                    _toRotate.rotation = Quaternion.Euler(xRot, yRot, zRot);
                }
                yield return null;
            }
        }
    }
}
