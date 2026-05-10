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

    private const string DefaultApiBaseUrl = "http://localhost:4000/api";

    private static GameSession instance;

    [SerializeField] private string apiBaseUrl = DefaultApiBaseUrl;

    private string operatorName = string.Empty;
    private string playerId = string.Empty;
    private string activeSaveSlotId = string.Empty;
    private int activeSaveSlotNumber;
    private int currentCyberStatus = 50;
    private int currentTrustTokens;
    private PendingRestoreState pendingRestore;

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
    public bool HasPendingRestore => pendingRestore != null;
    public PendingRestoreState CurrentPendingRestore => pendingRestore;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureExists();
    }

    public static void EnsureExists()
    {
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
        SyncOperatorNameFromLoadingScreen();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode _mode)
    {
        SyncOperatorNameFromLoadingScreen();
        ApplyPendingRestore(scene);
    }

    public void SetCurrentStats(int cyberStatus, int trustTokens)
    {
        currentCyberStatus = Mathf.Clamp(cyberStatus, 0, 100);
        currentTrustTokens = Mathf.Max(0, trustTokens);
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
            currentCyberStatus = slot.currentCyberStatus;
            currentTrustTokens = slot.currentTrustTokens;
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
        currentCyberStatus = Mathf.Clamp(slot.currentCyberStatus, 0, 100);
        currentTrustTokens = Mathf.Max(0, slot.currentTrustTokens);

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

    public void ClearPendingRestore()
    {
        pendingRestore = null;
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
        currentCyberStatus = 50;
        currentTrustTokens = 0;
        pendingRestore = null;
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

        currentCyberStatus = Mathf.Clamp(pendingRestore.cyberStatus, 0, 100);
        currentTrustTokens = Mathf.Max(0, pendingRestore.trustTokens);

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

        return string.IsNullOrWhiteSpace(request.error) ? "Unable to reach the backend." : request.error;
    }
}