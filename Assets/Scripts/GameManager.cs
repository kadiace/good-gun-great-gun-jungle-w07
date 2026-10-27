using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using LogKit;

namespace WeaponWorkshop
{
    [DefaultExecutionOrder(-200)]
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private UIManager uiManager;
        public UIManager UI => uiManager;
        // 플레이어 찾기 창구는 UIManager가 아니라 GameManager의 소유다.
        // 인스펙터 연결이 비어 있으면 씬에서 한 번 찾아 캐시한다.
        [SerializeField] private FirstPersonPlayer player;
        public FirstPersonPlayer Player
        {
            get
            {
                if (player == null) player = FindFirstObjectByType<FirstPersonPlayer>();
                return player;
            }
        }
        [SerializeField] private WorkshopSession workshopSession;
        private InputActionMap controls;
        private Coroutine cursorRestore;
        private UICancelRouter m_cancelRouter;
        private readonly System.Collections.Generic.List<GameObject> effects = new System.Collections.Generic.List<GameObject>();
        public static GameManager Instance { get; private set; }
        // 글자 입력 중에는 F(상호작용)·단축키 등 게임 조작이 입력과 함께 실행되지 않게 막는다.
        // 진입 커버가 떠 있는 동안에는 게임 조작도 막는다.
        public static bool GameplayBlocked => UIInputPolicy.IsGameplayBlocked;
        // 창·대화·일시정지 등 화면 상태로만 막힌 경우. 입력칸에서 바로 누를 수 있어야 하는 UI 버튼의 활성 판정에 쓴다.
        public static bool ModalBlocked => UIInputPolicy.IsModalBlocked;
        public static bool TextInputFocused => UIInputPolicy.IsTextInputFocused;
        public bool InManual => UIInputPolicy.InManual;
        public event System.Action ManualCloseRequested;
        public void SetManual(bool open) { UIInputPolicy.SetManual(open); }
        public bool InTargetSelection => UIInputPolicy.InTargetSelection;
        public event System.Action TargetSelectionCloseRequested;
        public void SetTargetSelection(bool open)
        {
            UIInputPolicy.SetTargetSelection(open);
        }
        public bool InDialogue => UIInputPolicy.InDialogue;
        public bool IsNight => UIInputPolicy.IsNight;
        public bool IsPaused => UIInputPolicy.IsPaused;
        public bool CanLeaveWorkshop => workshopSession != null && workshopSession.IsInWorkshop;

        [SerializeField] private PoolManager combatPoolManagerPrefab;

