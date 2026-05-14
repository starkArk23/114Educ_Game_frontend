using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RuntimeSceneTransition : MonoBehaviour
{
    private const string MovementLockId = "SceneTransition";
    private const float TransitionFailSafePaddingSeconds = 1f;

    private static RuntimeSceneTransition instance;
    private static string pendingSpawnPointId;
    private static string latestArrivalSceneName = string.Empty;
    private static string latestArrivalSpawnPointId = string.Empty;

    [SerializeField] private float fadeOutDuration = 0.35f;
    [SerializeField] private float blackPauseDuration = 0.1f;
    [SerializeField] private float fadeInDuration = 0.35f;
    [SerializeField] private Color fadeColor = Color.black;

    private Canvas transitionCanvas;
    private Image fadeImage;
    private bool isTransitioning;
    private string transitionTargetSceneName = string.Empty;
    private float transitionStartedAt;
    private Coroutine activeTransitionRoutine;
    private Coroutine spawnReapplyRoutine;
    private Coroutine transitionFailSafeRoutine;

    public static bool IsTransitioning => instance != null && instance.isTransitioning;

    public static bool ConsumeLatestArrival(string sceneName, string spawnPointId = null)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        string expectedSceneName = sceneName.Trim();
        if (!string.Equals(latestArrivalSceneName, expectedSceneName, StringComparison.Ordinal))
            return false;

        if (!string.IsNullOrWhiteSpace(spawnPointId)
            && !string.Equals(latestArrivalSpawnPointId, spawnPointId.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        latestArrivalSceneName = string.Empty;
        latestArrivalSpawnPointId = string.Empty;
        return true;
    }

    public static void TransitionTo(string sceneName, string spawnPointId)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[RuntimeSceneTransition] Target scene name is required.");
            return;
        }

        EnsureInstance();
        if (!instance.PrepareForTransition(sceneName.Trim()))
            return;

        instance.activeTransitionRoutine = instance.StartCoroutine(instance.TransitionRoutine(sceneName.Trim(), spawnPointId));
    }

    public static void TransitionWithWakeBlink(string sceneName, string spawnPointId, int blinkCount = 2)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[RuntimeSceneTransition] Target scene name is required.");
            return;
        }

        EnsureInstance();
        if (!instance.PrepareForTransition(sceneName.Trim()))
            return;

        instance.activeTransitionRoutine = instance.StartCoroutine(instance.WakeTransitionRoutine(sceneName.Trim(), spawnPointId, Mathf.Max(1, blinkCount)));
    }

    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject("RuntimeSceneTransition");
        instance = root.AddComponent<RuntimeSceneTransition>();
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
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureOverlay();
        SetOverlayAlpha(0f);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }
    }

    private IEnumerator TransitionRoutine(string sceneName, string spawnPointId)
    {
        if (isTransitioning)
            yield break;

        isTransitioning = true;
        transitionTargetSceneName = sceneName;
        transitionStartedAt = Time.unscaledTime;
        pendingSpawnPointId = string.IsNullOrWhiteSpace(spawnPointId) ? string.Empty : spawnPointId.Trim();
        StartTransitionFailSafe(sceneName);

        PlayerMovement.AddMovementLock(MovementLockId);
        yield return FadeOverlay(0f, 1f, fadeOutDuration);

        if (blackPauseDuration > 0f)
            yield return new WaitForSecondsRealtime(blackPauseDuration);

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        while (!operation.isDone)
            yield return null;

        yield return null;
        yield return FadeOverlay(1f, 0f, fadeInDuration);
        PlayerMovement.RemoveMovementLock(MovementLockId);
        activeTransitionRoutine = null;
        CompleteTransition();
    }

    private IEnumerator WakeTransitionRoutine(string sceneName, string spawnPointId, int blinkCount)
    {
        if (isTransitioning)
            yield break;

        isTransitioning = true;
        transitionTargetSceneName = sceneName;
        transitionStartedAt = Time.unscaledTime;
        pendingSpawnPointId = string.IsNullOrWhiteSpace(spawnPointId) ? string.Empty : spawnPointId.Trim();
        StartTransitionFailSafe(sceneName);

        PlayerMovement.AddMovementLock(MovementLockId);

        for (int blinkIndex = 0; blinkIndex < blinkCount; blinkIndex++)
        {
            yield return FadeOverlay(0f, 1f, fadeOutDuration * 0.35f);
            yield return FadeOverlay(1f, 0f, fadeInDuration * 0.2f);
        }

        yield return FadeOverlay(0f, 1f, fadeOutDuration);

        if (blackPauseDuration > 0f)
            yield return new WaitForSecondsRealtime(blackPauseDuration);

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        while (!operation.isDone)
            yield return null;

        yield return null;
        yield return FadeOverlay(1f, 0f, fadeInDuration);
        PlayerMovement.RemoveMovementLock(MovementLockId);
        activeTransitionRoutine = null;
        CompleteTransition();
    }

    private bool PrepareForTransition(string sceneName)
    {
        if (!isTransitioning)
            return true;

        if (!IsTransitionStale())
            return false;

        Debug.LogWarning($"[RuntimeSceneTransition] Resetting stale transition to '{transitionTargetSceneName}' before starting '{sceneName}'.");
        ResetTransitionState();
        return true;
    }

    private void StartTransitionFailSafe(string sceneName)
    {
        StopTransitionFailSafe();
        transitionFailSafeRoutine = StartCoroutine(TransitionFailSafeRoutine(sceneName));
    }

    private void StopTransitionFailSafe()
    {
        if (transitionFailSafeRoutine != null)
        {
            StopCoroutine(transitionFailSafeRoutine);
            transitionFailSafeRoutine = null;
        }
    }

    private IEnumerator TransitionFailSafeRoutine(string sceneName)
    {
        float timeoutSeconds = fadeOutDuration + blackPauseDuration + fadeInDuration + TransitionFailSafePaddingSeconds;
        yield return new WaitForSecondsRealtime(Mathf.Max(1f, timeoutSeconds));

        if (!isTransitioning)
        {
            transitionFailSafeRoutine = null;
            yield break;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!string.Equals(activeScene.name, sceneName, StringComparison.Ordinal))
        {
            Debug.LogWarning($"[RuntimeSceneTransition] Transition to '{sceneName}' stalled; forcing scene load.");
            SceneManager.LoadScene(sceneName);
            yield return null;
        }

        SetOverlayAlpha(0f);
        PlayerMovement.RemoveMovementLock(MovementLockId);
        CompleteTransition();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode _mode)
    {
        PlayerMovement.ResetMovementState(MovementLockId);

        if (isTransitioning
            && !string.IsNullOrWhiteSpace(transitionTargetSceneName)
            && !string.Equals(scene.name, transitionTargetSceneName, StringComparison.Ordinal))
        {
            Debug.LogWarning($"[RuntimeSceneTransition] Scene '{scene.name}' loaded while transitioning to '{transitionTargetSceneName}'. Resetting interrupted transition state.");
            ResetTransitionState();
            return;
        }

        if (isTransitioning)
        {
            latestArrivalSceneName = scene.name;
            latestArrivalSpawnPointId = string.IsNullOrWhiteSpace(pendingSpawnPointId)
                ? string.Empty
                : pendingSpawnPointId.Trim();
        }

        if (string.IsNullOrWhiteSpace(pendingSpawnPointId))
            return;

        SceneSpawnPoint[] spawnPoints = FindObjectsByType<SceneSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int index = 0; index < spawnPoints.Length; index++)
        {
            SceneSpawnPoint spawnPoint = spawnPoints[index];
            if (spawnPoint == null || !spawnPoint.Matches(pendingSpawnPointId))
                continue;

            Vector3 spawnPosition = spawnPoint.transform.position;
            PlayerMovement.ApplySavedPositionOnce(spawnPosition);
            ApplySpawn(spawnPosition);

            if (spawnReapplyRoutine != null)
                StopCoroutine(spawnReapplyRoutine);

            spawnReapplyRoutine = StartCoroutine(ReapplySpawnRoutine(spawnPosition));
            pendingSpawnPointId = string.Empty;
            return;
        }

        if (isTransitioning)
            Debug.LogWarning($"[RuntimeSceneTransition] No SceneSpawnPoint with id '{pendingSpawnPointId}' was found in scene '{scene.name}'.");
        pendingSpawnPointId = string.Empty;
    }

    private bool IsTransitionStale()
    {
        float timeoutSeconds = fadeOutDuration + blackPauseDuration + fadeInDuration + TransitionFailSafePaddingSeconds;
        return Time.unscaledTime - transitionStartedAt >= Mathf.Max(1f, timeoutSeconds);
    }

    private void CompleteTransition()
    {
        StopTransitionFailSafe();
        isTransitioning = false;
        transitionTargetSceneName = string.Empty;
        transitionStartedAt = 0f;
    }

    private void ResetTransitionState()
    {
        StopTransitionFailSafe();
        if (activeTransitionRoutine != null)
        {
            StopCoroutine(activeTransitionRoutine);
            activeTransitionRoutine = null;
        }

        if (spawnReapplyRoutine != null)
        {
            StopCoroutine(spawnReapplyRoutine);
            spawnReapplyRoutine = null;
        }

        pendingSpawnPointId = string.Empty;
        SetOverlayAlpha(0f);
        PlayerMovement.RemoveMovementLock(MovementLockId);
        latestArrivalSceneName = string.Empty;
        latestArrivalSpawnPointId = string.Empty;
        isTransitioning = false;
        transitionTargetSceneName = string.Empty;
        transitionStartedAt = 0f;
    }

    private static void ApplySpawn(Vector3 worldPosition)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            PlayerMovement.ApplySavedPositionOnce(worldPosition);
            return;
        }

        player.transform.position = worldPosition;

        Rigidbody2D rigidbody2d = player.GetComponent<Rigidbody2D>();
        if (rigidbody2d != null)
        {
            rigidbody2d.position = new Vector2(worldPosition.x, worldPosition.y);
            rigidbody2d.linearVelocity = Vector2.zero;
            rigidbody2d.angularVelocity = 0f;
        }
    }

    private IEnumerator ReapplySpawnRoutine(Vector3 worldPosition)
    {
        for (int frame = 0; frame < 4; frame++)
        {
            yield return null;
            ApplySpawn(worldPosition);
        }

        spawnReapplyRoutine = null;
    }

    private void EnsureOverlay()
    {
        if (transitionCanvas != null && fadeImage != null)
            return;

        GameObject canvasObject = new GameObject("TransitionCanvas");
        canvasObject.transform.SetParent(transform, false);

        transitionCanvas = canvasObject.AddComponent<Canvas>();
        transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        transitionCanvas.sortingOrder = short.MaxValue;

        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject imageObject = new GameObject("FadeOverlay");
        imageObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rectTransform = imageObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        fadeImage = imageObject.AddComponent<Image>();
        fadeImage.color = fadeColor;
        fadeImage.raycastTarget = true;
    }

    private IEnumerator FadeOverlay(float from, float to, float duration)
    {
        EnsureOverlay();

        if (duration <= 0f)
        {
            SetOverlayAlpha(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetOverlayAlpha(Mathf.Lerp(from, to, t));
            yield return null;
        }

        SetOverlayAlpha(to);
    }

    private void SetOverlayAlpha(float alpha)
    {
        EnsureOverlay();
        Color color = fadeColor;
        color.a = Mathf.Clamp01(alpha);
        fadeImage.color = color;
        fadeImage.enabled = color.a > 0.001f;
    }
}