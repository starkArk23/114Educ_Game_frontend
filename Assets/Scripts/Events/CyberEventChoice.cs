using System;

[Serializable]
public class CyberEventChoice
{
    public string choiceText;
    public string outcomeText;
    public bool isCorrect;
    public int riskDelta;
    public int rewardDelta;
    public string[] setsFlags;
    public string[] requiredFlags;
}
