using UnityEditor;
using UnityEngine;

namespace FuzzPhyte.Utility.Editor
{
    public static class FP_Utility_Editor
    {
        public static Color WarningColor = new Color(1f, 0.64f, 0);
        public static Color OkayColor = new Color(0.01f, 0.61f, 0.98f);
        /// <summary>
        /// Return Color for editor window to help with state of sequence
        /// </summary>
        /// <param name="status"></param>
        /// <returns></returns>
        public static Color ReturnColorByStatus(SequenceStatus status)
        {
            switch (status)
            {
                case SequenceStatus.None:
                    return Color.white;
                case SequenceStatus.Locked:
                    return Color.red;
                case SequenceStatus.Unlocked:
                    return Color.yellow;
                case SequenceStatus.Active:
                    return Color.green;
                case SequenceStatus.Finished:
                    return Color.cyan;
                default:
                    return Color.white;
            }
        }
        /// <summary>
        /// Draw a line
        /// </summary>
        /// <param name="lineColor">Color of a line</param>
        public static void DrawUILine(Color lineColor)
        {
            var rect = EditorGUILayout.BeginHorizontal();
            Handles.color = lineColor;
            Handles.DrawLine(new Vector2(rect.x, rect.y), new Vector2(rect.width, rect.y));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }
        /// <summary>
        /// Draw a line
        /// </summary>
        /// <param name="lineColor">Color of a line</param>
        /// <param name="leftPointShift">Negative value indents left to right</param>
        public static void DrawUILine(Color lineColor, float leftPointShift)
        {
            var rect = EditorGUILayout.BeginHorizontal();
            Handles.color = lineColor;
            Handles.DrawLine(new Vector2(rect.x - leftPointShift, rect.y), new Vector2(rect.width, rect.y));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }
        /// <summary>
        /// Draw a line
        /// </summary>
        /// <param name="lineColor">Color of a line</param>
        /// <param name="leftPointShift">Negative value indents left to right</param>
        /// <param name="rightPointShift">Positive value indents right to left</param>
        public static void DrawUILine(Color lineColor, float leftPointShift, float rightPointShift)
        {
            var rect = EditorGUILayout.BeginHorizontal();
            Handles.color = lineColor;
            Handles.DrawLine(new Vector2(rect.x - leftPointShift, rect.y), new Vector2(rect.width - rightPointShift, rect.y));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }
        /// <summary>
        /// Pass the rect information we need
        /// </summary>
        /// <param name="box"></param>
        public static void DrawUIBox(Rect box, float heightAdjustment, Color boxColor)
        {
            Vector3[] points = new Vector3[5];
            points[0] = box.min + new Vector2(15, 0);
            points[1] = box.min + new Vector2(box.width, 0);
            points[2] = box.max + new Vector2(0, heightAdjustment);
            points[3] = box.min + new Vector2(15, box.height + heightAdjustment);
            points[4] = box.min + new Vector2(15, 0);
            Handles.color = boxColor;
            Handles.DrawPolyLine(points);
        }
        /// <summary>
        /// Return a GUIStyle
        /// </summary>
        /// <param name="colorFont">Color of Font</param>
        /// <param name="styleFont">Style of Font</param>
        /// <param name="anchorFont">Anchor of Font</param>
        /// <returns></returns>
        public static GUIStyle ReturnStyle(Color colorFont, FontStyle styleFont, TextAnchor anchorFont)
        {
            GUIStyleState normalState = new GUIStyleState()
            {
                textColor = colorFont,
            };
            return new GUIStyle()
            {
                fontStyle = styleFont,
                normal = normalState,
                alignment = anchorFont
            };
        }
        public static GUIStyle ReturnStyleWrap(Color colorFont, FontStyle styleFont, TextAnchor anchorFont, bool useWordWrap)
        {
            var newStyle = ReturnStyle(colorFont, styleFont, anchorFont);
            newStyle.wordWrap = useWordWrap;
            return newStyle;
        }
        public static GUIStyle ReturnStyleRichText(Color colorFont, FontStyle styleFont, TextAnchor anchorFont)
        {
            var newStyle = ReturnStyle(colorFont, styleFont, anchorFont);
            newStyle.richText = true;
            return newStyle;
        }
    }
    
}
