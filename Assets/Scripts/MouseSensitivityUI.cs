using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    public sealed class MouseSensitivityUI : MonoBehaviour
    {
        private const string PreferenceKey = "MouseSensitivityMultiplier";
        private static float? current;
        public static float Multiplier => current ?? (current = Mathf.Clamp(PlayerPrefs.GetFloat(PreferenceKey, 1f), .1f, 3f)).Value;
        [SerializeField] private Slider slider;
        [SerializeField] private TMP_InputField valueInput;
        private void Awake()
        {
            slider.minValue = .1f; slider.maxValue = 3f;
            slider.wholeNumbers = false;
            Sync();
            slider.onValueChanged.AddListener(SetValue);
            valueInput.onEndEdit.AddListener(ApplyText);
        }
        private void OnEnable() { if (slider != null && valueInput != null) Sync(); }
        private void OnDestroy()
        {
            slider.onValueChanged.RemoveListener(SetValue);
            valueInput.onEndEdit.RemoveListener(ApplyText);
        }
        private void ApplyText(string text)
        {
            if ((float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ||
                 float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)) && !float.IsNaN(value) && !float.IsInfinity(value))
                SetValue(value);
            else Sync();
        }
        private void SetValue(float value)
        {
            current = Mathf.Round(Mathf.Clamp(value, .1f, 3f) * 100f) / 100f;
            PlayerPrefs.SetFloat(PreferenceKey, current.Value);
            PlayerPrefs.Save();
            Sync();
        }
        private void Sync()
        {
            slider.SetValueWithoutNotify(Multiplier);
            valueInput.SetTextWithoutNotify(Multiplier.ToString("0.00", CultureInfo.InvariantCulture));
        }
    }
}
