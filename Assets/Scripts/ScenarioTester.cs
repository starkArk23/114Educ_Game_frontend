using UnityEngine;

public class ScenarioTester : MonoBehaviour{
    public DialogueManager dialogueManager;
    public ScenarioData testScenario;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            dialogueManager.StartScenario(testScenario);
        }
    }
}