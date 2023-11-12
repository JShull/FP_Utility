using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.Events;

namespace FuzzPhyte.Utility
{
    /// <summary>
    /// A collection of static classes, enums, structs, and methods that are used throughout the FuzzPhyte Utility package
    /// </summary>
    public static class FP_UtilityData
    {
        private readonly static char[] invalidFilenameChars;
        private readonly static char[] invalidPathChars;
        private readonly static char[] parseTextImagefileChars;
        
        static FP_UtilityData()
        {
            invalidFilenameChars = Path.GetInvalidFileNameChars();
            invalidPathChars = Path.GetInvalidPathChars();
            parseTextImagefileChars = new char[1] { '~' };
            Array.Sort(invalidFilenameChars);
            Array.Sort(invalidPathChars);
        }
        /// <summary>
        /// Return color by status
        /// Aligns with our editor script
        /// Don't use this method if we are referencing editor scripts
        /// </summary>
        /// <param name="status">Sequence state/status</param>
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

        public static readonly Vector4[] s_UnitSquare =
        {
            new Vector4(-0.5f, 0.5f, 0, 1),
            new Vector4(0.5f, 0.5f, 0, 1),
            new Vector4(0.5f, -0.5f, 0, 1),
            new Vector4(-0.5f, -0.5f, 0, 1),
        };
        public static readonly Vector4[] s_UnitSphere = MakeUnitSphere(16);
        public static Vector4[] MakeUnitSphere(int len)
        {
            Debug.Assert(len > 2);
            var v = new Vector4[len * 3];
            for (int i = 0; i < len; i++)
            {
                var f = i / (float)len;
                float c = Mathf.Cos(f * (float)(Math.PI * 2.0));
                float s = Mathf.Sin(f * (float)(Math.PI * 2.0));
                v[0 * len + i] = new Vector4(c, s, 0, 1);
                v[1 * len + i] = new Vector4(0, c, s, 1);
                v[2 * len + i] = new Vector4(s, 0, c, 1);
            }
            return v;
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

    /// <summary>
    /// Core 'status' for all things sequence related
    /// Will be used heavily across sequence logic
    /// </summary>
    [Serializable]
    [SerializeField]
    public enum SequenceStatus
    {
        None = 0,
        Locked = 1,
        Unlocked = 2,
        Active = 3,
        Finished = 4,
    }
    /// <summary>
    /// Different 'types' of overlays we might have
    /// </summary>
    [Serializable]
    [SerializeField]
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
        [Header("Damping based for Dolley and Zoom")]
        public float PitchDamping;
        public float RollDamping;
        public float XDamping;
        public float YDamping;
        public float ZDamping;
        [Header("Auto Dolly Parameters")]
        public float PositionOffset;
        public int SearchRadius;
        public int SearchResolution;
        public int StepsPerSegment;
    }

    [Serializable]
    public enum StrandType
    {
        CS_Strands,
        TechnologyStrands,
        Career
    }
    #region Custom UnityEvents
    /// <summary>
    /// Special UnityEvent with float parameter
    /// https://docs.unity3d.com/ScriptReference/Events.UnityEvent_1.html
    /// </summary>
    [Serializable]
    public class FPFloatEvent : UnityEvent<float>
    {
        public float Value;
    }
    /// <summary>
    /// Special UnityEvent with int parameter
    /// https://docs.unity3d.com/ScriptReference/Events.UnityEvent_1.html
    /// </summary>
    [Serializable]
    public class FPIntEvent : UnityEvent<int>
    {
        public int Value;
    }
    #endregion
}
