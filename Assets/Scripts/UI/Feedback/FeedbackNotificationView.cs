using UnityEngine;

using TMPro;

namespace WeaponWorkshop
{
    // 상단 알림의 저작된 참조, 표시·숨김과 실제 시간 기반 연출을 담당한다.
    public sealed class FeedbackNotificationView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI m_notificationText;
        [SerializeField] private CanvasGroup m_notificationGroup;
        [SerializeField, Min(.01f)] private float m_enterDuration = .18f;
        [SerializeField, Min(0f)] private float m_holdDuration = 2f;
        [SerializeField, Min(.01f)] private float m_exitDuration = .4f;

        private float m_elapsed = -1f;
        private bool m_isInitialized;
        private bool m_hasAppliedInitialHiddenState;
        private bool m_hasLoggedReferenceError;

        private void Awake()
        {
            ApplyInitialHiddenState();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private void LateUpdate()
        {
            if (m_elapsed < 0f || m_notificationGroup == null)
            {
                return;
            }

            m_elapsed += Time.unscaledDeltaTime;
            float enter = Mathf.Max(.01f, m_enterDuration);
            float exit = Mathf.Max(.01f, m_exitDuration);
            if (m_elapsed < enter)
            {
                float t = Mathf.SmoothStep(0f, 1f, m_elapsed / enter);
                m_notificationGroup.alpha = t;
                m_notificationGroup.transform.localScale = Vector3.one * Mathf.Lerp(.92f, 1f, t);
                return;
            }

            if (m_elapsed < enter + m_holdDuration)
            {
                m_notificationGroup.alpha = 1f;
                m_notificationGroup.transform.localScale = Vector3.one;
                return;
            }

            if (m_elapsed < enter + m_holdDuration + exit)
            {
                m_notificationGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, (m_elapsed - enter - m_holdDuration) / exit);
                return;
            }

            HideNotification();
        }

        /// <summary>
        /// 필수 참조를 검증하고 알림을 숨긴 상태로 초기화한다. 중복 호출에 안전하다.
        /// </summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            ApplyInitialHiddenState();
            if (!ValidateReferences())
            {
                return false;
            }

            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// 알림을 숨긴다. 중복 호출에 안전하다.
        /// </summary>
        public void Dispose()
        {
            HideNotification();
            m_isInitialized = false;
        }

        /// <summary>
        /// 알림을 처음부터 다시 연출한다. message가 비어 있으면 즉시 숨긴다.
        /// </summary>
        public void ShowNotification(string _message)
        {
            if (m_notificationText == null || m_notificationGroup == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(_message))
            {
                HideNotification();
                return;
            }

            m_notificationText.text = _message;
            m_elapsed = 0f;
            m_notificationGroup.alpha = 0f;
            m_notificationGroup.transform.localScale = Vector3.one * .92f;
            m_notificationGroup.gameObject.SetActive(true);
            m_notificationGroup.transform.SetAsLastSibling();
        }

        /// <summary>
        /// 알림 표시를 끄고 상태를 초기화한다.
        /// </summary>
        public void HideNotification()
        {
            m_elapsed = -1f;
            if (m_notificationGroup == null)
            {
                return;
            }

            m_notificationGroup.alpha = 0f;
            m_notificationGroup.blocksRaycasts = m_notificationGroup.interactable = false;
            m_notificationGroup.transform.localScale = Vector3.one;
            m_notificationGroup.gameObject.SetActive(false);
        }

        private bool ValidateReferences()
        {
            bool isValid = m_notificationText != null && m_notificationGroup != null;
            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    "[FeedbackNotificationView] Initialize 실패: 필수 참조 누락 " +
                    $"(m_notificationText={m_notificationText != null}, m_notificationGroup={m_notificationGroup != null})",
                    this);
            }

            return isValid;
        }

        // Awake와 Initialize 중 먼저 실행된 쪽만 초기 숨김을 적용해 늦은 Awake가 표시 상태를 끄지 않게 한다.
        private void ApplyInitialHiddenState()
        {
            if (m_hasAppliedInitialHiddenState)
            {
                return;
            }

            m_hasAppliedInitialHiddenState = true;
            HideNotification();
        }
    }
}
