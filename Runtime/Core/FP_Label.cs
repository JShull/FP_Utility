namespace FuzzPhyte.Utility
{
    using UnityEngine;
    public class FP_Label:MonoBehaviour
    {
        public Transform LabelTransform;
        [SerializeField] protected bool useThisTransformForLabelPlacement = false;
        [TextArea(2, 6)]
        [SerializeField] protected string labelText = "Object Label";
        [SerializeField] protected float labelOffset = 1.5f; // Offset above the object
        [SerializeField] protected Color lineColor = Color.white; 
        [SerializeField] protected float lineThickness = 2f;
        [SerializeField] protected Color textColor = Color.white;
        [SerializeField] protected Color backgroundColor = Color.white;
        [SerializeField] protected Texture2D backgroundTexture;
        [SerializeField] protected float textSize = 12f; // Font size
        [SerializeField] protected float scaleFactor = 0.01f; // Adjust for world unit scaling
        
        protected virtual void Awake()
        {
            if(LabelTransform==null)
            {
                LabelTransform = this.transform;
            }
            if(backgroundTexture==null)
            {
                backgroundTexture = Texture2D.whiteTexture;
            }
        }
        protected virtual void OnDrawGizmos()
        {
            if(LabelTransform==null)
            {
                return;
            }
            

    #if UNITY_EDITOR
            Vector3 objectPosition = LabelTransform.position;
            Vector3 labelBasePosition = useThisTransformForLabelPlacement ? transform.position : objectPosition;
            Vector3 labelUpDirection = useThisTransformForLabelPlacement ? transform.up : Vector3.up;
            Vector3 labelPosition = labelBasePosition + labelUpDirection * labelOffset;
            GUIStyleState styleState = new GUIStyleState{
                textColor = this.textColor
            };
            if (backgroundTexture == null)
            {
                styleState.background = Texture2D.whiteTexture;    
            }else{
                styleState.background = backgroundTexture;
            }
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(textSize),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                clipping = TextClipping.Overflow,
                padding = new RectOffset(8, 8, 4, 4),
                normal = styleState
            };
            style.normal.textColor = textColor;
            style.normal.background = styleState.background;
            style.normal.scaledBackgrounds = new[] { styleState.background };

            // Estimate the label box height so the connector reaches the bottom edge instead of the center.
            string[] labelLines = string.IsNullOrEmpty(labelText) ? new[] { string.Empty } : labelText.Split('\n');
            float lineHeight = style.lineHeight > 0f ? style.lineHeight : style.CalcSize(new GUIContent("Ay")).y;
            float labelHeight = (lineHeight * labelLines.Length) + style.padding.top + style.padding.bottom;
            float handleSize = UnityEditor.HandleUtility.GetHandleSize(labelPosition) * scaleFactor;
            Vector3 lineEndPosition = labelPosition - labelUpDirection * (labelHeight * handleSize * 0.5f);

            // Draw label text
            Color previousGuiColor = GUI.color;
            GUI.color = backgroundColor;
            UnityEditor.Handles.Label(labelPosition, labelText, style);
            GUI.color = previousGuiColor;

            // Draw line connecting label to object
            UnityEditor.Handles.color = lineColor;
            UnityEditor.Handles.DrawAAPolyLine(Mathf.Max(1f, lineThickness), objectPosition, lineEndPosition);
           
    #endif
        }
    }
}

