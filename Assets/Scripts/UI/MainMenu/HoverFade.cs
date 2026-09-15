using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

public class HoverFade : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Graphic target;
    public float fadeIn = 0.15f;
    public float fadeOut = 0.25f;

    public AudioClip hoverSound;
    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        if (target != null) target.DOFade(0f, 0f);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (hoverSound != null) audioSource.PlayOneShot(hoverSound);
        target?.DOKill();
        target?.DOFade(90f / 255f, fadeIn);
    }

    public void OnPointerExit(PointerEventData e)
    {
        target?.DOKill();
        target?.DOFade(0f, fadeOut);
    }
}
