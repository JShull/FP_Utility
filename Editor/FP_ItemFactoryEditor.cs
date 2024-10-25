namespace FuzzPhyte.Utility.Editor
{
    using UnityEditor;
    using UnityEngine;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Collections;

    /// <summary>
    /// Editor window for creating FP_Data derived ScriptableObjects
    /// which process and generate their own unique id number as part of the setup.
    /// </summary>
    public class FP_ItemFactoryEditor : EditorWindow
    {
        private string projectName;
        private string itemName;
        private Color color = Color.white;
        private MonoScript script;
        private int selectedClassIndex;
        private List<Type> fpDataDerivedTypes;
        private GUIStyle buttonWarningStyle;
        private GUIStyle buttonActiveStyle;
        private Vector2 scrollPosition;
        private Vector2 secondScrollPosition;
        private Dictionary<string, object> dynamicFields = new Dictionary<string, object>();

        #region Color Parameters
        private List<Color> primaryColors = new List<Color>();
        private Color newColor = Color.white;
        private Color[] generatedColors;
        #endregion
        #region Handle Parameters
        private float lowerPanelHeight = 200f; // Initial height of the lower panel
        private bool isResizing = false;
        private Rect resizeHandleRect;
        private const float resizeHandleHeight = 5f; // Height of the resize handle
        private Texture2D handleTexture;
        private Color handleColor = Color.gray; // The color of the handle
        private const float minUpperPanelHeight = 100f; // Minimum height for the top panel
        #endregion
        [MenuItem("FuzzPhyte/Utility/FP Data Factory")]
        public static void ShowWindow()
        {
            GetWindow<FP_ItemFactoryEditor>("FP Data Factory");
        }

        private void OnEnable()
        {
            LoadDerivedTypes();
            CreateHandleTexture();
            //InitializeStyles();
        }
        
        private void LoadDerivedTypes()
        {
            fpDataDerivedTypes = new List<Type> { null }; // Add null to represent "NA"
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsSubclassOf(typeof(FP_Data)) && !type.IsAbstract)
                    {
                        fpDataDerivedTypes.Add(type);
                    }
                }
            }
        }
        private void CreateHandleTexture()
        {
            handleTexture = new Texture2D(1, 1);
            handleColor = FP_Utility_Editor.OkayColor;
            handleTexture.SetPixel(0, 0, handleColor);
            handleTexture.Apply();
        }
        private void OnGUI()
        {
            GUILayout.Label("Create FP_Data Derived ScriptableObject", EditorStyles.boldLabel);

            projectName = EditorGUILayout.TextField("Project Name", projectName);
            itemName = EditorGUILayout.TextField("Item Name", itemName);
            color = EditorGUILayout.ColorField("Color", color);

            string[] typeNames = fpDataDerivedTypes.ConvertAll(t => t == null ? "NA" : t.Name).ToArray();
            selectedClassIndex = EditorGUILayout.Popup("Select Class", selectedClassIndex, typeNames);

            script = (MonoScript)EditorGUILayout.ObjectField("Derived Class Script", script, typeof(MonoScript), false);
            
            //initialize styles
            if (buttonActiveStyle == null || buttonWarningStyle == null)
            {
                
                buttonWarningStyle = new GUIStyle(GUI.skin.button);
                buttonWarningStyle.normal.textColor = FP_Utility_Editor.WarningColor;
                buttonWarningStyle.active.textColor = FP_Utility_Editor.WarningColor;
                buttonWarningStyle.hover.textColor = FP_Utility_Editor.WarningColor;
                buttonWarningStyle.focused.textColor = FP_Utility_Editor.WarningColor;

                buttonActiveStyle = new GUIStyle(GUI.skin.button);
                buttonActiveStyle.normal.textColor = FP_Utility_Editor.OkayColor;
                buttonActiveStyle.active.textColor = FP_Utility_Editor.OkayColor;
                buttonActiveStyle.hover.textColor = FP_Utility_Editor.OkayColor;
                buttonActiveStyle.focused.textColor = FP_Utility_Editor.OkayColor;
            }
            
            bool canCreate = script != null || (selectedClassIndex > 0);
            Color lineColor = canCreate ? FP_Utility_Editor.OkayColor : FP_Utility_Editor.WarningColor;
            FP_Utility_Editor.DrawUILine(lineColor);

            // Begin scroll view
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
            DrawDynamicFields();
            EditorGUILayout.EndScrollView(); // End scroll view
            FP_Utility_Editor.DrawUILine(lineColor);

            EditorGUILayout.Space(); // Add some space between the scroll view and the button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUI.BeginDisabledGroup(!canCreate);
            if (!canCreate)
            {
                GUILayout.Button("Create ScriptableObject", buttonWarningStyle, GUILayout.Width(200));
            }
            else if (GUILayout.Button("Create ScriptableObject", buttonActiveStyle, GUILayout.Width(200)))
            {
                CreateScriptableObject();
            }
            EditorGUI.EndDisabledGroup();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // Add some space between the upper and lower sections
            EditorGUILayout.Space(); 
            
            // Handle resizing logic
            resizeHandleRect = new Rect(0, position.height - lowerPanelHeight - resizeHandleHeight, position.width, resizeHandleHeight);
            //handle colored
            // Handle resizing logic
            if (handleTexture != null)
            {
                GUI.DrawTexture(resizeHandleRect, handleTexture);
            }
            else
            {
                GUI.DrawTexture(resizeHandleRect, EditorGUIUtility.whiteTexture);
            }
            //GUI.DrawTexture(resizeHandleRect, EditorGUIUtility.whiteTexture);
            EditorGUIUtility.AddCursorRect(resizeHandleRect, MouseCursor.ResizeVertical);

            if (Event.current.type == EventType.MouseDown && resizeHandleRect.Contains(Event.current.mousePosition))
            {
                isResizing = true;
            }

            if (isResizing)
            {
                lowerPanelHeight = position.height - Event.current.mousePosition.y - resizeHandleHeight / 2;
                Repaint();
            }

            if (Event.current.type == EventType.MouseUp)
            {
                isResizing = false;
            }

            // Lower part: Color Theme Generator
            secondScrollPosition = EditorGUILayout.BeginScrollView(secondScrollPosition, GUILayout.Height(lowerPanelHeight));
            DrawColorSelectorTool();
            EditorGUILayout.EndScrollView();
        }
        private void DrawColorSelectorTool()
        {
            // Primary color selector
            EditorGUILayout.LabelField("Color Theme Generator", EditorStyles.boldLabel);
            newColor = EditorGUILayout.ColorField("New Primary Color", newColor);

            EditorGUILayout.Space();

            if (GUILayout.Button("Add Primary Color"))
            {
                primaryColors.Add(newColor);
            }
            if (primaryColors.Count > 0)
            {
                EditorGUILayout.LabelField("Primary Colors:");
                for (int i = 0; i < primaryColors.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    primaryColors[i] = EditorGUILayout.ColorField(primaryColors[i]);
                    if (GUILayout.Button("Remove"))
                    {
                        primaryColors.RemoveAt(i);
                        i--;
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space();

            // Disable the button if there's more than one color
            if (primaryColors.Count == 1 && GUILayout.Button("Generate Single Complementary Colors"))
            {
                generatedColors = FP_ColorTheory.GenerateSingleComplementaryColors(primaryColors[0]);
            }
            else if (primaryColors.Count != 1)
            {
                GUI.enabled = false;
                GUILayout.Button("Generate Single Complementary Colors");
                GUI.enabled = true;
            }

            if (primaryColors.Count > 0 && GUILayout.Button("Generate Complementary Colors"))
            {
                generatedColors = FP_ColorTheory.GenerateComplementaryColors(primaryColors.ToArray(),FP_ColorTheory.BlendColorsHSV);
            }

            if (primaryColors.Count > 0 && GUILayout.Button("Generate Analogous Colors"))
            {
                generatedColors = FP_ColorTheory.GenerateAnalogousColors(primaryColors[0]);
            }

            if (primaryColors.Count > 0 && GUILayout.Button("Generate Triadic Colors"))
            {
                generatedColors = FP_ColorTheory.GenerateTriadicColors(primaryColors[0]);
            }

            if (GUILayout.Button("Clear Generated Colors"))
            {
                generatedColors = null;
            }
            if (generatedColors != null && generatedColors.Length > 0)
            {
                if (GUILayout.Button("Replace Primary Colors with Generated Colors"))
                {
                    primaryColors = new List<Color>(generatedColors);
                    generatedColors = null;
                }

                EditorGUILayout.LabelField("Generated Colors:");
                if(generatedColors != null)
                {
                    for (int i = 0; i < generatedColors.Length; i++)
                    {
                        EditorGUILayout.ColorField("Color " + (i + 1), generatedColors[i]);
                    }
                }
                
            }
        }

        private void DrawDynamicFields()
        {
            Type selectedType = selectedClassIndex > 0 ? fpDataDerivedTypes[selectedClassIndex] : null;
            if (selectedType != null)
            {
                FieldInfo[] fields = selectedType.GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    if (field.Name == "UniqueID")
                    {
                        continue; // Skip the UniqueID field
                    }
                    string fieldName = field.Name;
                    Type fieldType = field.FieldType;

                    if (!dynamicFields.ContainsKey(fieldName))
                    {
                        dynamicFields[fieldName] = GetDefaultValue(fieldType);
                    }

                    object fieldValue = dynamicFields[fieldName];
                    dynamicFields[fieldName] = DrawField(fieldName, fieldType, fieldValue,field);
                }
            }
        }

        private object GetDefaultValue(Type type)
        {
            if (type == typeof(string))
            {
                return string.Empty;
            }
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }
            return null;
        }
        private object DrawField(string fieldName, Type fieldType, object fieldValue, FieldInfo fieldInfo = null)
        {
            // Handle TextArea attribute using the utility function
            if (HandleTextAreaAttribute(fieldInfo, fieldType, ref fieldValue))
            {
                return fieldValue;
            }
            
            if (fieldType == typeof(int))
            {
                return EditorGUILayout.IntField(ReturnLabelSpacedName(fieldName), (int)fieldValue);
            }
            if (fieldType == typeof(float))
            {
                return EditorGUILayout.FloatField(ReturnLabelSpacedName(fieldName), (float)fieldValue);
            }
            if (fieldType == typeof(string))
            {
                return EditorGUILayout.TextField(ReturnLabelSpacedName(fieldName), (string)fieldValue);
            }
            if (fieldType == typeof(bool))
            {
                return EditorGUILayout.Toggle(ReturnLabelSpacedName(fieldName), (bool)fieldValue);
            }
            if(fieldType == typeof(Vector2))
            {
                return EditorGUILayout.Vector2Field(ReturnLabelSpacedName(fieldName), (Vector2)fieldValue);
            }
            if(fieldType == typeof(Vector3))
            {
                return EditorGUILayout.Vector3Field(ReturnLabelSpacedName(fieldName), (Vector3)fieldValue);
            }
            if (fieldType == typeof(Color))
            {
                return EditorGUILayout.ColorField(ReturnLabelSpacedName(fieldName), (Color)fieldValue);
            }
            if (fieldType.IsEnum)
            {
                return EditorGUILayout.EnumPopup(ReturnLabelSpacedName(fieldName), (Enum)fieldValue);
            }
            if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
            {
                return EditorGUILayout.ObjectField(ReturnLabelSpacedName(fieldName), (UnityEngine.Object)fieldValue, fieldType, true);
            }
            
            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type itemType = fieldType.GetGenericArguments()[0];
                IList list = (IList)fieldValue ?? (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
                EditorGUILayout.LabelField(ReturnLabelSpacedName(fieldName));
                int listSize = EditorGUILayout.IntField("Size", list.Count);

                while (listSize > list.Count)
                {
                    list.Add(itemType.IsSubclassOf(typeof(ScriptableObject)) ? null : Activator.CreateInstance(itemType));
                }
                while (listSize < list.Count)
                {
                    list.RemoveAt(list.Count - 1);
                }

                for (int i = 0; i < list.Count; i++)
                {
                    list[i] = DrawField(ReturnLabelSpacedName(fieldName) + " " + i, itemType, list[i]);
                }
                return list;
            }
            if (fieldType == typeof(FP_Location))
            {
                return DrawFPLocationField(fieldName, (FP_Location)fieldValue);
            }
            if(fieldType == typeof(FP_Camera))
            {
                return DrawFPCameraField(fieldName, (FP_Camera)fieldValue);
            }
            if(fieldType == typeof(FP_Multilingual))
            {
                return DrawFPMultiLingualField(fieldName, (FP_Multilingual)fieldValue);
            }
            if(fieldType == typeof(FP_Audio))
            {
                return DrawFPAudioField(fieldName, (FP_Audio)fieldValue);
            }
            if(fieldType == typeof(ConvoTranslation))
            {
                return DrawConvoTranslationField(fieldName, (ConvoTranslation)fieldValue);
            }
            
            EditorGUILayout.LabelField(ReturnLabelSpacedName(fieldName), $"Unsupported field type: {fieldType.Name}");
            return fieldValue;
        }

        #region Draw Fields
        private ConvoTranslation DrawConvoTranslationField(string fieldName, ConvoTranslation data)
        {

            EditorGUILayout.LabelField(ReturnLabelSpacedName(fieldName));
            //data.Header = EditorGUILayout.TextField("Header", data.Header);
           
            var type = typeof(ConvoTranslation);
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var fieldValue = field.GetValue(data);
                var newName = ReturnLabelSpacedName(field.Name);
                
                var newValue = DrawField(newName, field.FieldType, fieldValue, field);
                if (!Equals(fieldValue, newValue))
                {
                    field.SetValueDirect(__makeref(data), newValue);
                }
            }
            return data;
        }
        //Returns a string with a space between two capital letters
        private string ReturnLabelSpacedName(string fieldName)
        {
            //ObjectNames.NicifyVariableName
            return ObjectNames.NicifyVariableName(fieldName);
            /*
            var newName = "";
            if (fieldName.Length > 1)
            {
                //var newName = "";
                for (int i = 0; i < fieldName.Length; i++)
                {
                    if (i > 0 && char.IsUpper(fieldName[i]))
                    {
                        newName += " ";
                    }
                    newName += fieldName[i];
                }
                //EditorGUILayout.LabelField(newName);
                return newName;
            }
            return fieldName;
            */
        }
      
        private FP_Audio DrawFPAudioField(string fieldName,FP_Audio data)
        {
            EditorGUILayout.LabelField(ReturnLabelSpacedName(fieldName));
            data.AudioClip = (AudioClip)EditorGUILayout.ObjectField("Audio Clip", data.AudioClip, typeof(AudioClip), false);
            data.URLAudioType = (AudioType)EditorGUILayout.EnumPopup("URL Audio Type", data.URLAudioType);
            data.URLReference = EditorGUILayout.TextField("URL Reference", data.URLReference);
            return data;
        }
        private FP_Location DrawFPLocationField(string fieldName, FP_Location location)
        {
            EditorGUILayout.LabelField(ReturnLabelSpacedName(fieldName));
            location.WorldLocation = EditorGUILayout.Vector3Field("World Location", location.WorldLocation);
            location.EulerRotation = EditorGUILayout.Vector3Field("Euler Rotation", location.EulerRotation);
            location.LocalScale = EditorGUILayout.Vector3Field("Local Scale", location.LocalScale);
            return location;
        }
        private FP_Multilingual DrawFPMultiLingualField(string fieldName, FP_Multilingual data)
        {
            EditorGUILayout.LabelField(ReturnLabelSpacedName(fieldName)) ;
            data.Primary = (FP_Language)EditorGUILayout.EnumPopup("Primary Language", data.Primary);
            data.Secondary = (FP_Language)EditorGUILayout.EnumPopup("Secondary Language", data.Secondary);
            data.Tertiary = (FP_Language)EditorGUILayout.EnumPopup("Tertiary Language", data.Tertiary);
            return data;
        }
        private FP_Camera DrawFPCameraField(string fieldName, FP_Camera camera)
        {
            EditorGUILayout.LabelField(ReturnLabelSpacedName(fieldName));
            //
            EditorGUILayout.BeginHorizontal();
            camera.CameraFOV = EditorGUILayout.Slider("Camera FOV",camera.CameraFOV, 5f, 178f); // Adjust range as needed
            EditorGUILayout.EndHorizontal();
            //
            //camera.CameraFOV = EditorGUILayout.FloatField("Camera FOV", camera.CameraFOV);
            camera.PitchDamping = EditorGUILayout.FloatField("Pitch Damping", camera.PitchDamping);
            camera.RollDamping = EditorGUILayout.FloatField("Roll Damping", camera.RollDamping);
            var v3 = EditorGUILayout.Vector3Field("Damping", new Vector3(camera.XDamping, camera.YDamping, camera.ZDamping));
            camera.XDamping = v3.x;
            camera.YDamping = v3.y;
            camera.ZDamping = v3.z;
            camera.PositionOffset = EditorGUILayout.FloatField("Position Offset", camera.PositionOffset);
            camera.SearchRadius = EditorGUILayout.IntField("Search Radius", camera.SearchRadius);
            camera.SearchResolution = EditorGUILayout.IntField("Search Resolution", camera.SearchResolution);
            camera.StepsPerSegment = EditorGUILayout.IntField("Steps Per Segment", camera.StepsPerSegment);
            camera.RendererIndex = EditorGUILayout.IntField("Renderer Index", camera.RendererIndex);
            return camera;    
        }

        #endregion
        private bool HandleTextAreaAttribute(FieldInfo fieldInfo, Type fieldType, ref object fieldValue)
        {
            if (fieldInfo != null)
            {
                // Handle Header attribute
                var headerAttribute = fieldInfo.GetCustomAttribute<HeaderAttribute>();
                if (headerAttribute != null)
                {
                    var headerName = ReturnLabelSpacedName(headerAttribute.header);
                    EditorGUILayout.LabelField(headerName, EditorStyles.boldLabel);
                    return false;
                }

                var textAreaAttribute = fieldInfo.GetCustomAttribute<TextAreaAttribute>();
                if (textAreaAttribute != null && fieldType == typeof(string))
                {
                    var textAreaName = ReturnLabelSpacedName(fieldInfo.Name);
                    EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(fieldInfo.Name)); // Add a label for the field
                    fieldValue = EditorGUILayout.TextArea((string)fieldValue, GUILayout.Height(EditorGUIUtility.singleLineHeight * (textAreaAttribute.minLines + 1)));
                    return true;
                }
            }
            return false;
        }
       
        private void CreateScriptableObject()
        {
            if (script == null && (selectedClassIndex == 0 || selectedClassIndex < 0))
            {
                Debug.LogError("Please assign a derived class script or select a class from the dropdown.");
                return;
            }

            Type scriptClass = script != null ? script.GetClass() : fpDataDerivedTypes[selectedClassIndex];
            if (scriptClass == null || !typeof(FP_Data).IsAssignableFrom(scriptClass))
            {
                Debug.LogError("Selected script is not derived from FP_Data.");
                return;
            }

            FP_Data instance = (FP_Data)ScriptableObject.CreateInstance(scriptClass);
            instance.Initialize(projectName, itemName, color);

            // Set dynamic fields
            FieldInfo[] fields = scriptClass.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.Name == "UniqueID")
                {
                    continue; // Skip the UniqueID field
                }

                if (dynamicFields.ContainsKey(field.Name))
                {
                    field.SetValue(instance, dynamicFields[field.Name]);
                }
            }

            string defaultFileName = $"{projectName}_{itemName}.asset";
            string path = EditorUtility.SaveFilePanelInProject("Save ScriptableObject", defaultFileName, "asset", "Please enter a file name to save the scriptable object to");
            if (path == "")
            {
                DestroyImmediate(instance);
                return;
            }

            AssetDatabase.CreateAsset(instance, path);
            AssetDatabase.SaveAssets();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = instance;

            // Update the itemName field to increment by 1
            UpdateItemName();
        }
        private void UpdateItemName()
        {
            int number;
            string baseName = itemName.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
            string numberPart = itemName.Substring(baseName.Length);

            if (int.TryParse(numberPart, out number))
            {
                number++;
            }
            else
            {
                number = 1;
            }

            itemName = $"{baseName}{number}";
        }
    }
}
