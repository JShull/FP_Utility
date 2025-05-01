namespace FuzzPhyte.Utility
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;
    using UnityEngine;
    using UnityEngine.Events;
    using Unity.Mathematics;
    using TMPro;
    using UnityEngine.EventSystems;
  
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
        public const string FP_HIDDENOBJECTS_KEY = "FP_HiddenObjects_Keys";
        public const string FP_HEADERSTYLE_VALUE = "FP_HHeaderDataStyle";
        public const string FP_HHeader_ENABLED_KEY = "FP_HHeader_Enabled";
        public const string FP_GIZMOS_DEFAULT = "FP";
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
        public static string ReturnIconAddressByStatus(SequenceStatus status)
        {
            switch (status)
            {
                case SequenceStatus.None:
                    return "/"+FP_GIZMOS_DEFAULT+"/NA.png";
                case SequenceStatus.Locked:
                    return "/" + FP_GIZMOS_DEFAULT +"/locked.png";
                case SequenceStatus.Unlocked:
                    return "/" + FP_GIZMOS_DEFAULT +"/unlocked.png";
                case SequenceStatus.Active:
                    return "/" + FP_GIZMOS_DEFAULT + "/active.png";
                case SequenceStatus.Finished:
                    return "/" + FP_GIZMOS_DEFAULT +"/finished.png";
                default:
                    return "/" + FP_GIZMOS_DEFAULT +"/error.png";
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
        
        #region Font Setting Stuff
        public static void ApplyFontSetting(TMP_Text textComponent, FontSetting fontSetting)
        {
            if (fontSetting != null && textComponent != null)
            {
                textComponent.font = fontSetting.Font;
                textComponent.color = fontSetting.FontColor;
                textComponent.alignment = fontSetting.FontAlignment;
                textComponent.fontSize = fontSetting.UseAutoSizing ? textComponent.fontSize : fontSetting.MaxSize;
                textComponent.fontStyle = fontSetting.FontStyle;
            }
        }
        #endregion
        #region Design & GUIStyle & Gizmo Related
        // <summary>
        /// Quick check on Material already set to transparent
        /// </summary>
        /// <param name="mat">material in question?</param>
        /// <returns></returns>
        public static bool IsMaterialTransparent(Material mat)
        {
            var keyWord = mat.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT");
            var hasProp = mat.HasProperty("_Surface") && mat.GetFloat("_Surface") == 1;

            if (keyWord)
            {
                return true;
            }
            if (hasProp)
            {
                return true;
            }
            return false;
        }
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
       

        #region Gizmo Related

        #endregion
        #endregion
        #region Units and Conversions
        /// <summary>
        /// Will return a point on a plane relative the mouse position and the camera
        /// E.g. plane: Plane customPlane = new Plane(Vector3.forward, new Vector3(0, 0, 10)); 
        /// </summary>
        /// <param name="camera"></param>
        /// <param name="mousePosition"></param>
        /// <param name="plane"></param>
        /// <returns></returns>
        public static (bool,Vector3) GetMouseWorldPositionOnPlane(Camera camera, Vector2 mousePosition, Plane plane)
        {
            //Plane customPlane = new Plane(Vector3.forward, new Vector3(0, 0, 10));
            Ray mouseRay;
            if (camera.orthographic)
            {
                Vector3 mouseWorld = camera.ScreenToWorldPoint(new Vector3(mousePosition.x,mousePosition.y, camera.nearClipPlane));
                mouseRay = new Ray(mouseWorld, camera.transform.forward);
            }
            else
            {
                mouseRay = camera.ScreenPointToRay(mousePosition);
            }
                
            if (plane.Raycast(mouseRay, out float distance))
            {
                return (true,mouseRay.GetPoint(distance));
            }
            return (false,Vector3.zero); // or some fallback
        }
        public static uint ReturnUINTByDate(System.DateTime theTime)
        {
            long ticks = theTime.Ticks;
            // Fold 64-bit ticks into 32-bit seed
            uint seed = (uint)(ticks ^ (ticks >> 32));
            return seed;
        }
        public static (bool,float) ReturnUnitByPixels(float refPixelPerUnit,float pixels,UnitOfMeasure units, float customValue=1)
        {
            var pixelPerUnit = pixels / refPixelPerUnit;
            switch(units)
            {
                case UnitOfMeasure.Millimeter:
                    return (true,pixelPerUnit * 1000f);
                case UnitOfMeasure.Centimeter:
                    return (true,pixelPerUnit * 100f);
                case UnitOfMeasure.Meter:
                    return (true,pixelPerUnit);
                case UnitOfMeasure.Kilometer:
                    return (true,pixelPerUnit / 1000f);
                case UnitOfMeasure.Inch:
                    return (true,pixelPerUnit * 39.37f);
                case UnitOfMeasure.Feet:
                    return (true,pixelPerUnit * 3.28084f);
                case UnitOfMeasure.Miles:
                    return (true,pixelPerUnit / 1609.34f);
                case UnitOfMeasure.NauticalMiles:
                    return (true,pixelPerUnit / 1852f);
                case UnitOfMeasure.Custom:
                    return (true, pixelPerUnit * customValue);

            }
            return (false,1);
        }
        public static Vector3 ReturnVector3InMeters(Vector3 measure, UnitOfMeasure units, float customValue=1f)
        {
            switch (units)
            {
                case UnitOfMeasure.Millimeter:
                    return measure / 1000f;
                case UnitOfMeasure.Centimeter:
                    return measure / 100f;
                case UnitOfMeasure.Meter:
                    return measure;
                case UnitOfMeasure.Kilometer:
                    return measure * 1000f;
                case UnitOfMeasure.Inch:
                    return measure / 39.37f;
                case UnitOfMeasure.Feet:
                    return measure / 3.28084f;
                case UnitOfMeasure.Miles:
                    return measure * 1609.34f;
                case UnitOfMeasure.NauticalMiles:
                    return measure * 1852f;
                case UnitOfMeasure.Custom:
                    return measure * customValue;
            }
            return measure;
        }
        public static (bool,float) ReturnValueInMeters (float measure, UnitOfMeasure units, float customValue=1f)
        {
            switch (units)
            {
                case UnitOfMeasure.Millimeter:
                    return (true, measure/1000f);
                case UnitOfMeasure.Centimeter:
                    return (true, measure/100f);
                case UnitOfMeasure.Meter:
                    return (true, measure);
                case UnitOfMeasure.Kilometer:
                    return (true, measure*1000f);
                case UnitOfMeasure.Inch:
                    return (true, measure/39.37f);
                case UnitOfMeasure.Feet:
                    return (true, measure / 3.28084f);
                case UnitOfMeasure.Miles:
                    return (true, measure*1609.34f);
                case UnitOfMeasure.NauticalMiles:
                    return (true, measure*1852f);
                    case UnitOfMeasure.Custom:
                    return (true, measure * customValue);
            }
            return (false, measure);
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
        /// <summary>
        /// Debug DrawLine
        /// </summary>
        /// <param name="position"></param>
        /// <param name="normal"></param>
        /// <param name="planeColor"></param>
        public static void DrawLinePlane(Vector3 position, Vector3 normal, Color planeColor, float planeScale=2f,float drawTime=5)
        {
            Vector3 v3;

            if (normal.normalized != Vector3.forward)
                v3 = Vector3.Cross(normal, Vector3.forward).normalized * planeScale;
            else
                v3 = Vector3.Cross(normal, Vector3.up).normalized * planeScale ;

            var corner0 = position + v3;
            var corner2 = position - v3;
            var q = Quaternion.AngleAxis(90.0f, normal);
            v3 = q * v3;
            var corner1 = position + v3;
            var corner3 = position - v3;

            Debug.DrawLine(corner0, corner2, planeColor, drawTime);
            Debug.DrawLine(corner1, corner3, planeColor, drawTime);
            Debug.DrawLine(corner0, corner1, planeColor, drawTime);
            Debug.DrawLine(corner1, corner2, planeColor, drawTime);
            Debug.DrawLine(corner2, corner3, planeColor, drawTime);
            Debug.DrawLine(corner3, corner0, planeColor, drawTime);
            Debug.DrawRay(position, normal, Color.blue, drawTime);
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

        /// <summary>
        /// Return the top level parent of a transform
        /// </summary>
        /// <param name="transform"></param>
        /// <returns></returns>
        public static Transform GetTopLevelParent(Transform transform)
        {
            while (transform.parent != null)
            {
                transform = transform.parent;
            }
            return transform;
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
    /// <summary>
    /// Initial interface for all simple tools
    /// </summary>
    public interface IFPTool
    {
        public virtual void Initialize(FP_Data data)
        {}
        public virtual bool ActivateTool()
        {
            Debug.LogError($"Implement Activate!");
            return false;
        }
        public virtual bool StartTool()
        {
            Debug.LogError($"Implement Start!");
            return false;
        }
        public virtual bool UseTool()
        {
            Debug.LogError($"Implement Use!");
            return false;
        }
        public virtual bool EndTool()
        {
            Debug.LogError($"Implement End!");
            return false;
        }
        public virtual bool DeactivateTool()
        {
            Debug.LogError($"Implement Deactivated");
            return false;
        }
        public virtual bool ForceDeactivateTool()
        {
            Debug.LogError($"Implement Forced Deactivation!");
            return false;
        }
        public virtual bool LockTool()
        {
            Debug.LogError($"Implement Lock!");
            return false;
        }
        public virtual bool UnlockTool()
        {
            Debug.LogError($"Implement Unlock!");
            return false;
        }
        public virtual FPToolState ReturnState()
        {
            Debug.LogError($"Implement ReturnState!");
            return FPToolState.NA;
        }
    }
    public interface IFPUIEventListener<T> where T : class
    {
        void OnUIEvent(FP_UIEventData<T> eventData);
        void PointerDown(PointerEventData eventData);
        void PointerUp(PointerEventData eventData);
        void PointerDrag(PointerEventData eventData);
        void ResetVisuals();
    }
    public interface IFPMotionController
    {
        void SetupMotion();
        void StartMotion();
        void PauseMotion();
        void ResumeMotion();
        void ResetMotion();
        void EndMotion();
        void OnDrawGizmos();
    }
    [Serializable]
    public enum FPToolState
    {
        Deactivated = 0,
        Activated = 1,
        Starting = 2,
        ActiveUse = 3,
        Ending = 9,
        Locked = 66,
        Unlocked = 99,
        NA = 1000,
    }
    public interface IFPTimer<T>
    {
        T StartTimer(float time, Action onFinish);
        T StartTimer(float time, int param, Action<int> onFinish);
        T StartTimer(float time, float param, Action<float> onFinish);
        T StartTimer(float time, string param, Action<string> onFinish);
        T StartTimer(float time, FP_Data param, Action<FP_Data> onFinish);
        T StartTimer(float time, GameObject param, Action<GameObject> onFinish);
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
    [Serializable]
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
    [Serializable]
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
    [Serializable]
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
    #region Enums and Data Classes
    [Serializable]
    public enum UnitOfMeasure
    {
        Millimeter = 0,
        Centimeter = 2,
        Meter = 3,
        Kilometer = 4,
        Inch = 10,
        Feet = 11,
        Miles=12,
        NauticalMiles=13,
        Custom=99
    }
    /// <summary>
    /// Core 'status' for all things sequence related
    /// Will be used heavily across sequence logic
    /// </summary>
    [Serializable]

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
    public enum NPCHackState
    {
        NA,
        Idle,
        Talking,
        Signalling
    }
    [Serializable]
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
    public enum FontSettingLabel
    {
        HeaderOne=1,
        HeaderTwo=2,
        HeaderThree=3,
        HeaderFour=4,
        HeaderFive=5,
        HeaderSix=6,
        Paragraph=7,
        Footer=8
    }
    // placeholder for now, we might need to expand this later
    [Serializable]
    public enum FP_Language
    {
        NA=0,
        USEnglish=1,
        Spanish=2,
        French=3,
    }
    [Serializable]
    public enum FP_LanguageLevel
    {
        LevelOne=1,
        LevelTwo=2,
        LevelThree=3,
        LevelFour=4
    }
    [Serializable]
    public enum CEFRLevel
    {
        NA = 0,
        A1 = 1, //Beginner
        A2 = 2, //Elementary
        B1 = 3, //Intermediate
        B2 = 4, //Upper Intermediate
        C1 = 5, //Advanced
        C2 = 6 //Proficient
    }
    [Serializable]
    public enum FP_VocabCategory
    {
        None,           // Default or un-categorized
        Greetings,      // Basic conversational phrases
        Numbers,        // Counting, phone numbers, etc.
        TimeAndDate,    // Days, months, seasons, telling time
        FoodAndDrink,   // Eating out, groceries, cooking
        Clothing,       // Apparel, shopping
        FamilyAndPeople,// Relationships, titles, occupations
        HomeAndFurniture,// Rooms, objects, cleaning
        Travel,         // Transportation, directions
        Weather,        // Forecasts, seasons, conditions
        School,         // Supplies, classrooms, schedules
        Shopping,       // Stores, money, purchases
        HobbiesAndSports,// Leisure activities, games
        Health,         // Body parts, symptoms, doctor visits
        PlacesAndCities,// Buildings, parks, landmarks
        Nature,         // Animals, plants, landscapes
        Technology,     // Gadgets, internet, media
        WorkAndBusiness,// Jobs, offices, tools
        Emotions,       // Feelings, moods, states
        ActionsAndVerbs,// Common actions (e.g., run, walk)
        ColorsAndShapes,// Visual descriptions
        Grammar,          // Articles, prepositions, etc.
        Reading_Writing, // books, meta text, table of contents, poster, printed text somewhere, journal etc.
    }
    [Serializable]
    public enum FP_VocabSupport
    {
        None=0,
        Beauty = 1,
        Age = 2,
        Goodness = 3,
        Size = 4,
        Shape = 5,
        Color =6,
        Origin = 7,
        Material =8,
        Purpose=9,
        Number = 10,
    }
    [Serializable]
    public enum FP_VocabAction
    {
        None,       // Default action
        Grab,       // Picking up an item
        Raycast,    // Using a raycast to interact
        Drop,       // Dropping an item
        Choice,     // Making a choice or selection
        Inspect,    // Looking closely at an item
        Listen,     // Hearing the pronunciation or description
        Speak,      // Speaking the word aloud
        Place,      // Placing an item in a specific location
        Open,       // Opening an object (e.g., drawer, door)
        Close       // Closing an object
    }
    [Serializable]
    public enum SemanticMapType
    {
        Synonym,
        Antonym,
        Category,
        PartOfWhole,
        RelatedConcept
    }
    [Serializable]
    public enum CollocationType
    {
        /*
        adverb + adjective
            Correct: fully aware
            Incorrect: outright aware
        adjective + noun
            Correct: deep sleep
            Incorrect: low sleep
        noun + noun
            Correct: round of applause
            Incorrect: group of applause
        noun + verb
            Correct: cats purr, dogs bark
            Incorrect: cats bark, dogs purr
        verb + noun
            Correct: give a speech
            Incorrect: send a speech
        verb + expression with preposition
            Correct: run out of time
            Incorrect: speed out of time
        verb + adverb
            Correct: speak loudly
            Incorrect: speak blaringly
        */
        NA,
        AdverbAdjective,
        AdjectiveNoun,
        NounNoun,
        NounVerb,
        VerbNoun,
        VerbPreposition,
        VerbAdverb,
    }

    [Serializable]
    public enum MWApiKeyType
    {
        NA,
        MWCollegeWord,
        MWLearnersWord,
        MWSchoolWord,
    }
    
    [Serializable]
    public enum HelperCategory
    {
        NA = 0,
        Input = 1,
        Menu = 2,
        Settings = 3,
        Movement = 4,
        Puzzle = 5,
        SequenceEvent = 10,
        SequenceAction = 11,
        GenericInteraction = 12,
        Dialogue = 13,
    }
    /// <summary>
    /// Goes with HelperCategory to determine code that we need to run
    /// </summary>
    [Serializable]
    public enum HelperAction
    {
        NA = 0,
        Replay = 1,
        Subtitles = 2,
        Translation = 3,
    }
    [Serializable]
    public enum FPImpactType
    {
        NA = 0,
        Low = 1,
        Medium =2,
        High = 3,
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
    /// <summary>
    /// Serialized struct to hold values for a specific helper threshold
    /// </summary>
    [Serializable]
    public struct HelperThreshold
    {
        public string HelperName;
        public HelperCategory Category;
        public SequenceStatus State;
        public float MaxDelay; // in seconds
    }

    [Serializable]
    public enum StrandType
    {
        CS_Strands,
        TechnologyStrands,
        Career
    }
    #endregion
    /// <summary>
    /// Hold my custom list of items that are serializable so I can save editor prefs and other items
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class FPSerializableList<T>
    {
        public List<T> list = new List<T>();
        public FPSerializableList()
        {

        }
        public FPSerializableList(List<T> list)
        {
            this.list = list;
        }
    }
    [Serializable]
    public class FPSerializableDictionary<TKey, TValue>
    {
        public FPSerializableList<TKey> keys = new FPSerializableList<TKey>(new List<TKey>());
        public FPSerializableList<FPSerializableList<TValue>> values = new FPSerializableList<FPSerializableList<TValue>>(new List<FPSerializableList<TValue>>());

        public FPSerializableDictionary() { } // Needed for Unity JSON serialization

        public FPSerializableDictionary(Dictionary<TKey, List<TValue>> dict)
        {
            keys = new FPSerializableList<TKey>(new List<TKey>(dict.Keys));

            foreach (var key in keys.list)
            {
                values.list.Add(new FPSerializableList<TValue>(dict[key])); // Store values as a List of Lists
            }
        }

        public Dictionary<TKey, List<TValue>> ToDictionary()
        {
            Dictionary<TKey, List<TValue>> dict = new Dictionary<TKey, List<TValue>>();

            for (int i = 0; i < keys.list.Count; i++)
            {
                if (i < values.list.Count && values.list[i] != null)
                {
                    dict[keys.list[i]] = new List<TValue>(values.list[i].list); // Convert back to dictionary
                }
                else
                {
                    Debug.LogWarning($"Mismatch in dictionary deserialization: Key '{keys.list[i]}' has no matching value.");
                    dict[keys.list[i]] = new List<TValue>(); // Assign empty list to avoid null issues
                }
            }

            return dict;
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
