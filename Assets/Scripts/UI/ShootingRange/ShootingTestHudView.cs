using TMPro;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 사격 시험 HUD의 저작된 패널·텍스트 참조, 표시 함수와 시작 숨김만 담당하는 View입니다.
    /// 시험 상태·남은 시간 계산은 ShootingTestHudPresenter가 소유합니다.
    /// </summary>
    public sealed class ShootingTestHudView : MonoBehaviour
    {
        private const string SCREEN_NAME = "ShootingTestHudView";

        [SerializeField] private GameObject m_panel;
        [SerializeField] private TMP_Text m_title;
        [SerializeField] private TMP_Text m_timer;
        [SerializeField] private TMP_Text m_details;
        [SerializeField] private TMP_Text m_retry;

        private bool m_isInitialized;
        private bool m_hasAppliedInitialHiddenState;
        private bool m_hasLoggedReferenceError;

        private void Awake()
        {
            if (!m_isInitialized)
            {
                ApplyInitialHiddenState();
            }
        }

        private void OnDestroy()
        {
            Dispose();
        }

        /// <summary>
        /// 시작 숨김을 적용하고 필수 직렬화 참조를 검증합니다. 중복 호출은 초기화된 상태를 유지합니다.
        /// </summary>
        public bool Initialize()
        {
            ApplyInitialHiddenState();

            if (m_isInitialized)
            {
                return true;
            }

            if (!ValidateReferences())
            {
                return false;
            }

            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// 패널을 숨기고 초기화 상태를 해제합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            SetVisible(false);
            m_isInitialized = false;
        }

        public void SetVisible(bool _visible)
        {
            if (m_panel != null)
            {
                m_panel.SetActive(_visible);
            }
        }

        /// <summary>
        /// 제목·남은 시간·상세·재시도 안내를 한 번에 갱신합니다.
        /// </summary>
        public void SetContent(string _title, string _timer, Color _timerColor, string _details, string _retry)
        {
            if (m_title != null)
            {
                m_title.text = _title ?? "";
            }

            if (m_timer != null)
            {
                m_timer.text = _timer ?? "";
                m_timer.color = _timerColor;
            }

            if (m_details != null)
            {
                m_details.text = _details ?? "";
            }

            if (m_retry != null)
            {
                m_retry.text = _retry ?? "";
            }
        }

        private void ApplyInitialHiddenState()
        {
            if (m_hasAppliedInitialHiddenState)
            {
                return;
            }

            m_hasAppliedInitialHiddenState = true;
            SetVisible(false);
        }

        private bool ValidateReferences()
        {
            bool isValid = m_panel != null && m_title != null && m_timer != null && m_details != null && m_retry != null;
            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    "[" + SCREEN_NAME + "] Initialize 실패: 필수 참조 누락 " +
                    "(m_panel=" + (m_panel != null) + ", m_title=" + (m_title != null) +
                    ", m_timer=" + (m_timer != null) + ", m_details=" + (m_details != null) +
                    ", m_retry=" + (m_retry != null) + ")",
                    this);
            }

            return isValid;
        }
    }
}
