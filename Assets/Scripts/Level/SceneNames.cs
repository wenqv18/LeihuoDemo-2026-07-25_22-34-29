using UnityEngine.SceneManagement;

public static class SceneNames
{
    public const string StartScene = "Start Scene";
    public const string UIScene = "UIController";
    public const string TutorialLevel = "TutorialLevel";
    public const string FirstLevel = "Level_01";
    public const string SecondLevel = "Level_02";
    public const string ThirdLevel = "Level_03";

    public static bool IsGameplayScene(Scene scene)
    {
        return scene.IsValid()
            && scene.isLoaded
            && (scene.name == TutorialLevel || scene.name.StartsWith("Level_"));
    }

    public static bool IsRuntimeBootstrapExcluded(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return true;
        }

        return scene.name == StartScene
            || scene.name == UIScene;
    }
}
