namespace FuzzPhyte.Utility
{
    using System.Collections;
    using UnityEngine;
    using static UnityEngine.GraphicsBuffer;

    public class FP_VectorMove : MonoBehaviour, IFPMotionController
    {
        // The axis along which the transform will move (e.g., Vector3.right for the X-axis)
        public Vector3 moveAxis = Vector3.right;

        // The distance to move along the axis
        public float moveDistance = 5f;

        // Time in seconds it takes to move the distance
        public float moveTime = 2f;

        // Boolean to decide if movement is in local or world space
        public bool useLocalSpace = false;

        // The coroutine that handles the movement
        private Coroutine moveCoroutine;
        [SerializeField] protected bool isActive = true;
        [SerializeField] protected bool isPaused = false;
        protected Vector3 targetPos;
        protected Vector3 startPos;

        
        private void OnEnable()
        {
            if (useLocalSpace)
            {
                // Calculate movement in local space
                startPos = transform.localPosition;
                targetPos = startPos + (moveAxis.normalized * moveDistance);
            }
            else
            {
                // Calculate movement in world space
                startPos = transform.position;
                targetPos = startPos + (moveAxis.normalized * moveDistance);
            }
            SetupMotion();
            StartMotion();
        }

        // Coroutine that loops the movement along the axis
        private IEnumerator MoveLoop()
        {
            while (isActive)
            {
                if (!isPaused)
                {
                    // Move in the positive direction
                    yield return StartCoroutine(MoveByDistance(moveAxis, moveDistance));
                }
                if (!isPaused)
                {
                    // Move in the opposite direction
                    yield return StartCoroutine(MoveByDistance(-moveAxis, moveDistance));
                }
                yield return null;
            }
        }
        private void OnDisable()
        {
            StopAllCoroutines();
        }


        // Coroutine that moves the transform by a certain distance along an axis
        private IEnumerator MoveByDistance(Vector3 direction, float distance)
        {
            startPos = transform.position;
            targetPos = startPos + (direction.normalized * distance);
            if (useLocalSpace)
            {
                // Calculate movement in local space
                startPos = transform.localPosition;
                targetPos = startPos + (direction.normalized * distance);
            }
            else
            {
                // Calculate movement in world space
                startPos = transform.position;
                targetPos = startPos + (direction.normalized * distance);
            }
            float elapsedTime = 0f;

            while (elapsedTime < moveTime)
            {
                // Move the transform based on elapsed time and moveTime
                if (useLocalSpace)
                {
                    transform.localPosition = Vector3.Lerp(startPos, targetPos, elapsedTime / moveTime);
                }
                else
                {
                    transform.position = Vector3.Lerp(startPos, targetPos, elapsedTime / moveTime);
                }
                
                elapsedTime += Time.deltaTime;

                yield return null; // Wait for the next frame
            }

            // Ensure the transform reaches the exact target position
            if (useLocalSpace)
            {
                transform.localPosition = targetPos;
            }
            else
            {
                transform.position = targetPos;
            }
        }

        public void SetupMotion()
        {
            StopAllCoroutines();
            isActive = true;
            isPaused = true;
            moveCoroutine = StartCoroutine(MoveLoop());
        }

        public void StartMotion()
        {
            isPaused = false;
        }

        public void PauseMotion()
        {
            isPaused = true;
        }

        public void ResumeMotion()
        {
            isPaused = false;
        }

        public void ResetMotion()
        {
            SetupMotion();
            StartMotion();
        }

        public void EndMotion()
        {
            isActive = false;
            StopAllCoroutines();
            isPaused = true;
        }

        public void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if (isActive&&UnityEditor.Selection.activeGameObject == this.gameObject)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLineStrip(new Vector3[] { startPos, targetPos },false);
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(startPos, 0.2f);
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(targetPos, 0.2f);
            }
#endif
        }
    }
}
