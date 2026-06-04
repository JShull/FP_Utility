// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

using FuzzPhyte.Words;
using UnityEngine;

[CreateAssetMenu(fileName ="KayleeWords", menuName ="FuzzPhyte/Kaylee/Kaylee Word", order =1)]
public class KayleeWord : FP_Word
{

    [TextArea(2, 4)]
    public string KayleeWordExampleSentence;
}
