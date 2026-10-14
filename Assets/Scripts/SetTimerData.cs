using System;

public enum setTimer
{
    morning,
    Afternoon,
    Evening,
    night
}

[Serializable]
public class SetTimerData
{
    public setTimer period;
    public int startsAtGuestCount;
    // Directional Light의 로컬 X축 목표 각도(도).
    public float rotationX;
}