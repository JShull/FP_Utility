namespace FuzzPhyte.Utility.FPSystem.Editor
{
    using UnityEditor;
    using UnityEngine;

    [InitializeOnLoad]
    public class FPExecutionOrder
    {
        static FPExecutionOrder()
        {
            SetScriptExecutionOrder("FPBootStrapper",-50);
        }
        private static void SetScriptExecutionOrder(string scriptName, int desiredOrder)
        {
            // Find the script asset
            MonoScript monoScript = FindMonoScript(scriptName);
            if (monoScript != null)
            {
                // Get the current execution order
                int currentOrder = MonoImporter.GetExecutionOrder(monoScript);
                // Check if the current order is different from the desired order
                if (currentOrder != desiredOrder)
                {
                    // Set the execution order
                    MonoImporter.SetExecutionOrder(monoScript, desiredOrder);
                    Debug.Log($"Set execution order for {scriptName} to {desiredOrder}");
                }
                else
                {
                    //Debug.Log($"Execution order for {scriptName} is already set to {desiredOrder}");
                }
            }
            else
            {
                Debug.LogWarning($"Script {scriptName} not found. Make sure the script name is correct.");
            }
        }
        private static MonoScript FindMonoScript(string scriptName)
        {
            // Find all MonoScript assets
            string[] guids = AssetDatabase.FindAssets($"t:MonoScript {scriptName}");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (monoScript != null && monoScript.name == scriptName)
                {
                    return monoScript;
                }
            }
            return null;
        }
    }
}
