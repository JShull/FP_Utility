using System;
using UnityEngine;

namespace FuzzPhyte.Utility.Meta
{
    [Serializable]
    [CreateAssetMenu(fileName = "Tag", menuName = "FuzzPhyte/Utility/Meta/Tag", order = 0)]
    public class FP_Tag : ScriptableObject
    {
        public string TagName;
        [TextArea(2,4)]
        public string Description;
    }
}
