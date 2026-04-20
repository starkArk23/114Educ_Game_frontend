using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ChoiceData
{
    public string choiceText;
    public int cyberStatusDelta;
    [TextArea] public string feedbackText;
    public bool isCorrect;
}

[CreateAssetMenu(menuName = "CyberGame/Scenario")]
public class ScenarioData : ScriptableObject
{
    public string scenarioTitle;
    [TextArea] public string dialogueText;
    public List<ChoiceData> choices;
}
