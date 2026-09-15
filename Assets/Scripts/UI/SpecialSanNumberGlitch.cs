using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SpecialSanNumberGlitch : MonoBehaviour
{
    [SerializeField] private Text targetText;
    [SerializeField] private int minValue;
    [SerializeField] private int maxValue = 200;
    [SerializeField] private float minInterval = 0.025f;
    [SerializeField] private float maxInterval = 0.08f;

    private float nextChangeTime;

    private void Awake()
    {
        ResolveText();
    }

    private void OnEnable()
    {
        ResolveText();
        ScheduleNextChange(true);
    }

    private void Update()
    {
        if (targetText == null)
        {
            ResolveText();
            if (targetText == null)
            {
                return;
            }
        }

        if (Time.unscaledTime < nextChangeTime)
        {
            return;
        }

        targetText.text = Random.Range(minValue, maxValue + 1).ToString();
        ScheduleNextChange(false);
    }

    private void ResolveText()
    {
        if (targetText == null)
        {
            targetText = GetComponent<Text>();
        }
    }

    private void ScheduleNextChange(bool immediate)
    {
        if (immediate && targetText != null)
        {
            targetText.text = Random.Range(minValue, maxValue + 1).ToString();
        }

        float low = Mathf.Max(0.005f, minInterval);
        float high = Mathf.Max(low, maxInterval);
        nextChangeTime = Time.unscaledTime + Random.Range(low, high);
    }
}
