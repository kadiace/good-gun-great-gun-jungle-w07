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
        [SerializeField] private Vector2 m_padding = new Vector2(12f, 12f);
        [SerializeField, Min(0f)] private float m_spacing = 6f;

        private RectTransform m_panelRect;
        private float m_layoutWidth = -1f;

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

            m_panelRect = m_panel.transform as RectTransform;
            m_layoutWidth = -1f;
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
            if (m_panel != null && m_panel.activeSelf != _visible)
            {
                m_panel.SetActive(_visible);
            }
        }

        /// <summary>
        /// 빈 항목을 숨기고 표시 중인 항목만으로 패널 높이를 맞춥니다.
        /// </summary>
        public void SetContent(string _title, string _timer, Color _timerColor, string _details, string _retry)
        {
            bool changed = SetText(m_title, _title, true);
            changed |= SetText(m_timer, _timer);
            changed |= SetText(m_details, _details);
            changed |= SetText(m_retry, _retry);
            if (m_timer != null)
            {
                m_timer.color = _timerColor;
            }
            if (m_panelRect != null && (changed || !Mathf.Approximately(m_layoutWidth, m_panelRect.rect.width)))
                Reflow();
        }

        private static bool SetText(TMP_Text label, string value, bool alwaysVisible = false)
        {
            if (label == null) return false;
            value = value ?? "";
            bool visible = alwaysVisible || !string.IsNullOrEmpty(value);
            bool changed = label.text != value || label.gameObject.activeSelf != visible;
            if (label.text != value) label.text = value;
            if (label.gameObject.activeSelf != visible) label.gameObject.SetActive(visible);
            return changed;
        }

        private void Reflow()
        {
            m_layoutWidth = m_panelRect.rect.width;
            float width = Mathf.Max(1f, m_layoutWidth - 2f * m_padding.x);
            float top = m_padding.y;
            int rows = 0;
            LayoutRow(m_title, width, ref top, ref rows);
            LayoutRow(m_timer, width, ref top, ref rows);
            LayoutRow(m_details, width, ref top, ref rows);
            LayoutRow(m_retry, width, ref top, ref rows);
            float height = top + m_padding.y - (rows > 0 ? m_spacing : 0f);
            m_panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        private void LayoutRow(TMP_Text label, float width, ref float top, ref int rows)
        {
            if (label == null || !label.gameObject.activeSelf) return;
            float height = Mathf.Ceil(Mathf.Max(label.fontSize, label.GetPreferredValues(label.text, width, Mathf.Infinity).y));
            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(-2f * m_padding.x, height);
            top += height + m_spacing;
            rows++;
        }

        private void ApplyInitialHiddenState()
        {
            if (m_hasAppliedInitialHiddenState)
            {
                return;
            }

            m_hasAppliedInitialHiddenState = true;
            if (m_title != null) m_title.gameObject.SetActive(true);
            if (m_timer != null) m_timer.gameObject.SetActive(false);
            if (m_details != null) m_details.gameObject.SetActive(false);
            if (m_retry != null) m_retry.gameObject.SetActive(false);
            SetVisible(false);
        }

        private bool ValidateReferences()
        {
            bool isValid = m_panel != null && m_panel.transform is RectTransform &&
                m_title != null && m_timer != null && m_details != null && m_retry != null;
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
