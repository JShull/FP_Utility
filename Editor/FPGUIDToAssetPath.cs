namespace FuzzPhyte.Utility.Editor
{
    using UnityEngine;
    using UnityEditor;
    public class FPGUIDToAssetPath:EditorWindow
    {
        string guidToLookUp = string.Empty;
        string pathResponse = string.Empty;

        [MenuItem("FuzzPhyte/Utility/Editor/GUIDToAsset", priority = FP_UtilityData.ORDER_SUBMENU_LVL7)]
        public static void GUIDWindow()
        {
            FPGUIDToAssetPath window = (FPGUIDToAssetPath)EditorWindow.GetWindowWithRect(typeof(FPGUIDToAssetPath), new Rect(0, 0, 400, 120));
        }
        void OnGUI()
        {
            GUILayout.Label("Enter guid");
            guidToLookUp = GUILayout.TextField(guidToLookUp);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Get Asset Path", GUILayout.Width(120)))
            {
                pathResponse = GetAssetPath(guidToLookUp);
            }
                
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Abort", GUILayout.Width(120)))
                Close();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Label(pathResponse);
        }
        static string GetAssetPath(string guid)
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            Debug.Log(p);
            if (p == string.Empty)
            {
                p = "Not Found!";
            }
            return p;
        }

    }
}
