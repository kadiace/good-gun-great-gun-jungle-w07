using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace WeaponWorkshop
{
    // Owns tutorial lifetime, progression, target selection and interaction permissions.
    [DisallowMultipleComponent, DefaultExecutionOrder(-100)]
    public sealed partial class TutorialManager : MonoBehaviour
    {
        private const float RETIREMENT_CAMERA_DURATION = 2f;

        [SerializeField] private DialogueUI dialogue;
        [SerializeField, Tooltip("Tutorial log guideMode. Policy names are not decided yet; empty by default.")] private string guideMode = "";
        [SerializeField] private TutorialSkipUI selection;
        [SerializeField] private DayTransitionUI dayTransition;
        [SerializeField] private NextDayInteractable shutter;
        [SerializeField] private Bell customerBell;
        [SerializeField] private CoreDispenser mainCoreDispenser;
        [SerializeField] private Workbench workbench;
        [SerializeField] private DestructibleTarget shootingTargetPrefab;
        [SerializeField] private Transform shootingSpawnPoint;
        [SerializeField] private string customerWarning = "저기, 조심 좀 하시죠!";
        private DestructibleTarget shootingTarget;
        private readonly Queue<Customer> pendingWarnings = new Queue<Customer>();
        private bool warningOpen;
        private Coroutine warningTurn;
        [SerializeField, Min(.01f)] private float warningTurnDuration = .25f;
        [SerializeField] private string blockedInteractionMessage = "튜토리얼 중에는 안내된 대상과만 상호작용할 수 있습니다.";
        [Header("튜토리얼 은퇴 소개")]
        [SerializeField] private Transform _retirementShotPoint;
        [SerializeField] private GameObject[] _retirementHudElements = Array.Empty<GameObject>();
        private readonly List<RetirementHudState> _retirementHudStates = new List<RetirementHudState>();
        private readonly Dictionary<Renderer, bool> _retirementWeaponRendererStates = new Dictionary<Renderer, bool>();
        private Camera _retirementCamera;
        private WeaponScope _retirementWeaponScope;
        private Vector3 _retirementCameraStartPosition;
        private Quaternion _retirementCameraStartRotation;
        private Coroutine _retirementCameraRoutine;
        private bool _retirementPresentationActive;
        private bool _retirementCameraOverrideActive;
        private bool _retirementOwnsInputBlock;
        private bool _retirementIntroductionAwaitingConfirmation;
        private bool _retirementFinalAwaitingConfirmation;
        private bool _retirementReturnInProgress;
        private bool _retirementReturnOnNextStep;

        private sealed class RetirementHudState
        {
            public CanvasGroup Group { get; }
            public float Alpha { get; }
            public bool Interactable { get; }
            public bool BlocksRaycasts { get; }
            public bool AddedByPresentation { get; }

            /// <summary>
            /// HUD CanvasGroup의 변경 전 값을 저장해 연출이 끝난 뒤 복원할 수 있게 합니다.
            /// </summary>
            /// <param name="group">연출 전에 상태를 보관할 HUD CanvasGroup입니다.</param>
            /// <param name="addedByPresentation">CanvasGroup을 이번 연출에서 추가했는지 나타냅니다.</param>
            public RetirementHudState(CanvasGroup group, bool addedByPresentation)
            {
                Group = group;
                Alpha = group.alpha;
                Interactable = group.interactable;
                BlocksRaycasts = group.blocksRaycasts;
                AddedByPresentation = addedByPresentation;
            }
        }

        private _DayManager dayZeroOwner;
        private DayTransitionUI endingTransition;
        private sealed class Request
        {
            public TutorialSequence sequence;
            public Component owner;
            public Action done;
            public Action<bool> completed;
        }
        private readonly Queue<Request> waiting = new Queue<Request>();
        private Request current;
        public static TutorialManager Instance { get; private set; }
        public TutorialSequence CurrentSequence => current != null ? current.sequence : null;
        public int StepIndex { get; private set; }
        public TutorialStep CurrentStep => CurrentSequence != null && StepIndex < CurrentSequence.steps.Length
            ? CurrentSequence.steps[StepIndex] : null;
        public bool IsRunning => current != null;
        public bool IsTutorialActive => dayZeroOwner != null || IsRunning;
        public Component CurrentInteractionTarget
        {
            get
            {
                var step = CurrentStep;
                if (step == null || step.completion == TutorialCompletion.Confirmation) return null;
                if (step.requiresWorkbenchEditing && !IsEditingTutorialWorkbench) return workbench;
                switch (step.target)
                {
                    case TutorialTarget.Shutter: return shutter;
                    case TutorialTarget.Customer: return customerBell != null ? customerBell.Current : null;
                    case TutorialTarget.MainCoreDispenser: return mainCoreDispenser;
                    case TutorialTarget.Workbench: return workbench;
                    case TutorialTarget.ShootingTarget: return shootingTarget;
                    case TutorialTarget.WorkbenchWeapon: return guidedWeapon;
                    case TutorialTarget.CloseShop: return closeShop;
                    case TutorialTarget.RaidEntrance: return raidEntrance;
                    case TutorialTarget.RaidLoot: return RaidLootTarget();
                    case TutorialTarget.RaidExit: return RaidExitTarget();
                    case TutorialTarget.RaidMaterialCrate: return RaidMaterialCrateTarget();
                    case TutorialTarget.RaidTank: return RaidTankTarget();
                    default: return null;
                }
            }
        }
        public event Action Changed;
        public Workbench TutorialWorkbench => workbench;
        public bool AllowsModule(int moduleId)
        {
            var allowed = CurrentStep != null ? CurrentStep.allowedModuleIds : null;
            return allowed == null || allowed.Length == 0 || Array.IndexOf(allowed, moduleId) >= 0;
        }
        public bool TryAuthorizeAttachAction()
        {
            if (CurrentStep != null && CurrentStep.completion == TutorialCompletion.Signal &&
                CurrentStep.requiredSignal == "recipe-inspected")
            {
                FeedbackUI.For(this)?.ShowNotification("먼저 레시피를 확인하세요.");
                return false;
            }
            bool allowed = TryAuthorizeAction("attach");
            if (!allowed && CurrentStep != null && CurrentStep.completion == TutorialCompletion.WorkshopExited)
                FeedbackUI.For(this)?.ShowNotification("무기가 완성되었습니다. 작업대에서 나와 무기를 집으세요.");
            return allowed;
        }
        public bool TryAuthorizePlacement(CubeAssembly assembly, Vector3Int cell, int moduleId, Vector3Int direction)
        {
            var step = CurrentStep;
            if (step == null) return true;
            if (!TryAuthorizeAttachAction()) return false;
            if (step.completion != TutorialCompletion.FiveCoreFacesBlocked && !step.restrictPlacement) return true;
            bool fiveFaces = step.completion == TutorialCompletion.FiveCoreFacesBlocked;
            bool allowed = AllowsPlacement(assembly, cell, moduleId, direction);
            if (!allowed)
            {
                string message = !fiveFaces && moduleId != step.requiredModuleId && !string.IsNullOrEmpty(step.wrongModuleMessage)
                    ? step.wrongModuleMessage : step.blockedPlacementMessage;
                FeedbackUI.For(this)?.ShowNotification(fiveFaces ? "지금은 사출구를 한 개로 만들어 봅시다." : message);
            }
            return allowed;
        }

        // 그리드 호버 표시용. 거부 메시지나 입력 부작용 없이 같은 배치 조건을 조회한다.
        public bool AllowsPlacement(CubeAssembly assembly, Vector3Int cell, int moduleId, Vector3Int direction)
        {
            var step = CurrentStep;
            if (step == null) return true;
            if (!AllowsAction("attach") || (step.completion == TutorialCompletion.Signal && step.requiredSignal == "recipe-inspected"))
                return false;
            if (step.completion != TutorialCompletion.FiveCoreFacesBlocked && !step.restrictPlacement) return true;
            var weapon = workbench != null ? workbench.CurrentWeapon : null;
            var expected = weapon != null ? weapon.GetComponentInChildren<CubeAssembly>() : null;
            return expected != null && assembly == expected && (step.completion == TutorialCompletion.FiveCoreFacesBlocked
                ? moduleId == 1 && cell != step.openCoreFace && Mathf.Abs(cell.x) + Mathf.Abs(cell.y) + Mathf.Abs(cell.z) == 1
                : moduleId == step.requiredModuleId && cell == step.requiredCell && step.MatchesOutputDirection(direction));
        }
        private bool ownsDialogue;
        private bool stepIntroductionOpen;
        private float nextBlockedActionNoticeTime;
        private bool tankSpawnGuidanceOpen;
        private bool loanGuideShown;

        private void OnEnable() => RaidEnemyCore.Spawned += HandleRaidEnemySpawned;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this; current = null; StepIndex = 0; ownsDialogue = false; stepIntroductionOpen = false; dayZeroOwner = null;
            if (shutter == null) shutter = FindFirstObjectByType<NextDayInteractable>();
            if (customerBell == null) customerBell = FindFirstObjectByType<Bell>();
            if (workbench == null) workbench = FindFirstObjectByType<Workbench>();
            closeShop = FindFirstObjectByType<CloseShopButton>();
            raidEntrance = FindFirstObjectByType<NightRaidEntrance>();
            if (mainCoreDispenser == null)
                foreach (var dispenser in FindObjectsByType<CoreDispenser>(FindObjectsSortMode.None))
                    if (dispenser.IsBasicCoreDispenser) { mainCoreDispenser = dispenser; break; }
        }
        private void Start() { if (TrainingRaidPending) StartCoroutine(ResumeTrainingRaid()); }
        private void ResolvePresentation()
        {
            var ui = GameManager.Instance != null ? GameManager.Instance.UI : null;
            if (ui == null) ui = FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
            if (ui == null) return;
            if (dialogue == null) dialogue = ui.GetComponentInChildren<DialogueUI>(true);
            if (selection == null) selection = ui.GetComponent<TutorialSkipUI>();
            if (dayTransition == null) dayTransition = ui.GetComponent<DayTransitionUI>();
        }
        public void BeginDayZero(_DayManager day)
        {
            if (day == null || dayZeroOwner != null) return;
            dayZeroOwner = day;
            warningOpen = false; pendingWarnings.Clear();
            ResolvePresentation();
            if (selection == null || !selection.Show(() => { LogTutorialSkipped(); EndDayZero(); }, StartDayZeroSequence)) StartDayZeroSequence();
        }
        private void StartDayZeroSequence()
        {
            BeginPractice();
            Run(dayZeroOwner.CurrentCustomerDay.startTutorial, dayZeroOwner, EndDayZero);
        }
        /// <summary>
        /// 튜토리얼을 마치고 기존 1일차 전환을 시작합니다.
        /// </summary>
        private void EndDayZero()
        {
            ResolvePresentation();
            endingTransition = dayTransition;
            if (endingTransition != null)
            {
                endingTransition.Completed += CompleteDayZero;
                endingTransition.Covered += PrepareFirstDay;
                if (_retirementPresentationActive) endingTransition.DayTextShown += RestoreRetirementPresentationOnDayText;
                if (endingTransition.BeginFirstDay(dayZeroOwner)) return;
                endingTransition.Completed -= CompleteDayZero;
                endingTransition.Covered -= PrepareFirstDay;
                endingTransition.DayTextShown -= RestoreRetirementPresentationOnDayText;
            }
            RestoreRetirementPresentation();
            PrepareFirstDay();
            if (dayZeroOwner != null) dayZeroOwner.BeginFirstBusinessDay();
            CompleteDayZero();
        }
        /// <summary>
        /// 0일차 튜토리얼 상태와 연출 참조를 정리합니다.
        /// </summary>
        private void CompleteDayZero()
        {
            RestoreRetirementPresentation();
            ClearShootingTarget(); pendingWarnings.Clear();
            if (endingTransition != null)
            {
                endingTransition.Completed -= CompleteDayZero;
                endingTransition.Covered -= PrepareFirstDay;
                endingTransition.DayTextShown -= RestoreRetirementPresentationOnDayText;
            }
            endingTransition = null; dayZeroOwner = null; Changed?.Invoke();
        }
        private void PrepareFirstDay()
        {
            var ui = GameManager.Instance != null ? GameManager.Instance.UI : null;
            if (ui != null && ui.Player != null)
            {
                var session = ui.Player.GetComponent<WorkshopSession>();
                if (session != null) session.LeaveImmediately();
            }
            EndPractice();
        }
        public static void Run(TutorialSequence sequence, Component owner, Action done = null, Action<bool> completed = null)
        {
            if (sequence == null || sequence.steps == null || sequence.steps.Length == 0 || Instance == null || !Instance.isActiveAndEnabled)
            { completed?.Invoke(false); done?.Invoke(); return; }
            // The authored day-zero sequence replaces legacy customer hints during its entire lifetime.
            if (Instance.dayZeroOwner != null && owner != Instance.dayZeroOwner) { completed?.Invoke(false); done?.Invoke(); return; }
            Instance.waiting.Enqueue(new Request { sequence = sequence, owner = owner, done = done, completed = completed });
            Instance.BeginNext();
        }
        private void BeginNext()
        {
            if (current != null || waiting.Count == 0) return;
            current = waiting.Dequeue(); StepIndex = 0; withheldBalance = 0; loanGuideShown = false; ShowStep();
        }
        /// <summary>
        /// 현재 단계의 준비·대사·은퇴 소개 연출을 표시합니다.
        /// </summary>
        private void ShowStep()
        {
            stepIntroductionOpen = false;
            nextBlockedActionNoticeTime = 0f;
            ResolvePresentation();
            while (CurrentSequence != null && StepIndex < CurrentSequence.steps.Length && CurrentSequence.steps[StepIndex] == null) StepIndex++;
            var step = CurrentStep;
            if (step == null) { Finish(); return; }
            LogStepStarted(step);
            PrepareStep(step);
            if (step.target == TutorialTarget.ShootingTarget && shootingTarget == null &&
                shootingTargetPrefab != null && shootingSpawnPoint != null)
            {
                shootingTarget = Instantiate(shootingTargetPrefab, shootingSpawnPoint.position, shootingSpawnPoint.rotation, transform);
                var body = shootingTarget.GetComponent<Rigidbody>();
                if (body == null) body = shootingTarget.gameObject.AddComponent<Rigidbody>();
                var falling = shootingTarget.GetComponent<FallingRangeTarget>();
                if (falling == null) falling = shootingTarget.gameObject.AddComponent<FallingRangeTarget>();
                body.WakeUp();
            }
            Changed?.Invoke();
            if (step.completion == TutorialCompletion.RetirementIntroduction)
            {
                if (!BeginRetirementIntroduction(step)) Finish("error");
                return;
            }
            if (_retirementReturnOnNextStep)
            {
                BeginRetirementFinalStep(step);
                return;
            }
            if (step.completion == TutorialCompletion.Confirmation ||
                (step.completion == TutorialCompletion.Signal && step.showDescriptionBeforeObjective &&
                    !string.IsNullOrWhiteSpace(step.description)))
            {
                if (dialogue == null) { Debug.LogError("튜토리얼 대화 UI 연결이 없습니다.", this); Finish(); return; }
                stepIntroductionOpen = step.completion == TutorialCompletion.Signal;
                ownsDialogue = true;
                if (!dialogue.Show(step.description, "확인", Confirm, "튜토리얼", step.confirmOnBackgroundClick,
                        step.dialogueInputLockSeconds))
                { ownsDialogue = false; Finish("error"); }
            }
            else if (step.completion == TutorialCompletion.RaidTankSpawned)
            {
                ReleaseDialogue();
                if (HasActiveRaidTank()) ShowRaidTankGuidance();
            }
            else ReleaseDialogue();
        }
        public bool ShowLoanGuide(FeedbackUI feedback)
        {
            var step = CurrentStep;
            if (loanGuideShown || step == null || step.requiredSignal != "loan-taken" ||
                string.IsNullOrEmpty(step.choiceGuidance) || feedback == null || !feedback.IsChoiceOpen) return false;
            ResolvePresentation();
            if (dialogue == null || dialogue.IsOpen) return false;
            feedback.SuspendChoiceInput();
            ownsDialogue = true;
            if (dialogue.Show(step.choiceGuidance, "확인", () =>
                {
                    ReleaseDialogue();
                    feedback.ResumeChoiceInput();
                }, "튜토리얼"))
            {
                loanGuideShown = true;
                return true;
            }
            ownsDialogue = false;
            feedback.ResumeChoiceInput();
            return false;
        }
        private static bool HasActiveRaidTank()
        {
            for (int i = 0; i < RaidEnemyCore.ActiveCount; i++)
            {
                var enemy = RaidEnemyCore.ActiveAt(i);
                if (enemy != null && !enemy.IsDestroyed && enemy.EnemyType == RaidEnemyType.Tank) return true;
            }
            return false;
        }
        private void HandleRaidEnemySpawned(RaidEnemyCore enemy)
        {
            if (!TrainingRaidPending || enemy == null || enemy.EnemyType != RaidEnemyType.Tank ||
                CurrentStep == null || CurrentStep.completion != TutorialCompletion.RaidTankSpawned) return;
            ShowRaidTankGuidance();
        }
        private void ShowRaidTankGuidance()
        {
            if (CurrentStep == null || CurrentStep.completion != TutorialCompletion.RaidTankSpawned || ownsDialogue) return;
            ResolvePresentation();
            if (dialogue == null) { Debug.LogError("튜토리얼 대화 UI 연결이 없습니다.", this); Finish("error"); return; }
            ownsDialogue = true;
            tankSpawnGuidanceOpen = true;
            if (!dialogue.Show(CurrentStep.description, "확인", Confirm, "튜토리얼"))
            { ownsDialogue = false; tankSpawnGuidanceOpen = false; Finish("error"); }
        }
        /// <summary>
        /// 대화 확인을 처리하고 은퇴 소개와 마지막 안내의 카메라 복귀를 진행합니다.
        /// </summary>
        private void Confirm()
        {
            var step = CurrentStep;
            if (step == null) return;
            if (_retirementIntroductionAwaitingConfirmation)
            {
                _retirementIntroductionAwaitingConfirmation = false;
                _retirementReturnOnNextStep = true;
                ReleaseDialogue();
                Advance();
                return;
            }
            if (_retirementFinalAwaitingConfirmation)
            {
                if (_retirementReturnInProgress) CompleteRetirementCameraReturn();
                _retirementFinalAwaitingConfirmation = false;
                ReleaseDialogue();
                Advance();
                return;
            }
            if (stepIntroductionOpen)
            {
                ReleaseDialogue();
                Changed?.Invoke();
                return;
            }
            if (step.completion == TutorialCompletion.Confirmation) { Advance(); return; }
            if (step.completion != TutorialCompletion.RaidTankSpawned || !tankSpawnGuidanceOpen) return;
            tankSpawnGuidanceOpen = false;
            Advance();
        }
        public void NotifyTargetHit(DestructibleTarget target)
        {
            if (target == null || target != shootingTarget || CurrentStep == null ||
                CurrentStep.target != TutorialTarget.ShootingTarget) return;
            NotifySignal("shooting-target-hit", target);
        }
        public bool HandleCustomerHit(Customer customer)
        {
            if (dayZeroOwner == null || customer == null) return false;
            if (CurrentStep != null && CurrentStep.completion == TutorialCompletion.CustomerKilled &&
                CurrentStep.target == TutorialTarget.Customer && customer == CurrentInteractionTarget) return false;
            if (warningOpen || pendingWarnings.Count > 0) return true;
            pendingWarnings.Enqueue(customer);
            return true;
        }
        private void LateUpdate()
        {
            bool saleCompleted = CurrentStep != null && (CurrentStep.completion == TutorialCompletion.CustomerSold ||
                CurrentStep.completion == TutorialCompletion.CustomerKilled);
            if (!stepIntroductionOpen && (!GameManager.GameplayBlocked || saleCompleted) &&
                !warningOpen && (AreFiveFacesBlocked() || IsActionStepComplete())) Advance();
            if (pendingWarnings.Count == 0 || warningOpen || dialogue == null || dialogue.IsOpen ||
                (dayTransition != null && dayTransition.IsTransitioning)) return;
            var customer = pendingWarnings.Dequeue();
            if (customer == null) return;
            warningOpen = true;
            if (GameManager.Instance != null) GameManager.Instance.SetDialogue(true);
            warningTurn = StartCoroutine(TurnToCustomer(customer));
        }
        private bool AreFiveFacesBlocked()
        {
            var step = CurrentStep;
            if (step == null || step.completion != TutorialCompletion.FiveCoreFacesBlocked || workbench == null) return false;
            var weapon = workbench.CurrentWeapon;
            var assembly = weapon != null ? weapon.GetComponentInChildren<CubeAssembly>() : null;
            if (assembly == null) return false;
            var blocks = assembly.GetComponentsInChildren<PlacedBlock>();
            int count = 0;
            foreach (var block in blocks)
            {
                if (block.transform.parent != assembly.transform) continue;
                if (block.ModuleSlot != 1 || block.Kind != BlockKind.Blocker || block.Cell == step.openCoreFace ||
                    Mathf.Abs(block.Cell.x) + Mathf.Abs(block.Cell.y) + Mathf.Abs(block.Cell.z) != 1) return false;
                count++;
            }
            var outputs = assembly.GetProjectilePaths();
            return count == 5 && outputs.Count == 1 && outputs[0].direction == step.openCoreFace;
        }
        private System.Collections.IEnumerator TurnToCustomer(Customer customer)
        {
            var ui = GameManager.Instance != null ? GameManager.Instance.UI : null;
            var player = ui != null ? ui.Player : null;
            if (player != null && player.ViewPoint != null)
            {
                Vector3 direction = customer.transform.position + Vector3.up - player.ViewPoint.position;
                if (direction.sqrMagnitude > .001f)
                {
                    var rotation = Quaternion.LookRotation(direction);
                    Quaternion bodyStart = player.transform.rotation;
                    Quaternion viewStart = player.ViewPoint.localRotation;
                    Quaternion bodyEnd = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
                    Quaternion viewEnd = Quaternion.Euler(Mathf.DeltaAngle(0f, rotation.eulerAngles.x), 0f, 0f);
                    float elapsed = 0f;
                    while (elapsed < warningTurnDuration)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / warningTurnDuration));
                        player.transform.rotation = Quaternion.Slerp(bodyStart, bodyEnd, t);
                        player.SetViewRotation(Quaternion.Slerp(viewStart, viewEnd, t));
                        yield return null;
                    }
                }
            }
            warningTurn = null;
            warningOpen = dialogue.Show(customerWarning, "죄송합니다!", () => { warningOpen = false; dialogue.Hide(); }, "손님");
            if (!warningOpen && GameManager.Instance != null) GameManager.Instance.SetDialogue(false);
        }
        private void ClearShootingTarget()
        {
            if (shootingTarget != null) Destroy(shootingTarget.gameObject);
            shootingTarget = null;
        }
        public void NotifySignal(string signal, Component source = null)
        {
            if (stepIntroductionOpen) return;
            var step = CurrentStep;
            if (step != null && step.completion == TutorialCompletion.Signal && !string.IsNullOrEmpty(step.requiredSignal) &&
                string.Equals(step.requiredSignal, signal, StringComparison.Ordinal) &&
                (step.target == TutorialTarget.None || (source != null && source == CurrentInteractionTarget))) Advance();
        }
        public bool AllowsInteraction(IInteractable target)
        {
            if (!IsTutorialActive) return true;
            var step = CurrentStep;
            if (step == null) return false;
            if (step.completion == TutorialCompletion.RaidTankSpawned) return true;
            if (step.completion == TutorialCompletion.CustomerKilled || !(target is Component component) || component == null)
                return false;
            var current = CurrentInteractionTarget;
            // 습격 픽업 목표는 상자 파괴 뒤 Transform으로 바뀌므로 컴포넌트 동일성만으로 비교하지 않는다.
            return component == current ||
                current is Component currentComponent && currentComponent != null && component.transform == currentComponent.transform;
        }
        public bool TryAuthorizeInteraction(IInteractable target)
        {
            if (AllowsInteraction(target)) return true;
            FeedbackUI.For(this)?.ShowNotification(blockedInteractionMessage);
            return false;
        }
        // Gameplay components query the same authored action permissions as the interaction policy.
        public bool AllowsAction(string action)
        {
            if (stepIntroductionOpen) return false;
            if (!IsTutorialActive) return true;
            var step = CurrentStep;
            if (step == null || !step.strictAllowedActions)
            {
                if (action == "fire") return true;
                if (action == "rotate" && IsEditingTutorialWorkbench) return true;
                // These inputs were unrestricted before strict steps were introduced.
                if (action == "select-module" || action == "zoom" || action == "scan") return true;
                if (action == "quickslot") action = "palette";
            }
            if (step == null || step.completion == TutorialCompletion.Confirmation) return false;
            if (step.requiresWorkbenchEditing && !IsEditingTutorialWorkbench) return false;
            var allowed = step.allowedActions;
            return (allowed != null && Array.IndexOf(allowed, action) >= 0) ||
                (!step.strictAllowedActions && (allowed == null || allowed.Length == 0));
        }
        public bool AllowsQuickslotSource(int moduleId) => AllowsAction("quickslot") &&
            (!IsTutorialActive || CurrentStep == null || !CurrentStep.restrictQuickslotChange ||
             moduleId == CurrentStep.requiredModuleId);

        public bool AllowsQuickslotChange(int moduleId, int targetIndex) => AllowsQuickslotSource(moduleId) &&
            (!IsTutorialActive || CurrentStep == null || !CurrentStep.restrictQuickslotChange ||
             targetIndex + 1 == CurrentStep.requiredQuickslotNumber);
        // Permission queries remain silent; only actual input attempts request feedback.
        public bool TryAuthorizeAction(string action)
        {
            if (AllowsAction(action)) return true;
            if (CurrentStep?.requiredSignal == "loan-taken")
                FeedbackUI.For(this)?.ShowNotification(blockedInteractionMessage);
            else NotifyBlockedAction();
            return false;
        }
        public bool TryAuthorizeQuickslotChange(int moduleId, int targetIndex)
        {
            if (AllowsQuickslotChange(moduleId, targetIndex)) return true;
            NotifyBlockedAction();
            return false;
        }
        public void NotifyBlockedAction()
        {
            var step = CurrentStep;
            if (!IsTutorialActive || step == null || !step.strictAllowedActions || stepIntroductionOpen ||
                GameManager.GameplayBlocked || !Application.isFocused ||
                string.IsNullOrWhiteSpace(step.blockedActionMessage) || Time.unscaledTime < nextBlockedActionNoticeTime) return;
            var feedback = FeedbackUI.For(this);
            if (feedback == null) return;
            nextBlockedActionNoticeTime = Time.unscaledTime + 2.6f;
            feedback.ShowNotification(step.blockedActionMessage);
        }
        private void Advance()
        {
            LogStepCompleted();
            if (CurrentStep != null && CurrentStep.restoreWithheldBalanceOnComplete && withheldBalance > 0)
            {
                TutorialPlayer?.GetComponent<PlayerWallet>()?.Credit(withheldBalance);
                withheldBalance = 0;
            }
            StepIndex++;
            ShowStep();
        }
        // 튜토리얼 퍼널 로그. 시각은 씬 이동·일시정지와 무관한 실행 후 실제 시간이다.
        private float stepStartedAt;
        private bool debugSkipping;
        private void LogStepStarted(TutorialStep step)
        {
            stepStartedAt = Time.realtimeSinceStartup;
            LogKit.GameLogger.Data("Info", "Tutorial", "step_started", new TutorialStepLog
            { sequence = CurrentSequence != null ? CurrentSequence.name : "", stepId = step.id ?? "", stepIndex = StepIndex, time = stepStartedAt, guideMode = guideMode ?? "" });
        }
        private void LogStepCompleted()
        {
            var step = CurrentStep;
            if (step == null) return;
            float now = Time.realtimeSinceStartup;
            LogKit.GameLogger.Data("Info", "Tutorial", "step_completed", new TutorialStepLog
            {
                sequence = CurrentSequence != null ? CurrentSequence.name : "", stepId = step.id ?? "", stepIndex = StepIndex,
                time = now, duration = Mathf.Max(0f, now - stepStartedAt), debugSkipped = debugSkipping
            });
        }
        private void LogTutorialEnded(string reason)
        {
            if (current == null || current.sequence == null) return;
            var steps = current.sequence.steps;
            // 끝까지 간 경우 StepIndex는 마지막 단계 다음을 가리킨다.
            int last = Mathf.Clamp(StepIndex, 0, steps != null && steps.Length > 0 ? steps.Length - 1 : 0);
            var step = steps != null && last < steps.Length ? steps[last] : null;
            LogKit.GameLogger.Data("Info", "Tutorial", "tutorial_ended", new TutorialEndLog
            {
                sequence = current.sequence.name, lastStepId = step != null ? step.id ?? "" : "", lastStepIndex = last,
                reason = reason, time = Time.realtimeSinceStartup
            });
        }
        // 0일차 시작 선택에서 튜토리얼을 건너뛴 경우. 단계를 하나도 시작하지 않았다.
        private void LogTutorialSkipped()
        {
            var sequence = dayZeroOwner != null && dayZeroOwner.CurrentCustomerDay != null ? dayZeroOwner.CurrentCustomerDay.startTutorial : null;
            LogKit.GameLogger.Data("Info", "Tutorial", "tutorial_skipped", new TutorialEndLog
            { sequence = sequence != null ? sequence.name : "", lastStepId = "", lastStepIndex = -1, reason = "skipped", time = Time.realtimeSinceStartup });
        }
        // 메뉴로 나가기·파산 복귀처럼 진행 중 튜토리얼을 버리는 경우.
        private static void LogAbandoned()
        {
            if (Instance != null && Instance.current != null) Instance.LogTutorialEnded("abandoned");
        }
        private void ReleaseDialogue()
        {
            stepIntroductionOpen = false;
            if (ownsDialogue && dialogue != null) dialogue.Hide();
            ownsDialogue = false;
            tankSpawnGuidanceOpen = false;
        }
        private void Finish(string reason = "completed")
        {
            LogTutorialEnded(reason);
            var done = current?.done;
            var completed = current?.completed;
            ReleaseDialogue(); current = null; StepIndex = 0; Changed?.Invoke();
            completed?.Invoke(reason == "completed");
            done?.Invoke(); BeginNext();
        }
        /// <summary>
        /// 지정한 소유자의 튜토리얼을 취소하고 연출 상태를 복원합니다.
        /// </summary>
        public void Cancel(Component owner)
        {
            int count = waiting.Count;
            for (int i = 0; i < count; i++) { var request = waiting.Dequeue(); if (request.owner != owner) waiting.Enqueue(request); }
            if (current == null || current.owner != owner) return;
            LogTutorialEnded("cancelled");
            ReleaseDialogue(); RestoreRetirementPresentation(); current = null; StepIndex = 0; Changed?.Invoke(); BeginNext();
        }
