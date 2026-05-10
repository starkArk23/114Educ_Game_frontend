using System;

[Serializable]
public class CyberEventChoice
{
    public string choiceText;
    public string outcomeText;
    public bool isCorrect;
    public int cyberStatusDelta;
    public string[] setsFlags;
    public string[] requiredFlags;
}
