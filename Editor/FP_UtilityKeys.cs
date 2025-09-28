namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    public class FP_UtilityKeys : EditorWindow
    {
        #region ChatGPT Parameters
        private string chatGptApiKey;
        private string chatGptOrganizationID;
        private string chatGptProjectID;
        private bool showChatGptKey;
        private bool showChatGptOrgID;
        private bool showChatGptProjectID;
        #endregion
        #region Merriam-Webster Parameters
        private bool showMWKey;
        //private List<Type> fpWordDerivedTypes;
        private MWApiKeyType selectedClassIndex;
        private Dictionary<string, string> savedKeys = new Dictionary<string, string>();
        private Dictionary<string, string> apiKeys = new Dictionary<string, string>();
        #endregion
        #region Polygon Parameters
        private string polygonApiKey;
        private bool showPolygonKey;
        #endregion

        [MenuItem("FuzzPhyte/Utility/Editor/Keys Manager", priority = FP_UtilityData.ORDER_SUBMENU_LVL7)]
        public static void ShowWindow()
        {
            GetWindow<FP_UtilityKeys>("FP Keys Manager");
        }

        private void OnEnable()
        {
            LoadMerriamWebster();
            // Load keys from EditorPrefs
            chatGptApiKey = EditorPrefs.GetString("ChatGptApiKey", "");
            chatGptOrganizationID = EditorPrefs.GetString("ChatGptOrganizationID", "");
            chatGptProjectID = EditorPrefs.GetString("ChatGptProjectID", "");
            //polygon
            polygonApiKey = EditorPrefs.GetString("PolygonApiKey", "");
        }
        private void LoadMerriamWebster()
        {
            foreach (var keyType in Enum.GetValues(typeof(MWApiKeyType)))
            {
                if ((MWApiKeyType)keyType != MWApiKeyType.NA)
                {
                    string keyPref = "Key(" + keyType + ")";
                    string savedKey = EditorPrefs.GetString(keyPref, "");
                    if (!string.IsNullOrEmpty(savedKey))
                    {
                        savedKeys[keyType.ToString()] = savedKey;
                    }
                }
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("Manage API Keys", EditorStyles.boldLabel);
            // ChatGPT API Key
            ChatGPTKeys();
            SpaceBetweenKeys();
            // Merriam-Webster API Keys
            MerriamWebsterKeys();
            SpaceBetweenKeys();
            // Polygon.io API Key
            PolygonKeys();
            SpaceBetweenKeys();
        }
        private void SpaceBetweenKeys()
        {
            EditorGUILayout.Space();
            FP_Utility_Editor.DrawUILine(FP_Utility_Editor.OkayColor);
            EditorGUILayout.Space();
        }
        private void MerriamWebsterKeys()
        {
            GUILayout.Label("Merriam-Webster Dictionary Keys", EditorStyles.boldLabel);

            //projectName = EditorGUILayout.TextField("Project Name", projectName);
            //word = EditorGUILayout.TextField("Word", word);

            // string[] typeNames = fpWordDerivedTypes.ConvertAll(t => t == null ? "NA" : t.Name).ToArray();
            selectedClassIndex = (MWApiKeyType)EditorGUILayout.EnumPopup("Select Merriam-Webster", selectedClassIndex);

            if (selectedClassIndex != MWApiKeyType.NA)
            {
                var typeName = selectedClassIndex.ToString();
                //string typeName = selectedType.Name;

                if (savedKeys.ContainsKey(typeName))
                {
                    savedKeys.TryGetValue(typeName, out string savedKey);
                    showMWKey = EditorGUILayout.Toggle("Show Key", showMWKey);
                    if (showMWKey)
                    {
                        apiKeys[typeName] = EditorGUILayout.TextField($"API Key ({typeName})", apiKeys.ContainsKey(typeName) ? apiKeys[typeName] : savedKey);
                    }
                    else
                    {
                        apiKeys[typeName] = EditorGUILayout.PasswordField($"API Key ({typeName})", apiKeys.ContainsKey(typeName) ? apiKeys[typeName] : savedKey);
                    }
                }
                else
                {
                    showMWKey = EditorGUILayout.Toggle("Show Key", showMWKey);
                    if (showMWKey)
                    {
                        apiKeys[typeName] = EditorGUILayout.TextField($"API Key ({typeName})", apiKeys.ContainsKey(typeName) ? apiKeys[typeName] : "");
                    }
                    else
                    {
                        apiKeys[typeName] = EditorGUILayout.PasswordField($"API Key ({typeName})", apiKeys.ContainsKey(typeName) ? apiKeys[typeName] : "");
                    }
                }

                if (GUILayout.Button("Save Key"))
                {
                    EditorPrefs.SetString("Key(" + typeName + ")", apiKeys[typeName]);
                    savedKeys[typeName] = apiKeys[typeName];
                }
            }
        }
        private void ChatGPTKeys()
        {
            showChatGptKey = EditorGUILayout.Toggle("Show ChatGPT API Key", showChatGptKey);
            if (showChatGptKey)
            {
                chatGptApiKey = EditorGUILayout.TextField("ChatGPT API Key", chatGptApiKey);
            }
            else
            {
                chatGptApiKey = EditorGUILayout.PasswordField("ChatGPT API Key", chatGptApiKey);
            }

            if (GUILayout.Button("Save ChatGPT Key"))
            {
                EditorPrefs.SetString("ChatGptApiKey", chatGptApiKey);
                Debug.Log("ChatGPT API Key saved");
            }

            // ChatGPT Organization ID
            showChatGptOrgID = EditorGUILayout.Toggle("Show ChatGPT Organization ID", showChatGptOrgID);
            if (showChatGptOrgID)
            {
                chatGptOrganizationID = EditorGUILayout.TextField("ChatGPT Organization ID", chatGptOrganizationID);
            }
            else
            {
                chatGptOrganizationID = EditorGUILayout.PasswordField("ChatGPT Organization ID", chatGptOrganizationID);
            }

            if (GUILayout.Button("Save ChatGPT Organization ID"))
            {
                EditorPrefs.SetString("ChatGptOrganizationID", chatGptOrganizationID);
                Debug.Log("ChatGPT Organization ID saved");
            }

            // ChatGPT Project ID
            showChatGptProjectID = EditorGUILayout.Toggle("Show ChatGPT Project ID", showChatGptProjectID);
            if (showChatGptProjectID)
            {
                chatGptProjectID = EditorGUILayout.TextField("ChatGPT Project ID", chatGptProjectID);
            }
            else
            {
                chatGptProjectID = EditorGUILayout.PasswordField("ChatGPT Project ID", chatGptProjectID);
            }

            if (GUILayout.Button("Save ChatGPT Project ID"))
            {
                EditorPrefs.SetString("ChatGptProjectID", chatGptProjectID);
                Debug.Log("ChatGPT Project ID saved");
            }
        }

        private void PolygonKeys()
        {
            GUILayout.Label("Polygon.io API Key", EditorStyles.boldLabel);

            showPolygonKey = EditorGUILayout.Toggle("Show Polygon Key", showPolygonKey);
            if (showPolygonKey)
            {
                polygonApiKey = EditorGUILayout.TextField("Polygon API Key", polygonApiKey);
            }
            else
            {
                polygonApiKey = EditorGUILayout.PasswordField("Polygon API Key", polygonApiKey);
            }

            if (GUILayout.Button("Save Polygon Key"))
            {
                EditorPrefs.SetString("PolygonApiKey", polygonApiKey);
                Debug.Log("Polygon.io API Key saved");
            }
        }
    }
}
