// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility
{
    using System;
    using System.Collections;
    using UnityEngine;
    using UnityEngine.Events;

    public enum FPArticulationGrabConstraintMode
    {
        Locked = 0,
        Compliant = 1
    }

    public enum FPArticulationGrabState
    {
        Disconnected = 0,
        Connected = 1,
        Paused = 2,
        Broken = 3,
        TrackingError = 4,
        InvalidConfiguration = 5
    }

    [Serializable]
    public sealed class FPArticulationGrabStateEvent : UnityEvent<FPArticulationGrabState>
    {
    }

    /// <summary>
    /// Connects a tracked pose to an ArticulationBody through a temporary
    /// ConfigurableJoint on a kinematic Rigidbody anchor.
    /// </summary>
    [DisallowMultipleComponent]
    public class FP_ArticulationGrabMotion : FP_MotionBase
    {
        [Header("Articulation Grab References")]
        [SerializeField]
        [Tooltip("Articulation link that owns the physical grab point.")]
        private ArticulationBody connectedBody;

        [SerializeField]
        [Tooltip("Point on the connected articulation link where the grip is applied.")]
        private Transform grabPoint;

        [SerializeField]
        [Tooltip("Tracked hand or other world-space pose that the physics anchor follows.")]
        private Transform followTarget;

        [SerializeField]
        [Tooltip("Optional external kinematic Rigidbody. A collider-free anchor is created when omitted.")]
        private Rigidbody physicsAnchor;

        [Header("Anchor Following")]
        [SerializeField]
        private bool createPhysicsAnchorIfMissing = true;

        [SerializeField]
        [Tooltip("Preserve the initial hand-to-grip offset instead of snapping the grip to the target pose.")]
        private bool preserveInitialGrabOffset = true;

        [SerializeField]
        [Tooltip("Before creating a new grab joint, teleport the articulation root so the physical grab point starts at the follow target. This is applied once per connection, not while following.")]
        private bool snapGrabPointToFollowTargetOnStart;

        [SerializeField]
        [Tooltip("Transmit tracked-hand rotation to the articulation in addition to position.")]
        private bool trackRotation = true;

        [Header("Grab Constraint")]
        [SerializeField]
        private FPArticulationGrabConstraintMode constraintMode =
            FPArticulationGrabConstraintMode.Locked;

        [SerializeField]
        [Tooltip("Used on all three linear axes when Constraint Mode is Compliant.")]
        private JointDrive linearDrive = new JointDrive
        {
            positionSpring = 1200f,
            positionDamper = 120f,
            maximumForce = 1000f,
            useAcceleration = false
        };

        [SerializeField]
        [Tooltip("Used as the Slerp drive when Constraint Mode is Compliant and rotation tracking is enabled.")]
        private JointDrive angularDrive = new JointDrive
        {
            positionSpring = 800f,
            positionDamper = 80f,
            maximumForce = 500f,
            useAcceleration = false
        };

        [SerializeField]
        [Tooltip("Joint break force. Zero or less means infinite.")]
        private float breakForce;

        [SerializeField]
        [Tooltip("Joint break torque. Zero or less means infinite.")]
        private float breakTorque;

        [Header("Tracking Safety")]
        [SerializeField]
        [Tooltip("Release the constraint when the hand and physical grip separate too far.")]
        private bool releaseOnMaximumTrackingError;

        [SerializeField]
        [Min(0.001f)]
        private float maximumTrackingError = 0.75f;

        [Header("Articulation Grab Events")]
        [SerializeField]
        private UnityEvent onGrabConnected = new UnityEvent();

        [SerializeField]
        private UnityEvent onGrabReleased = new UnityEvent();

        [SerializeField]
        private UnityEvent onGrabBroken = new UnityEvent();

        [SerializeField]
        private UnityEvent<float> onTrackingErrorExceeded = new UnityEvent<float>();

        [SerializeField]
        private UnityEvent<string> onGrabFailed = new UnityEvent<string>();

        [SerializeField]
        private FPArticulationGrabStateEvent onGrabStateChanged =
            new FPArticulationGrabStateEvent();

        private ConfigurableJoint grabJoint;
        private GameObject ownedAnchorObject;
        private Vector3 targetLocalPositionOffset;
        private Quaternion targetLocalRotationOffset = Quaternion.identity;
        private bool jointExpected;
        private FPArticulationGrabState state = FPArticulationGrabState.Disconnected;

        public ArticulationBody ConnectedBody => connectedBody;
        public Transform GrabPoint => grabPoint;
        public Transform FollowTarget => followTarget;
        public Rigidbody PhysicsAnchor => physicsAnchor;
        public ConfigurableJoint ActiveJoint => grabJoint;
        public FPArticulationGrabConstraintMode ConstraintMode => constraintMode;
        public bool SnapGrabPointToFollowTargetOnStart =>
            snapGrabPointToFollowTargetOnStart;
        public FPArticulationGrabState State => state;
        public bool IsConnected => jointExpected && grabJoint != null;
        public float CurrentPositionError { get; private set; }
        public float CurrentRotationError { get; private set; }
        public string LastFailureMessage { get; private set; } = string.Empty;

        public UnityEvent OnGrabConnected => onGrabConnected;
        public UnityEvent OnGrabReleased => onGrabReleased;
        public UnityEvent OnGrabBroken => onGrabBroken;
        public UnityEvent<float> OnTrackingErrorExceeded => onTrackingErrorExceeded;
        public UnityEvent<string> OnGrabFailed => onGrabFailed;
        public FPArticulationGrabStateEvent OnGrabStateChanged => onGrabStateChanged;

        public event Action<FP_ArticulationGrabMotion> GrabConnected;
        public event Action<FP_ArticulationGrabMotion> GrabReleased;
        public event Action<FP_ArticulationGrabMotion> GrabBroken;
        public event Action<FP_ArticulationGrabMotion, float> TrackingErrorExceeded;
        public event Action<FP_ArticulationGrabMotion, string> GrabFailed;
        public event Action<
            FP_ArticulationGrabMotion,
            FPArticulationGrabState,
            FPArticulationGrabState> GrabStateChanged;

        /// <summary>
        /// Assigns the articulation and physical grip. The target can be assigned now
        /// or supplied later through BeginGrab.
        /// </summary>
        public void Configure(
            ArticulationBody body,
            Transform physicalGrabPoint,
            Transform trackedTarget = null)
        {
            if (IsConnected)
            {
                ReleaseGrab();
            }

            connectedBody = body;
            grabPoint = physicalGrabPoint;
            followTarget = trackedTarget;
        }

        public void SetConnectedBody(ArticulationBody body)
        {
            if (connectedBody == body)
            {
                return;
            }

            if (IsConnected)
            {
                ReleaseGrab();
            }

            connectedBody = body;
        }

        public void SetGrabPoint(Transform physicalGrabPoint)
        {
            if (grabPoint == physicalGrabPoint)
            {
                return;
            }

            if (IsConnected)
            {
                ReleaseGrab();
            }

            grabPoint = physicalGrabPoint;
        }

        public void SetFollowTarget(Transform trackedTarget)
        {
            followTarget = trackedTarget;
            if (IsConnected && followTarget != null)
            {
                CaptureFollowOffsetFromAnchor();
            }
        }

        public void SetPhysicsAnchor(Rigidbody anchor)
        {
            if (physicsAnchor == anchor)
            {
                return;
            }

            if (IsConnected)
            {
                ReleaseGrab();
            }

            DestroyOwnedAnchor();
            physicsAnchor = anchor;
            ConfigurePhysicsAnchor();
        }

        public void SetConstraintMode(FPArticulationGrabConstraintMode mode)
        {
            if (constraintMode == mode)
            {
                return;
            }

            constraintMode = mode;
            if (IsConnected)
            {
                TryRebuildGrabJoint();
            }
        }

        /// <summary>
        /// Controls whether a new connection starts by aligning the articulation's
        /// physical grab point to the assigned follow target.
        /// </summary>
        public void SetSnapGrabPointToFollowTargetOnStart(bool snapOnStart)
        {
            snapGrabPointToFollowTargetOnStart = snapOnStart;
        }

        /// <summary>
        /// UnityEvent-friendly endpoint that uses the currently assigned target.
        /// </summary>
        public void BeginGrab()
        {
            TryBeginGrab(followTarget);
        }

        /// <summary>
        /// UnityEvent-friendly endpoint that assigns and follows a new tracked target.
        /// </summary>
        public void BeginGrab(Transform trackedTarget)
        {
            TryBeginGrab(trackedTarget);
        }

        public bool TryBeginGrab()
        {
            return TryBeginGrab(followTarget);
        }

        public bool TryBeginGrab(Transform trackedTarget)
        {
            if (trackedTarget != null)
            {
                SetFollowTarget(trackedTarget);
            }

            StartMotion();
            return IsConnected;
        }

        public void ReleaseGrab()
        {
            EndMotion();
        }

        public void SetGrabActive(bool active)
        {
            if (active)
            {
                BeginGrab();
            }
            else
            {
                ReleaseGrab();
            }
        }

        public void RebuildGrabJoint()
        {
            TryRebuildGrabJoint();
        }

        public bool TryRebuildGrabJoint()
        {
            bool wasConnected = jointExpected;
            ReleaseJoint(wasConnected);
            if (wasConnected)
            {
                RaiseGrabReleased();
            }

            StartMotion();
            return IsConnected;
        }

        public override void SetupMotion()
        {
            if (!TryPrepareConnection(out string message))
            {
                RaiseGrabFailure(message);
                return;
            }

            targetObject = physicsAnchor.transform;
            base.SetupMotion();
        }

        public override void StartMotion()
        {
            if (!TryPrepareConnection(out string message))
            {
                RaiseGrabFailure(message);
                return;
            }

            targetObject = physicsAnchor.transform;
            bool createdJoint = false;

            if (!IsConnected)
            {
                if (snapGrabPointToFollowTargetOnStart)
                {
                    SnapGrabPointToFollowTarget();
                }

                PrepareAnchorPose();
                CreateGrabJoint();
                createdJoint = true;
            }
            else
            {
                CaptureFollowOffsetFromAnchor();
            }

            base.StartMotion();
            SetState(FPArticulationGrabState.Connected);

            if (createdJoint)
            {
                LastFailureMessage = string.Empty;
                GrabConnected?.Invoke(this);
                onGrabConnected?.Invoke();
            }
        }

        public override void PauseMotion()
        {
            base.PauseMotion();
            if (IsConnected)
            {
                SetState(FPArticulationGrabState.Paused);
            }
        }

        public override void ResumeMotion()
        {
            base.ResumeMotion();
            if (IsConnected)
            {
                SetState(FPArticulationGrabState.Connected);
            }
        }

        public override void ResetMotion()
        {
            bool wasConnected = jointExpected;
            ReleaseJoint(wasConnected);
            base.ResetMotion();

            if (wasConnected)
            {
                RaiseGrabReleased();
            }

            SetState(FPArticulationGrabState.Disconnected);
        }

        public override void EndMotion()
        {
            bool wasConnected = jointExpected;
            ReleaseJoint(wasConnected);
            base.EndMotion();

            if (wasConnected)
            {
                RaiseGrabReleased();
            }

            SetState(FPArticulationGrabState.Disconnected);
        }

        public override void OnDisable()
        {
            bool wasConnected = jointExpected;
            ReleaseJoint(wasConnected);
            base.OnDisable();

            if (wasConnected)
            {
                RaiseGrabReleased();
            }

            SetState(FPArticulationGrabState.Disconnected);
        }

        private void OnDestroy()
        {
            ReleaseJoint(false);
            DestroyOwnedAnchor();
        }

        protected override IEnumerator MotionRoutine()
        {
            var waitForPhysics = new WaitForFixedUpdate();

            while (jointExpected)
            {
                yield return waitForPhysics;

                if (isPaused)
                {
                    continue;
                }

                if (grabJoint == null)
                {
                    HandleJointBroken();
                    yield break;
                }

                if (followTarget == null)
                {
                    HandleActiveFailure("The articulation grab follow target was removed while connected.");
                    yield break;
                }

                MovePhysicsAnchor();

                if (releaseOnMaximumTrackingError &&
                    CurrentPositionError > maximumTrackingError)
                {
                    HandleTrackingError();
                    yield break;
                }
            }
        }

        public override void OnDrawGizmos()
        {
            if (grabPoint == null)
            {
                return;
            }

            Gizmos.color = IsConnected ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(grabPoint.position, 0.025f);

            if (followTarget != null)
            {
                Gizmos.DrawLine(grabPoint.position, followTarget.position);
                Gizmos.DrawWireSphere(followTarget.position, 0.02f);
            }
        }

        private bool TryPrepareConnection(out string message)
        {
            if (connectedBody == null)
            {
                message = "An ArticulationBody must be assigned before beginning an articulation grab.";
                return false;
            }

            if (grabPoint == null)
            {
                message = "A physical grab point must be assigned before beginning an articulation grab.";
                return false;
            }

            ArticulationBody grabPointOwner =
                grabPoint.GetComponentInParent<ArticulationBody>();
            if (grabPointOwner != connectedBody)
            {
                message = "The grab point must belong to the assigned ArticulationBody link.";
                return false;
            }

            if (followTarget == null)
            {
                message = "A tracked follow target must be assigned before beginning an articulation grab.";
                return false;
            }

            if (!EnsurePhysicsAnchor())
            {
                message = "A kinematic physics anchor is required and automatic creation is disabled.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private bool EnsurePhysicsAnchor()
        {
            if (physicsAnchor != null)
            {
                ConfigurePhysicsAnchor();
                return true;
            }

            if (!createPhysicsAnchorIfMissing)
            {
                return false;
            }

            ownedAnchorObject = new GameObject($"{name} Articulation Grab Anchor");
            physicsAnchor = ownedAnchorObject.AddComponent<Rigidbody>();
            ConfigurePhysicsAnchor();
            return true;
        }

        private void ConfigurePhysicsAnchor()
        {
            if (physicsAnchor == null)
            {
                return;
            }

            physicsAnchor.isKinematic = true;
            physicsAnchor.useGravity = false;
            physicsAnchor.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void PrepareAnchorPose()
        {
            Vector3 anchorPosition = preserveInitialGrabOffset
                ? grabPoint.position
                : followTarget.position;
            Quaternion anchorRotation = preserveInitialGrabOffset
                ? grabPoint.rotation
                : followTarget.rotation;

            physicsAnchor.position = anchorPosition;
            physicsAnchor.rotation = anchorRotation;
            CaptureFollowOffsetFromAnchor();
        }

        private void SnapGrabPointToFollowTarget()
        {
            ArticulationBody rootBody = connectedBody;
            ArticulationBody parentBody =
                rootBody.transform.parent != null
                    ? rootBody.transform.parent.GetComponentInParent<ArticulationBody>()
                    : null;

            while (parentBody != null)
            {
                rootBody = parentBody;
                parentBody =
                    rootBody.transform.parent != null
                        ? rootBody.transform.parent.GetComponentInParent<ArticulationBody>()
                        : null;
            }

            Quaternion rotationDelta = trackRotation
                ? followTarget.rotation * Quaternion.Inverse(grabPoint.rotation)
                : Quaternion.identity;
            Quaternion targetRootRotation = rotationDelta * rootBody.transform.rotation;
            Vector3 rotatedGrabOffset =
                rotationDelta * (grabPoint.position - rootBody.transform.position);
            Vector3 targetRootPosition = followTarget.position - rotatedGrabOffset;

            rootBody.TeleportRoot(targetRootPosition, targetRootRotation);
        }

        private void CaptureFollowOffsetFromAnchor()
        {
            if (followTarget == null || physicsAnchor == null)
            {
                return;
            }

            if (!preserveInitialGrabOffset)
            {
                targetLocalPositionOffset = Vector3.zero;
                targetLocalRotationOffset = Quaternion.identity;
                return;
            }

            targetLocalPositionOffset =
                followTarget.InverseTransformPoint(physicsAnchor.position);
            targetLocalRotationOffset =
                Quaternion.Inverse(followTarget.rotation) * physicsAnchor.rotation;
        }

        private void CreateGrabJoint()
        {
            grabJoint = physicsAnchor.gameObject.AddComponent<ConfigurableJoint>();
            grabJoint.autoConfigureConnectedAnchor = false;
            grabJoint.connectedBody = null;
            grabJoint.connectedArticulationBody = connectedBody;
            grabJoint.anchor = Vector3.zero;
            grabJoint.connectedAnchor =
                connectedBody.transform.InverseTransformPoint(grabPoint.position);
            grabJoint.enableCollision = false;
            grabJoint.breakForce = breakForce > 0f ? breakForce : Mathf.Infinity;
            grabJoint.breakTorque = breakTorque > 0f ? breakTorque : Mathf.Infinity;

            if (constraintMode == FPArticulationGrabConstraintMode.Locked)
            {
                ConfigureLockedJoint(grabJoint);
            }
            else
            {
                ConfigureCompliantJoint(grabJoint);
            }

            jointExpected = true;
        }

        private void ConfigureLockedJoint(ConfigurableJoint joint)
        {
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;

            ConfigurableJointMotion angularMotion = trackRotation
                ? ConfigurableJointMotion.Locked
                : ConfigurableJointMotion.Free;
            joint.angularXMotion = angularMotion;
            joint.angularYMotion = angularMotion;
            joint.angularZMotion = angularMotion;
        }

        private void ConfigureCompliantJoint(ConfigurableJoint joint)
        {
            joint.xMotion = ConfigurableJointMotion.Free;
            joint.yMotion = ConfigurableJointMotion.Free;
            joint.zMotion = ConfigurableJointMotion.Free;
            joint.xDrive = linearDrive;
            joint.yDrive = linearDrive;
            joint.zDrive = linearDrive;
            joint.targetPosition = Vector3.zero;

            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Free;
            joint.angularZMotion = ConfigurableJointMotion.Free;

            if (trackRotation)
            {
                joint.rotationDriveMode = RotationDriveMode.Slerp;
                joint.slerpDrive = angularDrive;
                joint.targetRotation = Quaternion.identity;
            }
        }

        private void MovePhysicsAnchor()
        {
            Vector3 desiredPosition =
                followTarget.TransformPoint(targetLocalPositionOffset);
            Quaternion desiredRotation =
                followTarget.rotation * targetLocalRotationOffset;

            CurrentPositionError =
                Vector3.Distance(grabPoint.position, desiredPosition);
            CurrentRotationError = trackRotation
                ? Quaternion.Angle(grabPoint.rotation, desiredRotation)
                : 0f;

            physicsAnchor.MovePosition(desiredPosition);
            if (trackRotation)
            {
                physicsAnchor.MoveRotation(desiredRotation);
            }
        }

        private void HandleJointBroken()
        {
            jointExpected = false;
            grabJoint = null;
            GrabBroken?.Invoke(this);
            onGrabBroken?.Invoke();
            RaiseGrabReleased();
            SetState(FPArticulationGrabState.Broken);
            base.EndMotion();
        }

        private void HandleTrackingError()
        {
            float error = CurrentPositionError;
            ReleaseJoint(true);
            TrackingErrorExceeded?.Invoke(this, error);
            onTrackingErrorExceeded?.Invoke(error);
            RaiseGrabReleased();
            SetState(FPArticulationGrabState.TrackingError);
            base.EndMotion();
        }

        private void HandleActiveFailure(string message)
        {
            bool wasConnected = jointExpected;
            ReleaseJoint(wasConnected);
            if (wasConnected)
            {
                RaiseGrabReleased();
            }

            RaiseGrabFailure(message);
            base.EndMotion();
        }

        private void ReleaseJoint(bool detachBeforeDestroy)
        {
            jointExpected = false;
            CurrentPositionError = 0f;
            CurrentRotationError = 0f;

            if (grabJoint == null)
            {
                return;
            }

            ConfigurableJoint jointToDestroy = grabJoint;
            grabJoint = null;
            if (detachBeforeDestroy)
            {
                jointToDestroy.connectedArticulationBody = null;
            }

            DestroyUnityObject(jointToDestroy);
        }

        private void RaiseGrabReleased()
        {
            GrabReleased?.Invoke(this);
            onGrabReleased?.Invoke();
        }

        private void RaiseGrabFailure(string message)
        {
            LastFailureMessage = message;
            SetState(FPArticulationGrabState.InvalidConfiguration);
            GrabFailed?.Invoke(this, message);
            onGrabFailed?.Invoke(message);
            Debug.LogError($"[FP Articulation Grab] {message}", this);
        }

        private void SetState(FPArticulationGrabState nextState)
        {
            if (state == nextState)
            {
                return;
            }

            FPArticulationGrabState previousState = state;
            state = nextState;
            GrabStateChanged?.Invoke(this, previousState, nextState);
            onGrabStateChanged?.Invoke(nextState);
        }

        private void DestroyOwnedAnchor()
        {
            if (ownedAnchorObject == null)
            {
                return;
            }

            GameObject anchorToDestroy = ownedAnchorObject;
            ownedAnchorObject = null;
            physicsAnchor = null;
            DestroyUnityObject(anchorToDestroy);
        }

        private static void DestroyUnityObject(UnityEngine.Object objectToDestroy)
        {
            if (objectToDestroy == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(objectToDestroy);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(objectToDestroy);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maximumTrackingError = Mathf.Max(0.001f, maximumTrackingError);
        }
#endif
    }
}
