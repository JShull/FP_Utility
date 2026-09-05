// Copyright (c) 2026 John B. Shull
// FuzzPhyte LLC is a company associated with John B. Shull
// This file is part of FP_Utility Package.
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md COMMERCIAL-LICENSE.md, and NOTICE.md.

namespace FuzzPhyte.Utility.LabelDisplay
{
    using System.Collections;
    using UnityEngine;
    using UnityEngine.Networking;

    public class RemoteImageLoader : MonoBehaviour
    {
        public IEnumerator LoadImage(string imageID, System.Action<Sprite> onComplete)
        {
            Texture2D texture = Resources.Load<Texture2D>(imageID);

            if (texture == null)
            {
                Debug.LogError($"Could not find image: {imageID}");
                onComplete?.Invoke(null);
                yield break;
            }

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            onComplete?.Invoke(sprite);
        }
    }
}