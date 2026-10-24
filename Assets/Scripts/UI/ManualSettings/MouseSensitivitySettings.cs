using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 마우스 감도 배율의 PlayerPrefs 영속과 메모리 캐시를 담당합니다.
    /// FirstPersonPlayer가 사용하는 기존 MouseSensitivityUI.Multiplier의 원본 값입니다.
    /// </summary>
    public static class MouseSensitivitySettings
    {
        private const string PREFERENCE_KEY = "MouseSensitivityMultiplier";
        private const float MIN_MULTIPLIER = .1f;
        private const float MAX_MULTIPLIER = 3f;

        private static float? CACHED_MULTIPLIER;

        public static float Multiplier
        {
            get
            {
                if (!CACHED_MULTIPLIER.HasValue)
                {
                    CACHED_MULTIPLIER = Mathf.Clamp(PlayerPrefs.GetFloat(PREFERENCE_KEY, 1f),
                        MIN_MULTIPLIER, MAX_MULTIPLIER);
                }

                return CACHED_MULTIPLIER.Value;
            }
        }

        /// <summary>
        /// 0.01 단위로 반올림해 저장하고 적용된 배율을 돌려줍니다.
        /// </summary>
        public static float Save(float _value)
        {
            float clamped = Mathf.Round(Mathf.Clamp(_value, MIN_MULTIPLIER, MAX_MULTIPLIER) * 100f) / 100f;
            CACHED_MULTIPLIER = clamped;
            PlayerPrefs.SetFloat(PREFERENCE_KEY, clamped);
            PlayerPrefs.Save();
            return clamped;
        }
    }
}
