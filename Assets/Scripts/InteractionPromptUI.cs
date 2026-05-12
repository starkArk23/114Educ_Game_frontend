using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class InteractionPromptUI : MonoBehaviour
{
    private static InteractionPromptUI instance;

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text promptText;

    public static InteractionPromptUI ResolveOrCreate()
    {
        if (instance != null)
            return instance;

        instance = FindFirstObjectByType<InteractionPromptUI>(FindObjectsInactive.Include);
        if (instance != null)
            return instance;

        return CreateRuntimeUi();
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
        Hide();
    }

    public void Show(string message)
    {
        if (promptText == null)
            return;

        promptText.text = message ?? string.Empty;
        SetVisible(!string.IsNullOrWhiteSpace(promptText.text));
    }

    public void Hide()
    {
        if (promptText != null)
            promptText.text = string.Empty;

        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        if (promptText != null)
            promptText.enabled = visible;
    }

    private static InteractionPromptUI CreateRuntimeUi()
    {
        GameObject root = new GameObject("InteractionPromptCanvas");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();
        CanvasGroup rootGroup = root.AddComponent<CanvasGroup>();

        GameObject panelObject = new GameObject("InteractionPromptPanel");
        panelObject.transform.SetParent(root.transform, false);
        RectTransform panelRect = panelObject.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 72f);
        panelRect.sizeDelta = new Vector2(360f, 64f);

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0.05f, 0.07f, 0.1f, 0.9f);

        GameObject textObject = new GameObject("InteractionPromptText");
        textObject.transform.SetParent(panelObject.transform, false);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(20f, 10f);
        textRect.offsetMax = new Vector2(-20f, -10f);

        TextMeshProUGUI textComponent = textObject.AddComponent<TextMeshProUGUI>();
        textComponent.alignment = TextAlignmentOptions.Center;
        textComponent.fontSize = 24f;
        textComponent.color = Color.white;
        textComponent.text = string.Empty;
        if (TMP_Settings.defaultFontAsset != null)
            textComponent.font = TMP_Settings.defaultFontAsset;

        InteractionPromptUI promptUi = root.AddComponent<InteractionPromptUI>();
        promptUi.canvasGroup = rootGroup;
        promptUi.promptText = textComponent;
        promptUi.Hide();
        return promptUi;
    }
}