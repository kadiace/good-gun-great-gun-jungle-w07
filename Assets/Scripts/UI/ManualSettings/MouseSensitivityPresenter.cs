using System;
using System.Globalization;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 감도 값의 문화권 파싱·유한값 검사·즉시 저장과 슬라이더·수치 표시 동기화를 담당하는 Presenter입니다.
    /// 저장 시점은 기존처럼 값 변경·입력 확정 즉시이며, 실패한 입력은 저장 값으로 되돌립니다.
    /// </summary>
    public sealed class MouseSensitivityPresenter : IDisposable
    {
        private const string PRESENTER_NAME = "MouseSensitivityPresenter";

        private readonly MouseSensitivityView m_view;

        private bool m_isInitialized;
        private bool m_hasLoggedDependencyError;

        public MouseSensitivityPresenter(MouseSensitivityView _view)
        {
            m_view = _view;
        }

        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (m_view == null)
            {
                if (!m_hasLoggedDependencyError)
                {
                    m_hasLoggedDependencyError = true;
                    Debug.LogError(
                        $"[{PRESENTER_NAME}] Initialize 실패: MouseSensitivityView 참조가 없습니다.");
                }

                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            m_view.ValueChanged -= OnValueChanged;
            m_view.ValueChanged += OnValueChanged;
            m_view.TextCommitted -= OnTextCommitted;
            m_view.TextCommitted += OnTextCommitted;

            m_isInitialized = true;
            Refresh();
            return true;
        }

        /// <summary>
        /// 현재 저장된 배율을 슬라이더와 수치 입력에 알림 없이 반영합니다.
        /// </summary>
        public void Refresh()
        {
            if (!m_isInitialized)
            {
                Initialize();
                return;
            }

            float multiplier = MouseSensitivitySettings.Multiplier;
            m_view.SetValue(multiplier);
            m_view.SetText(multiplier.ToString("0.00", CultureInfo.InvariantCulture));
        }

        public void Dispose()
        {
            if (m_view != null)
            {
                m_view.ValueChanged -= OnValueChanged;
                m_view.TextCommitted -= OnTextCommitted;
                m_view.Dispose();
            }

            m_isInitialized = false;
        }

        private void OnValueChanged(float _value)
        {
            MouseSensitivitySettings.Save(_value);
            Refresh();
        }

        private void OnTextCommitted(string _text)
        {
            if ((float.TryParse(_text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ||
                 float.TryParse(_text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)) &&
                !float.IsNaN(value) && !float.IsInfinity(value))
            {
                MouseSensitivitySettings.Save(value);
            }

            Refresh();
        }
    }
}