        private void Awake()
        {
            Instance = this;
            PoolManager.Ensure(combatPoolManagerPrefab);
            UIInputPolicy.ResetToGameplay();
            m_cancelRouter = new UICancelRouter(this, uiManager, workshopSession,
                TogglePauseState, Resume, RequestManualClose, RequestTargetSelectionClose);
            if (inputActions == null) return;
            controls = inputActions.FindActionMap("Game", true).Clone();
            controls.FindAction("Pause", true).performed += OnPausePerformed;
            UIInputPolicy.RegisterGameMap(controls);
        }
        private void OnPausePerformed(InputAction.CallbackContext _context)
        {
            TogglePause();
        }
        private void OnEnable() => UIInputPolicy.RequestActionMapEnabled(controls, true);
        private void Start()
        {
            // LogKit 자동 초기화(AfterSceneLoad)가 첫 씬 Awake보다 늦으므로 Start에서 기록한다.
            PlayerIdentity.LogSessionStart();
            GameLogger.Data("Info", "System", "씬 시작", new SceneBootLog
            { scene = gameObject.scene.name, installId = PlayerIdentity.InstallId, saveId = PlayerIdentity.SaveId });
            uiManager?.Screen<PauseUI>()?.Refresh(this);
        }
        private void OnDisable()
        {
            if (cursorRestore != null) StopCoroutine(cursorRestore);
            cursorRestore = null;
            UIInputPolicy.RequestActionMapEnabled(controls, false);
            UIInputPolicy.UnregisterGameMap(controls);
        }
        private void OnDestroy()
        {
            UIInputPolicy.UnregisterGameMap(controls);
            controls?.Dispose();
            if (Instance == this) Instance = null;
        }
        // ESC의 화면별 우선순위는 UICancelRouter가 결정하고, 여기서는 진입 차단만 확인한다.
        public void TogglePause()
        {
            if (NightRaidEntryOverlay.InputBlocked)
            {
                return;
            }
            m_cancelRouter?.RouteCancel();
        }
        private void TogglePauseState()
        {
            SetPaused(!IsPaused);
        }
        private void SetPaused(bool _paused)
        {
            if (IsPaused == _paused)
            {
                return;
            }
            UIInputPolicy.SetPaused(_paused);
            GameLogger.Info("Game", IsPaused ? "일시정지" : "일시정지 해제");
            uiManager?.Screen<PauseUI>()?.Refresh(this);
            if (!IsPaused)
            {
                if (cursorRestore != null) StopCoroutine(cursorRestore);
                cursorRestore = StartCoroutine(RestoreCursorAfterPause());
            }
        }
        private void RequestManualClose()
        {
            ManualCloseRequested?.Invoke();
        }
        private void RequestTargetSelectionClose()
        {
            TargetSelectionCloseRequested?.Invoke();
        }
        private IEnumerator RestoreCursorAfterPause()
        {
            var pause = controls != null ? controls.FindAction("Pause") : null;
            while (pause != null && pause.IsPressed()) yield return null;
            yield return null;
            if (Application.isFocused) UIInputPolicy.RefreshCursor();
            yield return new WaitForEndOfFrame();
            if (Application.isFocused) UIInputPolicy.RefreshCursor();
            cursorRestore = null;
        }
        // UI 버튼 등 프로그램 경로는 화면 취소 라우팅을 거치지 않고 실제로 재개한다.
        public void Resume()
        {
            if (NightRaidEntryOverlay.InputBlocked)
            {
                return;
            }
            if (IsPaused)
            {
                SetPaused(false);
            }
        }
        public void LeaveWorkshop()
        {
            if (IsNight) return;
            Resume();
            workshopSession?.Leave();
        }
        public void Restart()
        {
            uiManager?.Screen<ShootingTestHUD>()?.CancelTest();
            ManualCloseRequested?.Invoke();
            TargetSelectionCloseRequested?.Invoke();
            if (IsNight) return;
            PoolManager.ClearActive();
            foreach (GameObject effect in effects) if (effect != null) Destroy(effect);
            effects.Clear();
            foreach (EnergyCube projectile in FindObjectsByType<EnergyCube>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                projectile.gameObject.SetActive(false);
                if (projectile.GetComponent<PooledCombatObject>() == null) Destroy(projectile.gameObject);
            }
            foreach (PowerCore core in FindObjectsByType<PowerCore>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                core.ResetFireCooldown();
            UIInputPolicy.ResetToGameplay();
            uiManager?.Screen<PauseUI>()?.Refresh(this);
        }
        // 대화 중엔 이동/사격/상호작용을 막고 커서를 풂.
        public void SetDialogue(bool open)
        {
            UIInputPolicy.SetDialogue(open);
        }
        public void SetNight(bool night)
        {
            if (night) uiManager?.Screen<ShootingTestHUD>()?.CancelTest();
            if (night) ManualCloseRequested?.Invoke();
            if (night) TargetSelectionCloseRequested?.Invoke();
            UIInputPolicy.SetNight(night);
            uiManager?.Screen<PauseUI>()?.Refresh(this);
        }
        public void RegisterEffect(GameObject effect)
        {
            effects.RemoveAll(item => item == null);
            if (effect != null) effects.Add(effect);
        }
        private void LateUpdate()
        {
            if (GameplayBlocked) UIInputPolicy.RefreshCursor();
        }
        public void RefreshInputState() => UIInputPolicy.RefreshCursor();
        private void RestoreCursor() => UIInputPolicy.RefreshCursor();
    }
}
