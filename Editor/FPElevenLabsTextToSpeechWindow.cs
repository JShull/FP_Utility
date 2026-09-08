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
        [SerializeField] private bool speechOnly;
        [SerializeField] private int maxGenerationRequests;
        [SerializeField] private int maxGenerationCharacters;
        [SerializeField] private string preparedManifestJson = string.Empty;
        [SerializeField] private bool preparedTranslation;
        private bool approvePreparedManifest;

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
                "Prepare and review translation or speech requests, then authorize generation within explicit limits.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(isRequestRunning))
            {
            DrawAuthenticationSection();
            DrawVoiceSection();
            DrawRequestSettingsSection();
            DrawRequestsSection();
            DrawOutputSection();
            DrawFPVocabSection();
            DrawGenerateSection();
            }

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
                if (GUILayout.Button(isRequestRunning ? "Request in progress..." : "Prepare Translations (Dry Run)", GUILayout.Height(26f)))
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

            speechOnly = EditorGUILayout.Toggle("Speech Only (Original Text)", speechOnly);
            maxGenerationRequests = EditorGUILayout.IntField("Maximum Paid Requests", maxGenerationRequests);
            maxGenerationCharacters = EditorGUILayout.IntField("Maximum Input Characters", maxGenerationCharacters);
            EditorGUILayout.HelpBox("Limits apply to each prepared manifest, including failed attempts. Zero permits cache reuse only. Characters are UTF-16 input units, not a currency estimate. Speech Only sends Original Text directly, including approved French dialogue.", MessageType.Info);

            bool canGenerate = HasElevenLabsApiKey()
                && GetSelectedVoice() != null
                && !string.IsNullOrWhiteSpace(modelId)
                && !string.IsNullOrWhiteSpace(voiceName)
                && (speechOnly || !generateFPVocab || FPElevenLabsVocabAssetUtility.IsAvailable)
                && (speechOnly ? HasAnyRequestText() : HasAnyCurrentTranslation());

            using (new EditorGUI.DisabledScope(isRequestRunning || !canGenerate))
            {
                if (GUILayout.Button(isRequestRunning ? "Request in progress..." : "Prepare Audio (Dry Run)", GUILayout.Height(30f)))
                {
                    GenerateSpeechPairs();
                }
            }

            if (!string.IsNullOrEmpty(preparedManifestJson))
            {
                EditorGUILayout.LabelField("Prepared Manifest", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(preparedManifestJson, GUILayout.MinHeight(180f));
                approvePreparedManifest = EditorGUILayout.ToggleLeft("Authorize this exact manifest and its limits", approvePreparedManifest);
                using (new EditorGUI.DisabledScope(isRequestRunning || !approvePreparedManifest))
                    if (GUILayout.Button("Execute / Resume Prepared Manifest")) ExecutePreparedManifest();
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

        private void TranslateRequests() => PrepareManifest(true);

        private void GenerateSpeechPairs() => PrepareManifest(false);

        private FPElevenLabsGenerationManifest BuildManifest(bool translation)
        {
            var requests = new List<FPElevenLabsGenerationRequest>();
            VoiceInfo voice = GetSelectedVoice();
            foreach (SpeechRequest row in speechRequests)
            {
                if (row == null || string.IsNullOrWhiteSpace(row.originalText))
                    throw new InvalidOperationException("Every row needs Original Text. Remove empty rows before preparing.");
                if (translation)
                {
                    requests.Add(BuildTranslationRequest(row.originalText, targetLanguage));
                    if (sourceLanguage != FPTranslationLanguage.English && targetLanguage != FPTranslationLanguage.English)
                        requests.Add(BuildTranslationRequest(row.originalText, FPTranslationLanguage.English));
                }
                else
                {
                    if (voice == null || string.IsNullOrWhiteSpace(voiceName) || string.IsNullOrWhiteSpace(row.baseFileName))
                        throw new InvalidOperationException("Select a voice and provide Voice Name and every Base File Name.");
                    if (!speechOnly && !HasCurrentTranslation(row))
                        throw new InvalidOperationException("Translate stale rows first, or select Speech Only.");
                    requests.Add(BuildSpeechRequest(row, row.originalText, "Original", sourceLanguage, voice.voice_id));
                    if (!speechOnly)
                        requests.Add(BuildSpeechRequest(row, row.translatedText, "Translation", targetLanguage, voice.voice_id));
                }
            }
            return new FPElevenLabsGenerationService().Prepare(requests.ToArray(), maxGenerationRequests, maxGenerationCharacters);
        }

        private FPElevenLabsGenerationRequest BuildTranslationRequest(string text, FPTranslationLanguage target)
        {
            return new FPElevenLabsGenerationRequest { operation = "translation", text = text,
                modelId = openAIModelId, sourceLanguage = sourceLanguage.ToString(), targetLanguage = target.ToString() };
        }

        private FPElevenLabsGenerationRequest BuildSpeechRequest(SpeechRequest row, string text,
            string variant, FPTranslationLanguage language, string voiceId)
        {
            return new FPElevenLabsGenerationRequest { text = text, voiceId = voiceId, modelId = modelId,
                sourceLanguage = language.ToString(), outputAssetPath = outputAssetFolder.TrimEnd('/') + "/" +
                FPElevenLabsEditorUtility.BuildLanguageMp3FileName(row.baseFileName, variant, language, voiceName, row.isColor) };
        }

        private void PrepareManifest(bool translation)
        {
            approvePreparedManifest = false;
            preparedManifestJson = string.Empty;
            try
            {
                var manifest = BuildManifest(translation);
                preparedTranslation = translation;
                preparedManifestJson = JsonUtility.ToJson(manifest, true);
                SetStatus($"Dry run: {manifest.newRequests} new request(s), {manifest.newCharacters} input characters. Review exact text, voice IDs, outputs, and limits below.", MessageType.Info);
            }
            catch (Exception exception) { SetStatus(exception.Message, MessageType.Error); }
        }

        private async void ExecutePreparedManifest()
        {
            if (isRequestRunning || !approvePreparedManifest) return;
            isRequestRunning = true;
            approvePreparedManifest = false;
            try
            {
                var manifest = JsonUtility.FromJson<FPElevenLabsGenerationManifest>(preparedManifestJson);
                if (BuildManifest(preparedTranslation).hash != manifest.hash)
                    throw new InvalidOperationException("Inputs or limits changed. Prepare and review a new manifest.");
                bool translation = preparedTranslation;
                bool pairs = !speechOnly;
                bool createVocab = !translation && pairs && generateFPVocab;
                Type vocabType = null;
                if (createVocab && !FPElevenLabsVocabAssetUtility.TryGetVocabType(out vocabType))
                    throw new InvalidOperationException("FP_Utility_EDU is required for FP_Vocab creation.");
                var rows = speechRequests.ToArray();
                var source = sourceLanguage;
                var target = targetLanguage;
                var level = vocabLevelIntroduced;
                var cefr = vocabCEFRLevel;
                var category = vocabCategory;
                SetStatus("Executing approved manifest. Responses are saved before import; resume reuses saved results.", MessageType.Info);
                manifest = await new FPElevenLabsGenerationService().ExecuteAsync(manifest, manifest.hash);
                preparedManifestJson = JsonUtility.ToJson(manifest, true);
                int index = 0;
                foreach (var row in rows)
                {
                    if (translation)
                    {
                        string original = manifest.requests[index].text;
                        string translated = manifest.items[index++].resultText;
                        string english = source != FPTranslationLanguage.English && target != FPTranslationLanguage.English
                            ? manifest.items[index++].resultText : string.Empty;
                        row.translatedText = translated;
                        row.translatedSourceText = original.Trim();
                        row.translatedSourceLanguage = source;
                        row.translatedTargetLanguage = target;
                        row.baseFileName = FPElevenLabsEditorUtility.GetEnglishBaseFileName(source, target, original, translated, english);
                    }
                    else
                    {
                        var original = manifest.requests[index++];
                        if (pairs)
                        {
                            var translated = manifest.requests[index++];
                            if (createVocab)
                                FPElevenLabsVocabAssetUtility.CreatePair(vocabType, original.text, translated.text,
                                    source, target, AssetDatabase.LoadAssetAtPath<AudioClip>(original.outputAssetPath),
                                    AssetDatabase.LoadAssetAtPath<AudioClip>(translated.outputAssetPath), original.outputAssetPath,
                                    translated.outputAssetPath, level, cefr, category);
                        }
                    }
                    SetRequestStatus(row, translation ? "Translation saved. Review before preparing audio." : "Audio saved and imported.", MessageType.Info);
                }
                SetStatus("Prepared manifest completed. Saved responses will be reused on resume.", MessageType.Info);
            }
            catch (Exception exception) { SetStatus(exception.Message, MessageType.Error); }
            finally { isRequestRunning = false; if (this != null) Repaint(); }
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
            var existingSource = AssetDatabase.LoadAssetAtPath<ScriptableObject>(sourceVocabPath);
            var existingTarget = AssetDatabase.LoadAssetAtPath<ScriptableObject>(targetVocabPath);
            if (File.Exists(sourceVocabPath) || File.Exists(targetVocabPath))
            {
                if (MatchesExistingVocab(existingSource, vocabType, sourceWord, sourceAudio, existingTarget) &&
                    MatchesExistingVocab(existingTarget, vocabType, targetWord, targetAudio, existingSource))
                    return new FPElevenLabsVocabAssetPair(existingSource, existingTarget);
                throw new InvalidOperationException("Existing FP_Vocab assets conflict or form an incomplete pair. Audio is saved; review the assets before resuming.");
            }
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

            return $"{assetFolder}/{BuildVocabAssetFileName(audioAssetPath)}";
        }

        private static bool MatchesExistingVocab(ScriptableObject vocab, Type type, string word,
            AudioClip audio, ScriptableObject translation)
        {
            if (vocab == null || translation == null || vocab.GetType() != type) return false;
            var serialized = new SerializedObject(vocab);
            var translations = FindRequiredProperty(serialized, "Translations");
            return FindRequiredProperty(serialized, "Word").stringValue == word &&
                FindRequiredRelativeProperty(FindRequiredProperty(serialized, "WordAudio"), "AudioClip").objectReferenceValue == audio &&
                translations.arraySize == 1 && translations.GetArrayElementAtIndex(0).objectReferenceValue == translation;
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
