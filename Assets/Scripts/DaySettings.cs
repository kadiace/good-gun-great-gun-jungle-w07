using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "DaySetting", menuName = "Game/Day Settings")]
public class DaySettings : ScriptableObject
{
    public List<SetTimerData> timing = new List<SetTimerData>();
    public int payPerGuest = 100;

}
