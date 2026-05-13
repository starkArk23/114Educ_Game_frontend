using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TrustTokenDisplayUI : MonoBehaviour
{
    private static Sprite fallbackTokenSprite;

    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text exactValueText;
    [SerializeField] private TMP_Text deltaPopupText;
    [SerializeField] private string exactValueFormat = "x {0}";
    [SerializeField] private string tokenIconResourcePath = "TrustTokenIcon";
    [SerializeField] private Vector2 rootOffset = new Vector2(0f, -44f);
    [SerializeField] private Vector2 iconOffset = new Vector2(-52f, -2f);
    [SerializeField] private Vector2 iconSize = new Vector2(32f, 32f);
    [SerializeField] private Vector2 exactValueOffset = new Vector2(12f, -4f);
    [SerializeField] private Vector2 deltaPopupOffset = new Vector2(0f, 18f);
    [SerializeField] private float deltaPopupDuration = 0.9f;
    [SerializeField] private float deltaPopupRise = 18f;
    [SerializeField] private Color positiveDeltaColor = new Color(1f, 0.92f, 0.4f, 1f);
    [SerializeField] private Color negativeDeltaColor = new Color(1f, 0.42f, 0.32f, 1f);
    [SerializeField] private bool suppressSaveSystemPopup = true;

    private GameSession session;
    private Coroutine deltaPopupRoutine;

    public static TrustTokenDisplayUI AttachToStatusBar(CyberStatusBarUI statusBar)
    {
        if (statusBar == null)
            return null;

        RectTransform statusRect = statusBar.GetComponent<RectTransform>();
        if (statusRect == null)
            return null;

        Transform existing = statusRect.Find("TrustTokenDisplay");
        GameObject rootObject;
        if (existing != null)
        {
            rootObject = existing.gameObject;
        }
        else
        {
            rootObject = new GameObject("TrustTokenDisplay", typeof(RectTransform));
            rootObject.transform.SetParent(statusRect, false);
        }

        TrustTokenDisplayUI display = rootObject.GetComponent<TrustTokenDisplayUI>();
        if (display == null)
            display = rootObject.AddComponent<TrustTokenDisplayUI>();

        display.EnsureUi();
        return display;
    }

    private void Awake()
    {
        EnsureUi();
    }

    private void OnEnable()
    {
        session = GameSession.Instance;
        if (session != null)
            session.TrustTokensChanged += OnTrustTokensChanged;

        Refresh(force: true);
        HideDeltaPopupImmediate();
    }

    private void OnDisable()
    {
        if (session != null)
            session.TrustTokensChanged -= OnTrustTokensChanged;

        if (deltaPopupRoutine != null)
        {
            StopCoroutine(deltaPopupRoutine);
            deltaPopupRoutine = null;
        }

        session = null;
    }

    private void OnTrustTokensChanged(GameSession.TrustTokenChange change)
    {
        Refresh(force: false);

        if (ShouldShowDeltaPopup(change))
            ShowDeltaPopup(change.delta);
    }

    public void Refresh(bool force)
    {
        EnsureUi();

        if (exactValueText != null)
            exactValueText.text = string.Format(exactValueFormat, session != null ? session.CurrentTrustTokens : GameSession.Instance.CurrentTrustTokens);

        if (iconImage == null)
            return;

        if (force || iconImage.sprite == null)
            iconImage.sprite = ResolveIconSprite();
    }

    private bool ShouldShowDeltaPopup(GameSession.TrustTokenChange change)
    {
        if (change.delta == 0 || deltaPopupText == null)
            return false;

        return !suppressSaveSystemPopup || !string.Equals(change.source, "SaveSystem", StringComparison.Ordinal);
    }

    private void ShowDeltaPopup(int delta)
    {
        EnsureUi();
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

    private void EnsureUi()
    {
        RectTransform rootRect = GetComponent<RectTransform>();
        if (rootRect == null)
            rootRect = gameObject.AddComponent<RectTransform>();

        rootRect.anchorMin = new Vector2(0.5f, 1f);
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.anchoredPosition = rootOffset;
        rootRect.sizeDelta = new Vector2(200f, 42f);

        if (iconImage == null)
            iconImage = CreateOrFindImage(rootRect, "TrustTokenIcon", iconOffset, iconSize);

        if (exactValueText == null)
            exactValueText = CreateOrFindText(rootRect, "TrustTokenValueText", exactValueOffset, 24f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

        if (deltaPopupText == null)
            deltaPopupText = CreateOrFindText(rootRect, "TrustTokenDeltaPopup", deltaPopupOffset, 22f, TextAlignmentOptions.Center, FontStyles.Bold);
    }

    private Sprite ResolveIconSprite()
    {
        Sprite resourceSprite = Resources.Load<Sprite>(tokenIconResourcePath);
        if (resourceSprite != null)
            return resourceSprite;

        if (fallbackTokenSprite != null)
            return fallbackTokenSprite;

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "FallbackTrustTokenIcon"
        };

        Color transparent = new Color(0f, 0f, 0f, 0f);
        Color outerRing = new Color(0.93f, 0.73f, 0.2f, 1f);
        Color innerFill = new Color(1f, 0.87f, 0.3f, 1f);
        Color coreGlow = new Color(0.6f, 0.95f, 1f, 1f);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                Color pixel = transparent;

                if (distance <= 28f)
                    pixel = outerRing;
                if (distance <= 23f)
                    pixel = innerFill;
                if (Mathf.Abs(x - center.x) < 3f || Mathf.Abs(y - center.y) < 3f)
                    pixel = distance <= 18f ? coreGlow : pixel;
                if (Mathf.Abs((x + y) - (center.x + center.y)) < 3.5f || Mathf.Abs((x - y) - (center.x - center.y)) < 3.5f)
                    pixel = distance <= 18f ? coreGlow : pixel;

                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        fallbackTokenSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 64f);
        fallbackTokenSprite.name = "FallbackTrustTokenIcon";
        return fallbackTokenSprite;
    }

    private Image CreateOrFindImage(RectTransform parentRect, string objectName, Vector2 anchoredPosition, Vector2 size)
    {
        Transform existing = parentRect.Find(objectName);
        Image image;
        if (existing != null)
        {
            image = existing.GetComponent<Image>();
            if (image == null)
                image = existing.gameObject.AddComponent<Image>();
        }
        else
        {
            GameObject imageObject = new GameObject(objectName, typeof(RectTransform));
            imageObject.transform.SetParent(parentRect, false);
            image = imageObject.AddComponent<Image>();
        }

        RectTransform imageRect = image.rectTransform;
        imageRect.anchorMin = new Vector2(0.5f, 0.5f);
        imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.anchoredPosition = anchoredPosition;
        imageRect.sizeDelta = size;

        image.raycastTarget = false;
        image.preserveAspect = true;
        image.color = Color.white;
        return image;
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
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0f, 0.5f);
        textRect.anchoredPosition = anchoredPosition;
        textRect.sizeDelta = new Vector2(150f, 36f);

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