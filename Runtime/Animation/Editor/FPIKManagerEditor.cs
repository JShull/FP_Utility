namespace FuzzPhyte.Utility.Animation.Editor
{
    using UnityEditor;
    using FuzzPhyte.Utility.Editor;
    using UnityEngine;

    [CustomEditor(typeof(FuzzPhyte.Utility.Animation.FPIKManager))]
    public class FPIKManagerEditor:Editor
    {
        private SerializedProperty ikActive;
        private SerializedProperty ikAnimator;
        private SerializedProperty hipRelativeForward;
        private SerializedProperty gizmoActive;
        private SerializedProperty headIKMode;

        // Hand IK properties
        private SerializedProperty useHandIK;
        private SerializedProperty useRightHandIK;
        private SerializedProperty rightHandTarget, rightHandHint, rightHandWeightScale;
        private SerializedProperty useLeftHandIK;
        private SerializedProperty leftHandTarget, leftHandHint, leftHandWeightScale;
        private SerializedProperty handIKSpeed, reachProximityMax;
        private SerializedProperty rightArmConstraint;

        // Head IK properties
        private SerializedProperty useHeadIK;
        private SerializedProperty headAimConstraint;
        private SerializedProperty maintainOffset, handRTOffsetPOS, handRTOffsetROT;
        private SerializedProperty trackingLookAtPosition, relativePivotPos;
        private SerializedProperty maxAngleDropoff, minAngleFullTracking, headIKSpeed;
        private SerializedProperty showLargeConeGizmo, showInteriorConeGizmo;
        private SerializedProperty showRightHandGizmo, showLeftHandGizmo;

        private void OnEnable()
        {
            // General IK settings
            ikActive = serializedObject.FindProperty("IKActive");
            ikAnimator = serializedObject.FindProperty("IKAnimator");
            hipRelativeForward = serializedObject.FindProperty("HipRelativeForward");
            gizmoActive = serializedObject.FindProperty("ShowHeadIKGizmo");
            showLargeConeGizmo = serializedObject.FindProperty("ShowHeadLargeConeGizmo");
            showInteriorConeGizmo = serializedObject.FindProperty("ShowInteriorConeGizmo");
            showRightHandGizmo = serializedObject.FindProperty("ShowRightHandGizmo");
            showLeftHandGizmo = serializedObject.FindProperty("ShowLeftHandGizmo");
            headIKMode = serializedObject.FindProperty("IKProvider");
            // Hand IK
            useHandIK = serializedObject.FindProperty("UseHandIK");
            handIKSpeed = serializedObject.FindProperty("HandIKSpeed");
            reachProximityMax = serializedObject.FindProperty("ReachProximityMax");
            rightArmConstraint = serializedObject.FindProperty("RightArmConstraint");
            handRTOffsetPOS = serializedObject.FindProperty("UseRtRigOffsetPos");
            handRTOffsetROT = serializedObject.FindProperty("UseRtRigOffsetRot");

            useRightHandIK = serializedObject.FindProperty("UseRightHandIK");
            rightHandTarget = serializedObject.FindProperty("RightHandTarget");
            rightHandHint = serializedObject.FindProperty("RightHandHint");
            rightHandWeightScale = serializedObject.FindProperty("RightHandWeightScale");

            useLeftHandIK = serializedObject.FindProperty("UseLeftHandIK");
            leftHandTarget = serializedObject.FindProperty("LeftHandTarget");
            leftHandHint = serializedObject.FindProperty("LeftHandHint");
            leftHandWeightScale = serializedObject.FindProperty("LeftHandWeightScale");

            // Head IK
            useHeadIK = serializedObject.FindProperty("UseHeadIK");
            headAimConstraint = serializedObject.FindProperty("HeadAimConstraint");
            maintainOffset = serializedObject.FindProperty("MaintainOffset");
            headIKSpeed = serializedObject.FindProperty("HeadIKSpeed");
            trackingLookAtPosition = serializedObject.FindProperty("TrackingLookAtPosition");
            relativePivotPos = serializedObject.FindProperty("RelativePivotPos");
            maxAngleDropoff = serializedObject.FindProperty("MaxAngleDropoff");
            minAngleFullTracking = serializedObject.FindProperty("MinAngleFullTracking");
            
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            float inspectorWidth = EditorGUIUtility.currentViewWidth;
            float dynamicSingleLineHeight = 2f + (EditorGUIUtility.singleLineHeight); // Example: extra height for content
            EditorGUILayout.PropertyField(ikActive);
            
            if (ikActive.boolValue)
            {
                
                EditorGUILayout.PropertyField(ikAnimator);
                EditorGUILayout.PropertyField(hipRelativeForward);
                FP_Utility_Editor.DrawUILine(FP_Utility_Editor.OkayColor);
                // Hand IK
                EditorGUILayout.Space();
                //EditorGUILayout.LabelField("Hand IK Settings", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(useHandIK);

                if (useHandIK.boolValue)
                {
                    
                    EditorGUILayout.PropertyField(handIKSpeed);
                    EditorGUILayout.PropertyField(reachProximityMax);
                    EditorGUILayout.Space();
                    // Right Hand IK
                    EditorGUILayout.PropertyField(useRightHandIK);
                    if (useRightHandIK.boolValue)
                    {
                        if ((HeadIKProvider)headIKMode.enumValueIndex == HeadIKProvider.AnimationRigging)
                        {
                            EditorGUILayout.PropertyField(rightArmConstraint);
                            EditorGUILayout.PropertyField(handRTOffsetPOS);
                            EditorGUILayout.PropertyField(handRTOffsetROT);
                        }
                        EditorGUILayout.PropertyField(showRightHandGizmo);
                        EditorGUILayout.PropertyField(rightHandTarget);
                        EditorGUILayout.PropertyField(rightHandHint);
                        EditorGUILayout.PropertyField(rightHandWeightScale);
                    }
                    else
                    {
                        FP_Utility_Editor.DrawUILine(FP_Utility_Editor.WarningColor);
                    }

                        // Left Hand IK
                    EditorGUILayout.PropertyField(useLeftHandIK);
                    if (useLeftHandIK.boolValue)
                    {
                        EditorGUILayout.PropertyField(showLeftHandGizmo);
                        EditorGUILayout.PropertyField(leftHandTarget);
                        EditorGUILayout.PropertyField(leftHandHint);
                        EditorGUILayout.PropertyField(leftHandWeightScale);
                    }
                    FP_Utility_Editor.DrawUILine(FP_Utility_Editor.OkayColor);
                }
                else
                {
                    var rect = EditorGUILayout.BeginHorizontal();
                    Rect responsiveRect = new Rect(rect.x, rect.y - dynamicSingleLineHeight, inspectorWidth, dynamicSingleLineHeight);
                    FP_Utility_Editor.DrawUIBox(responsiveRect, 0, FP_Utility_Editor.WarningColor, 0, -25);
                    EditorGUILayout.EndHorizontal();
                }

                    // Head IK
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Head IK Settings", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(useHeadIK);

                if (useHeadIK.boolValue)
                {
                    EditorGUILayout.PropertyField(headIKMode);
                    if((HeadIKProvider)headIKMode.enumValueIndex == HeadIKProvider.AnimationRigging)
                    {
                        EditorGUILayout.PropertyField(headAimConstraint);
                        EditorGUILayout.PropertyField(maintainOffset);
                    }
                    EditorGUILayout.PropertyField(gizmoActive);
                    if (gizmoActive.boolValue)
                    {
                        var rect = EditorGUILayout.BeginHorizontal();
                        Rect responsiveRect = new Rect(rect.x, rect.y - (dynamicSingleLineHeight), inspectorWidth, dynamicSingleLineHeight*2);
                        FP_Utility_Editor.DrawUIBox(responsiveRect, 0, FP_Utility_Editor.OkayColor, 0, -25);
                        EditorGUILayout.PropertyField(showLargeConeGizmo);
                        EditorGUILayout.PropertyField(showInteriorConeGizmo);
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUILayout.PropertyField(headIKSpeed);
                    EditorGUILayout.PropertyField(trackingLookAtPosition);
                    EditorGUILayout.PropertyField(relativePivotPos);
                    EditorGUILayout.PropertyField(maxAngleDropoff);
                    EditorGUILayout.PropertyField(minAngleFullTracking);
                    FP_Utility_Editor.DrawUILine(FP_Utility_Editor.OkayColor);

                }
                else
                {
                    
                    var rect = EditorGUILayout.BeginHorizontal();
                    Rect responsiveRect = new Rect(rect.x, rect.y-dynamicSingleLineHeight, inspectorWidth, dynamicSingleLineHeight);
                    FP_Utility_Editor.DrawUIBox(responsiveRect, 0, FP_Utility_Editor.WarningColor,0,-25);
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                // Define a responsive Rect based on the Inspector width
                

                // Dynamic Height
                // Example: Calculate height based on content (modify this as needed)
                
                Rect responsiveRect = new Rect(0, 5, inspectorWidth - 3, dynamicSingleLineHeight); // Adjust the width dynamically
                FP_Utility_Editor.DrawUIBox(responsiveRect,0, FP_Utility_Editor.WarningColor);
            }

                serializedObject.ApplyModifiedProperties();
        }
    }
}
