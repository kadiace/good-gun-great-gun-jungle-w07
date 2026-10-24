using TMPro;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 좌상단 튜토리얼 목표 HUD의 저작된 참조, 시작 숨김, 목표 문구와 튜토리얼용 좁은 폭 적용만 담당하는 View입니다.
    /// 목표 문구 선택과 날짜·습격 상태 판단은 TutorialQuestPresenter가 맡습니다.
    /// </summary>
    public sealed class TutorialQuestView : MonoBehaviour
    {
        private const string VIEW_NAME = "TutorialQuestView";

        [SerializeField, Min(0f)] private float m_compactWidth = 225f;

        private GameObject m_panel;
        private TMP_Text m_description;
        private QuestInfoTextSizing m_textSizing;

        private RectTransform m_panelRect;
        private Vector2 m_originalAnchorMin;
        private Vector2 m_originalAnchorMax;
        private Vector2 m_originalPosition;
        private Vector2 m_originalSize;
        private Vector2 m_originalPivot;
        private Vector3 m_originalScale;

        private bool m_hasCapturedLayout;
        private bool m_hasAppliedInitialState;
        private bool m_compact;
        private bool m_isInitialized;
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
        /// 브리지가 보유한 실효 참조(프리팹 값과 씬 오버라이드 반영)를 Initialize 전에 전달합니다.
        /// </summary>
        public void Configure(GameObject _panel, TMP_Text _description, QuestInfoTextSizing _textSizing)
        {
            m_panel = _panel;
            m_description = _description;
            m_textSizing = _textSizing;
            CacheLayout();
            ApplyInitialHiddenState();
        }

        /// <summary>
        /// 필수 참조를 검증하고 시작 숨김을 적용합니다. 중복 호출에 안전합니다.
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
        /// 표시 참조를 해제합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            m_isInitialized = false;
        }

        /// <summary>
        /// 튜토리얼 진행 중일 때만 기존 폭(300)으로 줄이고, 끝나면 원래 앵커·크기를 복원합니다.
        /// </summary>
        public void SetCompact(bool _compact)
        {
            if (!m_hasCapturedLayout || m_compact == _compact)
            {
                return;
            }

            m_compact = _compact;
            m_panelRect.anchorMin = m_originalAnchorMin;
            m_panelRect.anchorMax = m_originalAnchorMax;
            m_panelRect.pivot = m_originalPivot;
            m_panelRect.anchoredPosition = m_originalPosition;
            m_panelRect.sizeDelta = _compact
                ? new Vector2(m_compactWidth, m_originalSize.y)
                : m_originalSize;
            m_panelRect.localScale = m_originalScale;
            MarkTextDirty();
        }

        /// <summary>
        /// 목표가 없을 때 설명 텍스트 오브젝트를 숨깁니다. 상태가 같으면 다시 쓰지 않습니다.
        /// </summary>
        public void SetDescriptionVisible(bool _visible)
        {
            if (m_description == null || m_description.gameObject.activeSelf == _visible)
            {
                return;
            }

            m_description.gameObject.SetActive(_visible);
            MarkTextDirty();
        }

        /// <summary>
        /// 목표 문구를 갱신하고 레이아웃 계산을 다시 요청합니다.
        /// </summary>
        public void SetObjective(string _objective)
        {
            if (m_description == null || m_description.text == _objective)
            {
                return;
            }

            m_description.text = _objective;
            MarkTextDirty();
        }

        /// <summary>
        /// 목표 패널 전체 표시를 적용합니다.
        /// </summary>
        public void SetPanelVisible(bool _visible)
        {
            if (m_panel != null && m_panel.activeSelf != _visible)
            {
                m_panel.SetActive(_visible);
            }
        }

        private void ApplyInitialHiddenState()
        {
            if (m_hasAppliedInitialState || m_panel == null)
            {
                return;
            }

            m_hasAppliedInitialState = true;
            m_panel.SetActive(false);
        }

        private void CacheLayout()
        {
            if (m_hasCapturedLayout || m_panel == null)
            {
                return;
            }

            m_panelRect = m_panel.GetComponent<RectTransform>();
            if (m_panelRect == null)
            {
                return;
            }

            m_originalAnchorMin = m_panelRect.anchorMin;
            m_originalAnchorMax = m_panelRect.anchorMax;
            m_originalPosition = m_panelRect.anchoredPosition;
            m_originalSize = m_panelRect.sizeDelta;
            m_originalPivot = m_panelRect.pivot;
            m_originalScale = m_panelRect.localScale;
            m_hasCapturedLayout = true;
        }

        private bool ValidateReferences()
        {
            CacheLayout();
            bool isValid = m_panel != null && m_description != null && m_panelRect != null;
            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    "[" + VIEW_NAME + "] Initialize 실패: 필수 참조 누락 " +
                    "(m_panel=" + (m_panel != null) + ", m_description=" + (m_description != null) +
                    ", m_panelRect=" + (m_panelRect != null) + ")",
                    this);
            }

            return isValid;
        }

        private void MarkTextDirty()
        {
            if (m_textSizing != null)
            {
                m_textSizing.MarkDirty();
            }
        }
    }
}
