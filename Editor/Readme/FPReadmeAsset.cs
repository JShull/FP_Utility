namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    [CreateAssetMenu(fileName ="Readme",menuName = "FuzzPhyte/Utility/Editor/Readme Asset",order = 1)]
    public class FPReadmeAsset:ScriptableObject
    {
        [Header("Header")]
        public Texture2D icon;
        public string title = "Package Readme";
        public string subtitle = "";
        public string version = "0.0.1";

        [TextArea(3, 8)]
        public string overview;

        public List<FPReadmeSection> sections = new();
    }
    [Serializable]
    public class FPReadmeSection
    {
        public string heading;

        [TextArea(3, 12)]
        public string body;

        public List<FPReadmeLink> links = new();
    }

    [Serializable]
    public class FPReadmeLink
    {
        public string label;
        public string url;
    }
}
