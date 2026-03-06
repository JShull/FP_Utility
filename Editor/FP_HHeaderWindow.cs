namespace FuzzPhyte.Utility.Editor
{
    using UnityEditor;
    using UnityEngine;

    public class FP_HHeaderWindow : EditorWindow
    {
        private FP_HHeaderData headerData;

        [MenuItem("FuzzPhyte/Header/Header Options", false, priority = FP_UtilityData.ORDER_SUBMENU_LVL6)]
        private static void OpenWindow()
        {
            FP_HHeaderWindow window = GetWindow<FP_HHeaderWindow>("Header Options");
            window.minSize = new Vector2(360f, 180f);
            window.Show();
        }

        private void OnEnable()
        {
            if (headerData == null)
            {
                headerData = FP_HHeader.GetActiveHeaderDataAsset();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("FP Header Options", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Assign an FP_HHeaderData asset to apply the current header visuals or create scene headers from that data.", MessageType.Info);

            EditorGUI.BeginChangeCheck();
            headerData = (FP_HHeaderData)EditorGUILayout.ObjectField("Header Data", headerData, typeof(FP_HHeaderData), false);
            if (EditorGUI.EndChangeCheck())
            {
                Repaint();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Active Scene Style") && FP_HHeader.GetActiveHeaderDataAsset() != null)
                {
                    headerData = FP_HHeader.GetActiveHeaderDataAsset();
                }

                if (GUILayout.Button("Ping Asset") && headerData != null)
                {
                    EditorGUIUtility.PingObject(headerData);
                    Selection.activeObject = headerData;
                }
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(headerData == null))
            {
                if (GUILayout.Button("Apply Header Style", GUILayout.Height(28f)))
                {
                    FP_HHeader.ApplyHeaderDataAsset(headerData, false);
                }

                if (GUILayout.Button("Create Headers From Data", GUILayout.Height(28f)))
                {
                    FP_HHeader.ApplyHeaderDataAsset(headerData, true);
                }
            }

            if (headerData == null)
            {
                EditorGUILayout.HelpBox("No FP_HHeaderData asset assigned.", MessageType.Warning);
            }
        }
    }
}