
namespace FuzzPhyte.Utility.Editor
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using TMPro;
    public class FPComponentChange
    {
        public Component component;
        public string Name;
        public Dictionary<string, object> modifiedProperties = new Dictionary<string, object>();

        public FPComponentChange(ref string returnData,Component comp, SerializedObject serializedObject)
        {
            component = comp;
            Name = component.GetType().ToString();
            // Capture each property's value for later comparison or saving
            var property = serializedObject.GetIterator();
            while (property.NextVisible(true))
            {
                modifiedProperties[property.propertyPath] = GetPropertyValue(property);
            }
            // Capture additional non-serialized properties (e.g., TextMesh.text)
            returnData += CaptureNonSerializedProperties() +",";
        }

        private string CaptureNonSerializedProperties()
        {
            // Check for specific types and capture their fields with reflection
            if (component is TextMeshPro textMesh)
            {

                modifiedProperties["TextMeshPro"] = textMesh.text;
                return textMesh.text;
            }
            return "error";
            // Add other component-specific cases as needed
        }
        private object GetPropertyValue(SerializedProperty property)
        {
            // Handle common property types (add more as needed)
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float: return property.floatValue;
                case SerializedPropertyType.Integer: return property.intValue;
                case SerializedPropertyType.String: return property.stringValue;
                case SerializedPropertyType.Boolean: return property.boolValue;
                case SerializedPropertyType.Color: return property.colorValue;
                case SerializedPropertyType.Vector3: return property.vector3Value;
                case SerializedPropertyType.ObjectReference: return property.objectReferenceValue;
                default: return null;
            }
        }
    }
}
