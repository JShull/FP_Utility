using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.Events;

///script file to hold misc. utility structs, enums, etc.
namespace FuzzPhyte.Utility
{
    
    public static class FP_UtilityData
    {
        private readonly static char[] invalidFilenameChars;
        private readonly static char[] invalidPathChars;
        private readonly static char[] parseTextImagefileChars;
        
        static FP_UtilityData()
        {
            invalidFilenameChars = System.IO.Path.GetInvalidFileNameChars();
            invalidPathChars = System.IO.Path.GetInvalidPathChars();
            parseTextImagefileChars = new char[1] { '~' };
            Array.Sort(invalidFilenameChars);
            Array.Sort(invalidPathChars);
        }
        /// <summary>
        /// If we need to take a string function 
        /// </summary>
        /// <param name="target">The gameobject/component/item/class that has the function name</param>
        /// <param name="functionName">name of the function</param>
        /// <returns></returns>
        public static UnityAction StringFunctionToUnityAction(object target, string functionName)
        {
            try
            {
                UnityAction action = (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), target, functionName);
                return action;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Missing and/or probably wrong function name: {functionName} didn't work! Error Message: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check for invalid filename chars
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static bool CheckForInvalidFilenameChars(string filename)
        {
            if (filename.IndexOfAny(invalidFilenameChars) != -1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Check for invalid Paths Chars
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool CheckForInvalidPathChars(string path)
        {
            if (path.IndexOfAny(invalidPathChars) != -1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

    }
    public static class FP_SerilizeDeserialize
    {
        // Serialize collection of any type to a byte stream
        public static byte[] Serialize<T>(T obj)
        {
            using (MemoryStream memStream = new MemoryStream())
            {
                BinaryFormatter binSerializer = new BinaryFormatter();
                binSerializer.Serialize(memStream, obj);
                return memStream.ToArray();
            }
        }
        // DSerialize collection of any type to a byte stream
        public static T Deserialize<T>(byte[] serializedObj)
        {
            T obj = default(T);
            if (serializedObj == null)
            {
                return obj;
            }
            if (serializedObj.Length == 0)
            {
                return obj;
            }
            using (MemoryStream memStream = new MemoryStream(serializedObj))
            {
                BinaryFormatter binSerializer = new BinaryFormatter();
                obj = (T)binSerializer.Deserialize(memStream);
            }
            return obj;
        }
    }
    public enum OverlayType 
    { 
        TaskList, 
        Information, 
        Conversation, 
        CardDetails, 
        Vocabulary 
    };
    [Serializable]
    [SerializeField]
    public enum NPCHackState
    {
        Idle,
        Talking,
        Signalling
    }
    [Serializable]
    [SerializeField]
    public enum NPCHackTalkingState
    {
        None,
        Normal,
        Whisper,
        Argue,
        Excited
    }
    [Serializable]
    public struct FP_Location 
    {
        public Vector3 WorldLocation;
        public Vector3 EulerRotation;
        public Vector3 LocalScale;
    }
    [Serializable]
    public enum FP_DialogueType 
    { 
        Linear, 
        PlayerResponse 
    }
    /// <summary>
    /// Struct to hold values for camera adjustments we might need on the fly
    /// </summary>
    [Serializable]
    public struct FP_Camera
    {
        public float CameraFOV;
    }

    [Serializable]
    public enum StrandType
    {
        CS_Strands,
        TechnologyStrands,
        Career
    }
}
