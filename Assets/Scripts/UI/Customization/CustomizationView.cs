using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 커스텀 화면의 패널 선택 탭과 표시 영역을 담당하는 View다.
    // 도메인 상태는 읽지 않으며 저작된 버튼·패널 참조, 입력 이벤트, 표시 적용만 담당한다.
    public sealed class CustomizationView : MonoBehaviour
    {
        private const string VIEW_NAME = "CustomizationView";
        private static readonly Color SELECTED_COLOR = new Color(0.34f, 0.45f, 0.5f, 1f);
        private static readonly Color UNSELECTED_COLOR = new Color(0.16f, 0.19f, 0.22f, 1f);

        [SerializeField] private Button m_customButton;
        [SerializeField] private GameObject m_navigation;
        [SerializeField] private GameObject m_contentArea;
        [SerializeField] private GameObject m_contentPrompt;
        [SerializeField] private Button m_removeButton;
        [SerializeField] private Button[] m_panelButtons = Array.Empty<Button>();
        [SerializeField] private GameObject[] m_panels = Array.Empty<GameObject>();

        private readonly List<PanelBinding> m_panelBindings = new List<PanelBinding>();
        private GameObject m_savedFocus;
        private bool m_isShown;
        private bool m_isInitialized;
        private bool m_hasLoggedReferenceError;

        public event Action CustomRequested;
        public event Action RemoveRequested;
        public event Action<int> PanelRequested;

        public int PanelCount => m_panels != null ? m_panels.Length : 0;
        public bool IsInitialized => m_isInitialized;

        // 브리지가 읽은 실효 참조를 옮겨 받는다. 씬 오버라이드가 반영된 값이 그대로 쓰인다.
        public void Configure(Button _customButton, GameObject _navigation, GameObject _contentArea,
            GameObject _contentPrompt, Button _removeButton, Button[] _panelButtons, GameObject[] _panels)
        {
            m_customButton = _customButton;
            m_navigation = _navigation;
            m_contentArea = _contentArea;
            m_contentPrompt = _contentPrompt;
            m_removeButton = _removeButton;
            m_panelButtons = _panelButtons != null ? _panelButtons : Array.Empty<Button>();
            m_panels = _panels != null ? _panels : Array.Empty<GameObject>();
        }

        // 시작 상태(숨김)를 명시적으로 적용하고 입력 Action을 연결한다. 중복 호출에 안전하다.
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

            m_customButton.onClick.AddListener(OnCustomClicked);
            m_removeButton.onClick.AddListener(OnRemoveClicked);
            ClearPanelBindings();
            for (int i = 0; i < m_panelButtons.Length; i++)
            {
                WirePanelButton(i);
            }

            Render(false, -1);
            m_isInitialized = true;
            return true;
        }

        // 버튼 리스너를 해제한다. 숨김·콜백 실행은 하지 않는다.
        public void Dispose()
        {
            if (!m_isInitialized)
            {
                return;
            }

            if (m_customButton != null)
            {
                m_customButton.onClick.RemoveListener(OnCustomClicked);
            }

            if (m_removeButton != null)
            {
                m_removeButton.onClick.RemoveListener(OnRemoveClicked);
            }

            ClearPanelBindings();
            m_isInitialized = false;
        }

        // 탭 색과 패널 하나의 표시만 갱신한다. 표시 여부는 Render가 담당한다.
        public void RenderTabs(int _selectedIndex)
        {
            if (m_panelButtons == null)
            {
                return;
            }

            for (int i = 0; i < m_panelButtons.Length; i++)
            {
                Button button = m_panelButtons[i];
                if (button == null || button.targetGraphic == null)
                {
                    continue;
                }

                button.targetGraphic.color = i == _selectedIndex ? SELECTED_COLOR : UNSELECTED_COLOR;
            }
        }

        // 커스텀 모드 표시 여부와 선택 패널을 저작된 오브젝트에 적용한다.
        // 매 프레임 호출되므로 포커스는 표시 상태가 바뀔 때만 옮긴다(입력칸 선택을 덮어쓰지 않게).
        public void Render(bool _visible, int _selectedIndex)
        {
            bool shownChanged = _visible != m_isShown;
            m_isShown = _visible;
            if (_visible && shownChanged)
            {
                UIRoot.SaveFocus(ref m_savedFocus, transform);
            }

            SetActive(m_navigation, _visible);
            SetActive(m_contentArea, _visible);
            SetActive(m_removeButton != null ? m_removeButton.gameObject : null, _visible);
            SetActive(m_contentPrompt, _visible && _selectedIndex < 0);
            if (m_panels != null)
            {
                for (int i = 0; i < m_panels.Length; i++)
                {
                    SetActive(m_panels[i], _visible && i == _selectedIndex);
                }
            }

            RenderTabs(_selectedIndex);

            if (!shownChanged)
            {
                return;
            }

            if (_visible)
            {
                Selectable defaultBtn = null;
                if (m_panelButtons != null && m_panelButtons.Length > 0)
                {
                    if (_selectedIndex >= 0 && _selectedIndex < m_panelButtons.Length && UIRoot.IsValidSelectable(m_panelButtons[_selectedIndex]))
                    {
                        defaultBtn = m_panelButtons[_selectedIndex];
                    }
                    else
                    {
                        for (int i = 0; i < m_panelButtons.Length; i++)
                        {
                            if (UIRoot.IsValidSelectable(m_panelButtons[i]))
                            {
                                defaultBtn = m_panelButtons[i];
                                break;
                            }
                        }
                    }
                }

                if (defaultBtn == null)
                {
                    defaultBtn = m_removeButton;
                }

                UIRoot.SetFocus(defaultBtn, m_removeButton != null ? new Selectable[] { m_removeButton } : null);
            }
            else
            {
                UIRoot.RestoreFocus(ref m_savedFocus, m_customButton);
            }
        }

        public GameObject PanelAt(int _index)
        {
            return m_panels != null && _index >= 0 && _index < m_panels.Length ? m_panels[_index] : null;
        }

        // 런타임에 추가 등록된 패널을 탭 배열 끝에 붙이고 입력을 연결한다.
        public void AppendPanel(GameObject _panel, Button _button)
        {
            if (_panel == null || _button == null)
            {
                return;
            }

            int length = m_panels.Length;
            Array.Resize(ref m_panels, length + 1);
            Array.Resize(ref m_panelButtons, length + 1);
            m_panels[length] = _panel;
            m_panelButtons[length] = _button;
            if (m_isInitialized)
            {
                WirePanelButton(length);
            }
        }

        private void Awake()
        {
            // 저작 참조가 있고 아직 초기화되지 않은 경우에만 시작 상태를 숨긴다.
            if (m_isInitialized)
            {
                return;
            }

            SetActive(m_navigation, false);
            SetActive(m_contentArea, false);
            SetActive(m_removeButton != null ? m_removeButton.gameObject : null, false);
            SetActive(m_contentPrompt, false);
            if (m_panels != null)
            {
                for (int i = 0; i < m_panels.Length; i++)
                {
                    SetActive(m_panels[i], false);
                }
            }
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private void OnCustomClicked()
        {
            CustomRequested?.Invoke();
        }

        private void OnRemoveClicked()
        {
            RemoveRequested?.Invoke();
        }

        private void WirePanelButton(int _index)
        {
            Button button = m_panelButtons[_index];
            if (button == null)
            {
                return;
            }

            int index = _index;
            UnityAction action = () => PanelRequested?.Invoke(index);
            button.onClick.AddListener(action);
            m_panelBindings.Add(new PanelBinding { Button = button, Action = action });
        }

        private void ClearPanelBindings()
        {
            for (int i = 0; i < m_panelBindings.Count; i++)
            {
                PanelBinding binding = m_panelBindings[i];
                if (binding.Button != null && binding.Action != null)
                {
                    binding.Button.onClick.RemoveListener(binding.Action);
                }
            }

            m_panelBindings.Clear();
        }

        private bool ValidateReferences()
        {
            bool arraysValid = m_panelButtons != null && m_panels != null &&
                m_panelButtons.Length > 0 && m_panelButtons.Length == m_panels.Length &&
                !Array.Exists(m_panelButtons, _button => _button == null) &&
                !Array.Exists(m_panels, _panel => _panel == null);
            if (m_customButton != null && m_navigation != null && m_contentArea != null &&
                m_contentPrompt != null && m_removeButton != null && arraysValid)
            {
                return true;
            }

            if (!m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                List<string> missing = new List<string>();
                if (m_customButton == null)
                {
                    missing.Add(nameof(m_customButton));
                }

                if (m_navigation == null)
                {
                    missing.Add(nameof(m_navigation));
                }

                if (m_contentArea == null)
                {
                    missing.Add(nameof(m_contentArea));
                }

                if (m_contentPrompt == null)
                {
                    missing.Add(nameof(m_contentPrompt));
                }

                if (m_removeButton == null)
                {
                    missing.Add(nameof(m_removeButton));
                }

                if (!arraysValid)
                {
                    missing.Add(nameof(m_panelButtons) + "/" + nameof(m_panels));
                }

                Debug.LogError($"[{VIEW_NAME}] Initialize 실패: 누락/불일치 " + string.Join(", ", missing));
            }

            return false;
        }

        private static void SetActive(GameObject _target, bool _active)
        {
            if (_target != null && _target.activeSelf != _active)
            {
                _target.SetActive(_active);
            }
        }

        private sealed class PanelBinding
        {
            public Button Button;
            public UnityAction Action;
        }
    }
}
