namespace FuzzPhyte.Utility
{
    using UnityEngine;
    /// <summary>
    /// This is a tag, our BuildProcessor will remove this GameObject
    /// </summary>
    public class FP_EditorOnly : MonoBehaviour
    {
        [TextArea(3, 10)]
        public string Notes;
    }
}
