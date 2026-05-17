using UnityEngine;

/// <summary>
/// Drop this component anywhere in the scene (or on a DontDestroyOnLoad root).
/// It creates the QuestLogManager singleton and the QuestLogUI if they don't exist yet.
/// </summary>
[DisallowMultipleComponent]
public class QuestLogBootstrap : MonoBehaviour
{
    private void Awake()
    {
        QuestLogManager.EnsureExists();

        if (FindFirstObjectByType<QuestLogUI>() == null)
        {
            GameObject uiRoot = new GameObject("QuestLogUI");
            DontDestroyOnLoad(uiRoot);
            uiRoot.AddComponent<QuestLogUI>();
        }
    }
}
