#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Temporary editor helper — runs once via the menu to wire icons_8 to QuestLogButton,
/// then can be deleted.
/// </summary>
public static class QuestLogButtonWirer
{
    [MenuItem("Tools/Wire Quest Log Button Sprite")]
    public static void WireSprite()
    {
        UnityEngine.Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/visual designs/icons.png");

        Sprite icons8 = null;
        for (int i = 0; i < allAssets.Length; i++)
        {
            Sprite candidate = allAssets[i] as Sprite;
            if (candidate != null && candidate.name == "icons_8")
            {
                icons8 = candidate;
                break;
            }
        }

        if (icons8 == null)
        {
            Debug.LogError("[QuestLogButtonWirer] icons_8 sprite not found in Assets/visual designs/icons.png");
            return;
        }

        // Ensure every open scene has a wired button
        EnsureButtonInOpenScenes(icons8);

        // Find all QuestLogButton instances in all open scenes and wire sprite
        QuestLogButton[] buttons = UnityEngine.Object.FindObjectsByType<QuestLogButton>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (QuestLogButton btn in buttons)
        {
            SerializedObject so = new SerializedObject(btn);
            so.FindProperty("icon").objectReferenceValue = icons8;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(btn.gameObject);
            Debug.Log($"[QuestLogButtonWirer] Assigned icons_8 to {btn.gameObject.name} in {btn.gameObject.scene.name}");
        }

        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[QuestLogButtonWirer] Done — all scenes saved.");
    }

    private static void EnsureButtonInOpenScenes(Sprite icons8)
    {
        // If there's no QuestLogButton in the scene at all, create one inside DialogManager
        QuestLogButton[] existing = UnityEngine.Object.FindObjectsByType<QuestLogButton>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (existing.Length > 0)
            return;

        // Find a suitable Canvas host (prefer DialogManager)
        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Canvas target = null;
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i].gameObject.name == "DialogManager")
            {
                target = canvases[i];
                break;
            }
        }
        if (target == null && canvases.Length > 0)
            target = canvases[0];

        if (target == null)
        {
            Debug.LogError("[QuestLogButtonWirer] No Canvas found in scene to host QuestLogButton");
            return;
        }

        GameObject go = new GameObject("QuestLogButton");
        go.transform.SetParent(target.transform, false);
        go.layer = LayerMask.NameToLayer("UI");
        go.AddComponent<UnityEngine.UI.Image>();
        go.AddComponent<UnityEngine.UI.Button>();
        go.AddComponent<QuestLogButton>();
        Debug.Log($"[QuestLogButtonWirer] Created QuestLogButton inside {target.gameObject.name}");
    }
}
#endif
