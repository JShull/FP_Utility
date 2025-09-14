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
