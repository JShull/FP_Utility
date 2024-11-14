namespace FuzzPhyte.Utility
{
    using System.Collections;
    using UnityEngine;

    /// <summary>
    /// Moves the transform along a specified axis by a certain distance there and back
    /// </summary>
    public class FP_VectorMove : FP_MotionBase
    {
        [Space]
        [Header("Vector Move Settings")]
        // The axis along which the transform will move (e.g., Vector3.right for the X-axis)
        public Vector3 moveAxis = Vector3.right;
        public AnimationCurve VectorCurve;
        // The distance to move along the axis
        public float moveDistance = 5f;

        // Boolean to decide if movement is in local or world space
        public bool useLocalSpace = false;
        [Tooltip("With loop enabled & this enabled, the object will move back and forth. Just loop = jumps back")]
        public bool returnToDestination = false;
        protected Vector3 targetPos;
        protected Vector3 startPos;

        protected void OnEnable()
        {
            if (useLocalSpace)
            {
                // Calculate movement in local space
                startPos = targetObject.localPosition;
                targetPos = startPos + (moveAxis.normalized * moveDistance);
            }
            else
            {
                // Calculate movement in world space
                startPos = targetObject.position;
                targetPos = startPos + (moveAxis.normalized * moveDistance);
            }
        }

        // Coroutine that loops the movement along the axis
        protected override IEnumerator MotionRoutine()
        {
            do
            {
                if (!isPaused)
                {
                    // Move in the positive direction
                    yield return StartCoroutine(MoveByDistance(moveAxis, moveDistance));
                    if (loop && !returnToDestination)
                    {
                        //Jump back start position
                        if (useLocalSpace)
                        {
                            targetObject.localPosition = startPos;
                        }
                        else
                        {
                            targetObject.position = startPos;
                        }
                    }
                }
                if (!isPaused && returnToDestination)
                {
                    // Move in the opposite direction
                    yield return StartCoroutine(MoveByDistance(-moveAxis, moveDistance));
                }
                yield return null;
            }
            while (loop);
            EndMotion();
            
        }
        
        // Coroutine that moves the transform by a certain distance along an axis
        private IEnumerator MoveByDistance(Vector3 direction, float distance)
        {
            startPos = targetObject.position;
            targetPos = startPos + (direction.normalized * distance);
            if (useLocalSpace)
            {
                // Calculate movement in local space
                startPos = targetObject.localPosition;
                targetPos = startPos + (direction.normalized * distance);
            }
            else
            {
                // Calculate movement in world space
                startPos = targetObject.position;
                targetPos = startPos + (direction.normalized * distance);
            }
            float elapsedTime = 0f;

            while (elapsedTime < lerpDuration)
            {
                // Move the transform based on elapsed time and moveTime
                var outcome = VectorCurve.Evaluate(elapsedTime / lerpDuration);
                if (useLocalSpace)
                {
                    
                    targetObject.localPosition = Vector3.Lerp(startPos, targetPos, outcome);
                }
                else
                {
                    targetObject.position = Vector3.Lerp(startPos, targetPos, outcome);
                }
                
                elapsedTime += Time.deltaTime;

                yield return null; // Wait for the next frame
            }

            // Ensure the transform reaches the exact target position
            if (useLocalSpace)
            {
                targetObject.localPosition = targetPos;
            }
            else
            {
                targetObject.position = targetPos;
            }
        }
        public override void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if (UnityEditor.Selection.activeGameObject == this.gameObject)
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
