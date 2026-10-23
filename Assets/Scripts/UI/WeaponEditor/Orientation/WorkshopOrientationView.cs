using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    /// <summary>
    /// 공방 2D/3D 보기 전환·축 선택 UI의 직렬화 참조, 숨김 초기화, 버튼 입력의 C# 이벤트 전달과
    /// 축 그래픽·클릭 영역·자막·표시 갱신만 담당하는 View입니다. 도메인 객체나 다른 화면을 참조하지 않습니다.
    /// </summary>
    public sealed class WorkshopOrientationView : MonoBehaviour
    {
        private const string VIEW_NAME = "WorkshopOrientationView";
        private const string MODE_CAPTION_2D = "2D 모드";
        private const string MODE_CAPTION_3D = "3D 모드";
        private const string SELECTED_CAPTION_PREFIX = "● ";
        private const int AXIS_COUNT = 6;
        private static readonly string[] CAPTIONS = { "+FRONT", "+UP", "+RIGHT", "-FRONT", "-UP", "-RIGHT" };

        [SerializeField] private GameObject m_controlsRoot;
        [SerializeField] private Button m_modeButton;
        [SerializeField] private TMP_Text m_modeLabel;
        [SerializeField] private Button m_frontButton;
        [SerializeField] private Button m_upButton;
        [SerializeField] private Button m_rightButton;
        [SerializeField] private Button m_backButton;
        [SerializeField] private Button m_downButton;
        [SerializeField] private Button m_leftButton;
        [SerializeField] private TMP_Text m_frontLabel;
        [SerializeField] private TMP_Text m_upLabel;
        [SerializeField] private TMP_Text m_rightLabel;
        [SerializeField] private TMP_Text m_backLabel;
        [SerializeField] private TMP_Text m_downLabel;
        [SerializeField] private TMP_Text m_leftLabel;
        [SerializeField] private WorkshopAxesGraphic m_axesGraphic;
        [SerializeField] private Button m_axisMenuButton;
        [SerializeField] private GameObject m_axisMenuRoot;
        [SerializeField] private Button[] m_axisMenuButtons;
        [SerializeField] private Color m_inactiveColor = new Color(.08f, .1f, .11f, .75f);

        private Button[] m_buttons;
        private TMP_Text[] m_labels;
        private TMP_Text[] m_menuLabels;
        private UnityAction[] m_selectActions;
        private bool m_isInitialized;
        private bool m_hasLoggedReferenceError;

        public event Action ModeToggleRequested;
        public event Action<int> AxisSelectRequested;
        public event Action AxisMenuToggleRequested;

        public bool IsInitialized => m_isInitialized;

        private void OnDestroy() => Dispose();

        /// <summary>
        /// 브리지가 읽은 실효 직렬화 참조(씬 오버라이드 해석 결과)를 이 View로 옮긴다.
        /// 직렬화된 m_ 필드는 관리자가 저작한 기본값이며, 실행 시에는 이 참조를 사용한다. Initialize 전에 호출한다.
        /// </summary>
        public void Configure(GameObject _controlsRoot, Button _modeButton, TMP_Text _modeLabel,
            Button _frontButton, Button _upButton, Button _rightButton,
            Button _backButton, Button _downButton, Button _leftButton,
            TMP_Text _frontLabel, TMP_Text _upLabel, TMP_Text _rightLabel,
            TMP_Text _backLabel, TMP_Text _downLabel, TMP_Text _leftLabel,
            WorkshopAxesGraphic _axesGraphic, Button _axisMenuButton, GameObject _axisMenuRoot,
            Button[] _axisMenuButtons, Color _inactiveColor)
        {
            m_controlsRoot = _controlsRoot;
            m_modeButton = _modeButton;
            m_modeLabel = _modeLabel;
            m_frontButton = _frontButton;
            m_upButton = _upButton;
            m_rightButton = _rightButton;
            m_backButton = _backButton;
            m_downButton = _downButton;
            m_leftButton = _leftButton;
            m_frontLabel = _frontLabel;
            m_upLabel = _upLabel;
            m_rightLabel = _rightLabel;
            m_backLabel = _backLabel;
            m_downLabel = _downLabel;
            m_leftLabel = _leftLabel;
            m_axesGraphic = _axesGraphic;
            m_axisMenuButton = _axisMenuButton;
            m_axisMenuRoot = _axisMenuRoot;
            m_axisMenuButtons = _axisMenuButtons;
            m_inactiveColor = _inactiveColor;
            if (m_isInitialized)
            {
                // 초기화된 뒤 참조가 바뀌면 리스너를 다시 맺고 숨긴 초기 상태부터 다시 적용한다.
                UnbindButtons();
                ApplyInitialHiddenState();
                if (!ValidateReferences())
                {
                    m_isInitialized = false;
                    return;
                }

                BindButtons();
            }
        }

        /// <summary>
        /// 필수 참조를 검증하기 전에 컨트롤·축 메뉴·축 그래픽을 숨긴 초기 상태로 만들고,
        /// 참조가 온전하면 버튼 리스너를 연결합니다. 초기화는 한 번만 수행하며 중복 호출에 안전합니다.
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

            BindButtons();
            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// 버튼 리스너를 해제하고 초기화 상태를 되돌린다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            UnbindButtons();
            m_isInitialized = false;
        }

        public void SetControlsVisible(bool _visible)
        {
            if (m_controlsRoot != null && m_controlsRoot.activeSelf != _visible)
            {
                m_controlsRoot.SetActive(_visible);
            }
        }

        public void SetModeCaption(bool _planar)
        {
            if (m_modeLabel == null)
            {
                return;
            }

            string caption = _planar ? MODE_CAPTION_3D : MODE_CAPTION_2D;
            if (m_modeLabel.text != caption)
            {
                m_modeLabel.text = caption;
            }
        }

        public void SetModeInteractable(bool _interactable)
        {
            if (m_modeButton != null)
            {
                m_modeButton.interactable = _interactable;
            }
        }

        public void ToggleAxisMenu()
        {
            if (m_axisMenuRoot != null)
            {
                m_axisMenuRoot.SetActive(!m_axisMenuRoot.activeSelf);
            }
        }

        public void CloseAxisMenu()
        {
            if (m_axisMenuRoot != null)
            {
                m_axisMenuRoot.SetActive(false);
            }
        }

        /// <summary>
        /// 축 그래픽에 전달된 최종 자세를 반영하고, 같은 투영 결과로 6방향 버튼과 메뉴 버튼을 갱신합니다.
        /// </summary>
        public void ShowAxisPose(Quaternion _relativeRotation, int _selectedAxis, bool _interactable)
        {
            if (m_axesGraphic == null)
            {
                return;
            }

            if (!m_axesGraphic.gameObject.activeSelf)
            {
                m_axesGraphic.gameObject.SetActive(true);
            }

            m_axesGraphic.SetOrientation(_relativeRotation, _selectedAxis);
            if (m_axisMenuButton != null)
            {
                m_axisMenuButton.interactable = _interactable;
            }

            if (!_interactable)
            {
                CloseAxisMenu();
            }

            Rect bounds = m_axesGraphic.rectTransform.rect;
            if (bounds.width <= 0f || bounds.height <= 0f)
            {
                return;
            }

            for (int i = 0; i < AXIS_COUNT; i++)
            {
                WorkshopAxesGraphic.ProjectedAxis projected = m_axesGraphic.GetAxis(i);
                RectTransform rect = (RectTransform)m_buttons[i].transform;
                Vector2 center = new Vector2((projected.Point.x - bounds.xMin) / bounds.width,
                    (projected.Point.y - bounds.yMin) / bounds.height);
                // CoreAxes의 좌표계에서 같은 투영 결과로 그림과 클릭 영역을 배치한다.
                rect.anchorMin = center - new Vector2(.17f, .07f);
                rect.anchorMax = center + new Vector2(.17f, .07f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                SetAxis(m_buttons[i], m_labels[i], i, _selectedAxis, projected.Tint, _interactable);
                SetAxis(m_axisMenuButtons[i], m_menuLabels[i], i, _selectedAxis,
                    WorkshopAxesGraphic.AxisColor((WorkshopViewAxis)i), _interactable);
            }

            // 그리기와 클릭 순서를 깊이 순으로 일치시킨다. 겹친 축은 앞쪽이 입력을 받는다.
            for (int order = 0; order < AXIS_COUNT; order++)
            {
                Transform button = m_buttons[m_axesGraphic.GetDrawOrder(order)].transform;
                if (button.GetSiblingIndex() != order)
                {
                    button.SetSiblingIndex(order);
                }
            }
        }

        public void HideOrientation()
        {
            if (m_axisMenuRoot != null)
            {
                m_axisMenuRoot.SetActive(false);
            }

            if (m_axesGraphic == null)
            {
                return;
            }

            m_axesGraphic.ClearOrientation();
            m_axesGraphic.gameObject.SetActive(false);
        }

        private void ApplyInitialHiddenState()
        {
            if (m_controlsRoot != null)
            {
                m_controlsRoot.SetActive(false);
            }

            if (m_axisMenuRoot != null)
            {
                m_axisMenuRoot.SetActive(false);
            }

            if (m_axesGraphic != null)
            {
                m_axesGraphic.ClearOrientation();
                m_axesGraphic.gameObject.SetActive(false);
            }

            if (m_modeLabel != null)
            {
                m_modeLabel.text = MODE_CAPTION_2D;
            }
        }

        private bool HasAllAxisMenuButtons()
        {
            return m_axisMenuButtons != null && m_axisMenuButtons.Length == AXIS_COUNT &&
                Array.TrueForAll(m_axisMenuButtons, _button => _button != null);
        }

        private bool ValidateReferences()
        {
            if (m_controlsRoot != null && m_modeButton != null && m_modeLabel != null &&
                m_frontButton != null && m_upButton != null && m_rightButton != null &&
                m_backButton != null && m_downButton != null && m_leftButton != null &&
                m_frontLabel != null && m_upLabel != null && m_rightLabel != null &&
                m_backLabel != null && m_downLabel != null && m_leftLabel != null &&
                m_axesGraphic != null && m_axisMenuButton != null && m_axisMenuRoot != null &&
                HasAllAxisMenuButtons())
            {
                return true;
            }

            if (!m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    $"[{VIEW_NAME}] Initialize 실패: 필수 참조 누락 " +
                    $"(m_controlsRoot={m_controlsRoot != null}, m_modeButton={m_modeButton != null}, " +
                    $"m_modeLabel={m_modeLabel != null}, m_frontButton={m_frontButton != null}, " +
                    $"m_upButton={m_upButton != null}, m_rightButton={m_rightButton != null}, " +
                    $"m_backButton={m_backButton != null}, m_downButton={m_downButton != null}, " +
                    $"m_leftButton={m_leftButton != null}, m_frontLabel={m_frontLabel != null}, " +
                    $"m_upLabel={m_upLabel != null}, m_rightLabel={m_rightLabel != null}, " +
                    $"m_backLabel={m_backLabel != null}, m_downLabel={m_downLabel != null}, " +
                    $"m_leftLabel={m_leftLabel != null}, m_axesGraphic={m_axesGraphic != null}, " +
                    $"m_axisMenuButton={m_axisMenuButton != null}, m_axisMenuRoot={m_axisMenuRoot != null}, " +
                    $"m_axisMenuButtons={HasAllAxisMenuButtons()})", this);
            }

            return false;
        }

        private void BindButtons()
        {
            m_buttons = new[] { m_frontButton, m_upButton, m_rightButton, m_backButton, m_downButton, m_leftButton };
            m_labels = new[] { m_frontLabel, m_upLabel, m_rightLabel, m_backLabel, m_downLabel, m_leftLabel };
            m_menuLabels = new TMP_Text[AXIS_COUNT];
            m_selectActions = new UnityAction[AXIS_COUNT];
            for (int i = 0; i < AXIS_COUNT; i++)
            {
                int index = i;
                m_selectActions[i] = () => SelectAxis(index);
                m_buttons[i].onClick.AddListener(m_selectActions[i]);
                m_axisMenuButtons[i].onClick.AddListener(m_selectActions[i]);
                m_menuLabels[i] = m_axisMenuButtons[i].GetComponentInChildren<TMP_Text>(true);
            }

            m_modeButton.onClick.AddListener(OnModeButtonClicked);
            m_axisMenuButton.onClick.AddListener(OnAxisMenuButtonClicked);
        }

        private void UnbindButtons()
        {
            if (m_modeButton != null)
            {
                m_modeButton.onClick.RemoveListener(OnModeButtonClicked);
            }

            if (m_axisMenuButton != null)
            {
                m_axisMenuButton.onClick.RemoveListener(OnAxisMenuButtonClicked);
            }

            if (m_selectActions == null)
            {
                return;
            }

            for (int i = 0; i < AXIS_COUNT; i++)
            {
                if (m_buttons != null && m_buttons[i] != null)
                {
                    m_buttons[i].onClick.RemoveListener(m_selectActions[i]);
                }

                if (m_axisMenuButtons != null && m_axisMenuButtons[i] != null)
                {
                    m_axisMenuButtons[i].onClick.RemoveListener(m_selectActions[i]);
                }
            }
        }

        private void OnModeButtonClicked() => ModeToggleRequested?.Invoke();

        private void OnAxisMenuButtonClicked() => AxisMenuToggleRequested?.Invoke();

        private void SelectAxis(int _index) => AxisSelectRequested?.Invoke(_index);

        private void SetAxis(Button _button, TMP_Text _label, int _index, int _selected, Color _tint, bool _interactable)
        {
            bool active = _index == _selected;
            _button.interactable = _interactable;
            _button.image.color = active ? WorkshopAxesGraphic.AxisColor((WorkshopViewAxis)_index) : m_inactiveColor;
            _label.color = active ? Color.black : _tint;
            string text = active ? SELECTED_CAPTION_PREFIX + CAPTIONS[_index] : CAPTIONS[_index];
            if (_label.text != text)
            {
                _label.text = text;
            }
        }
    }
}
