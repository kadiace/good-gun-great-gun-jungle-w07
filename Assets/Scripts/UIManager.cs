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
        [Header("Workshop UI")]
        [SerializeField] private RotationCircleGraphic rotationCircle;
        [SerializeField] private RectTransform frontMark;
        [SerializeField] private RectTransform upMark;
        [SerializeField, Range(0f, 1f), Tooltip("이 값보다 확실히 뒤를 향할 때만 표시를 흐리게 합니다.")]
        private float directionBackThreshold = .05f;
        private UnityEngine.UI.Graphic[] upGraphics;
        private float[] upAlphas;
        private UnityEngine.UI.Graphic[] frontGraphics;
        private float[] frontAlphas;
        private void SetFrontOpacity(float opacity)
        {
            if (frontGraphics == null) return;
            for (int i = 0; i < frontGraphics.Length; i++)
            {
                if (frontGraphics[i] == null) continue;
                Color color = frontGraphics[i].color;
                color.a = frontAlphas[i] * opacity;
                frontGraphics[i].color = color;
            }
        }
        [SerializeField, Range(0f, 1f)] private float rotationCircleRadius = .5f;
        private WorkshopSession workshopSession;
        public float GetWorkshopRotationRadius(Rect viewport) => Mathf.Min(viewport.width, viewport.height) * rotationCircleRadius;
        public void SetWorkshopUI(bool editing)
        {
            if (upMark != null && upGraphics == null)
            {
                upGraphics = upMark.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
                upAlphas = new float[upGraphics.Length];
                for (int i = 0; i < upGraphics.Length; i++) upAlphas[i] = upGraphics[i].color.a;
            }
            if (upMark != null) upMark.gameObject.SetActive(editing);
            if (rotationCircle != null) rotationCircle.gameObject.SetActive(editing &&
                (workshopSession == null || workshopSession.PlanarView == null || !workshopSession.PlanarView.Is2D));
            if (frontMark != null) frontMark.gameObject.SetActive(false);
            if (!editing) SetFrontOpacity(1f);
        }
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
            // 프리팹 루트 스케일은 0이다. 아래 초기화 중 예외가 나도 HUD가 숨겨지지 않도록 가장 먼저 복구한다.
            transform.localScale = Vector3.one;
            nightRaidUI?.Initialize();
            foreach (var slots in GetComponentsInChildren<SaveSlotsUI>(true)) slots.Initialize();
            // 사운드 설정도 비활성 상태에서 초기 연결하고 닫힌 화면으로 시작한다.
            foreach (var sound in GetComponentsInChildren<SoundSettingsUI>(true)) sound.Initialize();
            if (frontMark != null)
            {
                frontGraphics = frontMark.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
                frontAlphas = new float[frontGraphics.Length];
                for (int i = 0; i < frontGraphics.Length; i++) frontAlphas[i] = frontGraphics[i].color.a;
            }
            SetWorkshopUI(false);
            workshopSession = Player != null ? Player.GetComponent<WorkshopSession>() : null;
            if (workshopSession != null) workshopSession.BindWorkshopUI(this);
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

            UpdateWorkshopDirectionUI();
        }
        private void UpdateWorkshopDirectionUI()
        {
            bool editing = workshopSession != null && workshopSession.IsEditing;
            PowerCore core = editing && workshopSession.ActiveWorkbench != null ? workshopSession.ActiveWorkbench.CurrentCore : null;
            Camera view = editing ? workshopSession.WorkshopCamera : null;
            bool planar = editing && workshopSession.PlanarView != null && workshopSession.PlanarView.Is2D;
            if (rotationCircle != null && rotationCircle.gameObject.activeSelf != (editing && !planar))
                rotationCircle.gameObject.SetActive(editing && !planar);
            if (editing && !planar && rotationCircle != null)
            {
                Rect viewport = workshopSession.RotationViewport;
                rotationCircle.SetGeometry(viewport, GetWorkshopRotationRadius(viewport));
            }
            if (core == null || view == null || planar)
            {
                if (frontMark != null) frontMark.gameObject.SetActive(false);
                if (upMark != null) upMark.gameObject.SetActive(false);
                return;
            }
            // 정면은 작업대 방향 오버레이가 표시하고, 기존 UP 표식만 유지한다.
            var assembly = core.GetComponent<CubeAssembly>();
            if (frontMark != null) frontMark.gameObject.SetActive(false);
            UpdateDirectionMark(upMark, Vector3Int.up, core, view, assembly, upGraphics, upAlphas);
        }
        private void UpdateDirectionMark(RectTransform mark, Vector3Int axis, PowerCore core, Camera view,
            CubeAssembly assembly, UnityEngine.UI.Graphic[] graphics, float[] alphas)
        {
            if (mark == null) return;
            Vector3 localFace = assembly != null ? assembly.GetOutermostFace(axis) : (Vector3)axis * .501f;
            Transform space = assembly != null ? assembly.transform : core.transform;
            Vector3 facePoint = space.TransformPoint(localFace);
            Vector3 screenPoint = view.WorldToScreenPoint(facePoint);
            bool visible = screenPoint.z > view.nearClipPlane;
            mark.gameObject.SetActive(visible);
            float opacity = Vector3.Dot(view.transform.forward, core.transform.TransformDirection((Vector3)axis)) > directionBackThreshold ? .2f : 1f;
            if (graphics != null && alphas != null)
                for (int i = 0; i < graphics.Length; i++)
                {
                    if (graphics[i] == null) continue;
                    var color = graphics[i].color; color.a = alphas[i] * opacity; graphics[i].color = color;
                }
            if (!visible) return;
            if (mark.parent is RectTransform parent)
            {
                Canvas canvas = mark.GetComponentInParent<Canvas>();
                Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, camera, out Vector2 position))
                    mark.localPosition = new Vector3(position.x, position.y, 0f);
            }
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
            SetWorkshopUI(false);
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
