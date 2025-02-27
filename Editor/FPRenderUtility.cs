namespace FuzzPhyte.Utility.Editor
{
    using UnityEngine;
    using UnityEditor;
    /// <summary>
    /// Initially designed to help recursively go through GI settings for Mesh Renderers
    /// will turn this into a more general utility for misc. rendering editor settings as needed
    /// </summary>
    public class FPRenderingUtilityEditor: Editor
    {
        #region Menu Items
        [MenuItem("FuzzPhyte/Utility/Rendering/GI/Enable GI", priority = 90)]
        private static void EnableContributeGlobalIllumination()
        {
            ApplyContributeGlobalIllumination(true);
        }
        [MenuItem("FuzzPhyte/Utility/Rendering/GI/Disable GI", priority = 91)]
        private static void DisableContributeGlobalIllumination()
        {
            ApplyContributeGlobalIllumination(false);
        }
        #endregion

        #region GI Enable/Disable Functions

        /// <summary>
        /// Stub-out Entry for UI Menu Items
        /// </summary>
        /// <param name="enable"></param>
        protected static void ApplyContributeGlobalIllumination(bool enable)
        {
            if (Selection.activeGameObject == null)
            {
                Debug.LogWarning("No GameObject selected. Please select a GameObject in the Hierarchy.");
                return;
            }
            GameObject selectedObject = Selection.activeGameObject;

            int updatedCount = 0;
            ProcessAllRenderers(selectedObject, enable, ref updatedCount);

            Debug.Log($"{updatedCount} MeshRenderers updated. Global Illumination is now {(enable ? "enabled" : "disabled")}.");
        }
        /// <summary>
        /// Main Loop Entry Function
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="enable"></param>
        /// <param name="updatedCount"></param>
        private static void ProcessAllRenderers(GameObject obj, bool enable, ref int updatedCount)
        {
            // Check my mesh renderer
            if(ProcessSingleRenderer(obj,enable))
            {
                updatedCount++;
            }
            // Process all children
            var childrenObjects = obj.GetComponentsInChildren<MeshRenderer>();
            foreach(var child in childrenObjects)
            {
                if(ProcessSingleRenderer(child.gameObject, enable))
                {
                    updatedCount++;
                }
            }
        }
        /// <summary>
        /// Single Object Processing - assumption with a MeshRenderer
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="enable"></param>
        /// <returns></returns>
        private static bool ProcessSingleRenderer(GameObject obj, bool enable)
        {
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                // Get current static flags
                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(obj);
                // Update the Contribute Global Illumination flag
                if (enable)
                {
                    flags |= StaticEditorFlags.ContributeGI; // Enable the flag
                }
                else
                {
                    flags &= ~StaticEditorFlags.ContributeGI; // Disable the flag
                }
                GameObjectUtility.SetStaticEditorFlags(obj, flags);
                return true;
            }
            return false;
        }
        
        #endregion
    }
}
