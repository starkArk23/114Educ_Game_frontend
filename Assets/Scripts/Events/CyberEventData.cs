using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "404/Events/Cyber Event Data", fileName = "NewCyberEvent")]
public class CyberEventData : ScriptableObject
{
    public string eventId;
    public string title;
    [TextArea(3, 12)]
    public string bodyText;
    public List<CyberEventChoice> choices = new List<CyberEventChoice>();
    public int correctChoiceIndex = -1;
    public bool isRepeatable = true;
    public float cooldownSeconds = 0f;
}
