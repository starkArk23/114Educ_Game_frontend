using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class LoadingScreen : MonoBehaviour
{
    public static string nextSceneName;
    public static string operatorName;
    
    [SerializeField] private Slider progressBar; // optional
    [SerializeField] private CanvasGroup loadingGroup;
    [SerializeField] private TMP_Text terminalText;
    [SerializeField] private Image fadeOverlay;
    [SerializeField] private float typeSpeed = 0.035f;
    [SerializeField] private float typeJitter = 0.02f;
    [SerializeField] private float linePause = 0.35f;
    [SerializeField] private float punctuationPause = 0.12f;
    [SerializeField] private float wordPauseChance = 0.35f;
    [SerializeField] private float wordPauseDuration = 0.18f;
    [SerializeField] private float holdAfterTyping = 0.4f;
    [SerializeField] private float fadeOutDuration = 5f;
    [SerializeField] private float textFadeOutMultiplier = 0.85f;
    [SerializeField] private float blackPauseDuration = 0.5f;
    [SerializeField] private float fadeInDuration = 2.75f;
    [SerializeField] private float promptBlinkInterval = 0.5f;
    [TextArea(3, 10)]
    [SerializeField] private string postNameScript =
        ">> OPERATOR VERIFIED: [PLAYER NAME]\n" +
        ">> INITIAL CYBERSTATUS: STABLE\n" +
        ">> SYSTEM READY\n" +
        "CONTROL TRANSFERRED. AWAITING OPERATOR ACTION...\n\n\n" +
        "YOU ARE NOW RESPONSIBLE.";
    [TextArea(3, 10)]
    [SerializeField] private string terminalScript =
        "INITIALIZING KERNEL...\n\n" +
        "LOADING SECURITY_PROTOCOL_V1.0.\n\n" +
        "CHECKING AUTHORIZATION...\n\n" +
        ">> PROTOCOL IDENTIFICATION REQUIRED.\n\n" +
        ">> INPUT OPERATOR NAME";

    private static LoadingScreen instance;
    private bool typingComplete;
    private GameObject loadingCanvasRoot;
    private bool waitingForName;
    private bool nameSubmitted;
    private string terminalBaseText = string.Empty;
    private float promptBlinkTimer;
    private bool promptVisible;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        if (loadingGroup != null)
        {
            loadingCanvasRoot = loadingGroup.transform.root.gameObject;
            DontDestroyOnLoad(loadingCanvasRoot);
        }
    }

    private void Start()
    {
        if (fadeOverlay != null)
        {
            SetOverlayAlpha(0f);
        }

        if (terminalText != null)
            terminalText.text = string.Empty;

        StartCoroutine(LoadAsync());
    }

    private void Update()
    {
        if (!waitingForName || terminalText == null || nameSubmitted)
            return;

        HandleNameInput();
        UpdatePromptLine();
    }

    private IEnumerator LoadAsync()
    {
        if (string.IsNullOrEmpty(nextSceneName))
            nextSceneName = "GameScene";

        AsyncOperation op = SceneManager.LoadSceneAsync(nextSceneName);
        op.allowSceneActivation = false;

        if (terminalText != null)
            StartCoroutine(TypeTerminal());
        else
            typingComplete = true;

        while (op.progress < 0.9f)
        {
            float p = Mathf.Clamp01(op.progress / 0.9f);
            if (progressBar != null) progressBar.value = p;
            yield return null;
        }

        if (progressBar != null) progressBar.value = 1f;

        yield return new WaitUntil(() => typingComplete);
        if (terminalText != null)
            yield return new WaitUntil(() => nameSubmitted);
        if (terminalText != null)
            yield return StartCoroutine(TypePostNameScript());
        yield return new WaitForSeconds(holdAfterTyping);

        // 1) fade to black and fade loading UI out underneath
        float uiFadeDuration = fadeOutDuration;
        if (textFadeOutMultiplier > 0f)
            uiFadeDuration = fadeOutDuration * Mathf.Clamp01(textFadeOutMultiplier);

        Coroutine uiFade = StartCoroutine(FadeCanvasGroup(loadingGroup, 1f, 0f, uiFadeDuration));
        yield return StartCoroutine(FadeOverlay(0f, 1f, fadeOutDuration));
        if (uiFade != null)
            yield return uiFade;

        // 3) hold on black
        if (blackPauseDuration > 0f)
            yield return new WaitForSeconds(blackPauseDuration);

        op.allowSceneActivation = true;
        yield return new WaitUntil(() => op.isDone);
        yield return null; // let the new scene render a frame under the overlay

        yield return StartCoroutine(FadeOverlay(1f, 0f, fadeInDuration));

        if (loadingCanvasRoot != null)
            Destroy(loadingCanvasRoot);

        Destroy(gameObject);
    }

    private IEnumerator TypeTerminal()
    {
        typingComplete = false;

        for (int i = 0; i < terminalScript.Length; i++)
        {
            char c = terminalScript[i];
            terminalText.text = terminalScript.Substring(0, i + 1);

            if (c == '\n')
            {
                yield return new WaitForSeconds(linePause);
                continue;
            }

            yield return new WaitForSeconds(GetTypeDelay(c));
        }

        typingComplete = true;
        BeginNameEntry();
    }

    private void BeginNameEntry()
    {
        waitingForName = true;
        nameSubmitted = false;
        promptBlinkTimer = 0f;
        promptVisible = true;
        terminalBaseText = terminalText.text.TrimEnd('\r', '\n');
        UpdatePromptLine();
    }

    private void HandleNameInput()
    {
        string input = Input.inputString;
        if (string.IsNullOrEmpty(input))
            return;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '\b')
            {
                if (!string.IsNullOrEmpty(operatorName))
                    operatorName = operatorName.Substring(0, operatorName.Length - 1);
                continue;
            }

            if (c == '\n' || c == '\r')
            {
                if (!string.IsNullOrEmpty(operatorName))
                    nameSubmitted = true;
                continue;
            }

            operatorName += c;
        }
    }

    private void UpdatePromptLine()
    {
        if (nameSubmitted)
            return;

        if (string.IsNullOrEmpty(operatorName))
        {
            promptBlinkTimer += Time.unscaledDeltaTime;
            if (promptBlinkInterval > 0f && promptBlinkTimer >= promptBlinkInterval)
            {
                promptBlinkTimer = 0f;
                promptVisible = !promptVisible;
            }
        }
        else
        {
            promptVisible = false;
        }

        string promptLine = string.IsNullOrEmpty(operatorName)
            ? (promptVisible ? "." : "")
            : operatorName;

        terminalText.text = terminalBaseText + "\n\n" + promptLine;
    }

    private IEnumerator TypePostNameScript()
    {
        waitingForName = false;

        string name = string.IsNullOrEmpty(operatorName) ? "UNKNOWN" : operatorName;
        terminalBaseText = terminalBaseText + "\n\n" + name;
        terminalText.text = terminalBaseText;

        if (string.IsNullOrEmpty(postNameScript))
            yield break;

        string script = postNameScript.Replace("[PLAYER NAME]", name);
        string typed = string.Empty;

        for (int i = 0; i < script.Length; i++)
        {
            char c = script[i];
            typed += c;
            terminalText.text = terminalBaseText + "\n\n" + typed;

            if (c == '\n')
            {
                yield return new WaitForSeconds(linePause);
                continue;
            }

            yield return new WaitForSeconds(GetTypeDelay(c));
        }
    }

    private float GetTypeDelay(char c)
    {
        float delay = typeSpeed;
        if (typeJitter > 0f)
            delay += Random.Range(-typeJitter, typeJitter);

        if (c == '.' || c == '!' || c == '?')
            delay += punctuationPause;

        if (c == ' ' && Random.value < wordPauseChance)
            delay += wordPauseDuration;

        return Mathf.Max(0.005f, delay);
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null || duration <= 0f)
            yield break;

        float elapsed = 0f;
        group.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        group.alpha = to;
        group.interactable = to > 0.5f;
        group.blocksRaycasts = to > 0.5f;
    }

    private IEnumerator FadeOverlay(float from, float to, float duration)
    {
        if (fadeOverlay == null || duration <= 0f)
            yield break;

        float elapsed = 0f;
        SetOverlayAlpha(from);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetOverlayAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetOverlayAlpha(to);
    }

    private void SetOverlayAlpha(float alpha)
    {
        if (fadeOverlay == null)
            return;

        fadeOverlay.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
    }
}
