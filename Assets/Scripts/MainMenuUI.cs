using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private string loadingSceneName = "LoadingScene";
    [SerializeField] private string gameSceneName = "RoomScene";
    [SerializeField] private TMP_InputField operatorNameInput;

    private readonly List<GameSession.SaveSlotInfo> cachedSaveSlots = new List<GameSession.SaveSlotInfo>();

    private void Awake()
    {
        Time.timeScale = 1f;
        PlayerMovement.RemoveMovementLock("Pause");
    }

    public void Play()
    {
        SetOperatorNameFromInput();
        GameSession.EnsureExists();
        GameSession.Instance.PrepareNewGame();

        Time.timeScale = 1f;
        PlayerMovement.RemoveMovementLock("Pause");
        LoadingScreen.skipNameEntry = !string.IsNullOrWhiteSpace(LoadingScreen.operatorName);
        LoadingScreen.nextSceneName = gameSceneName;
        SceneManager.LoadScene(loadingSceneName);
    }

    public void RefreshContinueSlots()
    {
        StartCoroutine(RefreshContinueSlotsRoutine());
    }

    public void ContinueFromSlotNumber(int slotNumber)
    {
        StartCoroutine(ContinueFromSlotNumberRoutine(slotNumber));
    }

    private IEnumerator RefreshContinueSlotsRoutine()
    {
        SetOperatorNameFromInput();
        GameSession.EnsureExists();

        List<GameSession.SaveSlotInfo> slots = null;
        string error = null;

        yield return GameSession.Instance.StartCoroutine(GameSession.Instance.ListSaveSlots((result, requestError) =>
        {
            slots = result;
            error = requestError;
        }));

        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogWarning($"[MainMenuUI] Unable to list save slots: {error}");
            yield break;
        }

        cachedSaveSlots.Clear();
        if (slots != null)
            cachedSaveSlots.AddRange(slots);
    }

    private IEnumerator ContinueFromSlotNumberRoutine(int slotNumber)
    {
        if (slotNumber < 1 || slotNumber > 5)
            yield break;

        SetOperatorNameFromInput();

        if (cachedSaveSlots.Count == 0)
            yield return StartCoroutine(RefreshContinueSlotsRoutine());

        GameSession.SaveSlotInfo slot = null;
        for (int index = 0; index < cachedSaveSlots.Count; index++)
        {
            GameSession.SaveSlotInfo candidate = cachedSaveSlots[index];
            if (candidate != null && candidate.slotNumber == slotNumber)
            {
                slot = candidate;
                break;
            }
        }

        if (slot == null || string.IsNullOrWhiteSpace(slot.id))
        {
            Debug.LogWarning($"[MainMenuUI] Slot {slotNumber} is empty.");
            yield break;
        }

        GameSession.SaveSlotDetail loadedSlot = null;
        string loadError = null;

        yield return GameSession.Instance.StartCoroutine(GameSession.Instance.LoadSaveSlot(slot.id, (result, error) =>
        {
            loadedSlot = result;
            loadError = error;
        }));

        if (!string.IsNullOrEmpty(loadError))
        {
            Debug.LogWarning($"[MainMenuUI] Unable to continue from slot {slotNumber}: {loadError}");
            yield break;
        }

        string targetScene = loadedSlot != null && !string.IsNullOrWhiteSpace(loadedSlot.currentScene)
            ? loadedSlot.currentScene
            : gameSceneName;

        Time.timeScale = 1f;
        PlayerMovement.RemoveMovementLock("Pause");
        LoadingScreen.skipNameEntry = true;
        LoadingScreen.nextSceneName = targetScene;
        SceneManager.LoadScene(loadingSceneName);
    }

    private void SetOperatorNameFromInput()
    {
        if (operatorNameInput == null)
            return;

        string candidate = operatorNameInput.text != null ? operatorNameInput.text.Trim() : string.Empty;
        if (!string.IsNullOrWhiteSpace(candidate))
            LoadingScreen.operatorName = candidate;
    }



    public void Quit()
    {
        Application.Quit();
    }
}
