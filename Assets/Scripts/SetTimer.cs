using System.Collections;
using UnityEngine;

public class SetTimer : MonoBehaviour
{
    [SerializeField] private Light sunLight;
    [SerializeField, Min(0.1f), Tooltip("영업 중 태양의 X축 회전 속도(도/초)")]
    private float rotationSpeed = 30f;
    [SerializeField, Min(0.1f), Tooltip("밤 정산 중 태양의 X축 회전 속도(도/초)")]
    private float nightRotationSpeed = 120f;

    public bool IsRotating { get; private set; }
    private Coroutine rotation;
    private float savedTarget, savedSpeed;
    private bool savedUnscaled, savedNormalize;
    public WeaponWorkshop.TimerSave CaptureSave() => new WeaponWorkshop.TimerSave { angle = currentX,
        rotating = IsRotating, target = savedTarget, speed = savedSpeed, unscaled = savedUnscaled, normalize = savedNormalize };
    public void RestoreSave(WeaponWorkshop.TimerSave data)
    {
        if (data == null || sunLight == null) return;
        InitializeSun(); CancelRotation(); currentX = data.angle; ApplyRotation();
        if (data.rotating) { IsRotating = true; rotation = StartCoroutine(RotateTo(data.target, data.speed, data.unscaled, data.normalize)); }
    }
    private bool initialized;
    private float currentX;
    private float sunY;
    private float sunZ;

    // 시작할 때 아침 각도를 명시적으로 적용합니다.
    public void SetImmediately(SetTimerData setting)
    {
        if (setting == null || sunLight == null) return;
        InitializeSun();
        CancelRotation();
        currentX = setting.rotationX;
        ApplyRotation();
    }

    public void Apply(SetTimerData setting)
    {
        if (setting == null || sunLight == null) return;
        InitializeSun();
        CancelRotation();
        IsRotating = true;
        // 밤에는 게임 시간이 멈춰도 일몰을 마저 진행합니다.
        rotation = StartCoroutine(RotateTo(setting.rotationX, rotationSpeed,
            setting.period == setTimer.night, false));
    }

    public void ReturnToMorning(SetTimerData morning)
    {
        if (morning == null || sunLight == null) return;
        InitializeSun();
        CancelRotation();
        // 일몰과 같은 방향으로 밤을 지나갑니다. -310도는 50도와 같은 회전입니다.
        float target = morning.rotationX;
        while (target >= currentX) target -= 360f;
        IsRotating = true;
        rotation = StartCoroutine(RotateTo(target, nightRotationSpeed, true, true));
    }

    private IEnumerator RotateTo(float target, float speed, bool unscaled, bool normalizeAtEnd)
    {
        savedTarget = target; savedSpeed = speed; savedUnscaled = unscaled; savedNormalize = normalizeAtEnd;
        while (!Mathf.Approximately(currentX, target))
        {
            float delta = unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            currentX = Mathf.MoveTowards(currentX, target, Mathf.Max(0.1f, speed) * delta);
            ApplyRotation();
            yield return null;
        }
        currentX = normalizeAtEnd ? Mathf.DeltaAngle(0f, target) : target;
        ApplyRotation();
        IsRotating = false;
        rotation = null;
    }

    private void InitializeSun()
    {
        if (initialized) return;
        Vector3 angles = sunLight.transform.localEulerAngles;
        currentX = Mathf.DeltaAngle(0f, angles.x);
        sunY = angles.y;
        sunZ = angles.z;
        initialized = true;
    }
    private void ApplyRotation() => sunLight.transform.localRotation = Quaternion.Euler(currentX, sunY, sunZ);
    private void CancelRotation()
    {
        if (rotation != null) StopCoroutine(rotation);
        rotation = null;
        IsRotating = false;
    }
    private void OnDisable() => CancelRotation();
}
