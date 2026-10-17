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
        [SerializeField] private WorkshopSession workshopSession;
        private InputActionMap controls;
        private Coroutine cursorRestore;
        private readonly System.Collections.Generic.List<GameObject> effects = new System.Collections.Generic.List<GameObject>();
        public static GameManager Instance { get; private set; }
        // 글자 입력 중에는 F(상호작용)·단축키 등 게임 조작이 입력과 함께 실행되지 않게 막는다.
        public static bool GameplayBlocked => ModalBlocked || TextInputFocused;
        // 창·대화·일시정지 등 화면 상태로만 막힌 경우. 입력칸에서 바로 누를 수 있어야 하는 UI 버튼의 활성 판정에 쓴다.
        public static bool ModalBlocked => Instance != null && (Instance.IsPaused || Instance.InDialogue || Instance.IsNight || Instance.InTargetSelection || Instance.InManual || (Instance.UI != null && Instance.UI.IsChoiceOpen));
        public static bool TextInputFocused
        {
            get
            {
                var selected = UnityEngine.EventSystems.EventSystem.current != null ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
                return selected != null && selected.TryGetComponent<TMPro.TMP_InputField>(out var field) && field.isFocused;
            }
        }
        public bool InManual { get; private set; }
        public event System.Action ManualCloseRequested;
        public void SetManual(bool open) { InManual = open; RestoreCursor(); }
        public bool InTargetSelection { get; private set; }
        public event System.Action TargetSelectionCloseRequested;
        public void SetTargetSelection(bool open)
        {
            InTargetSelection = open;
            RestoreCursor();
        }
        public bool InDialogue { get; private set; }
        public bool IsNight { get; private set; }
        public bool IsPaused { get; private set; }
        public bool CanLeaveWorkshop => workshopSession != null && workshopSession.IsInWorkshop;

        [SerializeField] private PoolManager combatPoolManagerPrefab;

        private void Awake()
        {
            Instance = this;
            PoolManager.Ensure(combatPoolManagerPrefab);
            Time.timeScale = 1f;
            IsPaused = false;
            IsNight = false;
            InDialogue = false;
            InTargetSelection = false;
            InManual = false;
            if (inputActions == null) return;
            controls = inputActions.FindActionMap("Game", true).Clone();
            controls.FindAction("Pause", true).performed += _ => TogglePause();
        }
        private void OnEnable() => controls?.Enable();
        private void Start()
        {
            // LogKit 자동 초기화(AfterSceneLoad)가 첫 씬 Awake보다 늦으므로 Start에서 기록한다.
            GameLogger.Data("Info", "System", "씬 시작", new SceneBootLog { scene = gameObject.scene.name });
            uiManager?.Refresh(this);
        }
        private void OnDisable()
        {
            if (cursorRestore != null) StopCoroutine(cursorRestore);
            cursorRestore = null;
            controls?.Disable();
            Time.timeScale = 1f;
        }
        private void OnDestroy()
        {
            controls?.Dispose();
            if (Instance == this) Instance = null;
        }
        public void TogglePause()
        {
            // ESC는 입력칸 선택 중에도 창 닫기·작업대 나가기로 동작하므로 화면 상태(ModalBlocked)만 본다.
            if (!ModalBlocked && uiManager != null && uiManager.GetComponent<ShootingTestHUD>()?.CloseRetry() == true) return;
            var tutorialSkip = uiManager != null ? uiManager.GetComponent<TutorialSkipUI>() : null;
            if (tutorialSkip != null && tutorialSkip.IsOpen) return;
            var recipe = uiManager != null ? uiManager.GetComponent<WeaponWorkshop.WeaponRecipeUI>() : null;
            if (recipe != null && recipe.IsOpen) { recipe.Close(); return; }
            if (InManual) { ManualCloseRequested?.Invoke(); return; }
            if (uiManager != null && uiManager.IsChoiceOpen)
            {
                if (!uiManager.Feedback.IsChoiceInputSuspended) uiManager.Feedback.Cancel();
                return;
            }
            var presets = uiManager != null ? uiManager.GetComponent<PresetPanelUI>() : null;
            if (presets != null && presets.IsOpen) { presets.Close(); return; }
            var weaponCode = uiManager != null ? uiManager.GetComponent<WeaponWorkshop.WeaponCodeUI>() : null;
            if (weaponCode != null && weaponCode.IsOpen) { weaponCode.Close(); return; }
            var weaponName = uiManager != null ? uiManager.GetComponent<WeaponWorkshop.WeaponNameUI>() : null;
            if (weaponName != null && weaponName.CancelEditing()) return;
            if (InTargetSelection) { TargetSelectionCloseRequested?.Invoke(); return; }
            if (!ModalBlocked)
            {
                var session = workshopSession;
                if (session == null && uiManager != null && uiManager.Player != null)
                    session = uiManager.Player.GetComponent<WorkshopSession>();
                if (session != null && session.IsInWorkshop)
                {
                    if (!session.IsTransitioning) session.Leave();
                    return;
                }
            }
            if (IsNight) return;
            IsPaused = !IsPaused;
            Time.timeScale = IsPaused ? 0f : 1f;
            GameLogger.Info("Game", IsPaused ? "일시정지" : "일시정지 해제");
            uiManager?.Refresh(this);
            RestoreCursor();
            if (!IsPaused)
            {
                if (cursorRestore != null) StopCoroutine(cursorRestore);
                cursorRestore = StartCoroutine(RestoreCursorAfterPause());
            }
        }
        private IEnumerator RestoreCursorAfterPause()
        {
            var pause = controls != null ? controls.FindAction("Pause") : null;
            while (pause != null && pause.IsPressed()) yield return null;
            yield return null;
            if (Application.isFocused) RestoreCursor();
            yield return new WaitForEndOfFrame();
            if (Application.isFocused) RestoreCursor();
            cursorRestore = null;
        }
        public void Resume()
        {
            if (IsPaused) TogglePause();
        }
        public void LeaveWorkshop()
        {
            if (IsNight) return;
            Resume();
            workshopSession?.Leave();
        }
        public void Restart()
        {
            uiManager?.GetComponent<ShootingTestHUD>()?.CancelTest();
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
            IsPaused = false;
            Time.timeScale = 1f;
            uiManager?.Refresh(this);
            RestoreCursor();
        }
        // 대화 중엔 이동/사격/상호작용을 막고 커서를 풂.
        public void SetDialogue(bool open)
        {
            InDialogue = open;
            RestoreCursor();
        }
        public void SetNight(bool night)
        {
            if (night) uiManager?.GetComponent<ShootingTestHUD>()?.CancelTest();
            if (night) ManualCloseRequested?.Invoke();
            if (night) TargetSelectionCloseRequested?.Invoke();
            IsNight = night;
            IsPaused = false;
            Time.timeScale = night ? 0f : 1f;
            uiManager?.Refresh(this);
            RestoreCursor();
        }
        public void RegisterEffect(GameObject effect)
        {
            effects.RemoveAll(item => item == null);
            if (effect != null) effects.Add(effect);
        }
        private void LateUpdate()
        {
            if (GameplayBlocked) RestoreCursor();
        }
        public void RefreshInputState() => RestoreCursor();
        private void RestoreCursor()
        {
            bool visible = GameplayBlocked || (workshopSession != null && workshopSession.IsInWorkshop);
            Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = visible;
        }
    }
}
