namespace FuzzPhyte.Utility.Animation
{
    using UnityEngine;
    using UnityEngine.Animations.Rigging;

    public enum HeadIKProvider { NA=0,AnimatorIK=1, AnimationRigging=2 }

    public class FPIKManager : MonoBehaviour
    {
        [SerializeField] protected float handRtDis = 0;
        [Header("General IK Setting")]
        public HeadIKProvider IKProvider = HeadIKProvider.AnimatorIK;
        public bool MaintainOffset = true;
        [Range(-1f, 1f)]
        public float IKScaleWeight = 1;
        public bool IKActive = false;
        public Animator IKAnimator;
        [Tooltip("Fixed Hip forward position for lateral information")]
        public Transform HipRelativeForward;
        
        //right
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
        //public Transform RightHandRef;
        public Transform RightHandHint;
        [Range(0,1)]
        public float RightHandWeightScale;
        public bool UseRtRigOffsetPos = false;
        public bool UseRtRigOffsetRot = false;

        //left
        [Space]
        public bool UseLeftHandIK = false;
        [Tooltip("Left hand target?")]
        public Transform LeftHandTarget;
        public Transform LeftHandRef;
        public Transform LeftHandHint;
        [Range(0, 1)]
        public float LeftHandWeightScale;
        
        //head
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

        //rig based
        [Space]
        public MultiAimConstraint HeadAimConstraint;
        public TwoBoneIKConstraint RightArmConstraint;
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
        public Color HeadTargetColor = Color.aliceBlue;
        public int ConeSegments = 16;
        #endregion
        
