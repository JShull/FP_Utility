using FuzzPhyte.Words;
using UnityEngine;

[CreateAssetMenu(fileName ="KayleeWords", menuName ="FuzzPhyte/Kaylee/Kaylee Word", order =1)]
public class KayleeWord : FP_Word
{

    [TextArea(2, 4)]
    public string KayleeWordExampleSentence;
}
