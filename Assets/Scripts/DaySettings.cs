using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "DaySetting", menuName = "Game/Day Settings")]
public class DaySettings : ScriptableObject
{
    public List<SetTimerData> timing = new List<SetTimerData>();
    public int payPerGuest = 100;
    [Min(0f), Tooltip("만족 판매 시 손님이 요청한 태그 1개당 제작비에 더해지는 비율. 0.5면 태그 2개일 때 제작비의 2배를 받음")] public float tagBonusRatio = 0.5f;

}
