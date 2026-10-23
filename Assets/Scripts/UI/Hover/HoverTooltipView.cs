using System.Collections.Generic;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 호버·스캔 HUD의 저작된 참조와 표시 함수만 담당한다.
    // 도메인 조회와 문구 가공은 HoverTooltipPresenter가 맡는다.
    public sealed class HoverTooltipView : MonoBehaviour
    {
        private const string SCREEN_NAME = "HoverTooltipView";

        [SerializeField] private GameObject m_panel;
        [SerializeField] private TMP_Text m_title;
        [SerializeField] private TMP_Text m_description;
        [SerializeField] private GameObject m_interact;
        [SerializeField] private RectTransform m_textArea;
        [SerializeField] private RectTransform m_point1;
        [SerializeField] private RectTransform m_point2;
        [SerializeField] private RectTransform m_connector;
        [SerializeField, Min(0f)] private float m_connectorWidth = 3f;
        [SerializeField] private Vector2 m_textOffset = new Vector2(30f, 30f);
        [SerializeField, Min(0f)] private float m_screenPadding = 9f;
        [SerializeField] private TMP_Text m_scanModeText;
        [SerializeField, Min(1f)] private float m_scanLoadingDiameter = 67.5f;
        [SerializeField, Min(0.1f)] private float m_scanLoadingThickness = 2.25f;
        [SerializeField, Min(0f)] private float m_scanTextRevealDuration = 0.25f;
        [SerializeField] private RectTransform m_tagGrid;
        [SerializeField] private RectTransform m_tagTemplate;
        [SerializeField] private ScanLoadingGraphic m_scanLoading;
        [SerializeField] private TMP_Text m_scanLoadingText;

        private readonly List<RectTransform> m_tagPills = new List<RectTransform>();
        private Vector2 m_pointerPosition;
        private Color m_bodyColor = Color.white;
        private bool m_isInitialized;
        private bool m_hasLoggedReferenceError;

        public Color BodyColor
        {
            get
            {
                return m_bodyColor;
            }
        }

        public bool HasDescription
        {
            get
            {
                return m_description != null;
            }
        }

        public bool HasTagLayout
        {
            get
            {
                return m_tagGrid != null && m_tagTemplate != null && m_textArea != null;
            }
        }

        public float RevealDuration
        {
            get
            {
                return m_scanTextRevealDuration;
            }
        }

        public bool IsPanelVisibleActive
        {
            get
            {
                return m_panel != null && m_panel.activeInHierarchy;
            }
        }

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
        /// 브리지가 보유한 실효 참조(프리팹 값과 씬 오버라이드 반영)를 View 직렬화 참조로 전달합니다.
        /// </summary>
        public void BindReferences(GameObject _panel, TMP_Text _title, TMP_Text _description, GameObject _interact,
            RectTransform _textArea, RectTransform _point1, RectTransform _point2, RectTransform _connector,
            float _connectorWidth, Vector2 _textOffset, float _screenPadding, TMP_Text _scanModeText,
            float _scanLoadingDiameter, float _scanLoadingThickness, float _scanTextRevealDuration,
            RectTransform _tagGrid, RectTransform _tagTemplate)
        {
            m_panel = _panel;
            m_title = _title;
            m_description = _description;
            m_interact = _interact;
            m_textArea = _textArea;
            m_point1 = _point1;
            m_point2 = _point2;
            m_connector = _connector;
            m_connectorWidth = _connectorWidth;
            m_textOffset = _textOffset;
            m_screenPadding = _screenPadding;
            m_scanModeText = _scanModeText;
            m_scanLoadingDiameter = _scanLoadingDiameter;
            m_scanLoadingThickness = _scanLoadingThickness;
            m_scanTextRevealDuration = _scanTextRevealDuration;
            m_tagGrid = _tagGrid;
            m_tagTemplate = _tagTemplate;
        }

        /// <summary>
        /// 시작 숨김을 적용하고 필수 참조를 검증합니다. 중복 호출은 초기화된 상태를 유지합니다.
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

            m_bodyColor = m_description.color;
            if (m_tagTemplate != null)
            {
                m_tagTemplate.gameObject.SetActive(false);
            }

            if (m_tagGrid != null)
            {
                m_tagGrid.gameObject.SetActive(false);
            }

            m_isInitialized = true;
            return true;
        }

        public void Dispose()
        {
            Hide();
            m_isInitialized = false;
        }

        public void Hide()
        {
            if (m_interact != null)
            {
                m_interact.SetActive(false);
            }

            if (m_panel != null)
            {
                m_panel.SetActive(false);
            }

            if (m_scanLoading != null)
            {
                m_scanLoading.gameObject.SetActive(false);
            }
        }

        public void SetPointerPosition(Vector2 _position)
        {
            m_pointerPosition = _position;
        }

        public void SetTagGridVisible(bool _visible)
        {
            if (m_tagGrid != null)
            {
                m_tagGrid.gameObject.SetActive(_visible);
            }
        }

        public void SetDescriptionVisible(bool _visible)
        {
            if (m_description != null)
            {
                m_description.gameObject.SetActive(_visible);
            }
        }

        public void SetInteractVisible(bool _visible)
        {
            if (m_interact != null)
            {
                m_interact.SetActive(_visible);
            }
        }

        public void SetScanModeTextVisible(bool _visible)
        {
            if (m_scanModeText != null)
            {
                m_scanModeText.gameObject.SetActive(_visible);
            }
        }

        public void SetScanLoadingVisible(bool _visible)
        {
            if (m_scanLoading != null)
            {
                m_scanLoading.gameObject.SetActive(_visible);
            }
        }

        public void SetTitleColor(Color _color)
        {
            if (m_title != null)
            {
                m_title.color = _color;
            }
        }

        public void SetDescriptionColor(Color _color)
        {
            if (m_description != null)
            {
                m_description.color = _color;
            }
        }

        public void ShowScanLoading(float _progress, string _label)
        {
            Hide();
            if (m_scanLoading == null || m_scanLoadingText == null)
            {
                return;
            }

            m_scanLoading.rectTransform.sizeDelta = Vector2.one * m_scanLoadingDiameter;
            m_scanLoading.SetProgress(_progress, m_scanLoadingThickness);
            m_scanLoadingText.rectTransform.anchoredPosition = new Vector2(0f, -m_scanLoadingDiameter * 0.5f - 18f);
            m_scanLoadingText.text = _label;
            m_scanLoading.gameObject.SetActive(true);
        }

        /// <summary>
        /// 스캔 완료 문구를 글자 수 기준으로 순차 노출합니다.
        /// </summary>
        public void SetReveal(float _reveal)
        {
            if (m_title == null || m_description == null)
            {
                return;
            }

            m_title.ForceMeshUpdate();
            m_description.ForceMeshUpdate();
            int titleCount = m_title.textInfo.characterCount;
            int count = Mathf.FloorToInt((titleCount + m_description.textInfo.characterCount) * Mathf.Clamp01(_reveal));
            m_title.maxVisibleCharacters = Mathf.Min(titleCount, count);
            m_description.maxVisibleCharacters = Mathf.Max(0, count - titleCount);
        }

        /// <summary>
        /// 매칭된 태그 알약을 저작 템플릿 복제로 표시합니다. 태그 목록 판정은 Presenter가 맡습니다.
        /// </summary>
        public void ShowWeaponTags(IReadOnlyList<WeaponTypeData> _tags, string _weaponName)
        {
            if (!HasTagLayout)
            {
                Hide();
                return;
            }

            m_tagGrid.gameObject.SetActive(_tags.Count > 0);
            for (int i = 0; i < _tags.Count; i++)
            {
                if (i >= m_tagPills.Count)
                {
                    RectTransform pill = Instantiate(m_tagTemplate, m_tagGrid);
                    foreach (Graphic graphic in pill.GetComponentsInChildren<Graphic>(true))
                    {
                        graphic.raycastTarget = false;
                    }

                    m_tagPills.Add(pill);
                }

                m_tagPills[i].gameObject.SetActive(true);
                m_tagPills[i].GetComponentInChildren<TMP_Text>(true).text = _tags[i].DisplayName;
                m_tagPills[i].GetComponent<TagPillGraphic>().color = _tags[i].TagColor;
            }

            for (int i = _tags.Count; i < m_tagPills.Count; i++)
            {
                m_tagPills[i].gameObject.SetActive(false);
            }

            if (m_description != null)
            {
                m_description.gameObject.SetActive(false);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(m_textArea);
            Show(_weaponName, string.Empty);
            SetTitleColor(Color.white);
            SetDescriptionColor(m_bodyColor);
        }

        /// <summary>
        /// 제목·본문 표시와 포인터 기준 패널·연결선 배치를 담당합니다.
        /// </summary>
        public void Show(string _heading, string _body, bool _richText = false)
        {
            if (m_title != null)
            {
                m_title.maxVisibleCharacters = int.MaxValue;
            }

            if (m_description != null)
            {
                m_description.maxVisibleCharacters = int.MaxValue;
            }

            if (m_interact != null)
            {
                m_interact.SetActive(false);
            }

            if (m_panel == null || m_title == null || m_description == null)
            {
                return;
            }

            m_description.richText = _richText;
            bool changed = m_title.text != _heading || m_description.text != _body;
            if (m_title.text != _heading)
            {
                m_title.text = _heading;
            }

            if (m_description.text != _body)
            {
                m_description.text = _body;
            }

            if (!m_panel.activeSelf)
            {
                m_panel.SetActive(true);
            }

            if (changed && m_textArea != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(m_textArea);
            }

            RectTransform parent = m_panel.transform.parent as RectTransform;
            Canvas canvas = m_panel.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (parent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, m_pointerPosition, camera, out Vector2 point))
            {
                m_panel.transform.localPosition = new Vector3(point.x, point.y, 0f);
                Vector2 offset = m_textOffset;
                if (m_textArea != null)
                {
                    Rect bounds = parent.rect;
                    offset.x = Mathf.Clamp(point.x + m_textOffset.x, bounds.xMin + m_screenPadding,
                        Mathf.Max(bounds.xMin + m_screenPadding, bounds.xMax - m_textArea.rect.width - m_screenPadding)) - point.x;
                    offset.y = Mathf.Clamp(point.y + m_textOffset.y, bounds.yMin + m_screenPadding,
                        Mathf.Max(bounds.yMin + m_screenPadding, bounds.yMax - m_textArea.rect.height - m_screenPadding)) - point.y;
                    m_textArea.anchoredPosition = offset;
                }

                // Point1 follows the cursor until Point2 would leave the top or right edge of the text area.
                // Never reverse either axis of the connection at a screen edge.
                Vector2 secondPoint = new Vector2(Mathf.Max(0f, offset.x), Mathf.Max(0f, offset.y));
                if (m_textArea != null && m_point2 != null)
                {
                    float radius = m_point2.rect.height * .5f;
                    float right = offset.x + m_textArea.rect.xMax - m_point2.rect.width * .5f;
                    secondPoint.x = Mathf.Min(secondPoint.x, Mathf.Max(offset.x, right));
                    float top = offset.y + m_textArea.rect.yMax - radius;
                    secondPoint.y = Mathf.Min(secondPoint.y, Mathf.Max(offset.y, top));
                }

                if (m_point1 != null)
                {
                    m_point1.anchoredPosition = new Vector2(Mathf.Min(0f, secondPoint.x), Mathf.Min(0f, secondPoint.y));
                }

                if (m_point2 != null)
                {
                    m_point2.anchoredPosition = secondPoint;
                }

                if (m_connector != null && m_point1 != null && m_point2 != null)
                {
                    Vector3 first = m_connector.parent.InverseTransformPoint(m_point1.position);
                    Vector3 second = m_connector.parent.InverseTransformPoint(m_point2.position);
                    Vector2 delta = second - first;
                    m_connector.localPosition = (first + second) * .5f;
                    m_connector.sizeDelta = new Vector2(delta.magnitude, m_connectorWidth);
                    m_connector.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                }
            }
        }

        private void ApplyInitialHiddenState()
        {
            Hide();
            SetScanModeTextVisible(false);
        }

        private bool ValidateReferences()
        {
            bool isValid = m_panel != null &&
                m_title != null &&
                m_description != null &&
                m_textArea != null &&
                m_scanLoading != null &&
                m_scanLoadingText != null;
            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    $"[{SCREEN_NAME}] Initialize 실패: 필수 참조 누락 " +
                    $"(m_panel={m_panel != null}, m_title={m_title != null}, m_description={m_description != null}, " +
                    $"m_textArea={m_textArea != null}, m_scanLoading={m_scanLoading != null}, " +
                    $"m_scanLoadingText={m_scanLoadingText != null})",
                    this);
            }

            return isValid;
        }
    }
}
