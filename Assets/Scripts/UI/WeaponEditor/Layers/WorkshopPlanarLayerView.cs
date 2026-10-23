using System;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

using TMPro;

namespace WeaponWorkshop
{
    // 층 이동 UI의 구체적인 View다. 저작된 텍스트·버튼 참조와 Action 이벤트, 표시 적용만 담당한다.
    // Presenter 생성·도메인 참조·탐색 위상은 뷰에 두지 않는다. 명시 탐색은 매니저가 프리팹에 저작한다.
    public sealed class WorkshopPlanarLayerView : MonoBehaviour
    {
        private const string VIEW_NAME = "WorkshopPlanarLayerView";

        // 플레이 전 인스펙터에도 저작 참조가 보이도록 직렬화한다. 런타임에는 브리지 Configure가 같은 필드를 덮어쓴다.
        [SerializeField] private GameObject m_panel;
        [SerializeField] private TMP_Text m_title;
        [SerializeField] private TMP_Text m_hint;
        [SerializeField] private Button m_previous;
        [SerializeField] private Button m_next;
        [SerializeField] private Button m_core;
        [SerializeField] private Button[] m_layers;

        private TMP_Text[] m_labels;
        private UnityAction[] m_layerActions;

        private bool m_isConfigured;
        private bool m_isInitialized;
        private bool m_hasLoggedReferenceError;

        public event Action PreviousRequested;
        public event Action NextRequested;
        public event Action CoreRequested;
        public event Action<int> LayerRequested;

        public bool IsInitialized => m_isInitialized;
        public bool IsVisible => m_panel != null && m_panel.activeSelf;
        public int SlotCount => m_layers != null ? m_layers.Length : 0;

        /// <summary>
        /// 브리지가 읽은 유효 저작 참조를 옮겨 받는다. 씬 오버라이드가 반영된 브리지 필드 값이 그대로 쓰인다.
        /// </summary>
        public void Configure(GameObject _panel, TMP_Text _title, TMP_Text _hint,
            Button _previous, Button _next, Button _core, Button[] _layers)
        {
            m_panel = _panel;
            m_title = _title;
            m_hint = _hint;
            m_previous = _previous;
            m_next = _next;
            m_core = _core;
            m_layers = _layers;
            m_isConfigured = true;
        }

        /// <summary>
        /// 명시적 숨김·차단 상태를 적용하고 버튼 Action을 연결한다. 브리지가 호출 순서를 보장하며 중복 호출에 안전하다.
        /// </summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (!m_isConfigured)
            {
                Debug.LogError($"[{VIEW_NAME}] Initialize 실패: 브리지 Configure가 먼저 호출되지 않았습니다.");
                return false;
            }

            if (!ValidateReferences())
            {
                return false;
            }

            SetVisible(false);
            int count = m_layers.Length;
            m_labels = new TMP_Text[count];
            m_layerActions = new UnityAction[count];
            for (int i = 0; i < count; i++)
            {
                int slot = i;
                m_labels[i] = m_layers[i].GetComponentInChildren<TMP_Text>(true);
                m_layers[i].interactable = false;
                m_layerActions[i] = () => LayerRequested?.Invoke(slot);
                m_layers[i].onClick.AddListener(m_layerActions[i]);
            }

            m_previous.interactable = false;
            m_next.interactable = false;
            m_core.interactable = false;
            m_previous.onClick.AddListener(OnPreviousClicked);
            m_next.onClick.AddListener(OnNextClicked);
            m_core.onClick.AddListener(OnCoreClicked);
            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// 버튼 리스너를 해제한다. 숨김·콜백 실행은 하지 않는다. 중복 호출에 안전하다.
        /// </summary>
        public void Dispose()
        {
            if (!m_isInitialized)
            {
                return;
            }

            m_previous.onClick.RemoveListener(OnPreviousClicked);
            m_next.onClick.RemoveListener(OnNextClicked);
            m_core.onClick.RemoveListener(OnCoreClicked);
            for (int i = 0; i < m_layerActions.Length; i++)
            {
                m_layers[i].onClick.RemoveListener(m_layerActions[i]);
            }

            m_layerActions = null;
            m_isInitialized = false;
        }

        /// <summary>Presenter가 계산한 한 프레임 표시 값을 저작된 오브젝트에 적용한다.</summary>
        public void Render(WorkshopPlanarLayerState _state)
        {
            if (_state == null)
            {
                return;
            }

            if (m_title.text != _state.Title)
            {
                m_title.text = _state.Title;
            }

            if (m_hint.text != _state.Hint)
            {
                m_hint.text = _state.Hint;
            }

            m_previous.interactable = _state.CanEdit;
            m_next.interactable = _state.CanEdit;
            m_core.interactable = _state.CanEdit;
            int count = Mathf.Min(m_layers.Length, _state.Slots.Length);
            for (int i = 0; i < count; i++)
            {
                WorkshopPlanarLayerSlotState slot = _state.Slots[i];
                if (m_labels[i] != null && m_labels[i].text != slot.Caption)
                {
                    m_labels[i].text = slot.Caption;
                }

                m_layers[i].interactable = slot.Interactable;
                m_layers[i].image.color = slot.IsSelected ? _state.SelectedColor : _state.UnselectedColor;
            }
        }

        /// <summary>
        /// UI 전용 포커스 보조다. 유효한 기존 선택(활성·상호작용 가능 Selectable)은 유지하고,
        /// 없을 때만 활성·상호작용 가능한 코어 버튼을 선택한다. 도메인을 읽지 않는다.
        /// </summary>
        public void FocusDefaultIfNeeded()
        {
            EventSystem current = EventSystem.current;
            if (current == null)
            {
                return;
            }

            GameObject selected = current.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy)
            {
                Selectable selectable = selected.GetComponent<Selectable>();
                if (selectable != null && selectable.IsActive() && selectable.interactable)
                {
                    return;
                }
            }

            if (m_core != null && m_core.isActiveAndEnabled && m_core.interactable)
            {
                current.SetSelectedGameObject(m_core.gameObject);
            }
        }

        /// <summary>패널 온오프만 담당한다. 숨김 때 콜백·도메인 처리를 하지 않는다.</summary>
        public void SetVisible(bool _visible)
        {
            if (m_panel == null || m_panel.activeSelf == _visible)
            {
                return;
            }

            m_panel.SetActive(_visible);
        }

        private void OnPreviousClicked()
        {
            PreviousRequested?.Invoke();
        }

        private void OnNextClicked()
        {
            NextRequested?.Invoke();
        }

        private void OnCoreClicked()
        {
            CoreRequested?.Invoke();
        }

        private bool ValidateReferences()
        {
            if (m_panel != null && m_title != null && m_hint != null &&
                m_previous != null && m_next != null && m_core != null &&
                m_layers != null && m_layers.Length > 0 && !Array.Exists(m_layers, _layer => _layer == null))
            {
                return true;
            }

            if (!m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError($"[{VIEW_NAME}] Initialize 실패: panel/title/hint/previous/next/core/layers 참조를 확인하세요.");
            }

            return false;
        }
    }
}
