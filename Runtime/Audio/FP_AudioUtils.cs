namespace FuzzPhyte.Utility.Audio
{
    using System;
    using System.Collections;
    using System.IO;
    using UnityEngine;
    using UnityEngine.Networking;

    public static class FP_AudioUtils
    {
        const int HEADER_SIZE = 44;
        struct ClipData
        {

            public int samples;
            public int channels;
            public float[] samplesData;
        }

        public static IEnumerator ConvertWavToAudioClip(AudioType URLAudioType, string text, AudioClip clip)
        {
#if UNITY_EDITOR
            //var filepath = AssetDatabase.GetAssetPath(wavFile);
            /*
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(filepath, URLAudioType))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.ConnectionError)
                {
                    Debug.Log(www.error);
                }
                else
                {
                    Debug.LogWarning($"Creating clip for {text}");
                    clip = DownloadHandlerAudioClip.GetContent(www);
                    yield return clip;
                }
            }
            */
            yield return null;
#else
return null;
#endif
        }
        public static byte[] ConvertAudioClipToWAV(AudioClip clip)
        {
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            var wav = new byte[samples.Length * 2 + 44];
            Buffer.BlockCopy(BitConverter.GetBytes(0x46464952), 0, wav, 0, 4); // "RIFF" in ASCII
            Buffer.BlockCopy(BitConverter.GetBytes(36 + samples.Length * 2), 0, wav, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(0x45564157), 0, wav, 8, 4); // "WAVE" in ASCII
            Buffer.BlockCopy(BitConverter.GetBytes(0x20746D66), 0, wav, 12, 4); // "fmt " in ASCII
            Buffer.BlockCopy(BitConverter.GetBytes(16), 0, wav, 16, 4); // Sub chunk size
            Buffer.BlockCopy(BitConverter.GetBytes((short)1), 0, wav, 20, 2); // Audio format (1 = PCM)
            Buffer.BlockCopy(BitConverter.GetBytes((short)clip.channels), 0, wav, 22, 2); // Number of channels
            Buffer.BlockCopy(BitConverter.GetBytes(clip.frequency), 0, wav, 24, 4); // Sample rate
            Buffer.BlockCopy(BitConverter.GetBytes(clip.frequency * clip.channels * 2), 0, wav, 28, 4); // Byte rate
            Buffer.BlockCopy(BitConverter.GetBytes((short)(clip.channels * 2)), 0, wav, 32, 2); // Block align
            Buffer.BlockCopy(BitConverter.GetBytes((short)16), 0, wav, 34, 2); // Bits per sample
            Buffer.BlockCopy(BitConverter.GetBytes(0x61746164), 0, wav, 36, 4); // "data" in ASCII
            Buffer.BlockCopy(BitConverter.GetBytes(samples.Length * 2), 0, wav, 40, 4); // Sub chunk 2 size

            // Convert and write the sample data
            for (int i = 0; i < samples.Length; i++)
            {
                var data = BitConverter.GetBytes((short)(samples[i] * 32767)); // Convert to 16-bit PCM
                Buffer.BlockCopy(data, 0, wav, 44 + i * 2, 2);
            }

            return wav;
        }
        static void ConvertAndWrite(MemoryStream memStream, ClipData clipData)
        {
            float[] samples = new float[clipData.samples * clipData.channels];

            samples = clipData.samplesData;

            Int16[] intData = new Int16[samples.Length];

            Byte[] bytesData = new Byte[samples.Length * 2];

            const float rescaleFactor = 32767; //to convert float to Int16

            for (int i = 0; i < samples.Length; i++)
            {
                intData[i] = (short)(samples[i] * rescaleFactor);
                //Debug.Log (samples [i]);
            }
            Buffer.BlockCopy(intData, 0, bytesData, 0, bytesData.Length);
            memStream.Write(bytesData, 0, bytesData.Length);
        }

        public static void SaveAudioClipToFile(byte[] audioData, string fileName)
        {
            string path = Path.Combine(Application.streamingAssetsPath, fileName);

            File.WriteAllBytes(path, audioData);
            Debug.Log("Saved audio file to: " + path);
        }
        public static IEnumerator GetAudioClip(string URL, AudioType audioType, string text, bool playImmediate, bool saveFile)
        {
            var httpLink = URL;
            if (audioType != AudioType.UNKNOWN)
            {
                using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(httpLink, audioType))
                {
                    yield return www.SendWebRequest();

                    if (www.result == UnityWebRequest.Result.ConnectionError)
                    {
                        Debug.Log(www.error);
                    }
                    else
                    {
                        Debug.LogWarning($"Creating clip for {text}");
                        AudioClip myClip = DownloadHandlerAudioClip.GetContent(www);
                        if (saveFile)
                        {
                            var path = Path.Combine(Application.streamingAssetsPath, @"Vocab/" + text + ".wav");
                            FP_SavWav.Save(path, myClip);
                        }

                    }
                }
            }
            else
            {

            }
        }
    }
    
}
