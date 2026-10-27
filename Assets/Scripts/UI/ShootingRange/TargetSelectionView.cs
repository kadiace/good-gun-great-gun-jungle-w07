using System;
using System.Collections.Generic;

using TMPro;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    /// <summary>
    /// 사격장 표적 선택창의 저작된 참조, 버튼·입력 이벤트 전달, 표시 갱신과 시작 숨김만 담당하는 View입니다.
    /// TargetBell·GameManager·ShootingTestBinding 같은 도메인은 Presenter가 소유합니다.
    /// </summary>
    public sealed class TargetSelectionView : MonoBehaviour
    {
        private const string SCREEN_NAME = "TargetSelectionView";
        private const int CUSTOM_INPUT_COUNT = 7;

        [SerializeField] private GameObject m_panel;
        [SerializeField] private RectTransform m_grid;
        [SerializeField] private Button m_buttonTemplate;
        [SerializeField] private Button m_closeButton;
        [SerializeField] private Button m_customToggleButton;
        [SerializeField] private GameObject m_customPanel;
        [SerializeField] private TMP_InputField[] m_customInputs;
        [SerializeField] private Button m_spawnButton;
        [SerializeField] private Button m_cleanupButton;
        [SerializeField] private TMP_Text m_statusText;
        [SerializeField, Tooltip("하위 요소를 저작한 Window 기준 크기. 실제 Window 크기에 비례해 여백·셀·글자를 확대합니다.")]
        private Vector2 m_layoutReferenceSize = new Vector2(465f, 315f);
        private TargetSelectionLayout m_layout;

        private readonly List<Button> m_targetRows = new List<Button>();
        private readonly List<Button> m_timedTestRows = new List<Button>();

        private GameObject m_savedFocus;
        private bool m_isInitialized;
        private bool m_hasAppliedInitialHiddenState;
        private bool m_hasLoggedReferenceError;

        public event Action CloseRequested;
        public event Action<int> TargetSelected;
        public event Action<int> TimedTestSelected;
        public event Action CustomToggleRequested;
        public event Action SpawnCustomRequested;
        public event Action ClearSpawnedRequested;

        public bool IsPanelSelfActive
        {
            get
            {
                return m_panel != null && m_panel.activeSelf;
            }
        }

        public Button SpawnButton
        {
            get
            {
                return m_spawnButton;
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
        /// 씬 오버라이드가 적용된 브리지의 실효 참조를 Initialize 전에 주입합니다.
        /// </summary>
        public void BindReferences(GameObject _panel, RectTransform _grid, Button _buttonTemplate, Button _closeButton)
        {
            m_panel = _panel;
            m_grid = _grid;
            m_buttonTemplate = _buttonTemplate;
            m_closeButton = _closeButton;
        }

        /// <summary>
        /// 시작 숨김을 적용하고 필수 참조를 검증한 뒤 닫기·청소·커스텀·소환 입력을 연결합니다.
        /// 중복 호출은 초기화된 상태를 유지합니다.
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

            BindFixedButtons();
            m_buttonTemplate.gameObject.SetActive(false);
            m_layout = new TargetSelectionLayout(m_grid.parent as RectTransform, m_layoutReferenceSize,
                m_customPanel.transform as RectTransform);
            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// 등록한 버튼 리스너를 해제합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            UnbindFixedButtons();
            m_layout?.Restore();
            m_layout = null;
            m_isInitialized = false;
        }

        private void LateUpdate()
        {
            if (m_isInitialized && m_panel != null && m_panel.activeInHierarchy) m_layout?.Apply();
        }

        /// <summary>
        /// 저작된 버튼 템플릿으로 표적·시험 행을 필요한 수만큼 만들고 커스텀 버튼을 마지막에 둡니다.
        /// 같은 수로 다시 호출하면 기존 행을 유지합니다.
        /// </summary>
        public void EnsureRows(int _targetCount, int _testCount)
        {
            EnsureRowList(m_targetRows, "Target", _targetCount, false);
            EnsureRowList(m_timedTestRows, "TimedTest", _testCount, true);
            if (m_customToggleButton != null)
            {
                m_customToggleButton.transform.SetAsLastSibling();
            }

            ApplyNavigation();
        }

        public Button GetTargetButton(int _index)
        {
            return _index >= 0 && _index < m_targetRows.Count ? m_targetRows[_index] : null;
        }

        public Button GetTimedTestButton(int _index)
        {
            return _index >= 0 && _index < m_timedTestRows.Count ? m_timedTestRows[_index] : null;
        }

        public void SetTargetRow(int _index, string _label, bool _interactable)
        {
            Button button = GetTargetButton(_index);
            if (button == null)
            {
                return;
            }

            button.interactable = _interactable;
            SetLabel(button, _label);
        }

        public void SetTimedTestRow(int _index, string _label, bool _interactable, bool _mandatory)
        {
            Button button = GetTimedTestButton(_index);
            if (button == null)
            {
                return;
            }

            button.interactable = _interactable;
            SetLabel(button, _label);
            if (m_buttonTemplate != null)
            {
                ColorBlock colors = m_buttonTemplate.colors;
                if (_mandatory)
                {
                    colors.normalColor = new Color(.75f, .62f, .3f);
                    colors.selectedColor = new Color(1f, .85f, .4f);
                }

                button.colors = colors;
            }
        }

        public void SetCustomInteractable(bool _interactable)
        {
            if (m_customToggleButton != null) m_customToggleButton.interactable = _interactable;
            if (m_spawnButton != null) m_spawnButton.interactable = _interactable;
            if (!_interactable) SetCustomPanelVisible(false);
        }

        public void SetStatus(string _text)
        {
            if (m_statusText != null)
            {
                m_statusText.text = _text ?? "";
            }
        }

        public void SetGridActive(bool _active)
        {
            if (m_grid != null)
            {
                m_grid.gameObject.SetActive(_active);
            }
        }

        public void Show()
        {
            UIRoot.SaveFocus(ref m_savedFocus, transform);
            if (m_panel != null)
            {
                m_panel.SetActive(true);
            }
            m_layout?.Apply();

            Selectable defaultSelectable = null;
            if (m_targetRows.Count > 0 && UIRoot.IsValidSelectable(m_targetRows[0]))
            {
                defaultSelectable = m_targetRows[0];
            }
            else if (m_timedTestRows.Count > 0 && UIRoot.IsValidSelectable(m_timedTestRows[0]))
            {
                defaultSelectable = m_timedTestRows[0];
            }
            else if (UIRoot.IsValidSelectable(m_customToggleButton))
            {
                defaultSelectable = m_customToggleButton;
            }
            else
            {
                defaultSelectable = m_closeButton;
            }

            UIRoot.SetFocus(defaultSelectable, m_closeButton != null ? new Selectable[] { m_closeButton } : null);
        }

        public void Hide()
        {
            if (m_panel != null)
            {
                m_panel.SetActive(false);
            }

            UIRoot.RestoreFocus(ref m_savedFocus);
        }

        public void SetCustomPanelVisible(bool _visible)
        {
            if (m_customPanel != null)
            {
                m_customPanel.SetActive(_visible);
            }
        }

        public bool ToggleCustomPanel()
        {
            if (m_customPanel == null)
            {
                return false;
            }

            bool visible = !m_customPanel.activeSelf;
            m_customPanel.SetActive(visible);
            return visible;
        }

        public string GetCustomInputText(int _index)
        {
            if (m_customInputs == null || _index < 0 || _index >= m_customInputs.Length || m_customInputs[_index] == null)
            {
                return "";
            }

            return m_customInputs[_index].text ?? "";
        }

        /// <summary>
        /// 사격장 UI의 포커스를 지정한 버튼으로 옮깁니다. 비활성·비상호작용 버튼은 무시합니다.
        /// </summary>
        public bool FocusButton(Button _button)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || _button == null || !_button.gameObject.activeInHierarchy ||
                !_button.IsInteractable())
            {
                return false;
            }

            eventSystem.SetSelectedGameObject(_button.gameObject);
            return true;
        }

        /// <summary>
        /// 그리드 행·닫기·청소·소환 버튼의 명시 Navigation을 비순환으로 다시 연결합니다.
        /// 상호작용 불가 행은 방향 탐색에서 건너뜁니다.
        /// </summary>
        public void ApplyNavigation()
        {
            if (m_grid == null)
            {
                return;
            }

            var buttons = new List<Button>();
            for (int i = 0; i < m_grid.childCount; i++)
            {
                Transform child = m_grid.GetChild(i);
                if (!child.gameObject.activeSelf)
                {
                    continue;
                }

                Button button = child.GetComponent<Button>();
                if (button == null)
                {
                    continue;
                }

                buttons.Add(button);
            }

            int columns = ResolveColumnCount();
            for (int i = 0; i < buttons.Count; i++)
            {
                SetExplicitNavigation(buttons[i],
                    FindVertical(buttons, i, columns, -columns),
                    FindVertical(buttons, i, columns, columns),
                    FindHorizontal(buttons, i, columns, -1),
                    FindHorizontal(buttons, i, columns, 1));
            }

            Selectable first = FindFirstInteractable(buttons);
            SetExplicitNavigation(m_closeButton, null, first, null, null);
            SetExplicitNavigation(m_cleanupButton, m_closeButton, null, null, null);
            SetExplicitNavigation(m_spawnButton, m_customToggleButton, null, null, null);
        }

        private void ApplyInitialHiddenState()
        {
            if (m_hasAppliedInitialHiddenState)
            {
                return;
            }

            m_hasAppliedInitialHiddenState = true;
            Hide();
            SetCustomPanelVisible(false);
            SetStatus("");
        }

        private bool ValidateReferences()
        {
            bool hasInputs = m_customInputs != null && m_customInputs.Length == CUSTOM_INPUT_COUNT;
            int invalidInputIndex = -1;
            if (hasInputs)
            {
                for (int i = 0; i < m_customInputs.Length; i++)
                {
                    if (m_customInputs[i] == null)
                    {
                        invalidInputIndex = i;
                        break;
                    }
                }
            }

            bool isValid = m_panel != null &&
                           m_grid != null &&
                           m_buttonTemplate != null &&
                           m_closeButton != null &&
                           m_customToggleButton != null &&
                           m_customPanel != null &&
                           m_spawnButton != null &&
                           m_cleanupButton != null &&
                           m_statusText != null &&
                           hasInputs &&
                           invalidInputIndex < 0;

            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    "[" + SCREEN_NAME + "] Initialize 실패: 필수 참조 누락 " +
                    "(m_panel=" + (m_panel != null) + ", m_grid=" + (m_grid != null) +
                    ", m_buttonTemplate=" + (m_buttonTemplate != null) + ", m_closeButton=" + (m_closeButton != null) +
                    ", m_customToggleButton=" + (m_customToggleButton != null) + ", m_customPanel=" + (m_customPanel != null) +
                    ", m_spawnButton=" + (m_spawnButton != null) + ", m_cleanupButton=" + (m_cleanupButton != null) +
                    ", m_statusText=" + (m_statusText != null) + ", m_customInputs=" +
                    (hasInputs ? "null at " + invalidInputIndex : "missing") + ")",
                    this);
            }

            return isValid;
        }

        private void BindFixedButtons()
        {
            UnbindFixedButtons();
            if (m_closeButton != null)
            {
                m_closeButton.onClick.AddListener(OnCloseClicked);
            }

            if (m_cleanupButton != null)
            {
                m_cleanupButton.onClick.AddListener(OnCleanupClicked);
            }

            if (m_customToggleButton != null)
            {
                m_customToggleButton.onClick.AddListener(OnCustomToggleClicked);
            }

            if (m_spawnButton != null)
            {
                m_spawnButton.onClick.AddListener(OnSpawnCustomClicked);
            }
        }

        private void UnbindFixedButtons()
        {
            if (m_closeButton != null)
            {
                m_closeButton.onClick.RemoveListener(OnCloseClicked);
            }

            if (m_cleanupButton != null)
            {
                m_cleanupButton.onClick.RemoveListener(OnCleanupClicked);
            }

            if (m_customToggleButton != null)
            {
                m_customToggleButton.onClick.RemoveListener(OnCustomToggleClicked);
            }

            if (m_spawnButton != null)
            {
                m_spawnButton.onClick.RemoveListener(OnSpawnCustomClicked);
            }
        }

        private void EnsureRowList(List<Button> _rows, string _prefix, int _count, bool _timed)
        {
            if (_rows.Count == _count)
            {
                return;
            }

            foreach (Button row in _rows)
            {
                if (row != null)
                {
                    Destroy(row.gameObject);
                }
            }

            _rows.Clear();
            if (m_buttonTemplate == null)
            {
                return;
            }

            for (int i = 0; i < _count; i++)
            {
                Button button = Instantiate(m_buttonTemplate, m_grid);
                m_layout?.RegisterRows(button.transform);
                button.name = _prefix + i;
                int index = i;
                if (_timed)
                {
                    button.onClick.AddListener(() => TimedTestSelected?.Invoke(index));
                }
                else
                {
                    button.onClick.AddListener(() => TargetSelected?.Invoke(index));
                }

                button.gameObject.SetActive(true);
                _rows.Add(button);
            }
        }

        private static void SetLabel(Button _button, string _label)
        {
            TMP_Text label = _button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = _label ?? "";
            }
        }

        private int ResolveColumnCount()
        {
            GridLayoutGroup layout = m_grid.GetComponent<GridLayoutGroup>();
            if (layout != null && layout.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            {
                return Mathf.Max(1, layout.constraintCount);
            }

            return 3;
        }

        private static Button FindVertical(List<Button> _buttons, int _index, int _columns, int _step)
        {
            for (int i = _index + _step; i >= 0 && i < _buttons.Count; i += _step)
            {
                if (IsInteractable(_buttons[i]))
                {
                    return _buttons[i];
                }
            }

            return null;
        }

        private static Button FindHorizontal(List<Button> _buttons, int _index, int _columns, int _step)
        {
            int row = _index / _columns;
            for (int i = _index + _step; i >= 0 && i < _buttons.Count && i / _columns == row; i += _step)
            {
                if (IsInteractable(_buttons[i]))
                {
                    return _buttons[i];
                }
            }

            return null;
        }

        private static Button FindFirstInteractable(List<Button> _buttons)
        {
            foreach (Button button in _buttons)
            {
                if (IsInteractable(button))
                {
                    return button;
                }
            }

            return null;
        }

        private static bool IsInteractable(Button _button)
        {
            // Open 전에는 패널이 비활성이라 계층 활성 여부 대신 자기 활성 상태로 행을 판정한다.
            return _button != null && _button.gameObject.activeSelf && _button.IsInteractable();
        }

        private static void SetExplicitNavigation(Selectable _selectable, Selectable _up, Selectable _down,
            Selectable _left, Selectable _right)
        {
            if (_selectable == null)
            {
                return;
            }

            Navigation navigation = _selectable.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.wrapAround = false;
            navigation.selectOnUp = _up;
            navigation.selectOnDown = _down;
            navigation.selectOnLeft = _left;
            navigation.selectOnRight = _right;
            _selectable.navigation = navigation;
        }

        private void OnCloseClicked()
        {
            CloseRequested?.Invoke();
        }

        private void OnCleanupClicked()
        {
            ClearSpawnedRequested?.Invoke();
        }

        private void OnCustomToggleClicked()
        {
            CustomToggleRequested?.Invoke();
        }

        private void OnSpawnCustomClicked()
        {
            SpawnCustomRequested?.Invoke();
        }
    }
}
