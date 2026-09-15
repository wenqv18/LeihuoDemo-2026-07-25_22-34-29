using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

public class HoverDisplay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public RectTransform targetRect;
    public GameObject backgroundObj;
    public float maxWidth = 200f;
    public float duration = 0.4f;
    public bool resetOnEnable = true;

    public AudioClip hoverSound;
    private AudioSource audioSource;
    private Tweener tweener;

    private void Awake()
    {
        EnsureAudioSource();
    }

    private void OnEnable()
    {
        if (resetOnEnable)
        {
            ResetHoverVisuals();
        }
    }

    private void Start()
    {
        ResetHoverVisuals();
    }

    private void OnDisable()
    {
        KillTween();
        ResetHoverVisuals();
    }

    private void OnDestroy()
    {
        KillTween();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        EnsureAudioSource();

        if (hoverSound != null) audioSource.PlayOneShot(hoverSound);
        if (backgroundObj != null) backgroundObj.SetActive(true);
        if (targetRect != null)
        {
            KillTween();
            targetRect.gameObject.SetActive(true);
            float targetWidth = Mathf.Max(0f, maxWidth);
            if (targetRect.sizeDelta.x < 0.01f)
            {
                targetRect.sizeDelta = new Vector2(0f, targetRect.sizeDelta.y);
            }

            tweener = targetRect
                .DOSizeDelta(new Vector2(targetWidth, targetRect.sizeDelta.y), duration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (targetRect != null)
        {
            KillTween();
            tweener = targetRect
                .DOSizeDelta(new Vector2(0f, targetRect.sizeDelta.y), duration * 0.5f)
                .SetEase(Ease.InCubic)
                .SetUpdate(true)
                .OnComplete(ResetHoverVisuals);
            return;
        }

        if (backgroundObj != null) backgroundObj.SetActive(false);
    }

    private void EnsureAudioSource()
    {
        if (audioSource != null)
        {
            return;
        }

        audioSource = GetComponent<AudioSource>();
    }

    private void ResetHoverVisuals()
    {
        if (targetRect != null)
        {
            targetRect.sizeDelta = new Vector2(0f, targetRect.sizeDelta.y);
            targetRect.gameObject.SetActive(false);
        }

        if (backgroundObj != null)
        {
            backgroundObj.SetActive(false);
        }
    }

    private void KillTween()
    {
        if (tweener == null)
        {
            return;
        }

        tweener.Kill();
        tweener = null;
    }
}
