using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 일시정지 메인 패널 감도 행의 호환 브리지. 기존 직렬화 필드(slider, valueInput)와
    // FirstPersonPlayer가 쓰는 정적 Multiplier API를 유지하고 표시·저장은 View/Presenter에 위임한다.
    public sealed class MouseSensitivityUI : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        [SerializeField] private TMP_InputField valueInput;
        [SerializeField] private MouseSensitivityView m_view;

        private MouseSensitivityPresenter m_presenter;
        private bool m_initialized;

        public static float Multiplier => MouseSensitivitySettings.Multiplier;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            if (m_initialized && m_presenter != null)
            {
                m_presenter.Refresh();
            }
        }

        private void OnDestroy()
        {
            if (m_presenter != null)
            {
                m_presenter.Dispose();
                m_presenter = null;
            }

            m_initialized = false;
        }

        /// <summary>
        /// 브리지의 실효 참조를 View에 전달하고 Presenter를 초기화합니다.
        /// </summary>
        public void Initialize()
        {
            if (m_initialized)
            {
                return;
            }

            if (m_view == null)
            {
                Debug.LogError(
                    "[MouseSensitivityUI] Initialize 실패: m_view에 MouseSensitivityView 참조가 필요합니다. " +
                    "UIManager.prefab의 MouseSensitivity 오브젝트에 View를 추가하고 연결하세요.", this);
                return;
            }

            m_view.BindReferences(slider, valueInput);
            m_presenter = new MouseSensitivityPresenter(m_view);
            if (!m_presenter.Initialize())
            {
                m_presenter = null;
                return;
            }

            m_initialized = true;
        }
    }
}
