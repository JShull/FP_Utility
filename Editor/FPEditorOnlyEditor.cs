#if UNITY_EDITOR
namespace FuzzPhyte.Utility.Editor
{
    using UnityEditor;
    using UnityEngine;
    [CustomEditor(typeof(FP_EditorOnly))]
    public class FPEditorOnlyEditor:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Begin a vertical layout with a yellow background
            // Create a style for the yellow box
            GUIStyle yellowBoxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { 
                    background = FP_Utility_Editor.ReturnGUITex(2, 2, FP_Utility_Editor.WarningColor) 
                    }, // Light yellow color
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(0, 0, 10, 10)
            };
            // Custom style for the warning text
            GUIStyle warningTextStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = Color.black }, // Set font color to red
                fontStyle = FontStyle.Bold,
                hover = { textColor = Color.white }, // Font color on hover
                wordWrap = true
            };
            // Get the current width of the Inspector
            float inspectorWidth = EditorGUIUtility.currentViewWidth;

            // Dynamic Height
            // Example: Calculate height based on content (modify this as needed)
            float dynamicHeight = 50f+(EditorGUIUtility.singleLineHeight * 2); // Example: extra height for content
        
            // Define a responsive Rect based on the Inspector width
            Rect responsiveRect = new Rect(0, 5, inspectorWidth - 3, dynamicHeight); // Adjust the width dynamically

            FP_Utility_Editor.DrawUIBox(responsiveRect,0,FP_Utility_Editor.WarningColor);
            // Draw the default inspector
            base.OnInspectorGUI();
            EditorGUILayout.BeginVertical(yellowBoxStyle,GUILayout.ExpandHeight(true));
            // Display a warning message
            GUILayout.Space(5);
            GUILayout.Label(
                "CAUTION: GameObject is for editor only and will not be included in the build.",
                warningTextStyle
            );
            GUILayout.EndVertical();
        }
    }
}
#endif