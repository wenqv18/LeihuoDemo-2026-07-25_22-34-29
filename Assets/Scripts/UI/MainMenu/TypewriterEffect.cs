using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TypewriterEffect : MonoBehaviour
{
    [TextArea]
    public string[] fullTexts;
    public Text[] textLines;
    public float linePause = 0.3f;

    public AudioClip typeSound;
    public AudioSource typeAudioSource;

    public bool startOnEnable = true;
    private bool playSound = true;

    private void OnEnable()
    {
        if (!startOnEnable) return;
        if (textLines == null || fullTexts == null) return;
        if (textLines.Length != fullTexts.Length) return;
        playSound = true;
        StopAllCoroutines();
        StartCoroutine(PlayTypewriter());
    }

    public void MuteTypingSound()
    {
        playSound = false;
        if (typeAudioSource != null) typeAudioSource.Stop();
    }

    public void StartTyping()
    {
        if (textLines == null || fullTexts == null) return;
        if (textLines.Length != fullTexts.Length) return;
        playSound = true;
        StopAllCoroutines();
        StartCoroutine(PlayTypewriter());
    }

    public void StopTyping()
    {
        StopAllCoroutines();
    }

    private IEnumerator PlayTypewriter()
    {
        for (int i = 0; i < textLines.Length; i++)
            if (textLines[i] != null) textLines[i].text = "";

        for (int i = 0; i < textLines.Length; i++)
        {
            if (textLines[i] == null) continue;
            string full = fullTexts[i];
            for (int j = 0; j <= full.Length; j++)
            {
                textLines[i].text = full.Substring(0, j);
                if (j < full.Length && playSound && typeSound != null && typeAudioSource != null)
                {
                    typeAudioSource.clip = typeSound;
                    typeAudioSource.Play();
                    yield return new WaitForSeconds(typeSound.length);
                }
            }
            yield return new WaitForSeconds(linePause);
        }
    }
}
