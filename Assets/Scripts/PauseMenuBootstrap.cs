using UnityEngine;
using UnityEngine.SceneManagement;

public static class PauseMenuBootstrap
{
    private static readonly string[] GameplaySceneNames =
    {
        "City",
        "GameScene",
        "HallwayScene",
        "MainScene",
        "RoomScene"
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

        for (int index = 0; index < GameplaySceneNames.Length; index++)
        {
            if (string.Equals(sceneName, GameplaySceneNames[index], System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}