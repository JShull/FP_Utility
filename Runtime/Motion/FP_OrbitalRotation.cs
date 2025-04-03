namespace FuzzPhyte.Utility
{
    using System.Collections;
    using UnityEngine;

    /// <summary>
    /// Austin Connolly
    /// April 3, 2025
    /// This script utilizes the FP_MotionBase and allows a simple orbital rotation
    /// </summary>

    public class FP_OrbitalRotation : FP_MotionBase
    {

        [Header("Austins Orbital Parameters")]
        public float rotationSpeed = 10f;
        protected Vector3 lastMousePosition; // Last mouse position, ofcourse  
        protected float rotationX = 0f; // Track rotation in the X direction
        protected float rotationY = 0f; // Track rotation in the Y direction
        protected float rotationZ = 0f; // Track roation in the Z direction 
        protected Vector3 currentCoordinate;

        public virtual void Setup(Vector3 startposition)
        {
            currentCoordinate = startposition;
            lastMousePosition = currentCoordinate;
            base.SetupMotion();
        }
        public override void StartMotion()
        {
            loop = true;
            base.StartMotion();
        }
        public override void EndMotion()
        {
            loop = false;
            base.EndMotion();
        }
        /// <summary>
        /// Pass in your updated coordinate
        /// </summary>
        /// <param name="Coordinate">This assumes mouse position</param>
        public virtual void OrbitalRotation(Vector3 Coordinate)
        {
            currentCoordinate = Coordinate;
        }
        protected override IEnumerator MotionRoutine()
        {
            do
            {
                Vector3 deltaMousePosition = currentCoordinate - lastMousePosition;
                rotationX += deltaMousePosition.y * rotationSpeed * Time.deltaTime;
                rotationY -= deltaMousePosition.x * rotationSpeed * Time.deltaTime;
                rotationZ += deltaMousePosition.z * rotationSpeed * Time.deltaTime;

                // Apply the rotation to the object
                targetObject.transform.rotation = Quaternion.Euler(rotationX, rotationY, rotationZ);

                // Update the last mouse position for the next frame
                lastMousePosition = currentCoordinate;
                yield return new WaitForEndOfFrame();
            }
            while (loop);
            EndMotion();
        }
    }
}

