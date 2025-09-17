using UnityEngine;
using System.Collections.Generic;
using FuzzPhyte.Words;
public class WordManager : MonoBehaviour
{
    /// <summary>
    /// Prefab for generating a word
    /// </summary>
    public GameObject WordPrefab;
    public Canvas MainCanvasDisplay;
    public List<KayleeWord> Words = new List<KayleeWord>();

    public GameObject ActiveWord;
    protected KayleeWord ReturnRandomWord()
    {
        int rand = Random.Range(0, Words.Count);
        Debug.Log(Words[rand].Headword);
        return Words[rand];
    }

    protected FP_Word GetWord(string word)
    {
        foreach (var w in Words)
        {
            if (w.Headword.ToLower() == word.ToLower())
            {
                return w;
            }
        }
        return null;
    }

    protected void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SpawnWord();
        }  
    }

    public void SpawnWord()
    {
        var word = ReturnRandomWord();
        if (word != null && WordPrefab != null && MainCanvasDisplay != null)
        {
            var go = Instantiate(WordPrefab, MainCanvasDisplay.transform);
            if(ActiveWord != null)
            {
                Destroy(ActiveWord);
            }
            ActiveWord = go;
            var kayleeWord = go.GetComponent<KayleeWordPrefab>();
            if (kayleeWord != null)
            {
                kayleeWord.SetupWord(word);
            }
        }
    }
}