#if UNITY_EDITOR
        public void DebugEndTutorial()
        {
            if (!IsTutorialActive && (selection == null || !selection.IsOpen)) return;
            LogTutorialEnded("debug_ended");
            EndPractice();
            ClearShootingTarget();
            if (selection != null) { selection.enabled = false; selection.enabled = true; }
            enabled = false;
            enabled = true;
        }
        // 에디터 치트(F3): 현재 단계를 성공 처리한다. 습격 입장·복귀 단계는 실제 씬 이동이 함께 일어나야 해서 넘기지 않는다.
        public void DebugSkipStep()
        {
            var step = CurrentStep;
            if (step == null) return;
            if (step.completion == TutorialCompletion.Signal &&
                (step.requiredSignal == "raid-entered" || step.requiredSignal == "home-returned"))
            {
                FeedbackUI.For(this)?.ShowNotification("습격 입장·복귀 단계는 직접 진행하세요.");
                return;
            }
            // 연습 모듈 획득 단계는 결과창·복귀 판정이 획득 기록을 보므로 기록을 남긴다.
            if (step.requiredSignal == "training-module-collected" && step.requiredModuleId > 0 && NightRaidController.Instance != null)
                NightRaidController.Instance.TryCollectTrainingModule(step.requiredModuleId);
            Debug.Log($"[튜토리얼] 디버그 단계 스킵: {step.id}", this);
            debugSkipping = true;
            try { Advance(); } finally { debugSkipping = false; }
        }
