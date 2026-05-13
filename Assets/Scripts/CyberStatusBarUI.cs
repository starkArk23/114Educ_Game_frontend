using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CyberStatusBarUI : MonoBehaviour
{
    [Serializable]
    private struct VisualState
    {
        public int exactCyberStatus;
        public Sprite sprite;
    }

    [SerializeField] private Image targetImage;
    [SerializeField] private CriticalErrorEffect errorBlinker;
    [SerializeField] private List<VisualState> visualStates = new List<VisualState>
    {
        new VisualState { exactCyberStatus = 100 },
        new VisualState { exactCyberStatus = 80 },
        new VisualState { exactCyberStatus = 65 },
        new VisualState { exactCyberStatus = 50 },
        new VisualState { exactCyberStatus = 35 },
        new VisualState { exactCyberStatus = 20 },
        new VisualState { exactCyberStatus = 0 }
    };
    [Header("Status Text")]
    [SerializeField] private TMP_Text exactValueText;
    [SerializeField] private Vector2 exactValueOffset = new Vector2(0f, 18f);
    [SerializeField] private string exactValueFormat = "{0}/100";

    [Header("Delta Popup")]
    [SerializeField] private TMP_Text deltaPopupText;
    [SerializeField] private Vector2 deltaPopupOffset = new Vector2(0f, 48f);
    [SerializeField] private float deltaPopupDuration = 0.9f;
    [SerializeField] private float deltaPopupRise = 22f;
    [SerializeField] private Color positiveDeltaColor = new Color(0.3f, 1f, 0.45f, 1f);
    [SerializeField] private Color negativeDeltaColor = new Color(1f, 0.32f, 0.32f, 1f);
    [SerializeField] private bool suppressSaveSystemPopup = true;

    private GameSession session;
    private Coroutine deltaPopupRoutine;

    private void Reset()
    {
        targetImage = GetComponent<Image>();
    }

    private void Awake()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();

        EnsureStatusTexts();
    }

    private void OnEnable()
    {
        session = GameSession.Instance;
        if (session != null)
            session.CyberStatusChanged += OnCyberStatusChanged;

        RefreshVisual(force: true);
        HideDeltaPopupImmediate();
    }

    private void OnDisable()
    {
        if (session != null)
            session.CyberStatusChanged -= OnCyberStatusChanged;

        if (deltaPopupRoutine != null)
        {
            StopCoroutine(deltaPopupRoutine);
            deltaPopupRoutine = null;
        }

        session = null;
    }

    private void OnValidate()
    {
        for (int i = 0; i < visualStates.Count; i++)
        {
            if (!GameSession.IsCyberStatusStepAligned(visualStates[i].exactCyberStatus))
                Debug.LogWarning($"[CyberStatusBarUI] Exact cyber status {visualStates[i].exactCyberStatus} should be divisible by {GameSession.CyberStatusStep}.", this);
        }
    }

    private void OnCyberStatusChanged(GameSession.CyberStatusChange change)
    {
        RefreshVisual(force: false, cyberStatusOverride: change.currentValue);
        if (errorBlinker != null)
        {
            errorBlinker.CheckCyberStatus(change.currentValue);
        }

        if (ShouldShowDeltaPopup(change))
            ShowDeltaPopup(change.delta);
    }

    public void RefreshVisual(bool force = false)
    {
        int currentCyberStatus = session != null ? session.CurrentCyberStatus : GameSession.Instance.CurrentCyberStatus;
        RefreshVisual(force, currentCyberStatus);
    }

    private void RefreshVisual(bool force, int cyberStatusOverride)
    {
        EnsureStatusTexts();

        if (exactValueText != null)
            exactValueText.text = string.Format(exactValueFormat, cyberStatusOverride);

        if (targetImage == null)
            return;

        if (!TryGetSpriteForStatus(cyberStatusOverride, out Sprite sprite))
            return;

        if (force || targetImage.sprite != sprite)
            targetImage.sprite = sprite;
    }

    private bool TryGetSpriteForStatus(int cyberStatus, out Sprite sprite)
    {
        sprite = null;
        int closestDistance = int.MaxValue;

        for (int i = 0; i < visualStates.Count; i++)
        {
            Sprite candidateSprite = visualStates[i].sprite;
            if (candidateSprite == null)
                continue;

            int distance = Mathf.Abs(visualStates[i].exactCyberStatus - cyberStatus);
            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            sprite = candidateSprite;

            if (distance == 0)
                return true;
        }

        return sprite != null;
    }

    private bool ShouldShowDeltaPopup(GameSession.CyberStatusChange change)
    {
        if (change.delta == 0 || deltaPopupText == null)
            return false;

        return !suppressSaveSystemPopup || !string.Equals(change.source, "SaveSystem", StringComparison.Ordinal);
    }

    private void ShowDeltaPopup(int delta)
    {
        EnsureStatusTexts();
        if (deltaPopupText == null)
            return;

        if (deltaPopupRoutine != null)
            StopCoroutine(deltaPopupRoutine);

        deltaPopupText.transform.SetAsLastSibling();
        deltaPopupRoutine = StartCoroutine(AnimateDeltaPopup(delta));
    }

    private IEnumerator AnimateDeltaPopup(int delta)
    {
        RectTransform popupRect = deltaPopupText.rectTransform;
        Vector2 startPosition = deltaPopupOffset;
        Vector2 endPosition = startPosition + Vector2.up * deltaPopupRise;
        Color popupColor = delta > 0 ? positiveDeltaColor : negativeDeltaColor;

        deltaPopupText.text = $"{delta:+#;-#;0}";
        popupRect.anchoredPosition = startPosition;
        SetTextAlpha(deltaPopupText, 1f, popupColor);
        deltaPopupText.gameObject.SetActive(true);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, deltaPopupDuration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            popupRect.anchoredPosition = Vector2.Lerp(startPosition, endPosition, t);
            SetTextAlpha(deltaPopupText, 1f - t, popupColor);
            yield return null;
        }

        HideDeltaPopupImmediate();
        deltaPopupRoutine = null;
    }

    private void HideDeltaPopupImmediate()
    {
        if (deltaPopupText == null)
            return;

        SetTextAlpha(deltaPopupText, 0f, deltaPopupText.color);
        deltaPopupText.rectTransform.anchoredPosition = deltaPopupOffset;
        deltaPopupText.gameObject.SetActive(false);
    }

    private void EnsureStatusTexts()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();

        RectTransform parentRect = targetImage != null ? targetImage.rectTransform : GetComponent<RectTransform>();
        if (parentRect == null)
            return;

        if (exactValueText == null)
            exactValueText = CreateOrFindText(parentRect, "CyberStatusValueText", exactValueOffset, 26f, TextAlignmentOptions.Center, FontStyles.Bold);

        if (deltaPopupText == null)
            deltaPopupText = CreateOrFindText(parentRect, "CyberStatusDeltaPopup", deltaPopupOffset, 28f, TextAlignmentOptions.Center, FontStyles.Bold);
    }

    private TMP_Text CreateOrFindText(RectTransform parentRect, string objectName, Vector2 anchoredPosition, float fontSize, TextAlignmentOptions alignment, FontStyles fontStyle)
    {
        Transform existing = parentRect.Find(objectName);
        TextMeshProUGUI text;
        if (existing != null)
        {
            text = existing.GetComponent<TextMeshProUGUI>();
            if (text == null)
                text = existing.gameObject.AddComponent<TextMeshProUGUI>();
        }
        else
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform));
            textObject.transform.SetParent(parentRect, false);
            text = textObject.AddComponent<TextMeshProUGUI>();
        }

        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = new Vector2(0.5f, 1f);
        textRect.anchorMax = new Vector2(0.5f, 1f);
        textRect.pivot = new Vector2(0.5f, 0f);
        textRect.anchoredPosition = anchoredPosition;
        textRect.sizeDelta = new Vector2(180f, 36f);

        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;

        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.color = Color.white;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color(0f, 0f, 0f, 0.85f);

        return text;
    }

    private static void SetTextAlpha(TMP_Text text, float alpha, Color baseColor)
    {
        Color color = baseColor;
        color.a = Mathf.Clamp01(alpha);
        text.color = color;
    }
}