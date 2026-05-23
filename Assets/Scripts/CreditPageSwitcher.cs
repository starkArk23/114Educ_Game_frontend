using UnityEngine;

public class CreditPageSwitcher : MonoBehaviour
{
    [Header("UI Slides")]
    [Tooltip("Drag your Page Image GameObjects here in sequential order.")]
    [SerializeField] private GameObject[] creditPages;

    [Header("Menu Panels")]
    [SerializeField] private GameObject mainMenuUI;
    [SerializeField] private GameObject creditsPanel;

    private int currentPageIndex = 0;

    // This triggers automatically every time the Credits Panel is turned on
    void OnEnable()
    {
        currentPageIndex = 0;

        // Ensure only the first page is visible at the start
        for (int i = 0; i < creditPages.Length; i++)
        {
            if (creditPages[i] != null)
            {
                creditPages[i].SetActive(i == 0);
            }
        }
    }

    // Call this to manually advance pages (can be linked to a next button click)
    public void AdvancePage()
    {
        if (creditPages == null || creditPages.Length == 0) return;

        // Hide current active page slide
        if (creditPages[currentPageIndex] != null)
        {
            creditPages[currentPageIndex].SetActive(false);
        }

        currentPageIndex++;

        // If we still have more slides left, show the next one
        if (currentPageIndex < creditPages.Length)
        {
            if (creditPages[currentPageIndex] != null)
            {
                creditPages[currentPageIndex].SetActive(true);
            }
        }
        else
        {
            // No more pages left! Exit back to the main menu layout
            ExitCredits();
        }
    }

    void Update()
    {
        // Let players advance pages using Spacebar, Left Mouse Click, or Enter
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return))
        {
            AdvancePage();
        }

        // Let players quit out early using Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ExitCredits();
        }
    }

    void ExitCredits()
    {
        if (creditsPanel != null) creditsPanel.SetActive(false);
        if (mainMenuUI != null) mainMenuUI.SetActive(true);
    }
}