using System;
using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // Owns tutorial lifetime, progression, target selection and interaction permissions.
    [DisallowMultipleComponent, DefaultExecutionOrder(-100)]
    public sealed partial class TutorialManager : MonoBehaviour
    {
        [SerializeField] private DialogueUI dialogue;
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
        private _DayManager dayZeroOwner;
        private DayTransitionUI endingTransition;
        private sealed class Request
        {
            public TutorialSequence sequence;
            public Component owner;
            public Action done;
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
            bool allowed = AllowsAction("attach");
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
            var weapon = workbench != null ? workbench.CurrentWeapon : null;
            var expected = weapon != null ? weapon.GetComponentInChildren<CubeAssembly>() : null;
            bool fiveFaces = step.completion == TutorialCompletion.FiveCoreFacesBlocked;
            bool allowed = expected != null && assembly == expected && (fiveFaces
                ? moduleId == 1 && cell != step.openCoreFace && Mathf.Abs(cell.x) + Mathf.Abs(cell.y) + Mathf.Abs(cell.z) == 1
                : moduleId == step.requiredModuleId && cell == step.requiredCell &&
                    step.MatchesOutputDirection(direction));
            if (!allowed)
            {
                string message = !fiveFaces && moduleId != step.requiredModuleId && !string.IsNullOrEmpty(step.wrongModuleMessage)
                    ? step.wrongModuleMessage : step.blockedPlacementMessage;
                FeedbackUI.For(this)?.ShowNotification(fiveFaces ? "지금은 사출구를 한 개로 만들어 봅시다." : message);
            }
            return allowed;
        }
        private bool ownsDialogue;
        private bool tankSpawnGuidanceOpen;
        private bool loanGuideShown;

        private void OnEnable() => RaidEnemyCore.Spawned += HandleRaidEnemySpawned;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this; current = null; StepIndex = 0; ownsDialogue = false; dayZeroOwner = null;
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
            if (selection == null || !selection.Show(EndDayZero, StartDayZeroSequence)) StartDayZeroSequence();
        }
        private void StartDayZeroSequence()
        {
            BeginPractice();
            Run(dayZeroOwner.CurrentCustomerDay.startTutorial, dayZeroOwner, EndDayZero);
        }
        private void EndDayZero()
        {
            ResolvePresentation();
            endingTransition = dayTransition;
            if (endingTransition != null)
            {
                endingTransition.Completed += CompleteDayZero;
                endingTransition.Covered += PrepareFirstDay;
                if (endingTransition.BeginFirstDay(dayZeroOwner)) return;
                endingTransition.Completed -= CompleteDayZero;
                endingTransition.Covered -= PrepareFirstDay;
            }
            PrepareFirstDay();
            if (dayZeroOwner != null) dayZeroOwner.BeginFirstBusinessDay();
            CompleteDayZero();
        }
        private void CompleteDayZero()
        {
            ClearShootingTarget(); pendingWarnings.Clear();
            if (endingTransition != null)
            {
                endingTransition.Completed -= CompleteDayZero;
                endingTransition.Covered -= PrepareFirstDay;
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
        public static void Run(TutorialSequence sequence, Component owner, Action done = null)
        {
            if (sequence == null || sequence.steps == null || sequence.steps.Length == 0 || Instance == null || !Instance.isActiveAndEnabled)
            { done?.Invoke(); return; }
            // The authored day-zero sequence replaces legacy customer hints during its entire lifetime.
            if (Instance.dayZeroOwner != null && owner != Instance.dayZeroOwner) { done?.Invoke(); return; }
            Instance.waiting.Enqueue(new Request { sequence = sequence, owner = owner, done = done });
            Instance.BeginNext();
        }
        private void BeginNext()
        {
            if (current != null || waiting.Count == 0) return;
            current = waiting.Dequeue(); StepIndex = 0; withheldBalance = 0; loanGuideShown = false; ShowStep();
        }
        private void ShowStep()
        {
            ResolvePresentation();
            while (CurrentSequence != null && StepIndex < CurrentSequence.steps.Length && CurrentSequence.steps[StepIndex] == null) StepIndex++;
            var step = CurrentStep;
            if (step == null) { Finish(); return; }
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
            if (step.completion == TutorialCompletion.Confirmation)
            {
                if (dialogue == null) { Debug.LogError("튜토리얼 대화 UI 연결이 없습니다.", this); Finish(); return; }
                ownsDialogue = true;
                if (!dialogue.Show(step.description, "확인", Confirm, "튜토리얼"))
                { ownsDialogue = false; Finish(); }
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
            if (dialogue == null) { Debug.LogError("튜토리얼 대화 UI 연결이 없습니다.", this); Finish(); return; }
            ownsDialogue = true;
            tankSpawnGuidanceOpen = true;
            if (!dialogue.Show(CurrentStep.description, "확인", Confirm, "튜토리얼"))
            { ownsDialogue = false; tankSpawnGuidanceOpen = false; Finish(); }
        }
        private void Confirm()
        {
            var step = CurrentStep;
            if (step == null) return;
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
            if ((!GameManager.GameplayBlocked || saleCompleted) && !warningOpen && (AreFiveFacesBlocked() || IsActionStepComplete())) Advance();
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
            var step = CurrentStep;
            if (step != null && step.completion == TutorialCompletion.Signal && !string.IsNullOrEmpty(step.requiredSignal) &&
                string.Equals(step.requiredSignal, signal, StringComparison.Ordinal) &&
                (step.target == TutorialTarget.None || (source != null && source == CurrentInteractionTarget))) Advance();
        }
        public bool AllowsInteraction(IInteractable target) => !IsTutorialActive ||
            (CurrentStep != null && (CurrentStep.completion == TutorialCompletion.RaidTankSpawned ||
                (CurrentStep.completion != TutorialCompletion.CustomerKilled &&
                    target is Component component && component != null && component == CurrentInteractionTarget)));
        public bool TryAuthorizeInteraction(IInteractable target)
        {
            if (AllowsInteraction(target)) return true;
            FeedbackUI.For(this)?.ShowNotification(blockedInteractionMessage);
            return false;
        }
        public static bool TryAuthorizeAction(string action)
        {
            var tutorial = Instance;
            if (tutorial == null || tutorial.AllowsAction(action)) return true;
            if (tutorial.CurrentStep?.requiredSignal == "loan-taken")
                FeedbackUI.For(tutorial)?.ShowNotification(tutorial.blockedInteractionMessage);
            return false;
        }
        // Gameplay components query the same authored action permissions as the interaction policy.
        public bool AllowsAction(string action)
        {
            if (!IsTutorialActive) return true;
            if (action == "fire") return true;
            if (action == "rotate" && IsEditingTutorialWorkbench) return true;
            if (CurrentStep == null || CurrentStep.completion == TutorialCompletion.Confirmation) return false;
            if (CurrentStep.requiresWorkbenchEditing && !IsEditingTutorialWorkbench) return false;
            var allowed = CurrentStep?.allowedActions;
            return allowed == null || allowed.Length == 0 || Array.IndexOf(allowed, action) >= 0;
        }
        private void Advance()
        {
            if (CurrentStep != null && CurrentStep.restoreWithheldBalanceOnComplete && withheldBalance > 0)
            {
                TutorialPlayer?.GetComponent<PlayerWallet>()?.Credit(withheldBalance);
                withheldBalance = 0;
            }
            StepIndex++;
            ShowStep();
        }
        private void ReleaseDialogue()
        {
            if (ownsDialogue && dialogue != null) dialogue.Hide();
            ownsDialogue = false;
            tankSpawnGuidanceOpen = false;
        }
        private void Finish()
        {
            var done = current?.done;
            ReleaseDialogue(); current = null; StepIndex = 0; Changed?.Invoke();
            done?.Invoke(); BeginNext();
        }
        public void Cancel(Component owner)
        {
            int count = waiting.Count;
            for (int i = 0; i < count; i++) { var request = waiting.Dequeue(); if (request.owner != owner) waiting.Enqueue(request); }
            if (current == null || current.owner != owner) return;
            ReleaseDialogue(); current = null; StepIndex = 0; Changed?.Invoke(); BeginNext();
        }
#if UNITY_EDITOR
        public void DebugEndTutorial()
        {
            if (!IsTutorialActive && (selection == null || !selection.IsOpen)) return;
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
            Advance();
        }
#endif
        private void OnDisable()
        {
            RaidEnemyCore.Spawned -= HandleRaidEnemySpawned;
            if (warningTurn != null) StopCoroutine(warningTurn);
            warningTurn = null;
            waiting.Clear(); ReleaseDialogue();
            if (warningOpen && dialogue != null) dialogue.Hide();
            if (warningOpen && GameManager.Instance != null) GameManager.Instance.SetDialogue(false);
            warningOpen = false; current = null; StepIndex = 0; CompleteDayZero();
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