        protected float _lastConeHeight, _lastMaxAngleDropoff, _lastMinAngleFullTracking, _headWeightSmoothed, _handWeightSmoothed;
        protected int _lastConeSegments;
        protected Vector3 leftHandPos;
        protected Vector3 rightHandPos;
        #region Unity Functions
        protected virtual void Start()
        {
            if (!IKActive)
            {
                return;
            }
            if (UseHandIK && IKProvider==HeadIKProvider.AnimationRigging)
            {

                EnsureHandSetupRigging();
            }
            
            if (UseHeadIK || IKProvider == HeadIKProvider.AnimationRigging)
            {
                EnsureHeadAimSetupRigging();
            }
            DebugArmReach(RightArmConstraint.data.root, RightArmConstraint.data.mid, RightArmConstraint.data.tip, RightHandTarget);
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
        protected virtual void LateUpdate()
        {
            if (!IKActive || IKProvider != HeadIKProvider.AnimationRigging)
            {
                return;
            }
            if (UseHeadIK || HeadAimConstraint != null)
            {
                var w = ComputeHeadWeight(useAnimatorIK: false, externalGate: HeadAimConstraint.weight);
                HeadAimConstraint.weight = w;
            }
            if(UseHandIK || RightArmConstraint != null)
            {
                //new stuff
                UpdateArmIK(RightArmConstraint, RightHandTarget);
                //old stuff
                //var rightHandW = ComputeHandIK(RightArmConstraint, RightHandTarget, RightHandHint, RightHandWeightScale, true);
                //RightArmConstraint.weight = rightHandW;
            }
        }

        protected virtual void OnAnimatorIK(int layerIndex)
        {
            if (!IKActive || IKAnimator == null || TrackingLookAtPosition == null || RelativePivotPos == null)
                return;

            //Debug.Log($"Animator Index{layerIndex}");
            if (layerIndex != AnimatorLayer) return;
            if (UseHeadIK && IKProvider==HeadIKProvider.AnimatorIK)
            {
                ApplyHeadIKLook(layerIndex);
            } else
            {
                return;
            }
            //not using handIK if we rig the head right now (will fix this soon)
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

        #endregion
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
                    lateralAdjustment = Mathf.Clamp01(1f - Mathf.Abs(-laterialBias)); // flip the sign
                }
                else
                {
                    lateralAdjustment = Mathf.Clamp01(1f - Mathf.Abs(laterialBias)); // 1 when centered, 0 when far left
                  
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
        void DebugArmReach(Transform shoulder, Transform elbow, Transform wrist, Transform target)
        {
            float l1 = Vector3.Distance(shoulder.position, elbow.position);
            float l2 = Vector3.Distance(elbow.position, wrist.position);
            float d = Vector3.Distance(shoulder.position, target.position);
            float dMin = Mathf.Abs(l1 - l2);
            float dMax = l1 + l2;
            bool reachable = d >= dMin && d <= dMax;
            Debug.Log($"Reachable:{reachable}  d={d:F3}  range=[{dMin:F3},{dMax:F3}], l1={l1:F3}, l2={l2:F3}");
        }
        protected virtual float ComputeHandIK(TwoBoneIKConstraint constraint, Transform target, Transform hint, float weightScale, bool rightHand=false)
        {
            handRtDis = Vector3.Distance(constraint.data.tip.position, target.position);
            if(handRtDis <= ReachProximityMax)
            {
                var normalizedHandWeight = 1f - Mathf.InverseLerp(0, ReachProximityMax, handRtDis);
                Vector3 toTarget = target.position - HipRelativeForward.position;
                var laterialBias = Vector3.Dot(HipRelativeForward.right, toTarget.normalized);
                float lateralAdjustment = 0;
                if (rightHand)
                {
                    lateralAdjustment = Mathf.Clamp01(1f - Mathf.Abs(-laterialBias)); // flip the sign
                    
                    rightHandPos = constraint.data.tip.position;
                }
                else
                {
                    lateralAdjustment = Mathf.Clamp01(1f - Mathf.Abs(laterialBias)); // 1 when centered, 0 when far left
                    //lateralAdjustment = Mathf.Clamp01(1f - Mathf.Abs(-laterialBias)); // flip the sign
                    leftHandPos = constraint.data.tip.position;
                }
               
                var handFinalWeight = weightScale * normalizedHandWeight * lateralAdjustment;
                return handFinalWeight;
            }
            return 0;
        }
        protected virtual void UpdateArmIK(TwoBoneIKConstraint constraint,Transform target)
        {
            var data = constraint.data;

            // 1) enforce offsets the way we want
            //data.maintainTargetPositionOffset = false;
            //data.maintainTargetRotationOffset = true;

            // 2) reach factors
            float l1 = Vector3.Distance(data.root.position, data.mid.position);
            float l2 = Vector3.Distance(data.mid.position, data.tip.position);
            float d = Vector3.Distance(data.root.position, target.position);
            float dMin = Mathf.Abs(l1 - l2);
            float dMax = l1 + l2;
            float pad = 0.05f;

            float near = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(dMin - pad, dMin, d));
            float far = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(dMax, dMax + pad, d));
            float reachFactor = Mathf.Clamp01(near * far);     // 1 in the sweet spot, 0 near singularities

            // 3) position weight: your existing hand weight (0..1)
            float posW = ComputeHandWeight(data.root, data.mid, data.tip, target, false);

            // 4) rotation weight fades near singularities
            float baseRotW = 0.35f;                 // feel free to expose
            float rotW = baseRotW * (reachFactor * reachFactor); // square for stronger falloff

            // 5) apply
            data.targetPositionWeight = posW;
            data.targetRotationWeight = rotW;
            data.hintWeight = 1f;
            constraint.data = data;

            // overall constraint weight (keep 1.0; let pos/rot weights do the gating)
            constraint.weight = 1f;
        }
        protected float ComputeHandWeight(Transform shoulder, Transform elbow, Transform wrist, Transform target,bool useAnimatorIK = false, float externalGate = 1f,float reachPadMeters = 0.05f, float ikSpeed = 12f)
        {
            // --- Distances / reach band ---
            float l1 = Vector3.Distance(shoulder.position, elbow.position);
            float l2 = Vector3.Distance(elbow.position, wrist.position);
            float d = Vector3.Distance(shoulder.position, target.position);
            float dMin = Mathf.Abs(l1 - l2);
            float dMax = l1 + l2;

            // Reach factor: ~1 inside [dMin,dMax], falls off smoothly outside
            float near = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(dMin - reachPadMeters, dMin, d));
            float far = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(dMax, dMax + reachPadMeters, d));
            float reachFactor = Mathf.Clamp01(near * far);

