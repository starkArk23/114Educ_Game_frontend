using UnityEngine;
using UnityEngine.SceneManagement;

public static class PauseMenuBootstrap
{
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

        if (string.Equals(sceneName, "MainMenu", System.StringComparison.Ordinal)
            || string.Equals(sceneName, "LoadingScene", System.StringComparison.Ordinal)
            || string.Equals(sceneName, "SystemCoreScene", System.StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }
}