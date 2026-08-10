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
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Networking;
    using UnityEngine.Serialization;

    public class FPElevenLabsTextToSpeechWindow : EditorWindow
    {
        private const string VoicesEndpoint = "https://api.elevenlabs.io/v2/voices";
        private const string TextToSpeechEndpoint = "https://api.elevenlabs.io/v1/text-to-speech";
        private const string OpenAIResponsesEndpoint = "https://api.openai.com/v1/responses";
        private const string OutputFormat = "mp3_44100_128";
        private const string DefaultModelId = "eleven_v3";
        private const string LegacyDefaultModelId = "eleven_multilingual_v2";
        private const string DefaultOpenAIModelId = "gpt-4o-mini";
        private const string OutputFolderPreference = "FPElevenLabs.OutputFolder";
        private const string SelectedVoicePreference = "FPElevenLabs.SelectedVoice";
        private const string ModelIdPreference = "FPElevenLabs.ModelId";
        private const string ModelDefaultMigratedPreference = "FPElevenLabs.ModelDefaultMigratedToElevenV3";
        private const string OpenAIModelIdPreference = "FPElevenLabs.OpenAIModelId";
        private const string SourceLanguagePreference = "FPElevenLabs.SourceLanguage";
        private const string TargetLanguagePreference = "FPElevenLabs.TargetLanguage";
        private const string VoiceNamePreference = "FPElevenLabs.VoiceName";

        private readonly List<VoiceInfo> voices = new List<VoiceInfo>();
        [FormerlySerializedAs("batchRequests")]
        [SerializeField] private List<SpeechRequest> speechRequests = new List<SpeechRequest> { new SpeechRequest() };
        [FormerlySerializedAs("batchRequestsExpanded")]
        [SerializeField] private bool requestsExpanded = true;
        [SerializeField] private string lastMarkdownPath = string.Empty;
        [SerializeField] private bool generateFPVocab;
        [SerializeField] private FP_LanguageLevel vocabLevelIntroduced = FP_LanguageLevel.LevelOne;
        [SerializeField] private CEFRLevel vocabCEFRLevel = CEFRLevel.NA;
        [SerializeField] private FP_VocabCategory vocabCategory = FP_VocabCategory.None;
        private string[] voiceDisplayNames = Array.Empty<string>();
        private int selectedVoiceIndex;
        private string modelId = DefaultModelId;
        private string openAIModelId = DefaultOpenAIModelId;
        private FPTranslationLanguage sourceLanguage = FPTranslationLanguage.English;
        private FPTranslationLanguage targetLanguage = FPTranslationLanguage.Spanish;
        private string outputAssetFolder = "Assets";
        private string voiceName = string.Empty;
        private string savedVoiceId = string.Empty;
        private string statusMessage = "Save the ElevenLabs and OpenAI credentials, then refresh voices.";
        private MessageType statusMessageType = MessageType.Info;
        private Vector2 scrollPosition;
        private bool isRequestRunning;

        [MenuItem("FuzzPhyte/Utility/Audio/ElevenLabs Text to Speech", priority = FP_UtilityData.MENU_UTILITY_AUDIO + 2)]
        public static void ShowWindow()
        {
            GetWindow<FPElevenLabsTextToSpeechWindow>("ElevenLabs TTS");
        }

        private void OnEnable()
        {
            if (speechRequests == null)
            {
                speechRequests = new List<SpeechRequest>();
            }

            outputAssetFolder = EditorPrefs.GetString(OutputFolderPreference, "Assets");
            savedVoiceId = EditorPrefs.GetString(SelectedVoicePreference, string.Empty);
            voiceName = EditorPrefs.GetString(VoiceNamePreference, string.Empty);
            modelId = EditorPrefs.GetString(ModelIdPreference, DefaultModelId);
            if (!EditorPrefs.GetBool(ModelDefaultMigratedPreference, false))
            {
                if (string.Equals(modelId, LegacyDefaultModelId, StringComparison.Ordinal))
                {
                    modelId = DefaultModelId;
                }

                EditorPrefs.SetBool(ModelDefaultMigratedPreference, true);
                EditorPrefs.SetString(ModelIdPreference, modelId);
            }

            openAIModelId = EditorPrefs.GetString(OpenAIModelIdPreference, DefaultOpenAIModelId);
            sourceLanguage = (FPTranslationLanguage)Mathf.Clamp(
                EditorPrefs.GetInt(SourceLanguagePreference, (int)FPTranslationLanguage.English),
                (int)FPTranslationLanguage.English,
                (int)FPTranslationLanguage.French);
            targetLanguage = (FPTranslationLanguage)Mathf.Clamp(
                EditorPrefs.GetInt(TargetLanguagePreference, (int)FPTranslationLanguage.Spanish),
                (int)FPTranslationLanguage.English,
                (int)FPTranslationLanguage.French);

            if (HasElevenLabsApiKey())
            {
                EditorApplication.delayCall += RefreshVoices;
            }
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= RefreshVoices;
            EditorPrefs.SetString(OutputFolderPreference, outputAssetFolder);
            EditorPrefs.SetString(ModelIdPreference, modelId);
            EditorPrefs.SetString(OpenAIModelIdPreference, openAIModelId);
            EditorPrefs.SetString(VoiceNamePreference, voiceName);
            EditorPrefs.SetInt(SourceLanguagePreference, (int)sourceLanguage);
            EditorPrefs.SetInt(TargetLanguagePreference, (int)targetLanguage);

            VoiceInfo selectedVoice = GetSelectedVoice();
            if (selectedVoice != null)
            {
                EditorPrefs.SetString(SelectedVoicePreference, selectedVoice.voice_id);
            }
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.LabelField("ElevenLabs Text to Speech", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Translate English, Spanish, or French text with OpenAI, then generate paired MP3 AudioClip assets with one ElevenLabs voice.",
                MessageType.Info);

            DrawAuthenticationSection();
            DrawVoiceSection();
            DrawRequestSettingsSection();
            DrawRequestsSection();
            DrawOutputSection();
            DrawFPVocabSection();
            DrawGenerateSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawAuthenticationSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Authentication", EditorStyles.boldLabel);

            bool hasElevenLabsKey = HasElevenLabsApiKey();
            bool hasOpenAICredentials = HasOpenAICredentials();
            if (hasElevenLabsKey && hasOpenAICredentials)
            {
                EditorGUILayout.HelpBox("ElevenLabs and OpenAI credentials found in FP Keys Manager.", MessageType.Info);
                return;
            }

            string missingCredentials = !hasElevenLabsKey && !hasOpenAICredentials
                ? "ElevenLabs and OpenAI credentials are missing."
                : !hasElevenLabsKey
                    ? "The ElevenLabs API key is missing."
                    : "The OpenAI API key, organization ID, or project ID is missing.";
            EditorGUILayout.HelpBox($"{missingCredentials} Save them in FP Keys Manager.", MessageType.Warning);
            if (GUILayout.Button("Open FP Keys Manager"))
            {
                FP_UtilityKeys.ShowWindow();
            }
        }

        private void DrawVoiceSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Voice", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(isRequestRunning || !HasElevenLabsApiKey()))
            {
                if (GUILayout.Button(isRequestRunning ? "Request in progress..." : "Refresh Voices"))
                {
                    RefreshVoices();
                }
            }

            if (voices.Count == 0)
            {
                EditorGUILayout.HelpBox("No voices loaded.", MessageType.Info);
            }
            else
            {
                int nextIndex = EditorGUILayout.Popup("Selected Voice", selectedVoiceIndex, voiceDisplayNames);
                if (nextIndex != selectedVoiceIndex)
                {
                    selectedVoiceIndex = nextIndex;
                    VoiceInfo selectedVoice = GetSelectedVoice();
                    if (selectedVoice != null)
                    {
                        savedVoiceId = selectedVoice.voice_id;
                        voiceName = GetVoiceDisplayName(selectedVoice);
                        EditorPrefs.SetString(SelectedVoicePreference, savedVoiceId);
                        EditorPrefs.SetString(VoiceNamePreference, voiceName);
                    }
                }
            }

            modelId = EditorGUILayout.TextField("ElevenLabs Model ID", modelId);
            EditorGUILayout.LabelField("Output Format", OutputFormat);
        }

        private void DrawRequestSettingsSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Request Settings", EditorStyles.boldLabel);
            sourceLanguage = (FPTranslationLanguage)EditorGUILayout.EnumPopup("Original Language", sourceLanguage);
            targetLanguage = (FPTranslationLanguage)EditorGUILayout.EnumPopup("Translate To", targetLanguage);
            openAIModelId = EditorGUILayout.TextField("OpenAI Model ID", openAIModelId);

            if (sourceLanguage == targetLanguage)
            {
                EditorGUILayout.HelpBox("Choose two different languages for translation.", MessageType.Warning);
            }
            else if (sourceLanguage != FPTranslationLanguage.English && targetLanguage != FPTranslationLanguage.English)
            {
                EditorGUILayout.HelpBox(
                    "This language pair excludes English, so each translation pass makes one additional OpenAI request to derive its English Base File Name.",
                    MessageType.Info);
            }
        }

        private void DrawRequestsSection()
        {
            EditorGUILayout.Space();
            requestsExpanded = EditorGUILayout.Foldout(
                requestsExpanded,
                $"Requests ({speechRequests.Count})",
                true);
            if (!requestsExpanded)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "One row and fifty rows use the same workflow. Every Base File Name should be the English form; translation fills it automatically.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(isRequestRunning))
                {
                    if (GUILayout.Button("Add Request"))
                    {
                        speechRequests.Add(new SpeechRequest());
                    }

                    if (GUILayout.Button("Load Markdown"))
                    {
                        LoadMarkdownRequests();
                    }

                    if (GUILayout.Button("Save Markdown"))
                    {
                        SaveMarkdownRequests();
                    }

                    using (new EditorGUI.DisabledScope(speechRequests.Count == 0))
                    {
                        if (GUILayout.Button("Clear All"))
                        {
                            ClearAllRequests();
                        }
                    }
                }
            }

            if (speechRequests.Count == 0)
            {
                EditorGUILayout.HelpBox("Add a request or load a markdown file to begin.", MessageType.Info);
                return;
            }

            GUIStyle textAreaStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true
            };

            for (int i = 0; i < speechRequests.Count; i++)
            {
                SpeechRequest speechRequest = speechRequests[i];
                if (speechRequest == null)
                {
                    speechRequest = new SpeechRequest();
                    speechRequests[i] = speechRequest;
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string rowLabel = string.IsNullOrWhiteSpace(speechRequest.baseFileName)
                            ? $"Request {i + 1}"
                            : $"Request {i + 1}: {speechRequest.baseFileName}";
                        speechRequest.expanded = EditorGUILayout.Foldout(speechRequest.expanded, rowLabel, true);

                        using (new EditorGUI.DisabledScope(isRequestRunning))
                        {
                            if (GUILayout.Button("Clear", GUILayout.Width(54f)))
                            {
                                ClearRequest(speechRequest);
                            }

                            if (GUILayout.Button("Remove", GUILayout.Width(64f)))
                            {
                                speechRequests.RemoveAt(i);
                                i--;
                                continue;
                            }
                        }
                    }

                    if (!speechRequest.expanded)
                    {
                        continue;
                    }

                    using (new EditorGUI.DisabledScope(isRequestRunning))
                    {
                        speechRequest.isColor = EditorGUILayout.Toggle("Color Item", speechRequest.isColor);
                        speechRequest.baseFileName = EditorGUILayout.TextField("Base File Name (English)", speechRequest.baseFileName);
                        EditorGUILayout.LabelField("Original Text", EditorStyles.miniLabel);
                        speechRequest.originalText = EditorGUILayout.TextArea(
                            speechRequest.originalText,
                            textAreaStyle,
                            GUILayout.MinHeight(80f),
                            GUILayout.ExpandWidth(true));
                    }

                    if (!string.IsNullOrWhiteSpace(speechRequest.translatedText))
                    {
                        EditorGUILayout.LabelField("Translated Text", EditorStyles.miniLabel);
                        using (new EditorGUI.DisabledScope(true))
                        {
                            EditorGUILayout.TextArea(
                                speechRequest.translatedText,
                                textAreaStyle,
                                GUILayout.MinHeight(64f),
                                GUILayout.ExpandWidth(true));
                        }

                        if (!HasCurrentTranslation(speechRequest))
                        {
                            EditorGUILayout.HelpBox(
                                "This row's text or the shared language pair changed. Translate the requests again before generating its audio.",
                                MessageType.Warning);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(speechRequest.statusMessage))
                    {
                        EditorGUILayout.HelpBox(speechRequest.statusMessage, speechRequest.statusMessageType);
                    }
                }
            }

            bool canTranslate = HasOpenAICredentials()
                && sourceLanguage != targetLanguage
                && !string.IsNullOrWhiteSpace(openAIModelId)
                && HasAnyRequestText();
            using (new EditorGUI.DisabledScope(isRequestRunning || !canTranslate))
            {
                if (GUILayout.Button(isRequestRunning ? "Request in progress..." : "Translate All Requests", GUILayout.Height(26f)))
                {
                    TranslateRequests();
                }
            }
        }

        private void DrawOutputSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Unity Output", EditorStyles.boldLabel);
            voiceName = EditorGUILayout.TextField("Voice Name", voiceName);
            EditorGUILayout.HelpBox(
                "Selected Voice controls synthesis. Voice Name is the editable suffix appended to generated filenames.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Folder");
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField(outputAssetFolder);
                }

                if (GUILayout.Button("Browse", GUILayout.Width(72f)))
                {
                    SelectOutputFolder();
                }
            }
        }

        private void DrawFPVocabSection()
        {
            EditorGUILayout.Space();
            generateFPVocab = EditorGUILayout.ToggleLeft("Generate FP_Vocab", generateFPVocab);
            if (!generateFPVocab)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (!FPElevenLabsVocabAssetUtility.IsAvailable)
                {
                    EditorGUILayout.HelpBox(
                        "FP_Vocab was not found. Install or enable FP_Utility_EDU before generating vocab assets.",
                        MessageType.Error);
                }

                vocabLevelIntroduced = (FP_LanguageLevel)EditorGUILayout.EnumPopup(
                    "Level Introduced",
                    vocabLevelIntroduced);
                vocabCEFRLevel = (CEFRLevel)EditorGUILayout.EnumPopup("CEFR Level", vocabCEFRLevel);
                vocabCategory = (FP_VocabCategory)EditorGUILayout.EnumPopup("Vocab Category", vocabCategory);
                EditorGUILayout.HelpBox(
                    "Two reciprocal FP_Vocab assets are created for every generated audio pair in the selected Unity output folder.",
                    MessageType.Info);
            }
        }

        private void DrawGenerateSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generate Audio", EditorStyles.boldLabel);

            bool canGenerate = HasElevenLabsApiKey()
                && GetSelectedVoice() != null
                && !string.IsNullOrWhiteSpace(modelId)
                && !string.IsNullOrWhiteSpace(voiceName)
                && (!generateFPVocab || FPElevenLabsVocabAssetUtility.IsAvailable)
                && HasAnyCurrentTranslation();

            using (new EditorGUI.DisabledScope(isRequestRunning || !canGenerate))
            {
                if (GUILayout.Button(isRequestRunning ? "Request in progress..." : "Generate All Audio Pairs", GUILayout.Height(30f)))
                {
                    GenerateSpeechPairs();
                }
            }

            if (!string.IsNullOrWhiteSpace(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, statusMessageType);
            }
        }

        private void LoadMarkdownRequests()
        {
            string selectedPath = EditorUtility.OpenFilePanel(
                "Load ElevenLabs request markdown",
                GetMarkdownDialogDirectory(),
                "md");
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            try
            {
                string markdown = File.ReadAllText(selectedPath, Encoding.UTF8);
                if (!FPElevenLabsMarkdownUtility.TryParse(markdown, out FPElevenLabsMarkdownDocument document, out string error))
                {
                    SetStatus($"Could not load markdown: {error}", MessageType.Error);
                    return;
                }

                if (speechRequests.Count == 1 && IsEmptyRequest(speechRequests[0]))
                {
                    speechRequests.Clear();
                }

                for (int i = 0; i < document.Items.Count; i++)
                {
                    FPElevenLabsMarkdownItem item = document.Items[i];
                    speechRequests.Add(new SpeechRequest
                    {
                        isColor = item.IsColor,
                        originalText = item.Text,
                        baseFileName = document.SourceLanguage == FPTranslationLanguage.English
                            ? item.Text
                            : string.Empty
                    });
                }

                voiceName = document.Person;
                sourceLanguage = document.SourceLanguage;
                targetLanguage = document.TargetLanguage;
                TrySelectVoiceByName(document.Person);
                lastMarkdownPath = selectedPath;
                requestsExpanded = true;
                SetStatus(
                    $"Appended {document.Items.Count} request(s) from {Path.GetFileName(selectedPath)} and applied {document.Person}, {FPElevenLabsEditorUtility.GetLanguageName(sourceLanguage)} to {FPElevenLabsEditorUtility.GetLanguageName(targetLanguage)}.",
                    MessageType.Info);
            }
            catch (Exception exception)
            {
                SetStatus($"Could not load markdown: {exception.Message}", MessageType.Error);
                Debug.LogError($"ElevenLabs markdown import failed: {exception.Message}");
            }
        }

        private void SaveMarkdownRequests()
        {
            var document = new FPElevenLabsMarkdownDocument
            {
                Person = voiceName.Trim(),
                SourceLanguage = sourceLanguage,
                TargetLanguage = targetLanguage
            };

            for (int i = 0; i < speechRequests.Count; i++)
            {
                SpeechRequest speechRequest = speechRequests[i];
                if (speechRequest == null || string.IsNullOrWhiteSpace(speechRequest.originalText))
                {
                    continue;
                }

                document.Items.Add(new FPElevenLabsMarkdownItem(
                    speechRequest.originalText.Trim(),
                    speechRequest.isColor));
            }

            if (string.IsNullOrWhiteSpace(document.Person))
            {
                SetStatus("Enter a Voice Name before saving markdown so the Person section can be written.", MessageType.Warning);
                return;
            }

            if (document.Items.Count == 0)
            {
                SetStatus("Add at least one request with Original Text before saving markdown.", MessageType.Warning);
                return;
            }

            if (document.SourceLanguage == document.TargetLanguage)
            {
                SetStatus("Choose two different languages before saving markdown.", MessageType.Warning);
                return;
            }

            string defaultFileName = string.IsNullOrWhiteSpace(lastMarkdownPath)
                ? "ElevenLabsRequests"
                : Path.GetFileNameWithoutExtension(lastMarkdownPath);
            string selectedPath = EditorUtility.SaveFilePanel(
                "Save ElevenLabs request markdown",
                GetMarkdownDialogDirectory(),
                defaultFileName,
                "md");
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            try
            {
                File.WriteAllText(
                    selectedPath,
                    FPElevenLabsMarkdownUtility.BuildMarkdown(document),
                    new UTF8Encoding(false));
                lastMarkdownPath = selectedPath;
                SetStatus($"Saved {document.Items.Count} request(s) to {selectedPath}.", MessageType.Info);
            }
            catch (Exception exception)
            {
                SetStatus($"Could not save markdown: {exception.Message}", MessageType.Error);
                Debug.LogError($"ElevenLabs markdown export failed: {exception.Message}");
            }
        }

        private void ClearAllRequests()
        {
            if (!EditorUtility.DisplayDialog(
                    "Clear All ElevenLabs Requests",
                    $"Clear all {speechRequests.Count} request(s)? This cannot be undone.",
                    "Clear All",
                    "Cancel"))
            {
                return;
            }

            speechRequests.Clear();
            SetStatus("Cleared all requests.", MessageType.Info);
        }

        private static void ClearRequest(SpeechRequest speechRequest)
        {
            speechRequest.isColor = false;
            speechRequest.baseFileName = string.Empty;
            speechRequest.originalText = string.Empty;
            speechRequest.translatedText = string.Empty;
            speechRequest.translatedSourceText = string.Empty;
            speechRequest.translatedSourceLanguage = default;
            speechRequest.translatedTargetLanguage = default;
            speechRequest.statusMessage = string.Empty;
            speechRequest.statusMessageType = MessageType.Info;
        }

        private static bool IsEmptyRequest(SpeechRequest speechRequest)
        {
            return speechRequest == null
                || (string.IsNullOrWhiteSpace(speechRequest.baseFileName)
                    && string.IsNullOrWhiteSpace(speechRequest.originalText)
                    && string.IsNullOrWhiteSpace(speechRequest.translatedText));
        }

        private string GetMarkdownDialogDirectory()
        {
            if (!string.IsNullOrWhiteSpace(lastMarkdownPath))
            {
                string directory = Path.GetDirectoryName(lastMarkdownPath);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    return directory;
                }
            }

            return Application.dataPath;
        }

        private bool TrySelectVoiceByName(string requestedVoiceName)
        {
            for (int i = 0; i < voices.Count; i++)
            {
                if (!string.Equals(GetVoiceDisplayName(voices[i]), requestedVoiceName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                selectedVoiceIndex = i;
                savedVoiceId = voices[i].voice_id;
                EditorPrefs.SetString(SelectedVoicePreference, savedVoiceId);
                return true;
            }

            return false;
        }

        private void SelectOutputFolder()
        {
            string currentFolder = FPElevenLabsEditorUtility.GetAbsoluteFolderPath(outputAssetFolder, Application.dataPath);
            string selectedFolder = EditorUtility.OpenFolderPanel("Select ElevenLabs output folder", currentFolder, string.Empty);
            if (string.IsNullOrWhiteSpace(selectedFolder))
            {
                return;
            }

            if (!FPElevenLabsEditorUtility.TryConvertAbsoluteFolderToAssetPath(
                    selectedFolder,
                    Application.dataPath,
                    out string selectedAssetFolder))
            {
                SetStatus("Select a folder inside this Unity project's Assets folder.", MessageType.Error);
                return;
            }

            outputAssetFolder = selectedAssetFolder;
            EditorPrefs.SetString(OutputFolderPreference, outputAssetFolder);
            SetStatus($"Output folder set to {outputAssetFolder}.", MessageType.Info);
        }

        private async void RefreshVoices()
        {
            if (isRequestRunning || this == null)
            {
                return;
            }

            string apiKey = GetElevenLabsApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                SetStatus("Save an ElevenLabs API key in FP Keys Manager first.", MessageType.Warning);
                return;
            }

            isRequestRunning = true;
            SetStatus("Requesting ElevenLabs voices...", MessageType.Info);

            try
            {
                List<VoiceInfo> requestedVoices = await RequestVoicesAsync(apiKey);
                if (this == null)
                {
                    return;
                }

                voices.Clear();
                voices.AddRange(requestedVoices);
                BuildVoiceDisplayNames();
                RestoreSelectedVoice();

                SetStatus(
                    voices.Count > 0
                        ? $"Loaded {voices.Count} ElevenLabs voice(s)."
                        : "ElevenLabs returned no voices for this account.",
                    voices.Count > 0 ? MessageType.Info : MessageType.Warning);
            }
            catch (Exception exception)
            {
                if (this != null)
                {
                    SetStatus($"Could not load ElevenLabs voices: {exception.Message}", MessageType.Error);
                    Debug.LogError($"ElevenLabs voice request failed: {exception.Message}");
                }
            }
            finally
            {
                if (this != null)
                {
                    isRequestRunning = false;
                    Repaint();
                }
            }
        }

        private async void TranslateRequests()
        {
            if (isRequestRunning || this == null)
            {
                return;
            }

            if (!HasOpenAICredentials())
            {
                SetStatus("Save the OpenAI API key, organization ID, and project ID in FP Keys Manager first.", MessageType.Warning);
                return;
            }

            if (sourceLanguage == targetLanguage)
            {
                SetStatus("Choose two different languages for translation.", MessageType.Warning);
                return;
            }

            string requestedApiKey = GetOpenAIApiKey();
            string requestedOrganizationId = GetOpenAIOrganizationId();
            string requestedProjectId = GetOpenAIProjectId();
            string requestedOpenAIModelId = openAIModelId.Trim();
            FPTranslationLanguage requestedSourceLanguage = sourceLanguage;
            FPTranslationLanguage requestedTargetLanguage = targetLanguage;
            int translatedCount = 0;
            int failedOrSkippedCount = 0;

            isRequestRunning = true;
            SetStatus(
                $"Translating {speechRequests.Count} request(s) from {FPElevenLabsEditorUtility.GetLanguageName(requestedSourceLanguage)} to {FPElevenLabsEditorUtility.GetLanguageName(requestedTargetLanguage)}...",
                MessageType.Info);

            try
            {
                for (int i = 0; i < speechRequests.Count; i++)
                {
                    SpeechRequest speechRequest = speechRequests[i];
                    if (speechRequest == null)
                    {
                        speechRequest = new SpeechRequest();
                        speechRequests[i] = speechRequest;
                    }

                    string sourceText = speechRequest.originalText.Trim();
                    if (string.IsNullOrWhiteSpace(sourceText))
                    {
                        SetRequestStatus(speechRequest, "Skipped because Original Text is empty.", MessageType.Warning);
                        failedOrSkippedCount++;
                        continue;
                    }

                    SetRequestStatus(speechRequest, $"Translating request {i + 1} of {speechRequests.Count}...", MessageType.Info);
                    Repaint();

                    try
                    {
                        string translation = await RequestTranslationAsync(
                            requestedApiKey,
                            requestedOrganizationId,
                            requestedProjectId,
                            requestedOpenAIModelId,
                            requestedSourceLanguage,
                            requestedTargetLanguage,
                            sourceText);
                        if (this == null)
                        {
                            return;
                        }

                        string additionalEnglishTranslation = string.Empty;
                        if (requestedSourceLanguage != FPTranslationLanguage.English
                            && requestedTargetLanguage != FPTranslationLanguage.English)
                        {
                            additionalEnglishTranslation = await RequestTranslationAsync(
                                requestedApiKey,
                                requestedOrganizationId,
                                requestedProjectId,
                                requestedOpenAIModelId,
                                requestedSourceLanguage,
                                FPTranslationLanguage.English,
                                sourceText);
                            if (this == null)
                            {
                                return;
                            }
                        }

                        string englishBaseFileName = FPElevenLabsEditorUtility.GetEnglishBaseFileName(
                            requestedSourceLanguage,
                            requestedTargetLanguage,
                            sourceText,
                            translation,
                            additionalEnglishTranslation);

                        speechRequest.translatedText = translation;
                        speechRequest.translatedSourceText = sourceText;
                        speechRequest.translatedSourceLanguage = requestedSourceLanguage;
                        speechRequest.translatedTargetLanguage = requestedTargetLanguage;
                        speechRequest.baseFileName = englishBaseFileName.Trim();
                        SetRequestStatus(
                            speechRequest,
                            HasCurrentTranslation(speechRequest)
                                ? "Translation and English Base File Name received."
                                : "Translation received, but this row changed during the request. Translate it again before generating audio.",
                            HasCurrentTranslation(speechRequest) ? MessageType.Info : MessageType.Warning);
                        translatedCount++;
                    }
                    catch (Exception exception)
                    {
                        failedOrSkippedCount++;
                        SetRequestStatus(speechRequest, $"Translation failed: {exception.Message}", MessageType.Error);
                        Debug.LogError($"OpenAI translation request {i + 1} failed: {exception.Message}");
                    }
                }

                EditorPrefs.SetString(OpenAIModelIdPreference, openAIModelId);
                EditorPrefs.SetInt(SourceLanguagePreference, (int)sourceLanguage);
                EditorPrefs.SetInt(TargetLanguagePreference, (int)targetLanguage);
                SetStatus(
                    failedOrSkippedCount == 0
                        ? $"Translated all {translatedCount} request(s). Review the translations, then generate all audio pairs."
                        : $"Translated {translatedCount} request(s); {failedOrSkippedCount} failed or were skipped. Review each row before generating audio.",
                    failedOrSkippedCount == 0 ? MessageType.Info : MessageType.Warning);
            }
            finally
            {
                if (this != null)
                {
                    isRequestRunning = false;
                    Repaint();
                }
            }
        }

        private async void GenerateSpeechPairs()
        {
            if (isRequestRunning || this == null)
            {
                return;
            }

            VoiceInfo selectedVoice = GetSelectedVoice();
            if (selectedVoice == null)
            {
                SetStatus("Select an ElevenLabs voice first.", MessageType.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(voiceName))
            {
                SetStatus("Enter a Voice Name for the output filename suffix.", MessageType.Warning);
                return;
            }

            string requestedApiKey = GetElevenLabsApiKey();
            string requestedVoiceId = selectedVoice.voice_id;
            string requestedVoiceName = voiceName.Trim();
            string requestedModelId = modelId.Trim();
            string requestedAssetFolder = outputAssetFolder;
            FPTranslationLanguage requestedSourceLanguage = sourceLanguage;
            FPTranslationLanguage requestedTargetLanguage = targetLanguage;
            bool requestedGenerateFPVocab = generateFPVocab;
            FP_LanguageLevel requestedLevelIntroduced = vocabLevelIntroduced;
            CEFRLevel requestedCEFRLevel = vocabCEFRLevel;
            FP_VocabCategory requestedVocabCategory = vocabCategory;
            Type requestedVocabType = null;
            if (requestedGenerateFPVocab
                && !FPElevenLabsVocabAssetUtility.TryGetVocabType(out requestedVocabType))
            {
                SetStatus("FP_Vocab was not found. Install or enable FP_Utility_EDU before generating vocab assets.", MessageType.Error);
                return;
            }

            string absoluteFolder = FPElevenLabsEditorUtility.GetAbsoluteFolderPath(requestedAssetFolder, Application.dataPath);
            if (!Directory.Exists(absoluteFolder))
            {
                SetStatus("The selected Unity output folder no longer exists. Choose another folder.", MessageType.Error);
                return;
            }

            int generatedCount = 0;
            int failedOrSkippedCount = 0;
            var importedClips = new List<UnityEngine.Object>();

            isRequestRunning = true;
            SetStatus($"Generating {speechRequests.Count} audio pair(s) with {selectedVoice.name}...", MessageType.Info);

            try
            {
                for (int i = 0; i < speechRequests.Count; i++)
                {
                    SpeechRequest speechRequest = speechRequests[i];
                    if (speechRequest == null)
                    {
                        speechRequest = new SpeechRequest();
                        speechRequests[i] = speechRequest;
                    }

                    if (string.IsNullOrWhiteSpace(speechRequest.baseFileName))
                    {
                        SetRequestStatus(speechRequest, "Skipped because the English Base File Name is empty.", MessageType.Warning);
                        failedOrSkippedCount++;
                        continue;
                    }

                    if (!HasCurrentTranslation(speechRequest, requestedSourceLanguage, requestedTargetLanguage))
                    {
                        SetRequestStatus(speechRequest, "Skipped because its translation is missing or stale.", MessageType.Warning);
                        failedOrSkippedCount++;
                        continue;
                    }

                    string requestedBaseFileName = speechRequest.baseFileName.Trim();
                    string requestedOriginalText = speechRequest.originalText.Trim();
                    string requestedTranslatedText = speechRequest.translatedText.Trim();
                    bool requestedIsColor = speechRequest.isColor;
                    bool audioAssetsSaved = false;
                    SetRequestStatus(speechRequest, $"Generating audio pair {i + 1} of {speechRequests.Count}...", MessageType.Info);
                    Repaint();

                    try
                    {
                        byte[] originalAudioBytes = await RequestSpeechAsync(
                            requestedApiKey,
                            requestedVoiceId,
                            requestedOriginalText,
                            requestedModelId);
                        byte[] translatedAudioBytes = await RequestSpeechAsync(
                            requestedApiKey,
                            requestedVoiceId,
                            requestedTranslatedText,
                            requestedModelId);
                        if (this == null)
                        {
                            return;
                        }

                        string originalFileName = FPElevenLabsEditorUtility.BuildLanguageMp3FileName(
                            requestedBaseFileName,
                            "Original",
                            requestedSourceLanguage,
                            requestedVoiceName,
                            requestedIsColor);
                        string translatedFileName = FPElevenLabsEditorUtility.BuildLanguageMp3FileName(
                            requestedBaseFileName,
                            "Translation",
                            requestedTargetLanguage,
                            requestedVoiceName,
                            requestedIsColor);
                        AudioClip originalClip = SaveAudioAsset(
                            originalAudioBytes,
                            originalFileName,
                            requestedAssetFolder,
                            absoluteFolder,
                            out string originalAssetPath);
                        AudioClip translatedClip = SaveAudioAsset(
                            translatedAudioBytes,
                            translatedFileName,
                            requestedAssetFolder,
                            absoluteFolder,
                            out string translatedAssetPath);
                        audioAssetsSaved = true;

                        if (originalClip != null)
                        {
                            importedClips.Add(originalClip);
                        }

                        if (translatedClip != null)
                        {
                            importedClips.Add(translatedClip);
                        }

                        FPElevenLabsVocabAssetPair vocabPair = default;
                        if (requestedGenerateFPVocab)
                        {
                            vocabPair = FPElevenLabsVocabAssetUtility.CreatePair(
                                requestedVocabType,
                                requestedOriginalText,
                                requestedTranslatedText,
                                requestedSourceLanguage,
                                requestedTargetLanguage,
                                originalClip,
                                translatedClip,
                                originalAssetPath,
                                translatedAssetPath,
                                requestedLevelIntroduced,
                                requestedCEFRLevel,
                                requestedVocabCategory);
                            importedClips.Add(vocabPair.SourceVocab);
                            importedClips.Add(vocabPair.TargetVocab);
                        }

                        generatedCount++;
                        SetRequestStatus(
                            speechRequest,
                            requestedGenerateFPVocab
                                ? $"Saved audio and reciprocal FP_Vocab assets for {requestedBaseFileName}."
                                : $"Saved {originalAssetPath} and {translatedAssetPath}.",
                            MessageType.Info);
                        Debug.Log($"Saved ElevenLabs audio to {originalAssetPath} and {translatedAssetPath}");
                    }
                    catch (Exception exception)
                    {
                        failedOrSkippedCount++;
                        SetRequestStatus(
                            speechRequest,
                            requestedGenerateFPVocab && audioAssetsSaved
                                ? $"Audio was saved, but FP_Vocab generation failed: {exception.Message}"
                                : $"Audio generation failed: {exception.Message}",
                            MessageType.Error);
                        Debug.LogError($"ElevenLabs audio/FP_Vocab request {i + 1} failed: {exception.Message}");
                    }
                }

                if (importedClips.Count > 0)
                {
                    Selection.objects = importedClips.ToArray();
                    EditorGUIUtility.PingObject(importedClips[importedClips.Count - 1]);
                }

                savedVoiceId = requestedVoiceId;
                EditorPrefs.SetString(SelectedVoicePreference, savedVoiceId);
                EditorPrefs.SetString(ModelIdPreference, modelId);
                EditorPrefs.SetString(VoiceNamePreference, voiceName);
                SetStatus(
                    failedOrSkippedCount == 0
                        ? requestedGenerateFPVocab
                            ? $"Generated all {generatedCount} audio and FP_Vocab pair(s) in {requestedAssetFolder}."
                            : $"Generated all {generatedCount} audio pair(s) in {requestedAssetFolder}."
                        : $"Generated {generatedCount} audio pair(s); {failedOrSkippedCount} failed or were skipped. Review each row for details.",
                    failedOrSkippedCount == 0 ? MessageType.Info : MessageType.Warning);
            }
            finally
            {
                if (this != null)
                {
                    isRequestRunning = false;
                    Repaint();
                }
            }
        }

        private static AudioClip SaveAudioAsset(
            byte[] audioBytes,
            string fileName,
            string assetFolder,
            string absoluteFolder,
            out string assetFilePath)
        {
            string requestedAssetPath = $"{assetFolder.TrimEnd('/')}/{fileName}";
            assetFilePath = AssetDatabase.GenerateUniqueAssetPath(requestedAssetPath);
            string absoluteFilePath = Path.Combine(absoluteFolder, Path.GetFileName(assetFilePath));
            File.WriteAllBytes(absoluteFilePath, audioBytes);
            AssetDatabase.ImportAsset(assetFilePath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(assetFilePath);
        }

        private static async Task<List<VoiceInfo>> RequestVoicesAsync(string apiKey)
        {
            var requestedVoices = new List<VoiceInfo>();
            var voiceIds = new HashSet<string>();
            string nextPageToken = null;

            do
            {
                string url = $"{VoicesEndpoint}?page_size=100&include_total_count=false&sort=name&sort_direction=asc";
                if (!string.IsNullOrWhiteSpace(nextPageToken))
                {
                    url += $"&next_page_token={Uri.EscapeDataString(nextPageToken)}";
                }

                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
                    request.SetRequestHeader("xi-api-key", apiKey);
                    request.timeout = 30;
                    await SendRequestAsync(request, "ElevenLabs");

                    VoicesResponse response = JsonUtility.FromJson<VoicesResponse>(request.downloadHandler.text);
                    if (response == null || response.voices == null)
                    {
                        throw new InvalidOperationException("ElevenLabs returned an invalid voices response.");
                    }

                    for (int i = 0; i < response.voices.Length; i++)
                    {
                        VoiceInfo voice = response.voices[i];
                        if (voice != null
                            && !string.IsNullOrWhiteSpace(voice.voice_id)
                            && voiceIds.Add(voice.voice_id))
                        {
                            requestedVoices.Add(voice);
                        }
                    }

                    if (!response.has_more)
                    {
                        nextPageToken = null;
                    }
                    else if (string.IsNullOrWhiteSpace(response.next_page_token))
                    {
                        throw new InvalidOperationException("ElevenLabs indicated more voices but did not return a page token.");
                    }
                    else
                    {
                        nextPageToken = response.next_page_token;
                    }
                }
            }
            while (!string.IsNullOrWhiteSpace(nextPageToken));

            requestedVoices.Sort((left, right) => string.Compare(
                left?.name,
                right?.name,
                StringComparison.OrdinalIgnoreCase));
            return requestedVoices;
        }

        private static async Task<string> RequestTranslationAsync(
            string apiKey,
            string organizationId,
            string projectId,
            string requestedModelId,
            FPTranslationLanguage originalLanguage,
            FPTranslationLanguage translationLanguage,
            string originalText)
        {
            var payload = new OpenAIResponsesRequest
            {
                model = requestedModelId,
                instructions = FPElevenLabsEditorUtility.BuildTranslationInstructions(
                    originalLanguage,
                    translationLanguage),
                input = originalText,
                max_output_tokens = 1024
            };

            byte[] payloadBytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
            using (var request = new UnityWebRequest(OpenAIResponsesEndpoint, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(payloadBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                request.SetRequestHeader("OpenAI-Organization", organizationId);
                request.SetRequestHeader("OpenAI-Project", projectId);
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 120;

                await SendRequestAsync(request, "OpenAI");
                return FPElevenLabsEditorUtility.ExtractOpenAIOutputText(request.downloadHandler.text);
            }
        }

        private static async Task<byte[]> RequestSpeechAsync(
            string apiKey,
            string voiceId,
            string text,
            string requestedModelId)
        {
            var payload = new TextToSpeechRequest
            {
                text = text,
                model_id = requestedModelId
            };

            string url = $"{TextToSpeechEndpoint}/{Uri.EscapeDataString(voiceId)}?output_format={OutputFormat}";
            byte[] payloadBytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));

            using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(payloadBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("xi-api-key", apiKey);
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "audio/mpeg");
                request.timeout = 120;

                await SendRequestAsync(request, "ElevenLabs");

                byte[] audioBytes = request.downloadHandler.data;
                if (audioBytes == null || audioBytes.Length == 0)
                {
                    throw new InvalidOperationException("ElevenLabs returned an empty audio file.");
                }

                return audioBytes;
            }
        }

        private static async Task SendRequestAsync(UnityWebRequest request, string serviceName)
        {
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                return;
            }

            string responseText = request.downloadHandler?.text;
            if (!string.IsNullOrWhiteSpace(responseText) && responseText.Length > 500)
            {
                responseText = responseText.Substring(0, 500);
            }

            string detail = string.IsNullOrWhiteSpace(responseText) ? request.error : responseText;
            throw new InvalidOperationException($"{serviceName} request failed ({request.responseCode}): {detail}");
        }

        private void BuildVoiceDisplayNames()
        {
            voiceDisplayNames = new string[voices.Count];
            for (int i = 0; i < voices.Count; i++)
            {
                VoiceInfo voice = voices[i];
                string displayName = GetVoiceDisplayName(voice);
                voiceDisplayNames[i] = string.IsNullOrWhiteSpace(voice.category)
                    ? displayName
                    : $"{displayName} ({voice.category})";
            }
        }

        private void RestoreSelectedVoice()
        {
            selectedVoiceIndex = 0;
            if (!string.IsNullOrWhiteSpace(voiceName) && TrySelectVoiceByName(voiceName))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(savedVoiceId))
            {
                for (int i = 0; i < voices.Count; i++)
                {
                    if (string.Equals(voices[i].voice_id, savedVoiceId, StringComparison.Ordinal))
                    {
                        selectedVoiceIndex = i;
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(voiceName))
            {
                VoiceInfo selectedVoice = GetSelectedVoice();
                if (selectedVoice != null)
                {
                    voiceName = GetVoiceDisplayName(selectedVoice);
                }
            }
        }

        private static string GetVoiceDisplayName(VoiceInfo voice)
        {
            return string.IsNullOrWhiteSpace(voice?.name) ? voice?.voice_id ?? string.Empty : voice.name;
        }

        private VoiceInfo GetSelectedVoice()
        {
            return selectedVoiceIndex >= 0 && selectedVoiceIndex < voices.Count
                ? voices[selectedVoiceIndex]
                : null;
        }

        private bool HasAnyRequestText()
        {
            for (int i = 0; i < speechRequests.Count; i++)
            {
                if (speechRequests[i] != null && !string.IsNullOrWhiteSpace(speechRequests[i].originalText))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasAnyCurrentTranslation()
        {
            for (int i = 0; i < speechRequests.Count; i++)
            {
                if (HasCurrentTranslation(speechRequests[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasCurrentTranslation(SpeechRequest speechRequest)
        {
            return HasCurrentTranslation(speechRequest, sourceLanguage, targetLanguage);
        }

        private static bool HasCurrentTranslation(
            SpeechRequest speechRequest,
            FPTranslationLanguage currentSourceLanguage,
            FPTranslationLanguage currentTargetLanguage)
        {
            return speechRequest != null
                && !string.IsNullOrWhiteSpace(speechRequest.translatedText)
                && string.Equals(speechRequest.translatedSourceText, speechRequest.originalText.Trim(), StringComparison.Ordinal)
                && speechRequest.translatedSourceLanguage == currentSourceLanguage
                && speechRequest.translatedTargetLanguage == currentTargetLanguage;
        }

        private static bool HasElevenLabsApiKey()
        {
            return !string.IsNullOrWhiteSpace(GetElevenLabsApiKey());
        }

        private static bool HasOpenAICredentials()
        {
            return !string.IsNullOrWhiteSpace(GetOpenAIApiKey())
                && !string.IsNullOrWhiteSpace(GetOpenAIOrganizationId())
                && !string.IsNullOrWhiteSpace(GetOpenAIProjectId());
        }

        private static string GetElevenLabsApiKey()
        {
            return EditorPrefs.GetString(FP_UtilityKeys.ElevenLabsApiKeyPreference, string.Empty).Trim();
        }

        private static string GetOpenAIApiKey()
        {
            return EditorPrefs.GetString(FP_UtilityKeys.ChatGptApiKeyPreference, string.Empty).Trim();
        }

        private static string GetOpenAIOrganizationId()
        {
            return EditorPrefs.GetString(FP_UtilityKeys.ChatGptOrganizationIdPreference, string.Empty).Trim();
        }

        private static string GetOpenAIProjectId()
        {
            return EditorPrefs.GetString(FP_UtilityKeys.ChatGptProjectIdPreference, string.Empty).Trim();
        }

        private void SetStatus(string message, MessageType messageType)
        {
            statusMessage = message;
            statusMessageType = messageType;
            Repaint();
        }

        private static void SetRequestStatus(SpeechRequest speechRequest, string message, MessageType messageType)
        {
            speechRequest.statusMessage = message;
            speechRequest.statusMessageType = messageType;
        }

        [Serializable]
        private sealed class SpeechRequest
        {
            public bool expanded = true;
            public bool isColor;
            public string baseFileName = string.Empty;
            public string originalText = string.Empty;
            public string translatedText = string.Empty;
            public string translatedSourceText = string.Empty;
            public FPTranslationLanguage translatedSourceLanguage;
            public FPTranslationLanguage translatedTargetLanguage;
            public string statusMessage = string.Empty;
            public MessageType statusMessageType = MessageType.Info;
        }

        [Serializable]
        private sealed class VoicesResponse
        {
            public VoiceInfo[] voices;
            public bool has_more;
            public string next_page_token;
        }

        [Serializable]
        private sealed class VoiceInfo
        {
            public string voice_id;
            public string name;
            public string category;
        }

        [Serializable]
        private sealed class TextToSpeechRequest
        {
            public string text;
            public string model_id;
        }

        [Serializable]
        private sealed class OpenAIResponsesRequest
        {
            public string model;
            public string instructions;
            public string input;
            public int max_output_tokens;
        }
    }

    internal enum FPTranslationLanguage
    {
        English = 0,
        Spanish = 1,
        French = 2
    }

    internal sealed class FPElevenLabsMarkdownDocument
    {
        internal string Person = string.Empty;
        internal FPTranslationLanguage SourceLanguage;
        internal FPTranslationLanguage TargetLanguage;
        internal readonly List<FPElevenLabsMarkdownItem> Items = new List<FPElevenLabsMarkdownItem>();
    }

    internal sealed class FPElevenLabsMarkdownItem
    {
        internal FPElevenLabsMarkdownItem(string text, bool isColor)
        {
            Text = text;
            IsColor = isColor;
        }

        internal string Text { get; }
        internal bool IsColor { get; }
    }

    internal static class FPElevenLabsMarkdownUtility
    {
        private enum MarkdownSection
        {
            None,
            Person,
            TranslationModel,
            Language,
            Color
        }

        internal static bool TryParse(
            string markdown,
            out FPElevenLabsMarkdownDocument document,
            out string error)
        {
            document = new FPElevenLabsMarkdownDocument();
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(markdown))
            {
                error = "The markdown file is empty.";
                return false;
            }

            MarkdownSection section = MarkdownSection.None;
            bool hasTranslationModel = false;
            string[] lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    section = GetSection(line.Substring(3).Trim());
                    continue;
                }

                if (!TryGetBulletValue(line, out string value))
                {
                    continue;
                }

                switch (section)
                {
                    case MarkdownSection.Person:
                        if (string.IsNullOrWhiteSpace(document.Person))
                        {
                            document.Person = value;
                        }
                        break;
                    case MarkdownSection.TranslationModel:
                        if (!TryParseLanguagePair(
                                value,
                                out FPTranslationLanguage sourceLanguage,
                                out FPTranslationLanguage targetLanguage))
                        {
                            error = $"Line {i + 1} has an unsupported Translation Model. Use formats such as 'Spanish to English'.";
                            return false;
                        }

                        document.SourceLanguage = sourceLanguage;
                        document.TargetLanguage = targetLanguage;
                        hasTranslationModel = true;
                        break;
                    case MarkdownSection.Language:
                        document.Items.Add(new FPElevenLabsMarkdownItem(value, false));
                        break;
                    case MarkdownSection.Color:
                        document.Items.Add(new FPElevenLabsMarkdownItem(value, true));
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(document.Person))
            {
                error = "The markdown file needs a Person heading with one bullet item.";
                return false;
            }

            if (!hasTranslationModel)
            {
                error = "The markdown file needs a Translation Model heading such as 'Spanish to English'.";
                return false;
            }

            if (document.SourceLanguage == document.TargetLanguage)
            {
                error = "The Translation Model must use two different languages.";
                return false;
            }

            if (document.Items.Count == 0)
            {
                error = "The markdown file needs at least one bullet under Language or Color.";
                return false;
            }

            return true;
        }

        internal static string BuildMarkdown(FPElevenLabsMarkdownDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var markdown = new StringBuilder();
            markdown.AppendLine("# Eleven Labs");
            markdown.AppendLine();
            markdown.AppendLine("Notes: Base File Names are generated from the English form. Items under Color receive a Color filename prefix.");
            markdown.AppendLine();
            markdown.AppendLine("## Person");
            markdown.AppendLine();
            markdown.AppendLine($"* {NormalizeBulletText(document.Person)}");
            markdown.AppendLine();
            markdown.AppendLine("## Translation Model");
            markdown.AppendLine();
            markdown.AppendLine(
                $"* {FPElevenLabsEditorUtility.GetLanguageName(document.SourceLanguage)} to {FPElevenLabsEditorUtility.GetLanguageName(document.TargetLanguage)}");
            markdown.AppendLine();
            markdown.AppendLine("## Language");
            markdown.AppendLine();
            AppendItems(markdown, document.Items, false);

            bool hasColorItems = false;
            for (int i = 0; i < document.Items.Count; i++)
            {
                if (document.Items[i].IsColor)
                {
                    hasColorItems = true;
                    break;
                }
            }

            if (hasColorItems)
            {
                markdown.AppendLine();
                markdown.AppendLine("## Color");
                markdown.AppendLine();
                AppendItems(markdown, document.Items, true);
            }

            return markdown.ToString();
        }

        internal static bool TryParseLanguagePair(
            string value,
            out FPTranslationLanguage sourceLanguage,
            out FPTranslationLanguage targetLanguage)
        {
            sourceLanguage = default;
            targetLanguage = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            const string separator = " to ";
            int separatorIndex = value.IndexOf(separator, StringComparison.OrdinalIgnoreCase);
            if (separatorIndex <= 0 || separatorIndex + separator.Length >= value.Length)
            {
                return false;
            }

            string sourceName = value.Substring(0, separatorIndex).Trim();
            string targetName = value.Substring(separatorIndex + separator.Length).Trim();
            return TryParseLanguage(sourceName, out sourceLanguage)
                && TryParseLanguage(targetName, out targetLanguage);
        }

        private static MarkdownSection GetSection(string heading)
        {
            if (string.Equals(heading, "Person", StringComparison.OrdinalIgnoreCase))
            {
                return MarkdownSection.Person;
            }

            if (string.Equals(heading, "Translation Model", StringComparison.OrdinalIgnoreCase))
            {
                return MarkdownSection.TranslationModel;
            }

            if (string.Equals(heading, "Language", StringComparison.OrdinalIgnoreCase))
            {
                return MarkdownSection.Language;
            }

            return string.Equals(heading, "Color", StringComparison.OrdinalIgnoreCase)
                ? MarkdownSection.Color
                : MarkdownSection.None;
        }

        private static bool TryGetBulletValue(string line, out string value)
        {
            value = string.Empty;
            if (line.Length < 2
                || (line[0] != '*' && line[0] != '-' && line[0] != '+')
                || !char.IsWhiteSpace(line[1]))
            {
                return false;
            }

            value = line.Substring(2).Trim();
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool TryParseLanguage(string value, out FPTranslationLanguage language)
        {
            for (int i = (int)FPTranslationLanguage.English; i <= (int)FPTranslationLanguage.French; i++)
            {
                FPTranslationLanguage candidate = (FPTranslationLanguage)i;
                if (string.Equals(
                        value,
                        FPElevenLabsEditorUtility.GetLanguageName(candidate),
                        StringComparison.OrdinalIgnoreCase))
                {
                    language = candidate;
                    return true;
                }
            }

            language = default;
            return false;
        }

        private static void AppendItems(
            StringBuilder markdown,
            IReadOnlyList<FPElevenLabsMarkdownItem> items,
            bool includeColorItems)
        {
            for (int i = 0; i < items.Count; i++)
            {
                FPElevenLabsMarkdownItem item = items[i];
                if (item.IsColor == includeColorItems)
                {
                    markdown.AppendLine($"* {NormalizeBulletText(item.Text)}");
                }
            }
        }

        private static string NormalizeBulletText(string value)
        {
            return (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        }
    }

    internal readonly struct FPElevenLabsVocabAssetPair
    {
        internal FPElevenLabsVocabAssetPair(UnityEngine.Object sourceVocab, UnityEngine.Object targetVocab)
        {
            SourceVocab = sourceVocab;
            TargetVocab = targetVocab;
        }

        internal UnityEngine.Object SourceVocab { get; }
        internal UnityEngine.Object TargetVocab { get; }
    }

    internal static class FPElevenLabsVocabAssetUtility
    {
        private const string VocabTypeName = "FuzzPhyte.Utility.EDU.FP_Vocab";
        private static Type cachedVocabType;
        private static bool searchedForVocabType;

        internal static bool IsAvailable => TryGetVocabType(out _);

        internal static bool TryGetVocabType(out Type vocabType)
        {
            if (!searchedForVocabType)
            {
                searchedForVocabType = true;
                foreach (Type candidate in TypeCache.GetTypesDerivedFrom<ScriptableObject>())
                {
                    if (string.Equals(candidate.FullName, VocabTypeName, StringComparison.Ordinal))
                    {
                        cachedVocabType = candidate;
                        break;
                    }
                }
            }

            vocabType = cachedVocabType;
            return vocabType != null;
        }

        internal static FPElevenLabsVocabAssetPair CreatePair(
            Type vocabType,
            string sourceWord,
            string targetWord,
            FPTranslationLanguage sourceLanguage,
            FPTranslationLanguage targetLanguage,
            AudioClip sourceAudio,
            AudioClip targetAudio,
            string sourceAudioAssetPath,
            string targetAudioAssetPath,
            FP_LanguageLevel levelIntroduced,
            CEFRLevel cefrLevel,
            FP_VocabCategory vocabCategory)
        {
            if (vocabType == null
                || !typeof(ScriptableObject).IsAssignableFrom(vocabType)
                || vocabType.IsAbstract)
            {
                throw new InvalidOperationException("FP_Vocab is unavailable or is not a concrete ScriptableObject type.");
            }

            if (sourceAudio == null || targetAudio == null)
            {
                throw new InvalidOperationException("Both imported AudioClips are required to create reciprocal FP_Vocab assets.");
            }

            string sourceVocabPath = BuildVocabAssetPath(sourceAudioAssetPath);
            string targetVocabPath = BuildVocabAssetPath(targetAudioAssetPath);
            ScriptableObject sourceVocab = null;
            ScriptableObject targetVocab = null;
            bool sourceAssetCreated = false;
            bool targetAssetCreated = false;

            try
            {
                sourceVocab = CreateConfiguredVocab(
                    vocabType,
                    sourceWord,
                    sourceLanguage,
                    sourceAudio,
                    Path.GetFileName(sourceAudioAssetPath),
                    levelIntroduced,
                    cefrLevel,
                    vocabCategory);
                targetVocab = CreateConfiguredVocab(
                    vocabType,
                    targetWord,
                    targetLanguage,
                    targetAudio,
                    Path.GetFileName(targetAudioAssetPath),
                    levelIntroduced,
                    cefrLevel,
                    vocabCategory);
                AssetDatabase.CreateAsset(sourceVocab, sourceVocabPath);
                sourceAssetCreated = true;
                AssetDatabase.CreateAsset(targetVocab, targetVocabPath);
                targetAssetCreated = true;
                SetTranslationReference(sourceVocab, targetVocab);
                SetTranslationReference(targetVocab, sourceVocab);
                EditorUtility.SetDirty(sourceVocab);
                EditorUtility.SetDirty(targetVocab);
                AssetDatabase.SaveAssetIfDirty(sourceVocab);
                AssetDatabase.SaveAssetIfDirty(targetVocab);
                return new FPElevenLabsVocabAssetPair(sourceVocab, targetVocab);
            }
            catch
            {
                if (targetAssetCreated)
                {
                    AssetDatabase.DeleteAsset(targetVocabPath);
                }
                else if (targetVocab != null)
                {
                    UnityEngine.Object.DestroyImmediate(targetVocab);
                }

                if (sourceAssetCreated)
                {
                    AssetDatabase.DeleteAsset(sourceVocabPath);
                }
                else if (sourceVocab != null)
                {
                    UnityEngine.Object.DestroyImmediate(sourceVocab);
                }

                throw;
            }
        }

        internal static FP_Language GetFPLanguage(FPTranslationLanguage language)
        {
            switch (language)
            {
                case FPTranslationLanguage.English:
                    return FP_Language.USEnglish;
                case FPTranslationLanguage.Spanish:
                    return FP_Language.Spanish;
                case FPTranslationLanguage.French:
                    return FP_Language.French;
                default:
                    throw new ArgumentOutOfRangeException(nameof(language), language, "Unsupported FP_Vocab language.");
            }
        }

        internal static string BuildVocabAssetFileName(string audioAssetPath)
        {
            return Path.GetFileNameWithoutExtension(audioAssetPath) + ".asset";
        }

        private static ScriptableObject CreateConfiguredVocab(
            Type vocabType,
            string word,
            FPTranslationLanguage language,
            AudioClip audioClip,
            string uniqueId,
            FP_LanguageLevel levelIntroduced,
            CEFRLevel cefrLevel,
            FP_VocabCategory vocabCategory)
        {
            ScriptableObject vocab = ScriptableObject.CreateInstance(vocabType);
            vocab.name = Path.GetFileNameWithoutExtension(uniqueId);
            var serializedVocab = new SerializedObject(vocab);
            FindRequiredProperty(serializedVocab, "UniqueID").stringValue = uniqueId;
            FindRequiredProperty(serializedVocab, "Word").stringValue = word;
            FindRequiredProperty(serializedVocab, "Language").intValue = (int)GetFPLanguage(language);
            FindRequiredProperty(serializedVocab, "LevelIntroduced").intValue = (int)levelIntroduced;
            FindRequiredProperty(serializedVocab, "CEFRLevel").intValue = (int)cefrLevel;
            FindRequiredProperty(serializedVocab, "VocabCategory").intValue = (int)vocabCategory;

            SerializedProperty wordAudio = FindRequiredProperty(serializedVocab, "WordAudio");
            FindRequiredRelativeProperty(wordAudio, "AudioClip").objectReferenceValue = audioClip;
            FindRequiredRelativeProperty(wordAudio, "URLAudioType").intValue = (int)AudioType.MPEG;
            FindRequiredRelativeProperty(wordAudio, "URLReference").stringValue = string.Empty;
            FindRequiredProperty(serializedVocab, "Translations").arraySize = 0;
            SerializedProperty semanticMaps = serializedVocab.FindProperty("SemanticMaps");
            if (semanticMaps != null)
            {
                semanticMaps.arraySize = 0;
            }

            serializedVocab.ApplyModifiedPropertiesWithoutUndo();
            return vocab;
        }

        private static void SetTranslationReference(ScriptableObject vocab, ScriptableObject translation)
        {
            var serializedVocab = new SerializedObject(vocab);
            SerializedProperty translations = FindRequiredProperty(serializedVocab, "Translations");
            translations.arraySize = 1;
            translations.GetArrayElementAtIndex(0).objectReferenceValue = translation;
            serializedVocab.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string BuildVocabAssetPath(string audioAssetPath)
        {
            string assetFolder = Path.GetDirectoryName(audioAssetPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(assetFolder))
            {
                throw new InvalidOperationException("The generated audio asset path has no Unity asset folder.");
            }

            return AssetDatabase.GenerateUniqueAssetPath(
                $"{assetFolder}/{BuildVocabAssetFileName(audioAssetPath)}");
        }

        private static SerializedProperty FindRequiredProperty(SerializedObject serializedObject, string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"FP_Vocab is missing the required '{propertyName}' serialized field.");
            }

            return property;
        }

        private static SerializedProperty FindRequiredRelativeProperty(SerializedProperty parent, string propertyName)
        {
            SerializedProperty property = parent.FindPropertyRelative(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"FP_Vocab WordAudio is missing the required '{propertyName}' serialized field.");
            }

            return property;
        }
    }

    internal static class FPElevenLabsEditorUtility
    {
        internal static string GetLanguageName(FPTranslationLanguage language)
        {
            switch (language)
            {
                case FPTranslationLanguage.English:
                    return "English";
                case FPTranslationLanguage.Spanish:
                    return "Spanish";
                case FPTranslationLanguage.French:
                    return "French";
                default:
                    throw new ArgumentOutOfRangeException(nameof(language), language, "Unsupported translation language.");
            }
        }

        internal static string BuildTranslationInstructions(
            FPTranslationLanguage originalLanguage,
            FPTranslationLanguage translationLanguage)
        {
            return $"Translate the user's text from {GetLanguageName(originalLanguage)} to {GetLanguageName(translationLanguage)}. "
                + "Preserve its meaning, tone, and punctuation. Return only the translated text with no label, quotation marks, commentary, or explanation.";
        }

        internal static string GetEnglishBaseFileName(
            FPTranslationLanguage sourceLanguage,
            FPTranslationLanguage targetLanguage,
            string sourceText,
            string translatedText,
            string additionalEnglishTranslation)
        {
            string englishText = sourceLanguage == FPTranslationLanguage.English
                ? sourceText
                : targetLanguage == FPTranslationLanguage.English
                    ? translatedText
                    : additionalEnglishTranslation;
            if (string.IsNullOrWhiteSpace(englishText))
            {
                throw new InvalidOperationException("An English translation is required for the Base File Name.");
            }

            return englishText.Trim();
        }

        internal static string ExtractOpenAIOutputText(string responseJson)
        {
            if (string.IsNullOrWhiteSpace(responseJson))
            {
                throw new InvalidOperationException("OpenAI returned an empty response.");
            }

            OpenAIResponse response = JsonUtility.FromJson<OpenAIResponse>(responseJson);
            if (response == null || response.output == null)
            {
                throw new InvalidOperationException("OpenAI returned an invalid response.");
            }

            var outputText = new StringBuilder();
            for (int outputIndex = 0; outputIndex < response.output.Length; outputIndex++)
            {
                OpenAIOutputItem outputItem = response.output[outputIndex];
                if (outputItem?.content == null)
                {
                    continue;
                }

                for (int contentIndex = 0; contentIndex < outputItem.content.Length; contentIndex++)
                {
                    OpenAIContentItem contentItem = outputItem.content[contentIndex];
                    if (contentItem == null
                        || !string.Equals(contentItem.type, "output_text", StringComparison.Ordinal)
                        || string.IsNullOrWhiteSpace(contentItem.text))
                    {
                        continue;
                    }

                    if (outputText.Length > 0)
                    {
                        outputText.AppendLine();
                    }

                    outputText.Append(contentItem.text.Trim());
                }
            }

            if (outputText.Length == 0)
            {
                throw new InvalidOperationException("OpenAI returned no translated text.");
            }

            return outputText.ToString();
        }

        internal static string BuildLanguageMp3FileName(
            string baseFileName,
            string variant,
            FPTranslationLanguage language,
            string voiceName,
            bool prependColor = false)
        {
            string safeBaseName = Path.GetFileNameWithoutExtension(SanitizeMp3FileName(baseFileName));
            if (prependColor && !safeBaseName.StartsWith("Color_", StringComparison.OrdinalIgnoreCase))
            {
                safeBaseName = $"Color_{safeBaseName}";
            }

            string safeVoiceName = Path.GetFileNameWithoutExtension(SanitizeMp3FileName(voiceName));
            return SanitizeMp3FileName($"{safeBaseName}_{variant}_{GetLanguageName(language)}_{safeVoiceName}");
        }

        internal static string SanitizeMp3FileName(string fileName)
        {
            string candidate = string.IsNullOrWhiteSpace(fileName)
                ? "ElevenLabsSpeech"
                : fileName.Trim();

            if (candidate.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            {
                candidate = candidate.Substring(0, candidate.Length - 4);
            }

            char[] invalidFileNameCharacters = Path.GetInvalidFileNameChars();
            const string portableInvalidCharacters = "<>:\"/\\|?*";
            var sanitized = new StringBuilder(candidate.Length);
            for (int i = 0; i < candidate.Length; i++)
            {
                char character = candidate[i];
                bool isInvalid = Array.IndexOf(invalidFileNameCharacters, character) >= 0
                    || portableInvalidCharacters.IndexOf(character) >= 0;
                sanitized.Append(isInvalid ? '_' : character);
            }

            string safeName = sanitized.ToString().Trim().TrimEnd(' ', '.');
            return (string.IsNullOrWhiteSpace(safeName) ? "ElevenLabsSpeech" : safeName) + ".mp3";
        }

        internal static bool TryConvertAbsoluteFolderToAssetPath(
            string absoluteFolder,
            string assetsAbsoluteFolder,
            out string assetFolder)
        {
            assetFolder = string.Empty;
            if (string.IsNullOrWhiteSpace(absoluteFolder) || string.IsNullOrWhiteSpace(assetsAbsoluteFolder))
            {
                return false;
            }

            string normalizedFolder = Path.GetFullPath(absoluteFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedAssets = Path.GetFullPath(assetsAbsoluteFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (string.Equals(normalizedFolder, normalizedAssets, comparison))
            {
                assetFolder = "Assets";
                return true;
            }

            string assetsPrefix = normalizedAssets + Path.DirectorySeparatorChar;
            if (!normalizedFolder.StartsWith(assetsPrefix, comparison))
            {
                return false;
            }

            string relativeFolder = normalizedFolder.Substring(assetsPrefix.Length).Replace('\\', '/');
            assetFolder = $"Assets/{relativeFolder}";
            return true;
        }

        internal static string GetAbsoluteFolderPath(string assetFolder, string assetsAbsoluteFolder)
        {
            string normalizedAssetsFolder = Path.GetFullPath(assetsAbsoluteFolder);
            string normalizedAssetFolder = (assetFolder ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            if (string.Equals(normalizedAssetFolder, "Assets", StringComparison.Ordinal))
            {
                return normalizedAssetsFolder;
            }

            if (!normalizedAssetFolder.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return normalizedAssetsFolder;
            }

            string relativeFolder = normalizedAssetFolder.Substring("Assets/".Length);
            string candidateFolder = Path.GetFullPath(Path.Combine(normalizedAssetsFolder, relativeFolder));
            return TryConvertAbsoluteFolderToAssetPath(candidateFolder, normalizedAssetsFolder, out _)
                ? candidateFolder
                : normalizedAssetsFolder;
        }

        [Serializable]
        private sealed class OpenAIResponse
        {
            public OpenAIOutputItem[] output;
        }

        [Serializable]
        private sealed class OpenAIOutputItem
        {
            public OpenAIContentItem[] content;
        }

        [Serializable]
        private sealed class OpenAIContentItem
        {
            public string type;
            public string text;
        }
    }
}
