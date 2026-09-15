using UnityEngine;
using UnityEngine.SceneManagement;

public static class TutorialLevelRuntimeInstaller
{
    private const string TutorialSceneName = "TutorialLevel";
    private const string TutorialNpcBodyPath = "NormalWorld/NPC/NPCBody";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureTutorialNpc(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureTutorialNpc(scene);
    }

    private static void EnsureTutorialNpc(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || scene.name != TutorialSceneName)
        {
            return;
        }

        GameObject npcBody = GameObject.Find(TutorialNpcBodyPath);
        if (npcBody == null)
        {
            npcBody = GameObject.Find("NPCBody");
        }

        if (npcBody == null)
        {
            Debug.LogWarning("[TutorialLevelRuntimeInstaller] Tutorial NPCBody not found.");
            return;
        }

        TutorialNpcDialogue2D dialogue = npcBody.GetComponent<TutorialNpcDialogue2D>();
        if (dialogue == null)
        {
            dialogue = npcBody.AddComponent<TutorialNpcDialogue2D>();
        }

        dialogue.ConfigureDefaultReferences(npcBody.transform);
    }
}
