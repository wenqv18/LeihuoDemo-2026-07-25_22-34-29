using UnityEngine;

public sealed class TutorialTaskMarker : MonoBehaviour
{
    [SerializeField] private string taskId;
    [SerializeField] private bool completeOnEnable;
    [SerializeField] private bool completeOnStart;

    public string TaskId => taskId;

    private void OnEnable()
    {
        if (completeOnEnable)
        {
            CompleteTask();
        }
    }

    private void Start()
    {
        if (completeOnStart)
        {
            CompleteTask();
        }
    }

    public void CompleteTask()
    {
        TutorialTaskRegistry.SetCompleted(taskId, true);
    }

    public void SetTaskIncomplete()
    {
        TutorialTaskRegistry.SetCompleted(taskId, false);
    }
}
