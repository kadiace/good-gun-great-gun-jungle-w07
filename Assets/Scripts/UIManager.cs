using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class UIManager : MonoBehaviour
    {
        [SerializeField] private FeedbackUI feedback;
        public FeedbackUI Feedback => feedback;
        public bool IsChoiceOpen => feedback != null && feedback.IsChoiceOpen;
        [SerializeField] private TMPro.TextMeshProUGUI notificationText;
        [SerializeField] private CanvasGroup notificationGroup;
        [SerializeField, Min(0.01f)] private float notificationEnterDuration = .18f;
        [SerializeField, Min(0f)] private float notificationHoldDuration = 2f;
        [SerializeField, Min(0.01f)] private float notificationExitDuration = .4f;
        private float notificationElapsed = -1f;
        public void ShowNotification(string message)
        {
            if (feedback != null) { feedback.ShowNotification(message); return; }
            if (!isActiveAndEnabled || notificationText == null || notificationGroup == null) return;
            if (string.IsNullOrEmpty(message)) { HideNotification(); return; }
            notificationText.text = message;
            notificationElapsed = 0f;
            notificationGroup.alpha = 0f;
            notificationGroup.transform.localScale = Vector3.one * .92f;
            notificationGroup.gameObject.SetActive(true);
        }
        private void HideNotification()
        {
            notificationElapsed = -1f;
            if (notificationText != null) notificationText.text = string.Empty;
            if (notificationGroup == null) return;
            notificationGroup.alpha = 0f;
            notificationGroup.interactable = notificationGroup.blocksRaycasts = false;
            notificationGroup.transform.localScale = Vector3.one;
            if (frontMark != null)
            {
                frontGraphics = frontMark.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
                frontAlphas = new float[frontGraphics.Length];
                for (int i = 0; i < frontGraphics.Length; i++) frontAlphas[i] = frontGraphics[i].color.a;
            }
            SetWorkshopUI(false);
            workshopSession = Player != null ? Player.GetComponent<WorkshopSession>() : null;
            if (workshopSession != null) workshopSession.BindWorkshopUI(this);
            notificationGroup.gameObject.SetActive(false);
        }
        private void UpdateNotification()
        {
            if (notificationElapsed < 0f || notificationGroup == null) return;
            notificationElapsed += Time.unscaledDeltaTime;
            float enter = Mathf.Max(.01f, notificationEnterDuration);
            float exit = Mathf.Max(.01f, notificationExitDuration);
            if (notificationElapsed < enter)
            {
                float t = Mathf.SmoothStep(0f, 1f, notificationElapsed / enter);
                notificationGroup.alpha = t;
                notificationGroup.transform.localScale = Vector3.one * Mathf.Lerp(.92f, 1f, t);
            }
            else if (notificationElapsed < enter + notificationHoldDuration)
            { notificationGroup.alpha = 1f; notificationGroup.transform.localScale = Vector3.one;
            if (frontMark != null)
            {
                frontGraphics = frontMark.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
                frontAlphas = new float[frontGraphics.Length];
                for (int i = 0; i < frontGraphics.Length; i++) frontAlphas[i] = frontGraphics[i].color.a;
            }
            SetWorkshopUI(false);
            workshopSession = Player != null ? Player.GetComponent<WorkshopSession>() : null;
            if (workshopSession != null) workshopSession.BindWorkshopUI(this); }
            else
            {
                float t = (notificationElapsed - enter - notificationHoldDuration) / exit;
                if (t >= 1f) { HideNotification(); return; }
                notificationGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, t);
            }
        }
        [SerializeField] private GameManager gameManager;
        [SerializeField] private FirstPersonPlayer player;
        [SerializeField] private AimUI aimUI;
        [SerializeField] private HoveringUI hoveringUI;
        private WeaponHolder weaponHolder;


        [SerializeField] private NightRaidUI nightRaidUI;
        public NightRaidUI RaidUI => nightRaidUI;


        public FirstPersonPlayer Player
        {
            get
            {
                if (player == null) player = FindFirstObjectByType<FirstPersonPlayer>();
                return player;
            }
        }

        public WeaponHolder PlayerWeaponHolder
        {
            get
            {
                FirstPersonPlayer currentPlayer = Player;
                if (currentPlayer == null) return null;
                if (weaponHolder == null || weaponHolder.gameObject != currentPlayer.gameObject)
                    weaponHolder = currentPlayer.GetComponent<WeaponHolder>();
                return weaponHolder;
            }
        }
        [SerializeField, Tooltip("비우면 플레이어 카메라의 CubeBuilder를 찾음.")]
        private CubeBuilder builder;
        [SerializeField, Tooltip("삭제 모드 표시. 시작 시 CubeBuilder에 넘겨줌.")]
        private GameObject eraseModeUI;
        [SerializeField] private TMPro.TextMeshProUGUI[] moduleLabels;
        [SerializeField] private Color selectedModuleColor = new Color(1f, 0.8f, 0.2f, 1f);
        private int lastSelectedSlot = -1;
        [Header("UI Layers & Pause MVP")]
        [SerializeField] private UIRoot m_uiRoot;
        [SerializeField] private PauseView m_pauseView;
        private PausePresenter m_pausePresenter;
        private bool m_hasLoggedPauseWiringError;

        public UIRoot Root
        {
            get
            {
                return m_uiRoot;
            }
        }

        public PauseView PauseView
        {
            get
            {
                return m_pauseView;
            }
        }

        private void Awake()
        {
            nightRaidUI?.Initialize();
            foreach (var slots in GetComponentsInChildren<SaveSlotsUI>(true)) slots.Initialize();
            // 사운드 설정도 비활성 상태에서 초기 연결하고 닫힌 화면으로 시작한다.
            foreach (var sound in GetComponentsInChildren<SoundSettingsUI>(true)) sound.Initialize();
            // CanvasScaler controls UI sizing; a zero authored root scale hides every panel.
            transform.localScale = Vector3.one;
            if (aimUI != null) aimUI.Initialize(this);
            weaponHolder = PlayerWeaponHolder;
            if (builder == null && Player != null) builder = Player.GetComponentInChildren<CubeBuilder>(true);
            foreach (var palette in GetComponentsInChildren<ModulePaletteUI>(true)) palette.Initialize(builder);
            if (eraseModeUI != null) eraseModeUI.SetActive(false);
            if (builder != null && eraseModeUI != null) builder.SetEraseModeUI(eraseModeUI);
            if (hoveringUI != null) hoveringUI.Initialize(builder, Player != null ? Player.GetComponentInChildren<Interaction>(true) : null);

            HideNotification();
            InitializePauseUI();
            UpdateModuleSelection();
        }
        public void Resume() => gameManager?.Resume();
        public void Restart() => gameManager?.Restart();
        public void LeaveWorkshop() => gameManager?.LeaveWorkshop();
        private void LateUpdate()
        {
            UpdateModuleSelection();

        }
        private void UpdateModuleSelection()
        {
            int slot = builder != null && !builder.DeleteMode ? builder.SelectedSlot : 0;
            if (lastSelectedSlot == slot) return;
            lastSelectedSlot = slot;
            if (moduleLabels == null) return;
            for (int i = 0; i < moduleLabels.Length; i++)
            {
                if (moduleLabels[i] == null) continue;
                bool selected = i + 1 == slot;
                moduleLabels[i].color = selected ? selectedModuleColor : Color.white;
                UIFonts.ApplyWeight(moduleLabels[i], selected ? 700 : 400);
            }
        }
        private void OnDisable()
        {
            feedback?.ForceClose();
        }

        private void OnDestroy()
        {
            if (m_pausePresenter != null)
            {
                m_pausePresenter.Dispose();
                m_pausePresenter = null;
            }
            else if (m_pauseView != null)
            {
                m_pauseView.Dispose();
            }
        }

        private void InitializePauseUI()
        {
            if (m_uiRoot == null || m_pauseView == null)
            {
                if (!m_hasLoggedPauseWiringError)
                {
                    m_hasLoggedPauseWiringError = true;
                    Debug.LogError(
                        $"[UIManager] Initialize 실패: 필수 참조 누락 (m_uiRoot={m_uiRoot != null}, m_pauseView={m_pauseView != null})",
                        this);
                }

                return;
            }

            m_uiRoot.ValidateLayers();
            m_pauseView.Initialize();

            if (gameManager != null)
            {
                EnsurePausePresenter(gameManager);
            }
        }

        private bool EnsurePausePresenter(GameManager _targetGameManager)
        {
            if (m_pauseView == null || m_uiRoot == null || _targetGameManager == null)
            {
                return false;
            }

            // Refresh에 다른 GameManager가 전달되면 기존 구독을 정리하고 새 모델로 다시 연결한다.
            if (m_pausePresenter != null && !m_pausePresenter.IsBoundTo(_targetGameManager))
            {
                m_pausePresenter.Dispose();
                m_pausePresenter = null;
            }

            if (m_pausePresenter == null)
            {
                m_pausePresenter = new PausePresenter(m_pauseView, _targetGameManager, m_uiRoot);
            }

            return m_pausePresenter.Initialize();
        }

        /// <summary>
        /// 명시적으로 전달된 GameManager의 일시정지 상태를 PausePresenter에 위임합니다.
        /// (P1-A 명시적 이관 브리지: 후속 전체 흐름 이관 전까지 GameManager -> UIManager.Refresh -> PausePresenter.Refresh 연결을 유지합니다.)
        /// </summary>
        public void Refresh(GameManager _game)
        {
            if (_game == null)
            {
                return;
            }

            bool wasPaused = (m_pausePresenter != null && m_pausePresenter.IsOpen) ||
                             (m_pauseView != null && m_pauseView.IsVisible);
            bool pauseChanged = wasPaused != _game.IsPaused;
            gameManager = _game;

            // 일시정지를 닫으면 하위 설정 화면도 닫아 다음 열기에서 메뉴가 겹치지 않게 한다.
            if (pauseChanged && !_game.IsPaused)
            {
                GetComponent<SoundSettingsUI>()?.Close();
            }

            if (!EnsurePausePresenter(_game))
            {
                return;
            }

            m_pausePresenter.Refresh();
        }
    }
}
