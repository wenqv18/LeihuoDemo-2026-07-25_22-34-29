using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TutorialTaskRegistry
{
    private static readonly Dictionary<string, bool> completedTasks = new Dictionary<string, bool>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        completedTasks.Clear();
    }

    public static void SetCompleted(string taskId, bool completed = true)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return;
        }

        completedTasks[taskId] = completed;
        Debug.Log($"[TutorialTaskRegistry] Task '{taskId}' completed={completed}");
    }

    public static bool IsCompleted(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return true;
        }

        bool completed;
        return completedTasks.TryGetValue(taskId, out completed) && completed;
    }

    public static void ResetTask(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return;
        }

        completedTasks.Remove(taskId);
    }

    public static void ResetAll()
    {
        completedTasks.Clear();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single)
        {
            completedTasks.Clear();
        }
    }
}
