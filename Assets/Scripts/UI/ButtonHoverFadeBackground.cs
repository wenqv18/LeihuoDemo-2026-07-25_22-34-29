using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ButtonHoverFadeBackground : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Graphic targetGraphic;
    [SerializeField] private float visibleAlpha = 0.22f;
    [SerializeField] private float fadeDuration = 0.12f;

    private Tween fadeTween;
    private bool configured;

    public void Configure(Graphic target, float fallbackVisibleAlpha, float duration)
    {
        targetGraphic = target;
        visibleAlpha = targetGraphic != null && targetGraphic.color.a > 0f
            ? targetGraphic.color.a
            : fallbackVisibleAlpha;
        fadeDuration = Mathf.Max(0.01f, duration);
        configured = targetGraphic != null;
        SetAlpha(0f);
    }

    private void Awake()
    {
        if (targetGraphic != null && targetGraphic.color.a > 0f)
        {
            visibleAlpha = targetGraphic.color.a;
            configured = true;
            SetAlpha(0f);
        }
    }

    private void OnDisable()
    {
        KillTween();
        if (configured)
        {
            SetAlpha(0f);
        }
    }

    private void OnDestroy()
    {
        KillTween();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        FadeTo(visibleAlpha);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        FadeTo(0f);
    }

    private void FadeTo(float alpha)
    {
        if (targetGraphic == null)
        {
            return;
        }

        KillTween();
        fadeTween = targetGraphic
            .DOFade(alpha, fadeDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .OnKill(() => fadeTween = null);
    }

    private void SetAlpha(float alpha)
    {
        if (targetGraphic == null)
        {
            return;
        }

        Color color = targetGraphic.color;
        color.a = alpha;
        targetGraphic.color = color;
    }

    private void KillTween()
    {
        if (fadeTween == null)
        {
            return;
        }

        fadeTween.Kill();
        fadeTween = null;
    }
}
