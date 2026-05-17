using UnityEngine;
using UnityEngine.SceneManagement;

public static class PauseMenuBootstrap
{
    // Scenes that should NOT have a pause menu (non-gameplay scenes).
    private static readonly string[] ExcludedSceneNames =
    {
        "MainMenu",
        "LoadingScene"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        if (!ShouldCreatePauseMenu(scene))
            return;

        if (Object.FindFirstObjectByType<PauseMenu>() != null)
            return;

        GameObject pauseManagerObject = new GameObject("GameUIManager");
        pauseManagerObject.AddComponent<PauseMenu>();
    }

    private static bool ShouldCreatePauseMenu(Scene scene)
    {
        string sceneName = scene.name;
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        for (int index = 0; index < ExcludedSceneNames.Length; index++)
        {
            if (string.Equals(sceneName, ExcludedSceneNames[index], System.StringComparison.Ordinal))
                return false;
        }

        return true;
    }
}