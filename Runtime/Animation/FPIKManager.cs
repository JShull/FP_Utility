namespace FuzzPhyte.Utility.Animation
{
    using UnityEngine;
    using UnityEngine.UIElements;
    using static UnityEngine.GraphicsBuffer;

    public class FPIKManager : MonoBehaviour
    {
        [Header("General IK Setting")]
        [Range(-1f, 1f)]
        public float IKScaleWeight = 1;
        public bool IKActive = false;
        public Animator IKAnimator;
        [Tooltip("Fixed Hip forward position for lateral information")]
        public Transform HipRelativeForward;
        [Space]
        [Header("Hand IK Parameters")]
        public bool UseHandIK = false;
        public bool UseHeadIK = false;
        [Range(30, 100)]
        public float HandIKSpeed = 50;
        public float ReachProximityMax = 1.5f;
        public bool UseRightHandIK = false;
        [Tooltip("Right hand target?")]
        public Transform RightHandTarget;
        public Transform RightHandRef;
        public Transform RightHandHint;
        [Range(0,1)]
        public float RightHandWeightScale;
        //[SerializeField]
        //protected float rightHandDist;
        
        [Space]
        public bool UseLeftHandIK = false;
        [Tooltip("Left hand target?")]
        public Transform LeftHandTarget;
        public Transform LeftHandRef;
        public Transform LeftHandHint;
        [Range(0, 1)]
        public float LeftHandWeightScale;
        [Space]
        [Header("Head IK Parameters")]
        [Tooltip("The item to look at")]
        public Transform TrackingLookAtPosition;
        [Tooltip("Fixed position relative rotation")]
        public Transform RelativePivotPos;

        [Tooltip("Angle diff between the relativePivot and the Tracking Look at")]
        public float MaxAngleDropoff = 60;
        [Tooltip("Min angle where head tracking is at full strength")]
        public float MinAngleFullTracking = 20f;
        
        [Tooltip("Speed of rotation blending")]
        [Range(30,100)]
        public float HeadIKSpeed = 50;
        [Tooltip("Layer of the Animator we want")]
        public int AnimatorLayer = 1;
        protected Quaternion initialLocalBoneRotation;
        protected float distanceToTarget;
        protected float hypotenuse;
        [Tooltip("Max Distance before character stops looking")]
        public float ConeHeight = 5f;
        [SerializeField]
        protected float measuredWeight;
        #region Gizmo Parameters
        public bool ShowHeadIKGizmo = true;
        public bool ShowRightHandGizmo = true;
        public bool ShowLeftHandGizmo = true;
        public bool ShowHeadLargeConeGizmo = true;
        public bool ShowInteriorConeGizmo = true;
        private Mesh coneMesh;
        private Mesh interiorConeMesh;
        public Color ConeColor = new Color(1f, 0.5f, 0f, 0.3f); // Orange semi-transparent
        public Color InteriorConeColor = new Color(1f, 1f, .2f,0.3f);
        public Color RightHandTargetColor = new Color(1f, 0f, 0f, .25f);
        public Color LeftHandTargetColor;
        public int ConeSegments = 16;
        #endregion
        
        protected float _lastConeHeight, _lastMaxAngleDropoff, _lastMinAngleFullTracking;
        protected int _lastConeSegments;
        protected Vector3 leftHandPos;
        protected Vector3 rightHandPos;

        protected virtual void Start()
        {
         
        }
        protected virtual void OnValidate()
        {
            // Detect changes in key parameters
            if (ConeHeight != _lastConeHeight || MaxAngleDropoff != _lastMaxAngleDropoff || ConeSegments != _lastConeSegments || MinAngleFullTracking!=_lastMinAngleFullTracking )
            {
                RegenerateConeMesh();
            }
        }
        
        protected virtual void Reset()
        {
            // If no animator is assigned, grab it from the same GameObject.
            if (IKAnimator == null)
            {
                IKAnimator = GetComponent<Animator>();
            }
            if(IKAnimator == null)
            {
                Debug.LogError($"Missing the animator! please make sure to assign the animator");
                IKActive = false;
            }
        }
        
        
        
        protected virtual void OnAnimatorIK(int layerIndex)
        {
            if (!IKActive || IKAnimator == null || TrackingLookAtPosition == null || RelativePivotPos == null)
                return;

            if (layerIndex != AnimatorLayer) return;
            if (UseHeadIK)
            {
                ApplyHeadIKLook(layerIndex);
            }
            if (UseHandIK)
            {
                if (UseRightHandIK && RightHandTarget!=null)
                {
                    rightHandPos = IKAnimator.GetIKPosition(AvatarIKGoal.RightHand);
                    CheckHandIK(layerIndex, AvatarIKGoal.RightHand, RightHandTarget, RightHandHint, RightHandWeightScale);
                   
                }
                if(UseLeftHandIK && LeftHandTarget != null)
                {
                    leftHandPos = IKAnimator.GetIKPosition(AvatarIKGoal.LeftHand);
                    CheckHandIK(layerIndex, AvatarIKGoal.LeftHand, LeftHandTarget, LeftHandHint, LeftHandWeightScale);
                }
            }

            
        }
        protected virtual void CheckHandIK(int layerIndex, AvatarIKGoal goal,Transform target, Transform hint, float weightScale)
        {
            var handDist = Vector3.Distance(IKAnimator.GetIKPosition(goal), target.position);
            if (handDist <= ReachProximityMax)
            {
                var normalizedHandWeight = 1f - Mathf.InverseLerp(0, ReachProximityMax, handDist);

                Vector3 toTarget = target.position - HipRelativeForward.position;
                var laterialBias = Vector3.Dot(HipRelativeForward.right, toTarget.normalized);
                //Adjust weight: If the object is too far left, reduce weight
                float lateralAdjustment = 0;
                if (goal == AvatarIKGoal.RightHand)
                {
                    lateralAdjustment = Mathf.Clamp01(1f - Mathf.Abs(laterialBias)); // 1 when centered, 0 when far left
                }
                else
                {
                    lateralAdjustment = Mathf.Clamp01(1f - Mathf.Abs(-laterialBias)); // flip the sign
                }
                var handFinalWeight = Mathf.Lerp(IKAnimator.GetIKPositionWeight(goal), IKScaleWeight * normalizedHandWeight * lateralAdjustment, Time.deltaTime * HandIKSpeed)* weightScale;
               
                // Apply IK
                IKAnimator.SetIKPositionWeight(goal, handFinalWeight);
                IKAnimator.SetIKRotationWeight(goal, handFinalWeight);
                IKAnimator.SetIKPosition(goal, target.position);
                IKAnimator.SetIKRotation(goal, target.rotation);

                // Apply Hint (Elbow direction)
                if (hint != null)
                {
                    IKAnimator.SetIKHintPositionWeight(goal == AvatarIKGoal.RightHand ? AvatarIKHint.RightElbow : AvatarIKHint.LeftElbow, handFinalWeight);
                    IKAnimator.SetIKHintPosition(goal == AvatarIKGoal.RightHand ? AvatarIKHint.RightElbow : AvatarIKHint.LeftElbow, hint.position);
                }
            }
        }
        /// <summary>
        /// Adjusts our head based on the parameters established and the layer index
        /// </summary>
        /// <param name="layerIndex"></param>
        protected virtual void ApplyHeadIKLook(int layerIndex)
        {
            // 1) Compute distance to target
            distanceToTarget = Vector3.Distance(RelativePivotPos.position, TrackingLookAtPosition.position);
            hypotenuse = ConeHeight / Mathf.Sin((180 - (90 + MaxAngleDropoff)) * Mathf.Deg2Rad) * Mathf.Sin(90 * Mathf.Deg2Rad);
            // 2) If the target is too far, stop looking
            //difference by negative value
            var normalizedDistance = Mathf.InverseLerp(0, hypotenuse, hypotenuse - distanceToTarget);
            
            // 3) Compute direction and angle from pivot to target
            Vector3 toTarget = TrackingLookAtPosition.position - RelativePivotPos.position;
            Vector3 pivotForward = RelativePivotPos.forward;
            float angle = Vector3.Angle(pivotForward, toTarget);

            // 4) Normalize the angle within MinAngleFullTracking → MaxAngleDropoff /invert this 
            var normalizedAngle = 1f - Mathf.InverseLerp(MinAngleFullTracking, MaxAngleDropoff, angle);

            // 5) Evaluate animation curve
            //float curveValue = WeightDropOffByAngle.Evaluate(normalizedAngle);

            // 6) Adjust LookAtWeight based on priority
            var coneFinalWeight = Mathf.Lerp(IKAnimator.GetLayerWeight(layerIndex), IKScaleWeight * normalizedAngle, Time.deltaTime * HeadIKSpeed);
            measuredWeight = (coneFinalWeight + normalizedDistance) * 0.5f;
            // 7) Apply LookAt weight & position
            IKAnimator.SetLookAtWeight(measuredWeight);

            if (measuredWeight > 0f)
            {
                IKAnimator.SetLookAtPosition(TrackingLookAtPosition.position);
            }
        }

        #region Gizmos & Visualizations
       
        protected virtual void OnDrawGizmos()
        {
            if (ShowHeadIKGizmo && ConeSegments > 1)
            {
                //show head gizmo
                if (coneMesh == null)
                {
                    RegenerateConeMesh();
                }

                Gizmos.color = ConeColor;
                Vector3 coneStart = RelativePivotPos.transform.position;
                if (ShowHeadLargeConeGizmo)
                {
                    Gizmos.DrawMesh(coneMesh, RelativePivotPos.position, Quaternion.LookRotation(RelativePivotPos.forward));
                }
                if (ShowInteriorConeGizmo)
                {
                    Gizmos.color = InteriorConeColor;
                    Gizmos.DrawMesh(interiorConeMesh, RelativePivotPos.position, Quaternion.LookRotation(RelativePivotPos.forward));
                }
                
                
                if (distanceToTarget > hypotenuse)
                {
                    return;
                }
                Gizmos.color = InteriorConeColor;
                Gizmos.DrawWireSphere(TrackingLookAtPosition.position, 0.75f);
            }
            if (ShowRightHandGizmo)
            {
                //right hand tracking addition
                var rightHandDist = Vector3.Distance(rightHandPos, RightHandTarget.position);
                //right hand tracking addition
                if (rightHandDist <= ReachProximityMax && RightHandRef != null)
                {
                    Gizmos.color = RightHandTargetColor;
                    Gizmos.DrawWireSphere(RightHandTarget.position, 0.15f);
                    //Vector3 toTarget = RightHandTarget.position - IKAnimator.GetIKPosition(AvatarIKGoal.RightHand);
                    Gizmos.DrawLine(RightHandRef.position, RightHandTarget.position);
                    //Gizmos.DrawRay(IKAnimator.GetIKPosition(AvatarIKGoal.RightHand), toTarget.normalized);
                }
            }
            if (ShowLeftHandGizmo)
            {
                var leftHandDist = Vector3.Distance(leftHandPos, LeftHandTarget.position);

                if (leftHandDist <= ReachProximityMax && LeftHandRef != null)
                {
                    Gizmos.color = LeftHandTargetColor;
                    Gizmos.DrawWireSphere(LeftHandTarget.position, 0.15f);
                    //Vector3 toTarget = RightHandTarget.position - IKAnimator.GetIKPosition(AvatarIKGoal.RightHand);
                    Gizmos.DrawLine(LeftHandRef.position, LeftHandTarget.position);
                    //Gizmos.DrawRay(IKAnimator.GetIKPosition(AvatarIKGoal.RightHand), toTarget.normalized);
                }
            }
        }
        protected void RegenerateConeMesh()
        {
            var mesh = FPGizmoDraw.GenerateConeMesh(ConeSegments, ConeHeight, MaxAngleDropoff);
            if (mesh.Item2)
            {
                coneMesh = mesh.Item1;
            }
            var intMesh = FPGizmoDraw.GenerateConeMesh(ConeSegments, ConeHeight, MinAngleFullTracking);
            if (intMesh.Item2)
            {
                interiorConeMesh = intMesh.Item1;
            }
           
            _lastConeHeight = ConeHeight;
            _lastMaxAngleDropoff = MaxAngleDropoff;
            _lastConeSegments = ConeSegments;
            _lastMinAngleFullTracking = MinAngleFullTracking;
        }
        #endregion
    }
}
