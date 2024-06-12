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

        private Dictionary<string, object> dynamicFields = new Dictionary<string, object>();

        [MenuItem("FuzzPhyte/Utility/FP Data Factory")]
        public static void ShowWindow()
        {
            GetWindow<FP_ItemFactoryEditor>("FP Data Factory");
        }

        private void OnEnable()
        {
            LoadDerivedTypes();
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

        private void InitializeStyles()
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

        private void OnGUI()
        {
            GUILayout.Label("Create FP_Data Derived ScriptableObject", EditorStyles.boldLabel);

            projectName = EditorGUILayout.TextField("Project Name", projectName);
            itemName = EditorGUILayout.TextField("Item Name", itemName);
            color = EditorGUILayout.ColorField("Color", color);

            string[] typeNames = fpDataDerivedTypes.ConvertAll(t => t == null ? "NA" : t.Name).ToArray();
            selectedClassIndex = EditorGUILayout.Popup("Select Class", selectedClassIndex, typeNames);

            script = (MonoScript)EditorGUILayout.ObjectField("Derived Class Script", script, typeof(MonoScript), false);

            if (buttonActiveStyle == null || buttonWarningStyle == null)
            {
                InitializeStyles();
            }
            bool canCreate = script != null || (selectedClassIndex > 0);
            Color lineColor = canCreate ? FP_Utility_Editor.OkayColor : FP_Utility_Editor.WarningColor;
            FP_Utility_Editor.DrawUILine(lineColor);
            // Draw a Line
            
            // Begin scroll view
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
            // Draw dynamic fields based on selected class
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
                    dynamicFields[fieldName] = DrawField(fieldName, fieldType, fieldValue);
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

        private object DrawField(string fieldName, Type fieldType, object fieldValue)
        {
            if (fieldType == typeof(int))
            {
                return EditorGUILayout.IntField(fieldName, (int)fieldValue);
            }
            if (fieldType == typeof(float))
            {
                return EditorGUILayout.FloatField(fieldName, (float)fieldValue);
            }
            if (fieldType == typeof(string))
            {
                return EditorGUILayout.TextField(fieldName, (string)fieldValue);
            }
            if (fieldType == typeof(bool))
            {
                return EditorGUILayout.Toggle(fieldName, (bool)fieldValue);
            }
            if (fieldType == typeof(Color))
            {
                return EditorGUILayout.ColorField(fieldName, (Color)fieldValue);
            }
            if (fieldType.IsEnum)
            {
                return EditorGUILayout.EnumPopup(fieldName, (Enum)fieldValue);
            }
            if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
            {
                return EditorGUILayout.ObjectField(fieldName, (UnityEngine.Object)fieldValue, fieldType, true);
            }
            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type itemType = fieldType.GetGenericArguments()[0];
                IList list = (IList)fieldValue ?? (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
                EditorGUILayout.LabelField(fieldName);
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
                    list[i] = DrawField(fieldName + " " + i, itemType, list[i]);
                }
                return list;
            }
            /*
            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type itemType = fieldType.GetGenericArguments()[0];
                IList list = (IList)fieldValue ?? (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
                EditorGUILayout.LabelField(fieldName);
                int listSize = EditorGUILayout.IntField("Size", list.Count);

                while (listSize > list.Count)
                    list.Add(Activator.CreateInstance(itemType));
                while (listSize < list.Count)
                    list.RemoveAt(list.Count - 1);

                for (int i = 0; i < list.Count; i++)
                {
                    list[i] = DrawField(fieldName + " " + i, itemType, list[i]);
                }
                return list;
            }
            */
            if (fieldType == typeof(FP_Location))
            {
                return DrawFPLocationField(fieldName, (FP_Location)fieldValue);
            }
            if(fieldType == typeof(FP_Camera))
            {
                return DrawFPCameraField(fieldName, (FP_Camera)fieldValue);
            }
            EditorGUILayout.LabelField(fieldName, $"Unsupported field type: {fieldType.Name}");
            return fieldValue;
        }
        private FP_Location DrawFPLocationField(string fieldName, FP_Location location)
        {
            EditorGUILayout.LabelField(fieldName);
            location.WorldLocation = EditorGUILayout.Vector3Field("World Location", location.WorldLocation);
            location.EulerRotation = EditorGUILayout.Vector3Field("Euler Rotation", location.EulerRotation);
            location.LocalScale = EditorGUILayout.Vector3Field("Local Scale", location.LocalScale);
            return location;
        }
        private FP_Camera DrawFPCameraField(string fieldName, FP_Camera camera)
        {
            EditorGUILayout.LabelField(fieldName);
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
            /*
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
            */
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
