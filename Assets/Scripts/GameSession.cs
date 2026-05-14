using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class GameSession : MonoBehaviour
{
    [Serializable]
    public struct CyberStatusChange
    {
        public int previousValue;
        public int currentValue;
        public int delta;
        public string source;
        public string reason;
    }

    [Serializable]
    public struct TrustTokenChange
    {
        public int previousValue;
        public int currentValue;
        public int delta;
        public string source;
        public string reason;
    }

    [Serializable]
    public struct CyberStatusEffect
    {
        public int delta;
        public string source;
        public string reason;
    }

    [Serializable]
    public class SaveSlotInfo
    {
        public string id;
        public int slotNumber;
        public string slotName;
        public string currentScene;
        public string currentLocation;
        public int currentCyberStatus;
        public int currentTrustTokens;
        public string lastPlayedAt;
    }

    [Serializable]
    public class SaveSlotDetail
    {
        public string id;
        public int slotNumber;
        public string slotName;
        public string currentScene;
        public string currentLocation;
        public string currentScenarioId;
        public int currentCyberStatus;
        public int currentTrustTokens;
        public List<string> unlockedFlags;
        public Dictionary<string, object> sessionState;
        public string lastPlayedAt;
    }

    public class PendingRestoreState
    {
        public string sceneName;
        public int cyberStatus;
        public int trustTokens;
        public List<string> unlockedFlags;
        public Vector3? playerPosition;
    }

    [Serializable]
    private class PlayerResponse
    {
        public string id;
        public string operatorName;
    }

    [Serializable]
    public class SecurityReportDetail
    {
        public string id;
        public string saveSlotId;
        public string summary;
        public int finalCyberStatus;
        public int finalTrustTokens;
        public Dictionary<string, object> detailedJson;
    }

    [Serializable]
    private class SaveSnapshot
    {
        public string slotName;
        public string currentScene;
        public string currentLocation;
        public string currentScenarioId;
        public int currentCyberStatus;
        public int currentTrustTokens;
        public List<string> unlockedFlags;
        public Dictionary<string, object> sessionState;
    }

    [Serializable]
    public class StoryChoiceDetail
    {
        public string id;
        public string label;
        public int trustTokenCost;
        public string outcomeText;
    }

    [Serializable]
    public class StoryGateProgressDetail
    {
        public string groupKey;
        public int currentCount;
        public int requiredCount;
    }

    [Serializable]
    public class StoryNodeDetail
    {
        public string chapterKey;
        public string nodeKey;
        public string speaker;
        public string title;
        public string bodyText;
        public bool canContinue;
        public List<StoryChoiceDetail> choices;
        public StoryGateProgressDetail gateProgress;
        public int currentCyberStatus;
        public int currentTrustTokens;
        public List<string> unlockedFlags;
        public bool endChapter;
    }

    private const string DefaultApiBaseUrl = "http://localhost:4000/api";
    public const int CyberStatusStep = 5;
    public const int MaxCyberStatus = 100;
    public const int MinCyberStatus = 0;

    private static GameSession instance;

    [SerializeField] private string apiBaseUrl = DefaultApiBaseUrl;

    private string operatorName = string.Empty;
    private string playerId = string.Empty;
    private string activeSaveSlotId = string.Empty;
    private int activeSaveSlotNumber;
    private int currentCyberStatus = 50;
    private int currentTrustTokens;
    private PendingRestoreState pendingRestore;
    private bool forceFreshSaveSlot;
    private SecurityReportDetail latestSecurityReport;

    public static GameSession Instance
    {
        get
        {
            EnsureExists();
            return instance;
        }
    }

    public string OperatorName => operatorName;
    public string PlayerId => playerId;
    public string ActiveSaveSlotId => activeSaveSlotId;
    public int ActiveSaveSlotNumber => activeSaveSlotNumber;
    public int CurrentCyberStatus => currentCyberStatus;
    public int CurrentTrustTokens => currentTrustTokens;
    public bool HasActiveSaveSlot => !string.IsNullOrWhiteSpace(activeSaveSlotId);
    public bool HasPendingRestore => pendingRestore != null;
    public PendingRestoreState CurrentPendingRestore => pendingRestore;
    public SecurityReportDetail LatestSecurityReport => latestSecurityReport;
    public event Action<CyberStatusChange> CyberStatusChanged;
    public event Action<TrustTokenChange> TrustTokensChanged;

    public void PrepareNewGame()
    {
        SyncOperatorNameFromLoadingScreen();
        activeSaveSlotId = string.Empty;
        activeSaveSlotNumber = 0;
        pendingRestore = null;
        forceFreshSaveSlot = true;
        UpdateCurrentStats(50, 0, "Session", "NewGame");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        ConfigureRuntimeExecution();
        EnsureExists();
    }

    private static void ConfigureRuntimeExecution()
    {
        Application.runInBackground = true;
    }

    public static void EnsureExists()
    {
        ConfigureRuntimeExecution();

        if (instance != null)
            return;

        GameObject existing = GameObject.Find("GameSession");
        if (existing != null)
        {
            instance = existing.GetComponent<GameSession>();
            if (instance == null)
                instance = existing.AddComponent<GameSession>();
            return;
        }

        GameObject root = new GameObject("GameSession");
        instance = root.AddComponent<GameSession>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        ConfigureRuntimeExecution();
        SyncOperatorNameFromLoadingScreen();
    }

    private void OnEnable()
    {
        ConfigureRuntimeExecution();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode _mode)
    {
        ConfigureRuntimeExecution();
        SyncOperatorNameFromLoadingScreen();
        ApplyPendingRestore(scene);
    }

    private void Update()
    {
        ConfigureRuntimeExecution();
    }

    private bool UpdateCurrentStats(int cyberStatus, int trustTokens, string source, string reason)
    {
        int previousCyberStatus = currentCyberStatus;
        int previousTrustTokens = currentTrustTokens;
        int clampedCyberStatus = Mathf.Clamp(cyberStatus, MinCyberStatus, MaxCyberStatus);
        int clampedTrustTokens = Mathf.Max(0, trustTokens);

        currentCyberStatus = clampedCyberStatus;
        currentTrustTokens = clampedTrustTokens;

        bool cyberStatusChanged = previousCyberStatus != currentCyberStatus;
        bool trustTokensChanged = previousTrustTokens != currentTrustTokens;

        if (cyberStatusChanged)
        {
            CyberStatusChanged?.Invoke(new CyberStatusChange
            {
                previousValue = previousCyberStatus,
                currentValue = currentCyberStatus,
                delta = currentCyberStatus - previousCyberStatus,
                source = source ?? string.Empty,
                reason = reason ?? string.Empty
            });
        }

        if (trustTokensChanged)
        {
            TrustTokensChanged?.Invoke(new TrustTokenChange
            {
                previousValue = previousTrustTokens,
                currentValue = currentTrustTokens,
                delta = currentTrustTokens - previousTrustTokens,
                source = source ?? string.Empty,
                reason = reason ?? string.Empty
            });
        }

        return cyberStatusChanged || trustTokensChanged;
    }

    public void SetCurrentStats(int cyberStatus, int trustTokens)
    {
        UpdateCurrentStats(cyberStatus, trustTokens, null, null);
    }

    public bool SetCurrentCyberStatus(int cyberStatus, string source, string reason = null)
    {
        return UpdateCurrentStats(cyberStatus, currentTrustTokens, source, reason);
    }

    public bool ApplyCyberStatusDelta(int delta, string source, string reason = null)
    {
        if (delta != 0 && !IsCyberStatusStepAligned(delta))
            Debug.LogWarning($"[GameSession] Cyber status delta should be divisible by {CyberStatusStep}. Received {delta} from {source ?? "Unknown"}.");

        return SetCurrentCyberStatus(currentCyberStatus + delta, source, reason);
    }

    public bool ApplyCyberStatusEffect(CyberStatusEffect effect)
    {
        return ApplyCyberStatusDelta(effect.delta, effect.source, effect.reason);
    }

    public static bool IsCyberStatusStepAligned(int value)
    {
        return value % CyberStatusStep == 0;
    }

    public IEnumerator ListSaveSlots(Action<List<SaveSlotInfo>, string> onComplete)
    {
        string ensureError = null;
        yield return StartCoroutine(EnsurePlayerRegistered((error) => ensureError = error));

        if (!string.IsNullOrEmpty(ensureError))
        {
            onComplete?.Invoke(new List<SaveSlotInfo>(), ensureError);
            yield break;
        }

        string responseText = null;
        string requestError = null;
        yield return StartCoroutine(SendRequest("GET", $"{apiBaseUrl}/players/{playerId}/save-slots", null, (body, error) =>
        {
            responseText = body;
            requestError = error;
        }));

        if (!string.IsNullOrEmpty(requestError))
        {
            onComplete?.Invoke(new List<SaveSlotInfo>(), requestError);
            yield break;
        }

        List<SaveSlotInfo> slots = new List<SaveSlotInfo>();
        if (!string.IsNullOrWhiteSpace(responseText))
        {
            try
            {
                slots = JsonConvert.DeserializeObject<List<SaveSlotInfo>>(responseText) ?? new List<SaveSlotInfo>();
            }
            catch (JsonException exception)
            {
                onComplete?.Invoke(new List<SaveSlotInfo>(), $"Unable to read save slots: {exception.Message}");
                yield break;
            }
        }

        onComplete?.Invoke(slots, null);
    }

    public IEnumerator SaveToSlot(int slotNumber, string slotName, Action<SaveSlotInfo, string> onComplete)
    {
        string ensureError = null;
        yield return StartCoroutine(EnsurePlayerRegistered((error) => ensureError = error));

        if (!string.IsNullOrEmpty(ensureError))
        {
            onComplete?.Invoke(null, ensureError);
            yield break;
        }

        string effectiveSlotName = string.IsNullOrWhiteSpace(slotName)
            ? operatorName
            : slotName.Trim();

        SaveSnapshot snapshot = BuildSnapshot(effectiveSlotName);
        string requestBody = JsonConvert.SerializeObject(snapshot);
        string responseText = null;
        string requestError = null;

        yield return StartCoroutine(SendRequest(
            "PUT",
            $"{apiBaseUrl}/players/{playerId}/save-slots/{slotNumber}",
            requestBody,
            (body, error) =>
            {
                responseText = body;
                requestError = error;
            }));

        if (!string.IsNullOrEmpty(requestError))
        {
            onComplete?.Invoke(null, requestError);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(responseText))
        {
            onComplete?.Invoke(null, "Backend returned an empty save response.");
            yield break;
        }

        SaveSlotInfo slot;
        try
        {
            slot = JsonConvert.DeserializeObject<SaveSlotInfo>(responseText);
        }
        catch (JsonException exception)
        {
            onComplete?.Invoke(null, $"Unable to read save response: {exception.Message}");
            yield break;
        }

        if (slot == null || slot.slotNumber < 1)
        {
            onComplete?.Invoke(null, "Backend returned an invalid save slot.");
            yield break;
        }

        if (slot != null)
        {
            activeSaveSlotId = slot.id ?? string.Empty;
            activeSaveSlotNumber = slot.slotNumber;
            UpdateCurrentStats(slot.currentCyberStatus, slot.currentTrustTokens, "SaveSystem", "SaveSlotSync");
        }

        onComplete?.Invoke(slot, null);
    }

    public IEnumerator LoadSaveSlot(string saveSlotId, Action<SaveSlotDetail, string> onComplete)
    {
        if (string.IsNullOrWhiteSpace(saveSlotId))
        {
            onComplete?.Invoke(null, "Choose a valid save slot.");
            yield break;
        }

        string ensureError = null;
        yield return StartCoroutine(EnsurePlayerRegistered((error) => ensureError = error));

        if (!string.IsNullOrEmpty(ensureError))
        {
            onComplete?.Invoke(null, ensureError);
            yield break;
        }

        string responseText = null;
        string requestError = null;
        yield return StartCoroutine(SendRequest("GET", $"{apiBaseUrl}/save-slots/{saveSlotId}", null, (body, error) =>
        {
            responseText = body;
            requestError = error;
        }));

        if (!string.IsNullOrEmpty(requestError))
        {
            onComplete?.Invoke(null, requestError);
            yield break;
        }

        SaveSlotDetail slot = JsonConvert.DeserializeObject<SaveSlotDetail>(responseText);
        if (slot == null)
        {
            onComplete?.Invoke(null, "Unable to load save slot details.");
            yield break;
        }

        activeSaveSlotId = slot.id ?? string.Empty;
        activeSaveSlotNumber = slot.slotNumber;
        UpdateCurrentStats(slot.currentCyberStatus, slot.currentTrustTokens, "SaveSystem", "LoadSaveSlot");

        pendingRestore = new PendingRestoreState
        {
            sceneName = string.IsNullOrWhiteSpace(slot.currentScene) ? string.Empty : slot.currentScene,
            cyberStatus = currentCyberStatus,
            trustTokens = currentTrustTokens,
            unlockedFlags = slot.unlockedFlags != null ? new List<string>(slot.unlockedFlags) : new List<string>(),
            playerPosition = TryGetPlayerPosition(slot.sessionState)
        };

        onComplete?.Invoke(slot, null);
    }

    public IEnumerator GetCurrentStoryNode(string startNodeKey, Action<StoryNodeDetail, string> onComplete)
    {
        string saveSlotError = null;
        yield return StartCoroutine(EnsureActiveSaveSlot(error => saveSlotError = error));

        if (!string.IsNullOrEmpty(saveSlotError))
        {
            onComplete?.Invoke(null, saveSlotError);
            yield break;
        }

        string query = string.IsNullOrWhiteSpace(startNodeKey)
            ? string.Empty
            : $"?startNodeKey={UnityWebRequest.EscapeURL(startNodeKey.Trim())}";

        yield return StartCoroutine(RequestStoryNode(
            "GET",
            $"{apiBaseUrl}/save-slots/{activeSaveSlotId}/story{query}",
            null,
            onComplete));
    }

    public IEnumerator ContinueStoryNode(string nodeKey, Action<StoryNodeDetail, string> onComplete)
    {
        string saveSlotError = null;
        yield return StartCoroutine(EnsureActiveSaveSlot(error => saveSlotError = error));

        if (!string.IsNullOrEmpty(saveSlotError))
        {
            onComplete?.Invoke(null, saveSlotError);
            yield break;
        }

        string requestBody = JsonConvert.SerializeObject(new Dictionary<string, string>
        {
            { "nodeKey", nodeKey ?? string.Empty }
        });

        yield return StartCoroutine(RequestStoryNode(
            "POST",
            $"{apiBaseUrl}/save-slots/{activeSaveSlotId}/story/continue",
            requestBody,
            onComplete));
    }

    public IEnumerator SubmitStoryChoice(string nodeKey, string choiceId, Action<StoryNodeDetail, string> onComplete)
    {
        string saveSlotError = null;
        yield return StartCoroutine(EnsureActiveSaveSlot(error => saveSlotError = error));

        if (!string.IsNullOrEmpty(saveSlotError))
        {
            onComplete?.Invoke(null, saveSlotError);
            yield break;
        }

        string requestBody = JsonConvert.SerializeObject(new Dictionary<string, string>
        {
            { "nodeKey", nodeKey ?? string.Empty },
            { "choiceId", choiceId ?? string.Empty }
        });

        yield return StartCoroutine(RequestStoryNode(
            "POST",
            $"{apiBaseUrl}/save-slots/{activeSaveSlotId}/story/choices",
            requestBody,
            onComplete));
    }

    public IEnumerator RegisterStoryInteraction(string interactionId, string groupKey, Action<StoryNodeDetail, string> onComplete)
    {
        string saveSlotError = null;
        yield return StartCoroutine(EnsureActiveSaveSlot(error => saveSlotError = error));

        if (!string.IsNullOrEmpty(saveSlotError))
        {
            onComplete?.Invoke(null, saveSlotError);
            yield break;
        }

        string requestBody = JsonConvert.SerializeObject(new Dictionary<string, string>
        {
            { "interactionId", interactionId ?? string.Empty },
            { "groupKey", groupKey ?? string.Empty }
        });

        yield return StartCoroutine(RequestStoryNode(
            "POST",
            $"{apiBaseUrl}/save-slots/{activeSaveSlotId}/story/interactions",
            requestBody,
            onComplete));
    }

    public IEnumerator GenerateSecurityReport(Action<SecurityReportDetail, string> onComplete)
    {
        string saveSlotError = null;
        yield return StartCoroutine(EnsureActiveSaveSlot(error => saveSlotError = error));

        if (!string.IsNullOrEmpty(saveSlotError))
        {
            onComplete?.Invoke(null, saveSlotError);
            yield break;
        }

        string requestBody = JsonConvert.SerializeObject(new Dictionary<string, string>
        {
            { "saveSlotId", activeSaveSlotId }
        });

        string responseText = null;
        string requestError = null;
        yield return StartCoroutine(SendRequest("POST", $"{apiBaseUrl}/reports", requestBody, (body, error) =>
        {
            responseText = body;
            requestError = error;
        }));

        if (!string.IsNullOrEmpty(requestError))
        {
            onComplete?.Invoke(null, requestError);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(responseText))
        {
            onComplete?.Invoke(null, "Backend returned an empty report response.");
            yield break;
        }

        SecurityReportDetail report;
        try
        {
            report = JsonConvert.DeserializeObject<SecurityReportDetail>(responseText);
        }
        catch (JsonException exception)
        {
            onComplete?.Invoke(null, $"Unable to read security report: {exception.Message}");
            yield break;
        }

        if (report == null || string.IsNullOrWhiteSpace(report.id))
        {
            onComplete?.Invoke(null, "Backend returned an invalid security report.");
            yield break;
        }

        latestSecurityReport = report;
        onComplete?.Invoke(report, null);
    }

    public void ClearPendingRestore()
    {
        pendingRestore = null;
    }

    private IEnumerator EnsureActiveSaveSlot(Action<string> onComplete)
    {
        if (HasActiveSaveSlot)
        {
            onComplete?.Invoke(null);
            yield break;
        }

        if (forceFreshSaveSlot)
        {
            string freshCreateError = null;
            yield return StartCoroutine(SaveToSlot(1, operatorName, (_slot, error) => freshCreateError = error));
            forceFreshSaveSlot = false;
            onComplete?.Invoke(freshCreateError);
            yield break;
        }

        List<SaveSlotInfo> slots = null;
        string slotsError = null;
        yield return StartCoroutine(ListSaveSlots((availableSlots, error) =>
        {
            slots = availableSlots;
            slotsError = error;
        }));

        if (!string.IsNullOrEmpty(slotsError))
        {
            onComplete?.Invoke(slotsError);
            yield break;
        }

        SaveSlotInfo existingSlot = null;
        if (slots != null && slots.Count > 0)
        {
            existingSlot = slots[0];
            for (int index = 1; index < slots.Count; index++)
            {
                SaveSlotInfo candidate = slots[index];
                if (candidate != null && (existingSlot == null || candidate.slotNumber < existingSlot.slotNumber))
                    existingSlot = candidate;
            }
        }

        if (existingSlot != null && !string.IsNullOrWhiteSpace(existingSlot.id))
        {
            string loadError = null;
            yield return StartCoroutine(LoadSaveSlot(existingSlot.id, (_slot, error) => loadError = error));
            onComplete?.Invoke(loadError);
            yield break;
        }

        string createError = null;
        yield return StartCoroutine(SaveToSlot(1, operatorName, (_slot, error) => createError = error));
        onComplete?.Invoke(createError);
    }

    private IEnumerator EnsurePlayerRegistered(Action<string> onComplete)
    {
        SyncOperatorNameFromLoadingScreen();

        if (!string.IsNullOrWhiteSpace(playerId))
        {
            onComplete?.Invoke(null);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(operatorName))
        {
            onComplete?.Invoke("Enter an operator name before saving.");
            yield break;
        }

        string requestBody = JsonConvert.SerializeObject(new Dictionary<string, string>
        {
            { "operatorName", operatorName }
        });

        string responseText = null;
        string requestError = null;
        yield return StartCoroutine(SendRequest("POST", $"{apiBaseUrl}/players", requestBody, (body, error) =>
        {
            responseText = body;
            requestError = error;
        }));

        if (!string.IsNullOrEmpty(requestError))
        {
            onComplete?.Invoke(requestError);
            yield break;
        }

        PlayerResponse response = JsonConvert.DeserializeObject<PlayerResponse>(responseText);
        if (response == null || string.IsNullOrWhiteSpace(response.id))
        {
            onComplete?.Invoke("Backend did not return a player id.");
            yield break;
        }

        playerId = response.id;
        onComplete?.Invoke(null);
    }

    private void SyncOperatorNameFromLoadingScreen()
    {
        string latestOperatorName = string.IsNullOrWhiteSpace(LoadingScreen.operatorName)
            ? string.Empty
            : LoadingScreen.operatorName.Trim();

        if (string.IsNullOrWhiteSpace(latestOperatorName))
            return;

        if (string.Equals(operatorName, latestOperatorName, StringComparison.Ordinal))
            return;

        operatorName = latestOperatorName;
        playerId = string.Empty;
        activeSaveSlotId = string.Empty;
        activeSaveSlotNumber = 0;
        UpdateCurrentStats(50, 0, "Session", "OperatorSync");
        pendingRestore = null;
        forceFreshSaveSlot = false;
    }

    private SaveSnapshot BuildSnapshot(string slotName)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        Transform playerTransform = FindPlayerTransform();
        EventManager eventManager = EventManager.Instance;
        List<string> unlockedFlags = new List<string>();
        string activeEventId = null;
        string activeEventTitle = null;
        bool isEventActive = false;

        if (eventManager != null)
        {
            unlockedFlags.AddRange(eventManager.UnlockedFlags);
            activeEventId = eventManager.CurrentEventId;
            activeEventTitle = eventManager.CurrentEventTitle;
            isEventActive = eventManager.IsEventActive;
        }

        string currentLocation = BuildLocationLabel(playerTransform);
        Dictionary<string, object> sessionState = new Dictionary<string, object>
        {
            { "savedAtUtc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) },
            { "operatorName", operatorName },
            { "unityScene", activeScene.name },
            { "localEventId", activeEventId },
            { "localEventTitle", activeEventTitle },
            { "isEventActive", isEventActive },
            { "playerPosition", playerTransform != null ? new Dictionary<string, float>
                {
                    { "x", playerTransform.position.x },
                    { "y", playerTransform.position.y },
                    { "z", playerTransform.position.z }
                } : null }
        };

        return new SaveSnapshot
        {
            slotName = slotName,
            currentScene = activeScene.name,
            currentLocation = currentLocation,
            currentScenarioId = null,
            currentCyberStatus = currentCyberStatus,
            currentTrustTokens = currentTrustTokens,
            unlockedFlags = unlockedFlags,
            sessionState = sessionState
        };
    }

    private static Transform FindPlayerTransform()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.transform : null;
    }

    private static string BuildLocationLabel(Transform playerTransform)
    {
        if (playerTransform == null)
            return SceneManager.GetActiveScene().name;

        Vector3 position = playerTransform.position;
        return $"{SceneManager.GetActiveScene().name} ({position.x:F1}, {position.y:F1})";
    }

    private void ApplyPendingRestore(Scene loadedScene)
    {
        if (pendingRestore == null)
            return;

        if (!string.IsNullOrWhiteSpace(pendingRestore.sceneName)
            && !string.Equals(loadedScene.name, pendingRestore.sceneName, StringComparison.Ordinal))
            return;

        UpdateCurrentStats(pendingRestore.cyberStatus, pendingRestore.trustTokens, "SaveSystem", "PendingRestore");

        EventManager eventManager = EventManager.Instance;
        if (eventManager != null)
            eventManager.RestoreUnlockedFlags(pendingRestore.unlockedFlags);

        if (pendingRestore.playerPosition.HasValue)
            PlayerMovement.ApplySavedPositionOnce(pendingRestore.playerPosition.Value);

        pendingRestore = null;
    }

    private static Vector3? TryGetPlayerPosition(Dictionary<string, object> sessionState)
    {
        if (sessionState == null || !sessionState.TryGetValue("playerPosition", out object rawPosition) || rawPosition == null)
            return null;

        if (!(rawPosition is JObject positionObject))
            return null;

        bool hasX = TryGetFloat(positionObject, "x", out float x);
        bool hasY = TryGetFloat(positionObject, "y", out float y);
        if (!hasX || !hasY)
            return null;

        float z = 0f;
        TryGetFloat(positionObject, "z", out z);
        return new Vector3(x, y, z);
    }

    private IEnumerator RequestStoryNode(string method, string url, string jsonBody, Action<StoryNodeDetail, string> onComplete)
    {
        string responseText = null;
        string requestError = null;

        yield return StartCoroutine(SendRequest(method, url, jsonBody, (body, error) =>
        {
            responseText = body;
            requestError = error;
        }));

        if (!string.IsNullOrEmpty(requestError))
        {
            onComplete?.Invoke(null, requestError);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(responseText))
        {
            onComplete?.Invoke(null, "Backend returned an empty story response.");
            yield break;
        }

        StoryNodeDetail node;
        try
        {
            node = JsonConvert.DeserializeObject<StoryNodeDetail>(responseText);
        }
        catch (JsonException exception)
        {
            onComplete?.Invoke(null, $"Unable to read story response: {exception.Message}");
            yield break;
        }

        if (node == null || string.IsNullOrWhiteSpace(node.nodeKey))
        {
            onComplete?.Invoke(null, "Backend returned an invalid story node.");
            yield break;
        }

        ApplyStoryNodeState(node);
        onComplete?.Invoke(node, null);
    }

    private void ApplyStoryNodeState(StoryNodeDetail node)
    {
        if (node == null)
            return;

        UpdateCurrentStats(node.currentCyberStatus, node.currentTrustTokens, "StorySystem", node.nodeKey);

        EventManager eventManager = EventManager.Instance;
        if (eventManager != null)
            eventManager.RestoreUnlockedFlags(node.unlockedFlags);
    }

    private static bool TryGetFloat(JObject source, string key, out float value)
    {
        value = 0f;
        if (source == null || string.IsNullOrEmpty(key) || !source.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken token))
            return false;

        switch (token.Type)
        {
            case JTokenType.Float:
            case JTokenType.Integer:
                value = token.ToObject<float>();
                return true;
            case JTokenType.String:
                return float.TryParse(token.ToObject<string>(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            default:
                return false;
        }
    }

    private IEnumerator SendRequest(string method, string url, string jsonBody, Action<string, string> onComplete)
    {
        using (UnityWebRequest request = new UnityWebRequest(url, method))
        {
            request.downloadHandler = new DownloadHandlerBuffer();

            if (!string.IsNullOrEmpty(jsonBody))
            {
                byte[] payload = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(payload);
                request.SetRequestHeader("Content-Type", "application/json");
            }

            request.SetRequestHeader("Accept", "application/json");

            yield return request.SendWebRequest();

            bool isSuccess = request.result != UnityWebRequest.Result.ConnectionError
                && request.result != UnityWebRequest.Result.DataProcessingError
                && request.responseCode >= 200
                && request.responseCode < 300;

            if (!isSuccess)
            {
                onComplete?.Invoke(null, ExtractError(request));
                yield break;
            }

            onComplete?.Invoke(request.downloadHandler.text, null);
        }
    }

    private static string ExtractError(UnityWebRequest request)
    {
        if (request == null)
            return $"Unable to reach the backend at {DefaultApiBaseUrl}. Start the backend server and try again.";

        if (request.result == UnityWebRequest.Result.ConnectionError)
        {
            return $"Unable to reach the backend at {DefaultApiBaseUrl}. Start the backend server on port 4000 and try again.";
        }

        string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                Dictionary<string, object> errorPayload = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);
                if (errorPayload != null && errorPayload.TryGetValue("message", out object message) && message != null)
                    return message.ToString();
            }
            catch (Exception)
            {
                // Ignore JSON parse failures and fall back to the raw body below.
            }

            return body;
        }

        return string.IsNullOrWhiteSpace(request.error)
            ? $"Unable to reach the backend at {DefaultApiBaseUrl}."
            : request.error;
    }
}