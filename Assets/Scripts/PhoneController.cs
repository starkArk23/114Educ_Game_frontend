using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PhoneController : MonoBehaviour
{
    private const string MovementLockId = "PhoneController";
    private const string OpeningPhoneNodeKey = "opening.room_free_roam";
    private const string OpeningPhoneGroupKey = "opening.system_core_phone";

    [SerializeField] private KeyCode toggleKey = KeyCode.F;
    [SerializeField] private GameObject phonePanel;
    [SerializeField] private TMP_Text phoneTitleText;
    [SerializeField] private TMP_Text phoneBodyText;

    [Header("Story Phone")]
    [SerializeField] private StoryManager storyManager;

    private bool isOpen;

    private void Awake()
    {
        EnsurePhoneUi();
        SetPhoneVisible(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (isOpen)
                ClosePhone();
            else if (PlayerMovement.CanMove)
                HandlePhoneInput();
        }

        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
            ClosePhone();
    }

    private void HandlePhoneInput()
    {
        if (TryAnswerStoryPhone())
            return;

        OpenPhone();
    }

    public void OpenPhone()
    {
        EnsurePhoneUi();
        RefreshPhoneText();
        SetPhoneVisible(true);
        isOpen = true;
        GameState.CanPlayerMove = false;
        PlayerMovement.AddMovementLock(MovementLockId);
    }

    public void ClosePhone()
    {
        SetPhoneVisible(false);
        isOpen = false;
        GameState.CanPlayerMove = true;
        PlayerMovement.RemoveMovementLock(MovementLockId);
    }

    private void RefreshPhoneText()
    {
        if (phoneTitleText != null)
            phoneTitleText.text = IsOpeningPhoneBeat() ? "INCOMING CALL" : "FIELD DEVICE";

        if (phoneBodyText == null)
            return;

        GameSession session = GameSession.Instance;
        string operatorName = !string.IsNullOrWhiteSpace(session.OperatorName)
            ? session.OperatorName
            : (string.IsNullOrWhiteSpace(LoadingScreen.operatorName) ? "UNKNOWN" : LoadingScreen.operatorName);

        phoneBodyText.text =
            BuildPhoneBody(operatorName, session);
    }

    private string BuildPhoneBody(string operatorName, GameSession session)
    {
        if (IsOpeningPhoneBeat())
        {
            return
                "The device is ringing. This is the call that wakes the operator in the System Core.\n\n" +
                "Press F to answer and continue the story.";
        }

        return
            "OPERATOR: " + operatorName + "\n" +
            "CYBERSTATUS: " + session.CurrentCyberStatus + "\n" +
            "TRUST TOKENS: " + session.CurrentTrustTokens + "\n\n" +
            "Press F to close.";
    }

    private bool TryAnswerStoryPhone()
    {
        StoryManager manager = ResolveStoryManager();
        if (manager == null || !IsOpeningPhoneBeat())
            return false;

        manager.HandleWorldInteraction(string.Empty, OpeningPhoneGroupKey, "PHONE", string.Empty, string.Empty);
        return true;
    }

    private bool IsOpeningPhoneBeat()
    {
        StoryManager manager = ResolveStoryManager();
        return manager != null && string.Equals(manager.CurrentNodeKey, OpeningPhoneNodeKey, System.StringComparison.Ordinal);
    }

    private StoryManager ResolveStoryManager()
    {
        if (storyManager == null)
            storyManager = FindFirstObjectByType<StoryManager>();

        return storyManager;
    }

    private void SetPhoneVisible(bool visible)
    {
        if (phonePanel != null)
            phonePanel.SetActive(visible);
    }

    private void EnsurePhoneUi()
    {
        if (phonePanel != null)
            return;

        GameObject root = new GameObject("PhoneCanvas");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();

        GameObject panelObject = new GameObject("PhonePanel");
        panelObject.transform.SetParent(root.transform, false);
        RectTransform panelRect = panelObject.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0.5f);
        panelRect.anchorMax = new Vector2(1f, 0.5f);
        panelRect.pivot = new Vector2(1f, 0.5f);
        panelRect.anchoredPosition = new Vector2(-32f, 0f);
        panelRect.sizeDelta = new Vector2(320f, 520f);

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0.05f, 0.08f, 0.11f, 0.96f);

        phoneTitleText = CreatePhoneText(panelObject.transform, "PhoneTitle", new Vector2(20f, -24f), new Vector2(-20f, -96f), 30f, FontStyles.Bold);
        phoneTitleText.alignment = TextAlignmentOptions.TopLeft;

        phoneBodyText = CreatePhoneText(panelObject.transform, "PhoneBody", new Vector2(20f, -100f), new Vector2(-20f, -20f), 24f, FontStyles.Normal);
        phoneBodyText.alignment = TextAlignmentOptions.TopLeft;
        phoneBodyText.enableWordWrapping = true;

        phonePanel = panelObject;
    }

    private static TextMeshProUGUI CreatePhoneText(Transform parent, string objectName, Vector2 offsetMin, Vector2 offsetMax, float fontSize, FontStyles fontStyle)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(offsetMin.x, -offsetMax.y);
        textRect.offsetMax = new Vector2(offsetMax.x, -offsetMin.y);

        TextMeshProUGUI textComponent = textObject.AddComponent<TextMeshProUGUI>();
        textComponent.fontSize = fontSize;
        textComponent.fontStyle = fontStyle;
        textComponent.color = Color.white;
        textComponent.text = string.Empty;
        if (TMP_Settings.defaultFontAsset != null)
            textComponent.font = TMP_Settings.defaultFontAsset;

        return textComponent;
    }
}