using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Place this on a child of the HUD Canvas (DialogManager).
/// Assign the icon sprite in the Inspector, then the button is ready.
/// Clicking it toggles the Quest Log panel.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
[RequireComponent(typeof(Button))]
public class QuestLogButton : MonoBehaviour
{
    [Header("Appearance")]
    [SerializeField] private Sprite icon;
    [SerializeField] private Vector2 size = new Vector2(44f, 44f);
    [SerializeField] private Vector2 anchoredPosition = new Vector2(-52f, -52f);
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.85f);
    [SerializeField] private Color highlightColor = new Color(0.7f, 0.9f, 1f, 1f);
    [SerializeField] private Color pressedColor = new Color(0.5f, 0.75f, 1f, 1f);

    private void Awake()
    {
        RectTransform rt = GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPosition;

        Image img = GetComponent<Image>();
        img.sprite = icon;
        img.preserveAspect = true;
        img.color = normalColor;
        img.raycastTarget = true;

        Button btn = GetComponent<Button>();
        btn.onClick.AddListener(OnClick);

        ColorBlock colors = btn.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = highlightColor;
        colors.pressedColor = pressedColor;
        colors.selectedColor = highlightColor;
        colors.fadeDuration = 0.1f;
        btn.colors = colors;
        btn.targetGraphic = img;
    }

    private void OnClick()
    {
        QuestLogUI ui = FindFirstObjectByType<QuestLogUI>();
        if (ui != null)
            ui.Toggle();
        else
            QuestLogManager.EnsureExists();
    }
}