#endif

        /// <summary>
        /// 은퇴 소개를 위해 현재 화면 상태를 보존하고 카메라를 촬영 기준점으로 이동합니다.
        /// </summary>
        /// <param name="step">은퇴 소개를 시작하는 현재 튜토리얼 단계입니다.</param>
        /// <returns>필수 카메라·촬영 지점·대화 UI가 연결되어 있으면 true를 반환합니다.</returns>
        private bool BeginRetirementIntroduction(TutorialStep step)
        {
            ReleaseDialogue();
            var player = TutorialPlayer;
            var holder = player != null ? player.GetComponent<WeaponHolder>() : null;
            _retirementCamera = holder != null ? holder.AimCamera : null;
            if (_retirementCamera == null && player != null && player.ViewPoint != null)
                _retirementCamera = player.ViewPoint.GetComponentInChildren<Camera>();
            if (_retirementCamera == null || _retirementShotPoint == null || dialogue == null)
            {
                Debug.LogError("은퇴 소개의 카메라·촬영 기준점·대화 UI 연결을 확인하세요.", this);
                return false;
            }

            _retirementPresentationActive = true;
            _retirementWeaponScope = player != null ? player.GetComponent<WeaponScope>() : null;
            if (_retirementWeaponScope != null)
            {
                _retirementWeaponScope.SetTutorialCameraOverride(true);
                _retirementCameraOverrideActive = true;
            }
            _retirementCameraStartPosition = _retirementCamera.transform.position;
            _retirementCameraStartRotation = _retirementCamera.transform.rotation;
            HideRetirementWeapon();
            HideRetirementHud();
            _retirementOwnsInputBlock = true;
            GameManager.Instance?.SetDialogue(true);
            _retirementCameraRoutine = StartCoroutine(MoveRetirementCamera(
                _retirementCameraStartPosition, _retirementCameraStartRotation,
                _retirementShotPoint.position, _retirementShotPoint.rotation, false));
            return true;
        }

        /// <summary>
        /// step의 은퇴 소개 대사에 실제 은퇴 비용을 치환해 확인 UI를 표시합니다.
        /// </summary>
        /// <param name="step">비용 토큰을 포함한 은퇴 소개 튜토리얼 단계입니다.</param>
        private void ShowRetirementIntroduction(TutorialStep step)
        {
            string description = step.description.Replace("{은퇴자금}", FormatKoreanRetirementCost(RetireUI.Cost));
            _retirementIntroductionAwaitingConfirmation = true;
            ownsDialogue = true;
            if (!dialogue.Show(description, "확인", Confirm, step.title))
            {
                ownsDialogue = false;
                _retirementIntroductionAwaitingConfirmation = false;
                RestoreRetirementPresentation();
                Finish("error");
                return;
            }
            _retirementOwnsInputBlock = false;
        }

        /// <summary>
        /// step의 마지막 안내를 표시하고 같은 2초 동안 카메라를 시작 시점으로 복귀시킵니다.
        /// </summary>
        /// <param name="step">복귀와 함께 표시할 마지막 튜토리얼 단계입니다.</param>
        private void BeginRetirementFinalStep(TutorialStep step)
        {
            _retirementReturnOnNextStep = false;
            if (!_retirementPresentationActive || dialogue == null || _retirementCamera == null)
            {
                RestoreRetirementPresentation();
                ownsDialogue = true;
                if (dialogue == null || !dialogue.Show(step.description, "확인", Confirm, step.title))
                { ownsDialogue = false; Finish("error"); }
                return;
            }

            _retirementFinalAwaitingConfirmation = true;
            _retirementReturnInProgress = true;
            ownsDialogue = true;
            if (!dialogue.Show(step.description, "확인", Confirm, step.title))
            {
                ownsDialogue = false;
                _retirementFinalAwaitingConfirmation = false;
                RestoreRetirementPresentation();
                Finish("error");
                return;
            }
            _retirementOwnsInputBlock = false;
            _retirementCameraRoutine = StartCoroutine(MoveRetirementCamera(
                _retirementCamera.transform.position, _retirementCamera.transform.rotation,
                _retirementCameraStartPosition, _retirementCameraStartRotation, true));
        }

        /// <summary>
        /// 시작·도착 위치와 회전을 따라 카메라를 2초간 이동시키고 복귀 여부에 맞춰 상태를 갱신합니다.
        /// </summary>
        /// <param name="fromPosition">이동 시작 월드 위치입니다.</param>
        /// <param name="fromRotation">이동 시작 월드 회전입니다.</param>
        /// <param name="toPosition">이동할 도착 월드 위치입니다.</param>
        /// <param name="toRotation">이동할 도착 월드 회전입니다.</param>
        /// <param name="returning">복귀 이동인지 나타내며, 완료 시 복귀 상태를 해제합니다.</param>
        private IEnumerator MoveRetirementCamera(Vector3 fromPosition, Quaternion fromRotation,
            Vector3 toPosition, Quaternion toRotation, bool returning)
        {
            float elapsed = 0f;
            while (elapsed < RETIREMENT_CAMERA_DURATION && _retirementPresentationActive && _retirementCamera != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / RETIREMENT_CAMERA_DURATION));
                _retirementCamera.transform.SetPositionAndRotation(
                    Vector3.Lerp(fromPosition, toPosition, t), Quaternion.Slerp(fromRotation, toRotation, t));
                yield return null;
            }
            if (!_retirementPresentationActive) yield break;
            if (_retirementCamera == null)
            {
                _retirementCameraRoutine = null;
                RestoreRetirementPresentation();
                Finish("error");
                yield break;
            }
            _retirementCamera.transform.SetPositionAndRotation(toPosition, toRotation);
            _retirementCameraRoutine = null;
            if (returning) _retirementReturnInProgress = false;
            else ShowRetirementIntroduction(CurrentStep);
        }

        /// <summary>
        /// 확인 입력이 카메라 복귀 중 들어오면 원래 위치와 방향을 즉시 복원합니다.
        /// </summary>
        private void CompleteRetirementCameraReturn()
        {
            if (_retirementCameraRoutine != null) StopCoroutine(_retirementCameraRoutine);
            _retirementCameraRoutine = null;
            if (_retirementCamera != null)
                _retirementCamera.transform.SetPositionAndRotation(_retirementCameraStartPosition, _retirementCameraStartRotation);
            _retirementReturnInProgress = false;
        }

        /// <summary>
        /// 장착 총의 Renderer 상태를 저장하고 화면에서 숨깁니다.
        /// </summary>
        private void HideRetirementWeapon()
        {
            _retirementWeaponRendererStates.Clear();
            var player = TutorialPlayer;
            var holder = player != null ? player.GetComponent<WeaponHolder>() : null;
            var weapon = holder != null ? holder.EquippedWeapon : null;
            if (weapon == null) return;
            foreach (Renderer renderer in weapon.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || _retirementWeaponRendererStates.ContainsKey(renderer)) continue;
                _retirementWeaponRendererStates.Add(renderer, renderer.enabled);
                renderer.enabled = false;
            }
        }

        /// <summary>
        /// 지정된 HUD를 숨기고 기존 CanvasGroup 상태를 보관해 이후 복원할 수 있게 합니다.
        /// </summary>
        private void HideRetirementHud()
        {
            _retirementHudStates.Clear();
            if (_retirementHudElements == null) return;
            foreach (GameObject element in _retirementHudElements)
            {
                if (element == null) continue;
                CanvasGroup group = element.GetComponent<CanvasGroup>();
                bool added = group == null;
                if (added) group = element.AddComponent<CanvasGroup>();
                _retirementHudStates.Add(new RetirementHudState(group, added));
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }
        }

        /// <summary>
        /// 은퇴 소개가 변경한 카메라·무기·HUD·입력 상태를 이전 값으로 되돌립니다.
        /// </summary>
        private void RestoreRetirementPresentation()
        {
            if (_retirementCameraRoutine != null) StopCoroutine(_retirementCameraRoutine);
            _retirementCameraRoutine = null;
            if (_retirementPresentationActive && _retirementCamera != null)
                _retirementCamera.transform.SetPositionAndRotation(_retirementCameraStartPosition, _retirementCameraStartRotation);
            foreach (KeyValuePair<Renderer, bool> state in _retirementWeaponRendererStates)
                if (state.Key != null) state.Key.enabled = state.Value;
            _retirementWeaponRendererStates.Clear();
            foreach (RetirementHudState state in _retirementHudStates)
            {
                if (state.Group == null) continue;
                state.Group.alpha = state.Alpha;
                state.Group.interactable = state.Interactable;
                state.Group.blocksRaycasts = state.BlocksRaycasts;
                if (state.AddedByPresentation) Destroy(state.Group);
            }
            _retirementHudStates.Clear();
            if (_retirementCameraOverrideActive && _retirementWeaponScope != null)
                _retirementWeaponScope.SetTutorialCameraOverride(false);
            _retirementCameraOverrideActive = false;
            if (_retirementOwnsInputBlock && GameManager.Instance != null) GameManager.Instance.SetDialogue(false);
            _retirementOwnsInputBlock = false;
            _retirementPresentationActive = false;
            _retirementIntroductionAwaitingConfirmation = false;
            _retirementFinalAwaitingConfirmation = false;
            _retirementReturnInProgress = false;
            _retirementReturnOnNextStep = false;
            _retirementCamera = null;
            _retirementWeaponScope = null;
        }

        /// <summary>
        /// Day 1 문구가 표시되는 시점에 은퇴 연출용 화면 상태를 복원합니다.
        /// </summary>
        private void RestoreRetirementPresentationOnDayText()
        {
            if (endingTransition != null) endingTransition.DayTextShown -= RestoreRetirementPresentationOnDayText;
            RestoreRetirementPresentation();
        }

        /// <summary>
        /// 은퇴 비용 cost를 만 단위와 달러 단위의 한글 문자열로 변환해 반환합니다.
        /// </summary>
        /// <param name="cost">달러 단위의 은퇴 비용입니다.</param>
        /// <returns>한글 금액과 달러 단위를 포함한 비용 문자열입니다.</returns>
        private static string FormatKoreanRetirementCost(int cost)
        {
            if (cost < 10000) return $"{cost.ToString("N0", CultureInfo.InvariantCulture)} 달러";
            int tenThousands = cost / 10000;
            int remainder = cost % 10000;
            return remainder == 0
                ? $"{tenThousands.ToString("N0", CultureInfo.InvariantCulture)}만 달러"
                : $"{tenThousands.ToString("N0", CultureInfo.InvariantCulture)}만 {remainder.ToString("N0", CultureInfo.InvariantCulture)} 달러";
        }

        private void OnDisable()
        {
            RaidEnemyCore.Spawned -= HandleRaidEnemySpawned;
            if (warningTurn != null) StopCoroutine(warningTurn);
            warningTurn = null;
            waiting.Clear(); ReleaseDialogue();
            RestoreRetirementPresentation();
            if (warningOpen && dialogue != null) dialogue.Hide();
            if (warningOpen && GameManager.Instance != null) GameManager.Instance.SetDialogue(false);
            warningOpen = false; current = null; StepIndex = 0; CompleteDayZero();
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
