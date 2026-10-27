using UnityEngine;
using UnityEngine.EventSystems;

namespace WeaponWorkshop
{
    /// <summary>
    /// 인트로 시작 메뉴의 패널 표시, 계속(이어하기) 버튼 노출 여부, 메뉴 버튼 선택 포커스만 담당하는 View입니다.
    /// 버튼 동작은 기존 Start_UI 브리지의 persistent 호출이 그대로 소유하므로 이 컴포넌트는 onClick을 구독하지 않습니다.
    /// </summary>
    public sealed class StartMenuView : MonoBehaviour
    {
        private const string VIEW_NAME = "StartMenuView";

        [SerializeField] private GameObject m_menuPanel;
        [SerializeField] private UnityEngine.UI.Button m_continueButton;
        [SerializeField] private UnityEngine.UI.Button m_newGameButton;
        [SerializeField] private UnityEngine.UI.Button m_loadButton;
        [SerializeField] private UnityEngine.UI.Button m_quitButton;

        private bool m_isInitialized;
        private bool m_hasLoggedReferenceError;

        public bool IsInitialized
        {
            get
            {
                return m_isInitialized;
            }
        }

        public bool IsMenuVisible
        {
            get
            {
                return m_menuPanel != null && m_menuPanel.activeSelf;
            }
        }

        /// <summary>
        /// 브리지 Start_UI의 유효 직렬화 참조(씬 오버라이드 반영)를 Initialize 전에 받아 같은 대상을 가리키게 합니다.
        /// null 인자는 무시하며 저작된 기본 참조를 유지합니다.
        /// </summary>
        public void BindReferences(GameObject _menuPanel, UnityEngine.UI.Button _continueButton)
        {
            if (_menuPanel != null)
            {
                m_menuPanel = _menuPanel;
            }

            if (_continueButton != null)
            {
                m_continueButton = _continueButton;
            }
        }

        /// <summary>
        /// 필수 직렬화 참조를 검증하고 시작 메뉴 패널을 표시 상태로 명시적으로 초기화합니다.
        /// 씬에 저장된 활성 상태에 의존하지 않습니다. 중복 호출은 초기화된 상태를 유지합니다.
        /// </summary>
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

            SetMenuVisible(true);
            m_isInitialized = true;
            return true;
        }

        /// <summary>시작 메뉴 패널 표시·숨김을 설정합니다.</summary>
        public void SetMenuVisible(bool _visible)
        {
            if (m_menuPanel != null)
            {
                m_menuPanel.SetActive(_visible);
            }
        }

        /// <summary>
        /// 불러올 저장이 있을 때만 계속 버튼을 표시합니다. 기존 Start_UI와 같이 오브젝트 SetActive로 제어합니다.
        /// 계속 버튼을 숨겨 선택 포커스가 무효가 되면 선택 후보를 다시 고릅니다.
        /// </summary>
        public void SetContinueAvailable(bool _available)
        {
            if (m_continueButton == null)
            {
                return;
            }

            bool changed = m_continueButton.gameObject.activeSelf != _available;
            m_continueButton.gameObject.SetActive(_available);

            if (!_available && changed)
            {
                ApplyDefaultSelection();
            }
        }

        /// <summary>
        /// 유효한 선택 포커스가 없을 때만 계속 버튼(숨겨져 있으면 새 게임 버튼)에 선택 포커스를 줍니다.
        /// 꺼졌거나 상호작용 불가능한 대상에 남은 선택은 무효로 보고, 저장 슬롯 패널 같은 유효한 선택은 건드리지 않습니다.
        /// </summary>
        public void ApplyDefaultSelection()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            if (HasValidSelection(eventSystem.currentSelectedGameObject))
            {
                return;
            }

            UnityEngine.UI.Button target = DefaultSelectable();
            if (target == null || !HasValidSelection(target.gameObject))
            {
                return;
            }

            eventSystem.SetSelectedGameObject(target.gameObject);
        }

        private bool HasValidSelection(GameObject _candidate)
        {
            if (_candidate == null || !_candidate.activeInHierarchy)
            {
                return false;
            }

            UnityEngine.UI.Selectable selectable = _candidate.GetComponent<UnityEngine.UI.Selectable>();
            return selectable != null && selectable.isActiveAndEnabled && selectable.interactable;
        }

        private UnityEngine.UI.Button DefaultSelectable()
        {
            if (m_continueButton != null && m_continueButton.gameObject.activeSelf)
            {
                return m_continueButton;
            }

            return m_newGameButton;
        }

        private bool ValidateReferences()
        {
            bool isValid = m_menuPanel != null &&
                           m_continueButton != null &&
                           m_newGameButton != null &&
                           m_loadButton != null &&
                           m_quitButton != null;

            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    $"[{VIEW_NAME}] Initialize 실패: 필수 참조 누락 " +
                    $"(m_menuPanel={m_menuPanel != null}, m_continueButton={m_continueButton != null}, " +
                    $"m_newGameButton={m_newGameButton != null}, m_loadButton={m_loadButton != null}, " +
                    $"m_quitButton={m_quitButton != null})",
                    this);
            }

            return isValid;
        }
    }
}
