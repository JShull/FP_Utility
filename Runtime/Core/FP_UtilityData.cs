namespace FuzzPhyte.Utility
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;
    using UnityEngine;
    using UnityEngine.Events;
    using Unity.Mathematics;
    using UnityEditor;

    /// <summary>
    /// A collection of static classes, enums, structs, and methods that are used throughout the FuzzPhyte Utility package
    /// </summary>
    public static class FP_UtilityData
    {
        //ScriptableObject Setup for other Utility Classes
        public const string MENU_COMPANY ="FuzzPhyte/";
        // Variable name for editor variables
        public const string LAST_SCENEPATH_VAR = "FP_LastActiveScenePath";
        public const string FP_FOLDOUTSTATES_KEY = "FP_FoldoutStates_Keys";
        public const string FP_FOLDOUTSTATES_VALUE = "FP_FoldoutStates_Values";
        public const string FP_PREVIOUSFOLDOUT_KEY = "FP_PreviousFoldout";
        public const string FP_PREVIOUSFOLDOUT_VALUE = "FP_PreviousFoldoutValue";
        // menu order for misc. menus
        public const int ORDER_MENU = 0;
        public const int ORDER_SUBMENU_LVL1 = 150;
        public const int ORDER_SUBMENU_LVL2 = 130;
        public const int ORDER_SUBMENU_LVL3 = 110;
        public const int ORDER_SUBMENU_LVL4 = 90;
        public const int ORDER_SUBMENU_LVL5 = 70;
        public const int ORDER_SUBMENU_LVL6 = 50;
        public const int ORDER_SUBMENU_LVL7 = 30;
        public const int ORDER_SUBMENU_LVL8 = 10;
        
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
        public static void SetPropertyValue(SerializedProperty property, object value)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    property.floatValue = (float)value;
                    break;
                case SerializedPropertyType.Integer:
                    property.intValue = (int)value;
                    break;
                case SerializedPropertyType.String:
                    property.stringValue = (string)value;
                    break;
                case SerializedPropertyType.Boolean:
                    property.boolValue = (bool)value;
                    break;
                case SerializedPropertyType.Color:
                    property.colorValue = (Color)value;
                    break;
                case SerializedPropertyType.Vector3:
                    property.vector3Value = (Vector3)value;
                    break;
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = (UnityEngine.Object)value;
                    break;
            }
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
        /// Populates the dropdown with string values from the provided enum type.
        /// </summary>
        /// <typeparam name="T">The enum type to populate the dropdown from.</typeparam>
        /// <param name="Dropdown">The TMP dropdown to populate.</param>
        /// <param name="DropdownEvent">The UnityAction to call when a dropdown value is selected.</param>
        public static void EnumToDropDown<T>(TMPro.TMP_Dropdown Dropdown,UnityAction<int> DropdownEvent) where T : Enum
        {
            Dropdown.ClearOptions();
            List<string> enumNames = new List<string>(Enum.GetNames(typeof(T)));
            Dropdown.AddOptions(enumNames);
            Dropdown.onValueChanged.AddListener(DropdownEvent);
        }
        #region GUIStyle Returns
        /// <summary>
        /// Return a GUIStyle
        /// </summary>
        /// <param name="colorFont">Color of Font</param>
        /// <param name="styleFont">Style of Font</param>
        /// <param name="anchorFont">Anchor of Font</param>
        /// <returns></returns>
        public static GUIStyle ReturnStyle(Color colorFont, FontStyle styleFont, TextAnchor anchorFont)
        {
            GUIStyleState normalState = new GUIStyleState()
            {
                textColor = colorFont,
            };
            return new GUIStyle()
            {
                fontStyle = styleFont,
                normal = normalState,
                alignment = anchorFont
            };
        }
        public static GUIStyle ReturnStyleWrap(Color colorFont, FontStyle styleFont, TextAnchor anchorFont, bool useWordWrap)
        {
            var newStyle = ReturnStyle(colorFont, styleFont, anchorFont);
            newStyle.wordWrap = useWordWrap;
            return newStyle;
        }
        public static GUIStyle ReturnStyleRichText(Color colorFont, FontStyle styleFont, TextAnchor anchorFont)
        {
            var newStyle = ReturnStyle(colorFont, styleFont, anchorFont);
            newStyle.richText = true;
            return newStyle;
        }
        #endregion
        /// <summary>
        /// If we need to take a string function and return a Unity Action on said target
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

        #region UnitSquare and UnitSphere
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
        #endregion
    
        public static FP_BoundingBoxInfo? CreateBoundingBox(Vector3 worldPosition, Quaternion worldRotation, Renderer objectRenderer)
        {
            
            if (objectRenderer == null)
            {
                Debug.LogError("Renderer component is null.");
                return null;
            }

            // Calculate the bounds of the object
            Bounds bounds = objectRenderer.bounds;

            // Create and return the bounding box information
            return new FP_BoundingBoxInfo
            {
                Center = worldPosition,
                Size = bounds.size,
                Rotation = worldRotation
            };
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
    ///<summary>
    ///Hold some static string names for legacy input scenarios
    ///</summary>
    public static class FP_InputData
    {
        #region Old Input Settings
        public const string OVR_ButtonTwo = "Oculus_CrossPlatform_Button2";
        public const string OVR_ButtonFour = "Oculus_CrossPlatform_Button4";
        public const string OVR_PrimaryTrigger ="Oculus_CrossPlatform_PrimaryIndexTrigger";
        public const string OVR_SecondaryTrigger = "Oculus_CrossPlatform_SecondaryIndexTrigger";
        public const string OVR_PrimaryGrip = "Oculus_CrossPlatform_PrimaryHandTrigger";
        public const string OVR_SecondaryGrip = "Oculus_CrossPlatform_SecondaryHandTrigger";
        #endregion
    }

    #region Generic Enums for Players and NPCs
    [Serializable]
    [SerializeField]
    /// <summary>
    /// Enum representing generic roles in the context of the player and various projects.
    /// </summary>
    public enum FP_Role
    {
        NA,       // Default value, no specific role
        Player,     // The main player character
        NPC,        // Non-player character
        Boss,       // Boss character, typically an antagonist
        Helper,     // Helper character, assists the player
        Enemy,      // Generic enemy character
        Merchant,   // Character that sells items
        QuestGiver, // Character that provides quests or tasks
        Companion,  // Companion character, accompanies the player
        Trainer,    // Character that provides training or skills
        Guard,      // Character that guards areas or items
        Healer,     // Character that provides healing
        Guide,      // Character that provides guidance or navigation
        Vendor,     // Character that buys/sells items
        Civilian,   // General non-hostile character
        Leader      // Character that leads a group or faction
    }
    /// <summary>
    /// Enum representing generic character motion states.
    /// </summary>
    public enum MotionState
    {
        NA,         // Default value, no specific state
        Idle,       // Character is not moving
        Walking,    // Character is walking
        Running,    // Character is running
        Jumping,    // Character is jumping
        Falling,    // Character is falling
        Climbing,   // Character is climbing
        Swimming,   // Character is swimming
        Crawling,   // Character is crawling
        Sitting,    // Character is sitting
        LyingDown   // Character is lying down
    }
    /// <summary>
    /// Enum representing generic dialogue/conversation states.
    /// </summary>
    /// <summary>
    /// Enum representing generic dialogue/conversation states focusing on vocal strength.
    /// </summary>
    public enum DialogueState
    {
        NA,         // Default value, no specific state
        Normal,     // Regular conversation
        Whisper,    // Whispering
        Shout,      // Shouting
        Soft,       // Speaking softly
        Loud        // Speaking loudly
    }
    /// <summary>
    /// Enum representing generic emotional states of the character.
    /// </summary>
    public enum EmotionalState
    {
        Neutral,    // No strong emotions
        Happy,      // Feeling happy
        Sad,        // Feeling sad
        Angry,      // Feeling angry
        Excited,    // Feeling excited
        Nervous,    // Feeling nervous
        Confused,   // Feeling confused
        Fearful,    // Feeling fearful
        Surprised,  // Feeling surprised
        Disgusted   // Feeling disgusted
    }
    #endregion
    /// <summary>
    /// Core 'status' for all things sequence related
    /// Will be used heavily across sequence logic
    /// </summary>
    [Serializable]
    [SerializeField]
    public enum SequenceStatus
    {
        NA = 0,
        None = 1,
        Locked = 2,
        Unlocked = 3,
        Active = 4,
        Finished = 5,
    }
    /// <summary>
    /// Core 'transition' for all things sequence related
    /// </summary>
    [Serializable]
    [SerializeField]
    public enum SequenceTransition
    {
        NA = 0,
        NoneToLock = 1,
        NoneToUnlock = 2,
        UnlockToLock = 3,
        LockToUnlock = 4,
        UnlockToActive = 5,
        ActiveToFinished = 6,
        FinishedToLock = 7
    }
    /// <summary>
    /// Different 'types' of overlays we might have
    /// </summary>
    [Serializable]
    [SerializeField]
    public enum OverlayType 
    {   
        NA,
        TaskList, 
        Information, 
        Conversation, 
        MiscDetails, 
        Vocabulary 
    };
    [Serializable]
    [SerializeField]
    public enum NPCHackState
    {
        NA,
        Idle,
        Talking,
        Signalling
    }
    [Serializable]
    [SerializeField]
    public enum NPCHackTalkingState
    {
        NA,
        Normal,
        Whisper,
        Argue,
        Excited
    }
    /// <summary>
    /// this is used mainly for our characters so we can correctly match different services later as needed
    /// some of these items came from this list "https://www.alba.network/GSDinclusiveforms"
    /// </summary>
    [Serializable]
    [SerializeField]
    public enum FP_Gender
    {
        NA,
        Man,
        Woman,
        Agender,
        Androgynous,
        Bigender,
        Genderfluid,
        Genderqueer,
        GenderNonConforming,
        NonBinary,
        Pangender,
        TwoSpirit,
        Other,
        Robot // AI Buddies
    }
    [Serializable]
    [SerializeField]
    public enum FP_Ethnicity
    {
        Unknown,         // Default value, unknown ethnicity
        Asian,           // Asian
        Black,           // Black or African American
        Hispanic,        // Hispanic or Latino
        Indigenous,      // Indigenous or Native
        MiddleEastern,   // Middle Eastern or North African
        Mixed,           // Mixed or Multi-racial
        PacificIslander, // Native Hawaiian or Other Pacific Islander
        White,           // White or Caucasian
    }
    [Serializable]
    [SerializeField]
    public enum FontSettingLabel
    {
        HeaderOne,
        HeaderTwo,
        HeaderThree,
        HeaderFour,
        HeaderFive,
        HeaderSix,
        Paragraph,
        Footer
    }
    // placeholder for now, we might need to expand this later
    [SerializeField]
    public enum FP_Language
    {
        NA,
        USEnglish,
        Spanish,
        French,
    }
    [SerializeField]
    public enum FP_LanguageLevel
    {
        LevelOne,
        LevelTwo,
        LevelThree,
        LevelFour
    }
    [SerializeField]
    public enum MWApiKeyType
    {
        NA,
        MWCollegeWord,
        MWLearnersWord,
        MWSchoolWord,
    }
    //keep tabs on what languages we could be using for a given object
    [Serializable]
    public struct FP_Multilingual
    {
        public FP_Language Primary;
        public FP_Language Secondary;
        public FP_Language Tertiary;
    }
    
    // Hold data associated with a specific audio clip
    // might need to load this from the web for various reasons
    [Serializable]
    public struct FP_Audio
    {
        public AudioClip AudioClip;
        public AudioType URLAudioType;
        public string URLReference;
    }
    [Serializable]
    public struct ConvoTranslation
    {
        public FP_Language Language;
        public string Header;
        [TextArea(2, 4)]
        public string Text;
        public FP_Audio AudioText;
    }
    
    [Serializable]
    public struct FP_Location 
    {
        public Vector3 WorldLocation;
        public Vector3 EulerRotation;
        public Vector3 LocalScale;
    }
    [Serializable]
    public struct FP_BoundingBoxInfo
    {
        public float3 Center;
        public float3 Size;
        public Quaternion Rotation;
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
        [Header("Camera Renderer Settings")]
        public int RendererIndex;
    }

    [Serializable]
    public enum StrandType
    {
        CS_Strands,
        TechnologyStrands,
        Career
    }
    /// <summary>
    /// Hold my custom list of items that are serializable so I can save editor prefs and other items
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class FPSerializableList<T>
    {
        public List<T> list;
        public FPSerializableList(List<T> list)
        {
            this.list = list;
        }
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
