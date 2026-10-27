using System;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    /// <summary>
    /// 일시정지 메인 패널의 감도 슬라이더·수치 입력 참조와 값 변경·입력 확정 이벤트,
    /// 범위 설정과 표시 갱신만 담당하는 View입니다. 저장과 파싱 정책은 Presenter가 소유합니다.
    /// </summary>
    public sealed class MouseSensitivityView : MonoBehaviour
    {
        private const float MIN_VALUE = .1f;
        private const float MAX_VALUE = 3f;

        [SerializeField] private Slider m_slider;
        [SerializeField] private TMP_InputField m_valueInput;

        private bool m_isInitialized;
        private bool m_isBound;
        private bool m_hasLoggedReferenceError;

        public event Action<float> ValueChanged;
        public event Action<string> TextCommitted;

        private void OnDestroy()
        {
            Dispose();
        }

        /// <summary>
        /// 씬 오버라이드가 적용된 브리지의 실효 참조를 Initialize 전에 주입합니다.
        /// </summary>
        public void BindReferences(Slider _slider, TMP_InputField _valueInput)
        {
            Unbind();
            m_slider = _slider;
            m_valueInput = _valueInput;
        }

        /// <summary>
        /// 필수 참조를 검증하고 슬라이더 범위와 값 변경·입력 확정 리스너를 연결합니다.
        /// 중복 호출은 초기화된 상태를 유지합니다.
        /// </summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (!ValidateReferences())
            {
                return false;
            }

            m_slider.minValue = MIN_VALUE;
            m_slider.maxValue = MAX_VALUE;
            m_slider.wholeNumbers = false;
            Bind();
            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// 입력 리스너를 해제합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            Unbind();
            m_isInitialized = false;
        }

        public void SetValue(float _value)
        {
            if (m_slider != null)
            {
                m_slider.SetValueWithoutNotify(_value);
            }
        }

        public void SetText(string _text)
        {
            if (m_valueInput != null)
            {
                m_valueInput.SetTextWithoutNotify(_text);
            }
        }

        private bool ValidateReferences()
        {
            bool isValid = m_slider != null && m_valueInput != null;
            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    "[MouseSensitivityView] Initialize 실패: 필수 참조 누락 " +
                    $"(m_slider={m_slider != null}, m_valueInput={m_valueInput != null})",
                    this);
            }

            return isValid;
        }

        private void Bind()
        {
            if (m_isBound)
            {
                return;
            }

            m_isBound = true;
            m_slider.onValueChanged.RemoveListener(OnSliderValueChanged);
            m_slider.onValueChanged.AddListener(OnSliderValueChanged);
            m_valueInput.onEndEdit.RemoveListener(OnTextEndEdit);
            m_valueInput.onEndEdit.AddListener(OnTextEndEdit);
        }

        private void Unbind()
        {
            if (!m_isBound)
            {
                return;
            }

            m_isBound = false;
            if (m_slider != null)
            {
                m_slider.onValueChanged.RemoveListener(OnSliderValueChanged);
            }

            if (m_valueInput != null)
            {
                m_valueInput.onEndEdit.RemoveListener(OnTextEndEdit);
            }
        }

        private void OnSliderValueChanged(float _value)
        {
            ValueChanged?.Invoke(_value);
        }

        private void OnTextEndEdit(string _text)
        {
            TextCommitted?.Invoke(_text);
        }
    }
}
