using DG.Tweening;
using UnityEngine;

public sealed class FloatingPromptTween : MonoBehaviour
{
    [SerializeField] private float floatDistance = 0.12f;
    [SerializeField] private float halfDuration = 0.55f;

    private Vector3 initialLocalPosition;
    private Tween floatTween;

    private void Awake()
    {
        initialLocalPosition = transform.localPosition;
    }

    private void OnEnable()
    {
        initialLocalPosition = transform.localPosition;
        PlayFloat();
    }

    private void OnDisable()
    {
        StopFloat();
    }

    private void OnDestroy()
    {
        StopFloat();
    }

    private void PlayFloat()
    {
        StopFloat();
        floatTween = transform
            .DOLocalMoveY(initialLocalPosition.y + floatDistance, halfDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true)
            .OnKill(() => floatTween = null);
    }

    private void StopFloat()
    {
        if (floatTween == null)
        {
            return;
        }

        floatTween.Kill();
        floatTween = null;
        transform.localPosition = initialLocalPosition;
    }
}
