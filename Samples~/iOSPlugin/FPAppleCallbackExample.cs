namespace FuzzPhyte.Utility
{
    using FuzzPhyte.Utility.Audio;
    using UnityEngine;
    using System.Collections;
    using System;
    using TMPro;

    public class FPAppleCallbackExample : MonoBehaviour
    {
        public delegate void CallbackEventHandler(string fileType);
        public event CallbackEventHandler OnExportWavComplete;
        public event CallbackEventHandler OnFilePickerClosedCallback;
        public event CallbackEventHandler OnMarkdownFileLoadedCallback;
        public static FPAppleCallbackExample Instance;
        public TMP_Text DebuggerText;
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            gameObject.name = "UnityIOSCallback"; // must match native code target
            Debug.LogWarning($"FPAppleCallbackExample instance created and we've changed our name to match: {gameObject.name}");
        }
        #region Unity Function Type/Name Specific to iOS Plugin
        /// <summary>
        /// Function has to be this name to work with the iOS plugin.
        /// </summary>
        /// <param name="fileType"></param>
        public void OnFilePickerClosed(string fileType)
        {
            Debug.Log($"iOS file picker closed for: {fileType}");
            OnFilePickerClosedCallback?.Invoke(fileType);
            /*
            if (fileType == "json")
            {
                StartCoroutine(DelayExportWav());
            }
            */
            DebuggerText.text += $"iOS file picker closed for: {fileType}\n";
        }
        /// <summary>
        /// Function has to be this name to work with the iOS plugin.
        /// This is called when a markdown file is loaded from the iOS file picker.
        /// </summary>
        /// <param name="mdText"></param>
        public void OnMarkdownFileLoaded(string mdText)
        {
            Debug.Log($"Markdown file loaded: {mdText}");
            // Handle the loaded markdown text as needed
            DebuggerText.text += $"Markdown file loaded: {mdText}\n";
            OnMarkdownFileLoadedCallback?.Invoke("md");
            // You can also call a method to process the markdown text if needed
        }
        #endregion
        #region Calling Unity iOS FPApplePlugin Example

        #region Audio Export Functions Needed
        protected string audioDeviceName;
        protected AudioClip recordedClip;
        [SerializeField] protected AudioSource audioSource;
        protected bool audioFailed = false;
        public void UIStartRecordingAudio()
        {
            StartRecordingAudio(300); // Default duration of 5 minutes
            Debug.Log("Started recording audio.");
        }
        /// <summary>
        /// Internal function to start recording audio.
        /// </summary>
        /// <param name="durationSeconds"></param>
        protected void StartRecordingAudio(int durationSeconds = 300)
        {
            int audioIndexValue = 0; // This should be set based on your UI selection or logic
            if (Microphone.devices.Length == 0)
            {
                DebuggerText.text = "Critical Error, No microphone found";
                audioFailed = true;
                return;
            }

            audioDeviceName = Microphone.devices[audioIndexValue];
            recordedClip = Microphone.Start(audioDeviceName, loop: false, durationSeconds, 48000);
            if (recordedClip == null)
            {
                TMP_Text debuggerText = DebuggerText;
                debuggerText.text = debuggerText.text + "Something's wrong with the mic " + audioDeviceName;
                audioFailed = true;
            }
            else
            {
                audioSource.clip = recordedClip;
            }
        }
        /// <summary>
        /// We generally request the JSON file to be saved first, once we get that callback we can then export the WAV file.
        /// </summary>
        public void CallbackFromiOSToSaveWav()
        {
            if (!audioFailed)
            {
                // if you were keeping track of the time of the recording, you could pass that in here instead of 300 to cut/clip the audio
                byte[] array = StopRecordingAndSaveAudio(300);
                if (array != null)
                {
                    StartCoroutine(DelayedExportWav(array, "RecordingName.wav"));
                    TMP_Text debuggerText = DebuggerText;
                    debuggerText.text = debuggerText.text + "Audio data saved for " + "RecordingName.wav" + "\n";
                }
                else
                {
                    DebuggerText.text += "Error, Null byteData";
                }
            }

            audioFailed = false;
        }
        /// <summary>
        /// Internal function to stop the recording and save the audio data.
        /// </summary>
        /// <param name="durationSeconds"></param>
        /// <returns></returns>
        protected byte[] StopRecordingAndSaveAudio(float durationSeconds)
        {
            Microphone.End(audioDeviceName);
            int num = Mathf.FloorToInt((float)(recordedClip.frequency * recordedClip.channels) * durationSeconds);
            float[] array = new float[num];
            float[] array2 = new float[recordedClip.samples * recordedClip.channels];
            recordedClip.GetData(array2, 0);
            Array.Copy(array2, array, num);
            var trimmedClipCache = AudioClip.Create("TrimmedRecording", num / recordedClip.channels, recordedClip.channels, recordedClip.frequency, stream: false);
            trimmedClipCache.SetData(array, 0);
            return FP_AudioUtils.ConvertAudioClipToWAV(trimmedClipCache);
        }
        protected IEnumerator DelayedExportWav(byte[] wav, string fileName)
        {
            yield return new WaitForSeconds(1f);
            FPApplePluginExample.ExportWav(wav, fileName);
            yield return new WaitForEndOfFrame();
            OnExportWavComplete?.Invoke("wav");
        }
        #endregion
        #endregion



    }
}


