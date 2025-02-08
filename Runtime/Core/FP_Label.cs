namespace FuzzPhyte.Utility
{
    using UnityEngine;
    public class FP_Label:MonoBehaviour
    {
        public Transform LabelTransform;
        [SerializeField] protected string labelText = "Object Label";
        [SerializeField] protected float labelOffset = 1.5f; // Offset above the object
        [SerializeField] protected Color lineColor = Color.white; 
        [SerializeField] protected Color textColor = Color.white;
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
            Vector3 labelPosition = objectPosition + Vector3.up * labelOffset;
            // Adjust label size based on world scale
            float handleSize = UnityEditor.HandleUtility.GetHandleSize(labelPosition) * scaleFactor;

            // Calculate approximate background size
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
                normal = styleState
            };

            // Draw label text
            UnityEditor.Handles.Label(labelPosition, labelText, style);

            // Draw line connecting label to object
            Gizmos.color = lineColor;
            Gizmos.DrawLine(objectPosition, labelPosition);
           
    #endif
        }
    }
}

