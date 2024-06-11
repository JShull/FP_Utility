namespace FuzzPhyte.Utility
{
    using UnityEngine;

    /// <summary>
    /// Core class for all things tied to Data and Scriptable Objects
    /// </summary>
    public abstract class FP_Data : ScriptableObject
    {
        [Tooltip("Must be UNIQUE")]
        public string UniqueID;

        public string Initialize(string projectName, string itemName, Color color)
        {
            if (string.IsNullOrEmpty(UniqueID))
            {
                UniqueID = System.Guid.NewGuid().ToString();
            }
            UniqueID = FP_UniqueGenerator.Encode(projectName, itemName, color);
            return UniqueID;
        }
    }
}