            // --- Angular factor (same idea you used for head) ---
            Vector3 toTarget = (target.position - shoulder.position).normalized;
            Vector3 pivotFwd = (RelativePivotPos ? RelativePivotPos.forward : shoulder.forward);
            float angle = Vector3.Angle(pivotFwd, toTarget);
            float angularFactor = Mathf.Clamp01(1f - Mathf.InverseLerp(MinAngleFullTracking, MaxAngleDropoff, angle));

            // --- Gate by Animator layer when using Animator IK; otherwise use external rig gate (usually 1) ---
            float gate = useAnimatorIK
                ? (IKAnimator != null ? IKAnimator.GetLayerWeight(AnimatorLayer) : 1f)
                : Mathf.Clamp01(externalGate);

            // --- Target weight (pre-smoothing) ---
            float targetWeight = gate * IKScaleWeight * reachFactor * angularFactor;

            // --- Framerate-independent smoothing toward target ---
            float alpha = 1f - Mathf.Exp(-ikSpeed * Time.deltaTime);
            _handWeightSmoothed = Mathf.Lerp(_handWeightSmoothed, targetWeight, alpha);

            return _handWeightSmoothed;
        }
        /// <summary>
        /// Adjusts our head based on the parameters established and the layer index
        /// </summary>
        /// <param name="layerIndex"></param>
        protected virtual void ApplyHeadIKLook(int layerIndex)
        {
            // 1) Compute distance to target
            //FUNCTION use
            measuredWeight = ComputeHeadWeight(true);
            /*
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
            //Debug.LogWarning($"Look weight? {measuredWeight}");
            */
            IKAnimator.SetLookAtWeight(measuredWeight);

            if (measuredWeight > 0f)
            {
                IKAnimator.SetLookAtPosition(TrackingLookAtPosition.position);
            }
        }

        /// <summary>
        /// Logic on just HeadLook Weight for IK
        /// </summary>
        /// <returns></returns>
        protected virtual float ComputeHeadWeight(bool useAnimatorIK = true, float externalGate=1f)
        {
            // 1) Distance factor
            distanceToTarget = Vector3.Distance(RelativePivotPos.position, TrackingLookAtPosition.position);
            hypotenuse = ConeHeight / Mathf.Sin((180 - (90 + MaxAngleDropoff)) * Mathf.Deg2Rad) * Mathf.Sin(90 * Mathf.Deg2Rad);
            var distanceFactor = Mathf.Clamp01(Mathf.InverseLerp(0f, hypotenuse, hypotenuse - distanceToTarget));

            // 2) Angular factor
            Vector3 toTarget = TrackingLookAtPosition.position - RelativePivotPos.position;
            Vector3 pivotForward = RelativePivotPos.forward;
            float angle = Vector3.Angle(pivotForward, toTarget);
            var angularFactor = Mathf.Clamp01(1f - Mathf.InverseLerp(MinAngleFullTracking, MaxAngleDropoff, angle));

            // 3) Gate (Animator IK layer or external rig layer)
            float gate = useAnimatorIK
                ? (IKAnimator != null ? IKAnimator.GetLayerWeight(AnimatorLayer) : 1f)
                : Mathf.Clamp01(externalGate); // for RigBuilder path, often 1f or the Rig/RigLayer weight

            // 4) Target cone weight and compose with distance
            float coneTarget = gate * IKScaleWeight * angularFactor;
            float target = 0.5f * (coneTarget + distanceFactor);

            // 5) Framerate-independent smoothing toward target
            float alpha = 1f - Mathf.Exp(-HeadIKSpeed * Time.deltaTime); // smooth factor
            _headWeightSmoothed = Mathf.Lerp(_headWeightSmoothed, target, alpha);

            measuredWeight = _headWeightSmoothed;
            return measuredWeight;
        }
        
        /// <summary>
        /// For rigging purposes
        /// </summary>
        protected virtual void EnsureHeadAimSetupRigging()
        {
            if (HeadAimConstraint == null || TrackingLookAtPosition==null) return;

            // Get and edit the constraint's data (struct)
            var data = HeadAimConstraint.data;

            // Use your existing target as the single source
            var sources = data.sourceObjects;                 // WeightedTransformArray
            bool needsRewire =
                sources.Count != 1 ||
                sources[0].transform != TrackingLookAtPosition ||
                sources[0].weight != 1f;

            if (needsRewire)
            {
                sources.Clear();
                sources.Add(new WeightedTransform(TrackingLookAtPosition, 1f));
                data.sourceObjects = sources;                 // assign back to data
            }
            //stablize roll with up
            if (HipRelativeForward != null)
            {
                data.worldUpType = MultiAimConstraintData.WorldUpType.ObjectRotationUp;
                data.worldUpObject = HipRelativeForward;
            }
            data.limits = new Vector2(-1* MaxAngleDropoff, MaxAngleDropoff);
            // Optional: preserve current pose when enabling the constraint
            data.maintainOffset = MaintainOffset;   // or expose as a serialized toggle

            HeadAimConstraint.data = data; // <- push modified struct back
            
        }
        protected virtual void EnsureHandSetupRigging()
        {
            if (RightArmConstraint == null || RightHandTarget == null) return;
            var rightHandData = RightArmConstraint.data;
            // align
            rightHandData.target = RightHandTarget;
            rightHandData.hint = RightHandHint;
            rightHandData.maintainTargetPositionOffset = UseRtRigOffsetPos;
            rightHandData.maintainTargetRotationOffset = UseRtRigOffsetRot;
            rightHandPos = RightArmConstraint.data.tip.position;
            RightArmConstraint.data = rightHandData;
        }
        #region Gizmos & Visualizations
       
        protected virtual void OnDrawGizmos()
        {
            if (ShowHeadIKGizmo && ConeSegments > 1 && RelativePivotPos!=null && TrackingLookAtPosition!=null)
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
                Gizmos.color = HeadTargetColor;
                Gizmos.DrawWireSphere(TrackingLookAtPosition.position, 0.5f);
                
                Gizmos.DrawLine(RelativePivotPos.position, TrackingLookAtPosition.position);
            }
            if (ShowRightHandGizmo && RightHandTarget!=null)
            {
                //right hand tracking addition
                var rightHandDist = Vector3.Distance(rightHandPos, RightHandTarget.position);
                //right hand tracking addition
                Vector3 rightHandRef = Vector3.zero;
                if (IKProvider== HeadIKProvider.AnimationRigging)
                {
                    rightHandRef = RightArmConstraint.data.tip.position;
                }
                if(IKProvider == HeadIKProvider.AnimatorIK)
                {
                    rightHandRef = IKAnimator.GetIKPosition(AvatarIKGoal.RightHand);
                }
                
                if (rightHandDist <= ReachProximityMax)
                {
                    Gizmos.color = RightHandTargetColor;
                    Gizmos.DrawWireSphere(RightHandTarget.position, 0.15f);
                    //Vector3 toTarget = RightHandTarget.position - IKAnimator.GetIKPosition(AvatarIKGoal.RightHand);
                    Gizmos.DrawLine(rightHandRef, RightHandTarget.position);
                    //Gizmos.DrawRay(IKAnimator.GetIKPosition(AvatarIKGoal.RightHand), toTarget.normalized);
                }
            }
            if (ShowLeftHandGizmo && LeftHandTarget!=null)
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
