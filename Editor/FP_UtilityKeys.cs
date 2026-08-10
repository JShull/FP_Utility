// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    public class FP_UtilityKeys : EditorWindow
    {
        #region ChatGPT Parameters
        internal const string ChatGptApiKeyPreference = "ChatGptApiKey";
        internal const string ChatGptOrganizationIdPreference = "ChatGptOrganizationID";
        internal const string ChatGptProjectIdPreference = "ChatGptProjectID";
        private string chatGptApiKey;
        private string chatGptOrganizationID;
        private string chatGptProjectID;
        private bool showChatGptKey;
        private bool showChatGptOrgID;
        private bool showChatGptProjectID;
        #endregion
        #region ElevenLabs Parameters
        internal const string ElevenLabsApiKeyPreference = "ElevenLabsApiKey";
        private string elevenLabsApiKey;
        private bool showElevenLabsKey;
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

        [MenuItem("FuzzPhyte/Utility/Editor/Testing/Keys Manager", priority = FP_UtilityData.MENU_UTILITY_EDITOR + 11)]
        public static void ShowWindow()
        {
            GetWindow<FP_UtilityKeys>("FP Keys Manager");
        }

        private void OnEnable()
        {
            LoadMerriamWebster();
            // Load keys from EditorPrefs
            chatGptApiKey = EditorPrefs.GetString(ChatGptApiKeyPreference, "");
            chatGptOrganizationID = EditorPrefs.GetString(ChatGptOrganizationIdPreference, "");
            chatGptProjectID = EditorPrefs.GetString(ChatGptProjectIdPreference, "");
            // ElevenLabs
            elevenLabsApiKey = EditorPrefs.GetString(ElevenLabsApiKeyPreference, "");
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
            // ElevenLabs API Key
            ElevenLabsKeys();
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
                EditorPrefs.SetString(ChatGptApiKeyPreference, chatGptApiKey);
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
                EditorPrefs.SetString(ChatGptOrganizationIdPreference, chatGptOrganizationID);
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
                EditorPrefs.SetString(ChatGptProjectIdPreference, chatGptProjectID);
                Debug.Log("ChatGPT Project ID saved");
            }
        }

        private void ElevenLabsKeys()
        {
            GUILayout.Label("ElevenLabs API Key", EditorStyles.boldLabel);

            showElevenLabsKey = EditorGUILayout.Toggle("Show ElevenLabs Key", showElevenLabsKey);
            if (showElevenLabsKey)
            {
                elevenLabsApiKey = EditorGUILayout.TextField("ElevenLabs API Key", elevenLabsApiKey);
            }
            else
            {
                elevenLabsApiKey = EditorGUILayout.PasswordField("ElevenLabs API Key", elevenLabsApiKey);
            }

            if (GUILayout.Button("Save ElevenLabs Key"))
            {
                EditorPrefs.SetString(ElevenLabsApiKeyPreference, elevenLabsApiKey);
                Debug.Log("ElevenLabs API Key saved");
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
