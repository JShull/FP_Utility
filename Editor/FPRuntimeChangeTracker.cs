namespace FuzzPhyte.Utility.Editor
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    public class FPRuntimeChangeTracker
    {
        public Dictionary<GameObject, List<FPComponentChange>> changeCache = new Dictionary<GameObject, List<FPComponentChange>>();

        public string TrackGameObjectChanges(GameObject rootObject)
        {
            string componentTypes = "";
            var listofChildren = rootObject.GetComponentsInChildren<Transform>();
            for (int i=0; i<listofChildren.Length;i++)
            {
                var childObject = listofChildren[i].gameObject;
                var returnString = TrackComponentsOnObject(childObject);
                componentTypes += "(" + returnString + ")";
            }
            
            return componentTypes;
        }
        public void RemoveTrackedGameObject(GameObject rootObject)
        {
            if (changeCache.ContainsKey(rootObject))
            {
                changeCache.Remove(rootObject);
            }
        }

        private string TrackComponentsOnObject(GameObject obj)
        {
            if (!changeCache.ContainsKey(obj))
            {
                changeCache.Add(obj, new List<FPComponentChange>());
                //changeCache[obj] = new List<FPComponentChange>();
            }
            string componentTypes = "";
            string textMeshReturnData = "";
            var components = obj.GetComponents<Component>();
            for (int i=0;i< components.Length; i++)
            {
                var component = components[i];
                var serializedObject = new SerializedObject(component);
                componentTypes += component.GetType() + ",\n";
                var componentChange = new FPComponentChange(ref textMeshReturnData, component, serializedObject);
                
                changeCache[obj].Add(componentChange);
            }
            /*
            foreach (var component in obj.GetComponents<Component>())
            {
                var serializedObject = new SerializedObject(component);
                componentTypes+=component.GetType()+",\n";
                var componentChange = new FPComponentChange(ref textMeshReturnData,component, serializedObject);
                changeCache[obj].Add(componentChange);
            }
            */
            return componentTypes + "| " + textMeshReturnData+" |";
        }

        public void ClearCache()
        {
            changeCache.Clear();
        }
    }

}
