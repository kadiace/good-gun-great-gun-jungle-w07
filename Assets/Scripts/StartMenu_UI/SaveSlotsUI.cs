using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    /// <summary>
    /// 저장 슬롯 패널의 호환 브리지입니다. 기존 직렬화 필드 이름(panel·slotButtons·confirmDelete 등)과
    /// UIManager.prefab·StartUI_Manager.prefab의 persistent 호출(Open·Close·SelectSlot·DeleteSlot·DeleteAutoSave),
    /// 씬 오버라이드(Main의 saveController, Intro의 deleteButtons·panel 비활성)를 그대로 유지하고
    /// 실제 표시·판정은 SaveSlotsView·SaveSlotsPresenter로 위임합니다.
    /// 카드·삭제 버튼과 SaveSlotCardView·SaveSlotsView 참조는
    /// Tools/UI/Authoring/SaveSlots/Author 저작 명령이 프리팹에 만들어 연결합니다.
    /// </summary>
    public sealed class SaveSlotsUI : MonoBehaviour, IUIScreen
    {
        // 아래 직렬화 필드 이름은 기존 UIManager.prefab·StartUI_Manager.prefab·씬 오버라이드와의 호환을 위해 유지한다.
        [SerializeField] private GameObject panel;
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject openSaveSlotsButton;
        [SerializeField] private TMPro.TextMeshProUGUI title;
        [SerializeField] private Button[] slotButtons;
        [SerializeField] private TMPro.TextMeshProUGUI[] slotTexts;
        [SerializeField] private Button autoSaveButton;
        [SerializeField] private TMPro.TextMeshProUGUI autoSaveText;
        [SerializeField] private GameSaveController saveController;
        [SerializeField] private Start_UI startMenu;
        [SerializeField] private Button[] deleteButtons;
        [SerializeField] private Button autoSaveDeleteButton;
        [SerializeField] private bool confirmDelete = true;
        [SerializeField] private SaveSlotsView m_view;

        private SaveSlotsPresenter m_presenter;
        private bool m_hasLoggedMissingView;

        public bool IsOpen
        {
            get
            {
                if (m_presenter != null)
                {
                    return m_presenter.IsOpen;
                }

                return panel != null && panel.activeInHierarchy;
            }
        }

        public void InitializeScreen(UIContext _context)
        {
            var previous = saveController;
            ResolveSaveController(_context);
            if (m_presenter != null && previous == null && saveController != null)
            {
                m_presenter.Dispose();
                m_presenter = null;
                EnsureInitialized();
            }
        }

        private void ResolveSaveController(UIContext _context = null)
        {
            if (saveController != null)
            {
                return;
            }

            if (_context != null && _context.Game != null)
            {
                saveController = _context.Game.GetComponent<GameSaveController>() ??
                                 _context.Game.GetComponentInChildren<GameSaveController>();
            }

            if (saveController == null)
            {
                saveController = FindFirstObjectByType<GameSaveController>();
            }
        }

        private void Awake()
        {
            ResolveSaveController();
            ResolveStartMenu();
            EnsureInitialized();
            // 기존 Awake와 같이 컴포넌트가 패널 본체가 아니면(인트로의 SlotMenu) 시작 때 패널을 닫는다.
            if (m_presenter != null)
            {
                if (panel != gameObject)
                {
                    m_presenter.Close();
                }
            }
            else if (panel != gameObject)
            {
                FallbackClose();
            }
        }

        private void OnDisable()
        {
            if (m_presenter != null && m_presenter.IsInitialized)
            {
                m_presenter.Close();
            }
        }

        private void OnDestroy()
        {
            if (m_presenter != null)
            {
                m_presenter.Dispose();
                m_presenter = null;
            }
        }

        /// <summary>UIManager가 비활성 패널까지 포함해 호출하는 초기 연결점이다. 기존과 같이 닫힌 화면으로 시작한다.</summary>
        public void Initialize()
        {
            EnsureInitialized();
            if (m_presenter != null)
            {
                m_presenter.Close();
            }
            else
            {
                FallbackClose();
            }
        }

        public void Open()
        {
            if (!EnsureInitialized())
            {
                return;
            }

            m_presenter.Open();
        }

        public void Close()
        {
            if (!EnsureInitialized())
            {
                return;
            }

            m_presenter.Close();
        }

        /// <summary>UICancelRouter가 ESC로 호출하는 Pause 하위 저장 닫기 경로다.</summary>
        public bool TryCloseForCancel()
        {
            if (!EnsureInitialized())
            {
                return false;
            }

            return m_presenter.TryCloseForCancel();
        }

        /// <summary>저장 파일 상태로 카드 표시를 다시 읽어온다. 기존 public API를 유지한다.</summary>
        public void Refresh()
        {
            if (!EnsureInitialized())
            {
                return;
            }

            m_presenter.Refresh();
        }

        /// <summary>슬롯 카드 persistent 호출(SelectSlot N) 경로다. 저장·불러오기 모드 판정은 Presenter가 한다.</summary>
        public void SelectSlot(int _slot)
        {
            if (!EnsureInitialized())
            {
                return;
            }

            m_presenter.SelectSlot(_slot);
        }

        /// <summary>삭제 버튼 persistent 호출(DeleteSlot N) 경로다. Intro 씬에 추가된 삭제 버튼도 이 메서드를 호출한다.</summary>
        public void DeleteSlot(int _slot)
        {
            if (!EnsureInitialized())
            {
                return;
            }

            m_presenter.DeleteSlot(_slot);
        }

        /// <summary>자동저장 삭제 버튼 persistent 호출(DeleteAutoSave) 경로다.</summary>
        public void DeleteAutoSave()
        {
            if (!EnsureInitialized())
            {
                return;
            }

            m_presenter.DeleteAutoSave();
        }

        private bool EnsureInitialized()
        {
            if (m_presenter != null)
            {
                return true;
            }

            if (m_view == null)
            {
                if (!m_hasLoggedMissingView)
                {
                    m_hasLoggedMissingView = true;
                    Debug.LogError(
                        $"[{nameof(SaveSlotsUI)}] Initialize 실패: m_view에 SaveSlotsView 참조가 필요합니다. " +
                        "SaveSlots 저작 명령(Tools/UI/Authoring/SaveSlots/Author)으로 프리팹에 View를 추가하고 연결하세요.",
                        this);
                }

                return false;
            }

            // 씬 오버라이드가 반영된 브리지 유효 참조를 View에 먼저 전달한다.
            m_view.BindReferences(panel, mainPanel, openSaveSlotsButton, title,
                autoSaveButton, autoSaveText, slotButtons, slotTexts, deleteButtons, autoSaveDeleteButton);
            ResolveSaveController();
            var domain = new SaveSlotsDomain(saveController, startMenu, this, confirmDelete);
            m_presenter = new SaveSlotsPresenter(m_view, domain);
            if (!m_presenter.Initialize())
            {
                m_presenter = null;
                return false;
            }

            return true;
        }

        /// <summary>기존 ResolveStartMenu와 같이 같은 씬의 Start_UI를 찾아 불러오기 문맥을 채운다.</summary>
        private void ResolveStartMenu()
        {
            if (startMenu != null)
            {
                return;
            }

            var menus = FindObjectsByType<Start_UI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(menu => menu.gameObject.scene == gameObject.scene).ToArray();
            if (menus.Length == 1)
            {
                startMenu = menus[0];
            }
        }

        /// <summary>
        /// 초기화에 실패했을 때도 기존 Initialize·Close와 같은 표시 상태(패널 닫힘, 메인 패널·여는 버튼 복원)로 시작한다.
        /// </summary>
        private void FallbackClose()
        {
            FeedbackUI.For(this)?.CancelChoice(this);
            if (panel != null)
            {
                panel.SetActive(false);
            }

            if (mainPanel != null)
            {
                mainPanel.SetActive(true);
            }

            if (openSaveSlotsButton != null)
            {
                openSaveSlotsButton.SetActive(true);
            }
        }
    }
}
