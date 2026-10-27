using System;

using TMPro;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 지도 패널의 버튼·라벨·정보 문구 표시와 포인터 입력 전달만 담당한다.
    public sealed class RaidMapView : MonoBehaviour
    {
        private const string DEFAULT_REGION_INFO =
            "영역 위에 커서를 올려\n모듈과 상자 정보를 확인하세요.";

        [SerializeField] private GameObject m_panel;
        [SerializeField] private Button[] m_regionButtons = Array.Empty<Button>();
        [SerializeField] private Button m_backButton;
        [SerializeField] private TMP_Text m_regionInfo;
        [SerializeField] private TMP_Text[] m_regionLabels = Array.Empty<TMP_Text>();
        // 슬롯 순서가 아니라 표의 씬 이름으로 지역을 식별한다.
        [SerializeField] private string[] m_regionSceneKeys = Array.Empty<string>();

        private string[] m_originalLabels;
        private UnityAction[] m_regionClickHandlers;
        private UnityAction m_backClickHandler;
        private GameObject m_savedFocus;
        private bool m_isInitialized;
        private bool m_isButtonBound;
        private bool m_hasLoggedReferenceError;

        public event Action<int> RegionClicked;
        public event Action BackClicked;
        public event Action<int> RegionHovered;
        public event Action<int> RegionHoverCleared;

        public int RegionSlotCount => m_regionButtons != null ? m_regionButtons.Length : 0;

        public bool IsShown => m_panel != null && m_panel.activeSelf;

        public bool IsVisibleInHierarchy => m_panel != null && m_panel.activeInHierarchy;

        public string RegionSceneKey(int _slot)
        {
            if (m_regionSceneKeys == null || _slot < 0 || _slot >= m_regionSceneKeys.Length)
            {
                return null;
            }

            return m_regionSceneKeys[_slot];
        }

        private void Awake()
        {
            if (!m_isInitialized)
            {
                ApplyHiddenState();
            }
        }

        private void OnDestroy()
        {
            Dispose();
        }

        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            ApplyHiddenState();
            if (!ValidateReferences())
            {
                return false;
            }

            if (m_originalLabels == null)
            {
                m_originalLabels = new string[m_regionLabels.Length];
                for (int i = 0; i < m_regionLabels.Length; i++)
                {
                    m_originalLabels[i] = m_regionLabels[i] != null ? m_regionLabels[i].text : string.Empty;
                }
            }

            m_regionClickHandlers = new UnityAction[m_regionButtons.Length];
            m_backClickHandler = OnBackClicked;
            BindButtons();
            m_isInitialized = true;
            return true;
        }

        public void Dispose()
        {
            UnbindButtons();
            RestoreLabels();
            m_isInitialized = false;
        }

        public void SetVisible(bool _show)
        {
            if (m_panel == null)
            {
                return;
            }

            if (_show)
            {
                UIRoot.SaveFocus(ref m_savedFocus, transform);
            }

            m_panel.SetActive(_show);
            if (_show)
            {
                m_panel.transform.SetAsLastSibling();
                Selectable defaultButton = (m_regionButtons != null && m_regionButtons.Length > 0)
                    ? m_regionButtons[0]
                    : (Selectable)m_backButton;
                UIRoot.SetFocus(defaultButton, m_backButton != null ? new Selectable[] { m_backButton } : null);
            }
            else
            {
                UIRoot.RestoreFocus(ref m_savedFocus);
            }
        }

        public void BringToFront()
        {
            if (m_panel != null)
            {
                m_panel.transform.SetAsLastSibling();
            }
        }

        public void SetRegionListed(int _slot, bool _listed)
        {
            Button button = GetRegionButton(_slot);
            if (button != null)
            {
                button.gameObject.SetActive(_listed);
            }
        }

        public void SetRegionInteractable(int _slot, bool _interactable)
        {
            Button button = GetRegionButton(_slot);
            if (button != null)
            {
                button.interactable = _interactable;
            }
        }

        public void SetAllInteractable(bool _interactable)
        {
            if (m_regionButtons != null)
            {
                foreach (Button button in m_regionButtons)
                {
                    if (button != null)
                    {
                        button.interactable = _interactable;
                    }
                }
            }

            if (m_backButton != null)
            {
                m_backButton.interactable = _interactable;
            }
        }

        public void SetBackInteractable(bool _interactable)
        {
            if (m_backButton != null)
            {
                m_backButton.interactable = _interactable;
            }
        }

        public void SetRegionFirstClearMark(int _slot, bool _available)
        {
            if (m_regionLabels == null || _slot < 0 || _slot >= m_regionLabels.Length)
            {
                return;
            }

            TMP_Text label = m_regionLabels[_slot];
            if (label == null)
            {
                return;
            }

            string original = m_originalLabels != null && _slot < m_originalLabels.Length
                ? m_originalLabels[_slot] : string.Empty;
            label.text = _available
                ? original + " <size=65%><color=#F04D4D>●</color></size>"
                : original;
        }

        public void SetRegionInfoText(string _text)
        {
            if (m_regionInfo != null)
            {
                m_regionInfo.text = _text ?? string.Empty;
            }
        }

        public void ShowDefaultRegionInfo()
        {
            SetRegionInfoText(DEFAULT_REGION_INFO);
        }

        public RectTransform GetRegionRect(int _slot)
        {
            Button button = GetRegionButton(_slot);
            return button != null ? button.transform as RectTransform : null;
        }

        public void NotifyRegionHovered(int _slot)
        {
            RegionHovered?.Invoke(_slot);
        }

        public void NotifyRegionHoverCleared(int _slot)
        {
            RegionHoverCleared?.Invoke(_slot);
        }

        private Button GetRegionButton(int _slot)
        {
            if (m_regionButtons == null || _slot < 0 || _slot >= m_regionButtons.Length)
            {
                return null;
            }

            return m_regionButtons[_slot];
        }

        private void ApplyHiddenState()
        {
            if (m_panel != null)
            {
                m_panel.SetActive(false);
            }

            if (m_regionButtons != null && m_regionButtons.Length > 4 && m_regionButtons[4] != null)
            {
                m_regionButtons[4].gameObject.SetActive(false);
            }

            if (m_regionInfo != null)
            {
                m_regionInfo.text = DEFAULT_REGION_INFO;
            }
        }

        private bool ValidateReferences()
        {
            bool isValid = m_panel != null && m_backButton != null && m_regionInfo != null &&
                m_regionButtons != null && m_regionButtons.Length > 0 &&
                m_regionLabels != null && m_regionLabels.Length == m_regionButtons.Length;
            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    $"[RaidMapView] 필수 참조 누락: panel={m_panel != null}, back={m_backButton != null}, " +
                    $"info={m_regionInfo != null}, buttons={m_regionButtons?.Length ?? 0}, " +
                    $"labels={m_regionLabels?.Length ?? 0}. NightRaidUI 지도 프리팹 저작을 실행하세요.", this);
            }

            return isValid;
        }

        private void BindButtons()
        {
            if (m_isButtonBound)
            {
                return;
            }

            for (int i = 0; i < m_regionButtons.Length; i++)
            {
                int slot = i;
                if (m_regionButtons[i] != null)
                {
                    if (m_regionClickHandlers[i] == null)
                    {
                        m_regionClickHandlers[i] = () => OnRegionClicked(slot);
                    }

                    m_regionButtons[i].onClick.AddListener(m_regionClickHandlers[i]);
                }
            }

            if (m_backButton != null)
            {
                m_backButton.onClick.AddListener(m_backClickHandler);
            }

            m_isButtonBound = true;
        }

        private void UnbindButtons()
        {
            if (!m_isButtonBound)
            {
                return;
            }

            for (int i = 0; i < m_regionButtons.Length; i++)
            {
                if (m_regionButtons[i] != null && m_regionClickHandlers != null &&
                    i < m_regionClickHandlers.Length && m_regionClickHandlers[i] != null)
                {
                    m_regionButtons[i].onClick.RemoveListener(m_regionClickHandlers[i]);
                }
            }

            if (m_backButton != null && m_backClickHandler != null)
            {
                m_backButton.onClick.RemoveListener(m_backClickHandler);
            }

            m_isButtonBound = false;
        }

        private void RestoreLabels()
        {
            if (m_originalLabels == null || m_regionLabels == null)
            {
                return;
            }

            int count = Mathf.Min(m_originalLabels.Length, m_regionLabels.Length);
            for (int i = 0; i < count; i++)
            {
                if (m_regionLabels[i] != null)
                {
                    m_regionLabels[i].text = m_originalLabels[i];
                }
            }
        }

        private void OnRegionClicked(int _slot)
        {
            RegionClicked?.Invoke(_slot);
        }

        private void OnBackClicked()
        {
            BackClicked?.Invoke();
        }
    }
}
