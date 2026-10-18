// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

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
        [Tooltip("1 = allow rotation, 0 = lock relative to the starting pose. Z-only aims local up; other combinations aim local -Z.")]
        private Vector3 _zeroOutAxis = new Vector3(1, 1, 1); // Controls which axes to zero out      
        private Quaternion _originalRot;
        private bool _hasStartingRotation;
        private float _outerAngle;

        protected override void Start()
        {
            if (!_hasStartingRotation)
            {
                _originalRot = (targetObject != null ? targetObject : transform).rotation;
                _hasStartingRotation = true;
            }
            base.Start();
        }
        public override void SetupMotion()
        {
            if (targetObject == null)
            {
                targetObject = transform;
            }
            _originalRot = targetObject.rotation;
            _hasStartingRotation = true;
            _outerAngle = 0;
            base.SetupMotion();
        }
        public void SetupLockAxis(Transform objectToRotate, Transform objectToRotateTowards, bool startImmediately = false)
        {
            targetObject = objectToRotate;
            targetAligned = objectToRotateTowards;
            EndMotion();
            SetupMotion();
            if (startImmediately && !IsRunning)
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
                if (!isPaused && targetObject != null && targetAligned != null)
                {
                    Vector3 direction = targetAligned.position - targetObject.position;
                    if (direction.sqrMagnitude > 1e-12f)
                    {
                        targetObject.rotation = CalculateRotation(direction);
                    }
                }
                yield return null;
            }
            while (loop);
            EndMotion();
        }
        private Quaternion CalculateRotation(Vector3 direction)
        {
            bool x = _zeroOutAxis.x == 1;
            bool y = _zeroOutAxis.y == 1;
            bool z = _zeroOutAxis.z == 1;
            int count = (x ? 1 : 0) + (y ? 1 : 0) + (z ? 1 : 0);
            if (count == 0)
            {
                return _originalRot;
            }

            Vector3 localDirection = Quaternion.Inverse(_originalRot) * direction.normalized;
            if (count == 1)
            {
                Vector3 axis = x ? Vector3.right : y ? Vector3.up : Vector3.forward;
                // Roll cannot steer forward, so a Z-only pointer tracks using its up axis.
                Vector3 reference = z ? Vector3.up : Vector3.back;
                Vector3 projected = Vector3.ProjectOnPlane(localDirection, axis);
                if (projected.sqrMagnitude < 1e-12f)
                {
                    return targetObject.rotation;
                }
                return _originalRot * Quaternion.AngleAxis(Vector3.SignedAngle(reference, projected, axis), axis);
            }

            Vector3 forward = -localDirection;
            if (count == 3)
            {
                Vector3 up = Vector3.ProjectOnPlane(Quaternion.Inverse(_originalRot) * targetObject.up, forward);
                if (up.sqrMagnitude < 1e-8f)
                {
                    up = Vector3.ProjectOnPlane(Vector3.up, forward);
                    if (up.sqrMagnitude < 1e-8f)
                    {
                        up = Vector3.ProjectOnPlane(Vector3.forward, forward);
                    }
                }
                return _originalRot * Quaternion.LookRotation(forward, up);
            }

            // Each pair is a two-axis gimbal in the starting frame. The outer axis
            // steers the inner axis, giving full directional coverage without Euler extraction.
            Vector3 outerAxis = z ? Vector3.forward : Vector3.up;
            Vector3 innerAxis = x ? Vector3.right : Vector3.up;
            float radius = z ? new Vector2(forward.x, forward.y).magnitude
                : new Vector2(forward.x, forward.z).magnitude;
            float outer = z ? Mathf.Atan2(x ? forward.x : forward.y, x ? -forward.y : forward.x)
                : Mathf.Atan2(forward.x, forward.z);
            float inner = z ? Mathf.Atan2(radius, forward.z) : Mathf.Atan2(-forward.y, radius);
            if (radius < 1e-6f)
            {
                // The outer angle is undefined at a gimbal pole. Retain its previous heading.
                outer = _outerAngle * Mathf.Deg2Rad;
            }
            outer *= Mathf.Rad2Deg;
            inner *= Mathf.Rad2Deg;
            Quaternion first = _originalRot * Quaternion.AngleAxis(outer, outerAxis) * Quaternion.AngleAxis(inner, innerAxis);
            float alternateInner = z ? -inner : 180f - inner;
            Quaternion second = _originalRot * Quaternion.AngleAxis(outer + 180f, outerAxis) * Quaternion.AngleAxis(alternateInner, innerAxis);
            bool useFirst = Quaternion.Angle(targetObject.rotation, first) <= Quaternion.Angle(targetObject.rotation, second);
            _outerAngle = useFirst ? outer : outer + 180f;
            return useFirst ? first : second;
        }
        #endregion
    }
}
