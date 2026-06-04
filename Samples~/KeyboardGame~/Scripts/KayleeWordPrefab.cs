// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

using TMPro;
using UnityEngine;

public class KayleeWordPrefab : MonoBehaviour
{
    public TextMeshProUGUI Word;
    public TextMeshProUGUI Definition;

    public void SetupWord(KayleeWord word)
    {
        Word.text = word.Headword;
        if (word.Definition.Length > 0)
        {
            Definition.text = word.Definition[0];
            return;
        }
    }
}
