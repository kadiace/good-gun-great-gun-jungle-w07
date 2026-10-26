using System;
using System.Collections;
using UnityEngine;

namespace WeaponWorkshop
{
    // Scene handoffs retain authored progress and practice data, never a live player or UI.
    public sealed partial class TutorialManager
    {
        private CloseShopButton closeShop;
        private NightRaidEntrance raidEntrance;
        private EquipableWeapon guidedWeapon;
        public bool IsEditingTutorialWorkbench
        {
            get
            {
                var player = TutorialPlayer;
                var session = player != null ? player.GetComponent<WorkshopSession>() : null;
                return session != null && session.IsInWorkshop && session.ActiveWorkbench == workbench;
            }
        }
        private bool MatchesRotationObjective(bool outsideCircle)
        {
            string signal = CurrentStep != null ? CurrentStep.requiredSignal : null;
            return signal != "rotate-inside-circle" && signal != "rotate-outside-circle" ||
                signal == (outsideCircle ? "rotate-outside-circle" : "rotate-inside-circle");
        }
        public void NotifyRotation(bool outsideCircle, Workbench source, float rotatedDegrees)
        {
            var step = CurrentStep;
            if (step == null || source != workbench || !MatchesRotationObjective(outsideCircle) ||
                rotatedDegrees < step.requiredRotationDegrees) return;
            NotifySignal(outsideCircle ? "rotate-outside-circle" : "rotate-inside-circle", source);
        }
        [SerializeField, Min(0)] private int practiceMoney = 5000;
        [SerializeField, Min(0)] private int practiceModuleCredits = 200;
        private static TutorialSequence trainingSequence;
        private static int trainingStep;
        private static bool trainingReturning;
        private static bool practiceEconomy;
        private static bool _trainingPresetOneSaved;
        private bool _raidExitSaveRequested;
        private static int originalMoney, originalCredits;
        private static int[] originalUnlocks;
        private static string[] originalPresets;
        private static int originalSlotMachineDestructions, originalSlotMachineDestroyedDay;
        private static bool originalSlotMachineDestroyedAtNight;
        public const string TrainingScene = "Tutorial_WoodenHouse";
        // 0일차 야간 준비에서 퇴장 저장을 요구하는 슬롯(5번)과 튜토리얼이 불러오게 하는 슬롯(4번). 0 기반 인덱스.
        public const int RaidPresetSaveSlot = 4;
        public const int RaidPresetLoadSlot = 3;
        public bool IsGuidedDayZero => dayZeroOwner != null || trainingSequence != null;
        public static bool TrainingRaidPending => trainingSequence != null && !trainingReturning;
        public static bool TrainingReturnPending => trainingSequence != null && trainingReturning;
        public bool RaidPresetSaveRequested => _raidExitSaveRequested;
        public bool TrainingTankStageReached { get; private set; }
        public static void AbandonTraining() { LogAbandoned(); ResetTraining(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTraining()
        {
            trainingSequence = null; trainingStep = 0; trainingReturning = practiceEconomy = false;
            _trainingPresetOneSaved = false;
            originalUnlocks = null; originalPresets = null;
            originalSlotMachineDestructions = 0; originalSlotMachineDestroyedDay = -1; originalSlotMachineDestroyedAtNight = false;
        }
        private FirstPersonPlayer TutorialPlayer => GameManager.Instance != null
            ? GameManager.Instance.Player : null;
        private CubeAssembly TutorialAssembly => workbench != null && workbench.CurrentWeapon != null
            ? workbench.CurrentWeapon.GetComponentInChildren<CubeAssembly>() : null;
        private Component PresetDispenserOneTarget()
        {
            foreach (var dispenser in FindObjectsByType<CoreDispenser>(FindObjectsSortMode.None))
                if (dispenser.PresetSlot == RaidPresetSaveSlot) return dispenser;
            return null;
        }
        public static bool ActionAllowed(string action) => Instance == null || Instance.AllowsAction(action);
        public static bool TryAction(string action) => Instance == null || Instance.TryAuthorizeAction(action);
        private void BeginPractice()
        {
            var player = TutorialPlayer;
            if (player == null || practiceEconomy) return;
            var wallet = player.GetComponent<PlayerWallet>();
            originalMoney = wallet.Balance; originalCredits = wallet.ModuleCredits;
            originalUnlocks = player.GetComponent<PlayerModuleUnlocks>().CaptureUnlockedModules();
            originalPresets = player.GetComponent<WeaponPresets>().Capture();
            _trainingPresetOneSaved = false;
            _raidExitSaveRequested = false;
            var slotMachine = FindFirstObjectByType<SlotMachineReels>();
            originalSlotMachineDestructions = slotMachine != null ? slotMachine.DestructionCount : 0;
            originalSlotMachineDestroyedDay = slotMachine != null ? slotMachine.DestroyedDay : -1;
            originalSlotMachineDestroyedAtNight = slotMachine != null && slotMachine.DestroyedAtNight;
            var emptyFirst = (string[])originalPresets.Clone();
            emptyFirst[RaidPresetSaveSlot] = null;
            player.GetComponent<WeaponPresets>().Restore(emptyFirst);
            practiceEconomy = true;
            wallet.RestoreBalance(Mathf.Max(originalMoney, practiceMoney));
            wallet.RestoreModuleCredits(Mathf.Max(originalCredits, practiceModuleCredits));
        }

        /// <summary>
        /// Day0에서 첫 번째 프리셋 저장 여부를 기록해 작업대 퇴장 전에 필요한 안내를 생략할 수 있게 합니다.
        /// </summary>
        /// <param name="slot">저장한 프리셋의 0 기반 슬롯 번호입니다.</param>
        public void RecordTutorialPresetSaved(int slot)
        {
            if (IsGuidedDayZero && slot == RaidPresetSaveSlot)
            {
                _trainingPresetOneSaved = true;
                _raidExitSaveRequested = false;
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// Day0 작업대 퇴장을 처음 시도할 때 1번 슬롯 저장 안내를 시작합니다.
        /// </summary>
        /// <returns>저장 안내를 보여 주며 퇴장을 막아야 하면 false를 반환합니다.</returns>
        public bool TryAuthorizeRaidWorkbenchExit()
        {
            var step = CurrentStep;
            if (!IsGuidedDayZero || step == null || step.id != "raid-exit-workbench" || _trainingPresetOneSaved) return true;
            if (!_raidExitSaveRequested)
            {
                _raidExitSaveRequested = true;
                Changed?.Invoke();
                ResolvePresentation();
                if (dialogue != null)
                {
                    ownsDialogue = dialogue.Show("작업대를 나가기 전에 현재 무기를 프리셋 5번에 저장하세요.", "확인",
                        ReleaseDialogue, "튜토리얼");
                }
            }
            return false;
        }

        /// <summary>
        /// Day0에서 프리셋 5번 저장 후 작업대를 나가면 퇴장 완료 신호를 보냅니다. 무기는 작업대에 남고 쓰레기통 실습 단계에서 다룹니다.
        /// </summary>
        public void NotifyTutorialWorkbenchExited()
        {
            var step = CurrentStep;
            if (!IsGuidedDayZero || step == null || step.id != "raid-exit-workbench" || !_trainingPresetOneSaved) return;
            NotifySignal("raid-workbench-exited");
        }
        private void EndPractice()
        {
            var player = TutorialPlayer;
            if (practiceEconomy && player != null)
            {
                var held = player.GetComponent<WeaponHolder>().TakeWeapon();
                if (held != null) Destroy(held.gameObject);
                if (workbench != null && workbench.CurrentWeapon != null) Destroy(workbench.CurrentWeapon.gameObject);
                var wallet = player.GetComponent<PlayerWallet>();
                wallet.RestoreBalance(originalMoney); wallet.RestoreModuleCredits(originalCredits);
                player.GetComponent<PlayerModuleUnlocks>().RestoreUnlockedModules(originalUnlocks);
                if (originalPresets != null)
                    player.GetComponent<WeaponPresets>()?.Restore(originalPresets);
                var slotMachine = FindFirstObjectByType<SlotMachineReels>();
                if (slotMachine != null) slotMachine.RestoreDamage(originalSlotMachineDestructions,
                    originalSlotMachineDestroyedDay, originalSlotMachineDestroyedAtNight);
            }
            ResetTraining();
        }
        private int withheldBalance;
        private void PrepareStep(TutorialStep step)
        {
            // 티켓 단계는 경보나 웨이브 타이머를 기다리지 않고 연습 습격에 탱크를 최소 1대 즉시 출격시킨다.
            if (step.completion == TutorialCompletion.RaidTicketCollected && TrainingRaidPending)
            {
                TrainingTankStageReached = true;
                FindFirstObjectByType<RaidEnemySpawnManager>()?.EnsureTrainingTankSpawned();
            }
            if (step.setBalanceOnStart >= 0)
            {
                var wallet = TutorialPlayer != null ? TutorialPlayer.GetComponent<PlayerWallet>() : null;
                if (wallet != null)
                {
                    withheldBalance = Mathf.Max(0, wallet.Balance - step.setBalanceOnStart);
                    wallet.RestoreBalance(step.setBalanceOnStart);
                }
            }
            if (step.discardHeldWeapon)
            {
                var player = TutorialPlayer;
                var holder = player != null ? player.GetComponent<WeaponHolder>() : null;
                var held = holder != null ? holder.TakeWeapon() : null;
                if (held != null) Destroy(held.gameObject);
            }
            if (step.prepareBlockedCore && workbench != null)
            {
                var player = TutorialPlayer;
                var holder = player != null ? player.GetComponent<WeaponHolder>() : null;
                // A retry already carries the departure weapon; do not exchange it for a second practice core.
                if (workbench.CurrentWeapon == null && (holder == null || holder.EquippedWeapon == null))
                {
                    if (step.preparedWeaponPrefab == null) workbench.SpawnCore();
                    else
                    {
                        var prepared = Instantiate(step.preparedWeaponPrefab);
                        prepared.name = step.preparedWeaponPrefab.name;
                        var preparedAssembly = prepared.GetComponentInChildren<CubeAssembly>();
                        bool placed = false;
                        if (preparedAssembly != null && preparedAssembly.TryCaptureLayout(out var layout))
                        {
                            var blocks = new System.Collections.Generic.List<CubeAssembly.BlockSnapshot>();
                            foreach (var block in layout.blocks)
                                if (Array.IndexOf(step.preparedWeaponOpenCells, block.cell) < 0) blocks.Add(block);
                            placed = preparedAssembly.RestoreLayout(new CubeAssembly.LayoutSnapshot(blocks.ToArray(), layout.paidConstructionCost)) &&
                                workbench.PlaceWeapon(prepared);
                        }
                        if (!placed) Destroy(prepared.gameObject);
                    }
                }
                var assembly = TutorialAssembly;
                if (assembly != null)
                    foreach (var face in new[] { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, Vector3Int.back })
                        if (assembly.CanAttach(face, 1)) assembly.TryAttach(face, 1, face, face);
            }
            if (step.target == TutorialTarget.WorkbenchWeapon)
                guidedWeapon = workbench != null ? workbench.CurrentWeapon : null;
        }
        private bool IsActionStepComplete()
        {
            var step = CurrentStep;
            if (step == null) return false;
            var player = TutorialPlayer;
            var session = player != null ? player.GetComponent<WorkshopSession>() : null;
            if (step.requiresWorkbenchEditing && (session == null || !session.IsEditing || session.ActiveWorkbench != workbench)) return false;
            var assembly = TutorialAssembly;
            switch (step.completion)
            {
                case TutorialCompletion.Signal:
                    // 작업대 모드 전환 단계는 신호 외에 현재 상태로도 판정한다. 삭제 모드는 모듈 선택·작업대 퇴장 등으로
                    // 신호 없이 꺼지고, 팔레트는 단계 시작 전에 이미 열려 있을 수 있어 신호만 기다리면 진행이 막힌다.
                    var builder = player != null ? player.GetComponentInChildren<CubeBuilder>(true) : null;
                    if (builder == null) return false;
                    switch (step.requiredSignal)
                    {
                        case "delete-mode-enabled": return builder.DeleteMode;
                        case "delete-mode-disabled": return !builder.DeleteMode;
                        case "palette-opened": return builder.PaletteOpen;
                        default: return false;
                    }
                case TutorialCompletion.TargetNearby:
                    // 강제 스캔 교육은 제거됐다. TargetScanned는 직렬화 정수 보존용으로만 남기며 어떤 단계도 기다리지 않는다.
                    var target = CurrentInteractionTarget;
                    if (step.id == "raid-approach-crate" && target != null &&
                        target.GetComponent<ModuleUnlockPickup>() != null) return true;
                    if (player == null || target == null || target.GetComponentInParent<ModuleUnlockCrate>() == null) return false;
                    return (player.transform.position - target.transform.position).sqrMagnitude <=
                        step.requiredTargetDistance * step.requiredTargetDistance;
                case TutorialCompletion.ModuleInstalled:
                case TutorialCompletion.CellEmpty:
                    if (assembly == null) return false;
                    foreach (var block in assembly.GetComponentsInChildren<PlacedBlock>())
                    {
                        if (block.transform.parent != assembly.transform || block.Cell != step.requiredCell) continue;
                        var output = block.GetComponent<ExtensionModule>();
                        return step.completion == TutorialCompletion.ModuleInstalled && block.ModuleSlot == step.requiredModuleId &&
                            (!step.requireOutputDirection || (output != null && step.MatchesOutputDirection(output.OutputDirection)));
                    }
                    return step.completion == TutorialCompletion.CellEmpty;
                case TutorialCompletion.CustomerWeaponReady:
                    return session != null && session.IsEditing && workbench.CurrentWeapon != null &&
                        customerBell != null && customerBell.Current != null && customerBell.Current.CheckWeapon(workbench.CurrentWeapon);
                case TutorialCompletion.WeaponTagged:
                    return session != null && session.IsEditing && workbench.CurrentWeapon != null &&
                        TableManager.Instance.CustomerRequest.MeetsTag(step.requiredTagId,
                            new CustomerRequestInfo.WeaponSnapshot(workbench.CurrentWeapon));
                case TutorialCompletion.WorkshopExited:
                    return session != null && !session.IsInWorkshop && !session.IsTransitioning;
                case TutorialCompletion.WeaponHeld:
                    var holder = player != null ? player.GetComponent<WeaponHolder>() : null;
                    return holder != null && holder.EquippedWeapon != null && !holder.EquippedWeapon.IsMoving &&
                        (guidedWeapon == null || holder.EquippedWeapon == guidedWeapon);
                case TutorialCompletion.SettlementClosed:
                    return dayZeroOwner != null && dayZeroOwner.IsNight && !dayZeroOwner.IsSettlementOpen;
                case TutorialCompletion.CustomerSold:
                    return dayZeroOwner != null && dayZeroOwner.CompletedGuests >= step.requiredCustomerCount;
                case TutorialCompletion.CustomerKilled:
                    return dayZeroOwner != null && dayZeroOwner.CompletedGuests >= step.requiredCustomerCount && dayZeroOwner.KilledGuests > 0;
                case TutorialCompletion.RaidTicketCollected:
                    return NightRaidController.Instance != null && NightRaidController.Instance.IsInRaid &&
                        NightRaidController.Instance.RaidEarnedTickets > 0;
                case TutorialCompletion.RaidMaterialCollected:
                    // 재료 상자는 파괴하면 달러 픽업을 함께 떨어뜨린다. 달러를 실제로 주워야 완료한다.
                    return NightRaidController.Instance != null && NightRaidController.Instance.IsInRaid &&
                        NightRaidController.Instance.RaidEarnedDollars > 0;
                default: return false;
            }
        }
        /// <summary>
        /// Day0 첫 손님의 첫 제출을 거절하고 확인 뒤 관통 안내로 진행합니다. 무기와 판매 집계는 유지하며 후속 요구와 보상을 함께 계약합니다.
        /// </summary>
        public bool TryRejectFirstSubmission(Customer customer)
        {
            var step = CurrentStep;
            if (dayZeroOwner == null || dayZeroOwner.DayNumber != 0 || dayZeroOwner.CompletedGuests != 0 ||
                customerBell == null || customer != customerBell.Current || customer.Profile == null ||
                step == null || step.requiredSignal != "first-sale-rejected") return false;
            if (ownsDialogue) return true;
            ResolvePresentation();
            var order = TableManager.Instance.CustomerOrder.FindGuest(customer.Profile.ProfileId);
            if (dialogue == null || order == null || string.IsNullOrWhiteSpace(order.tutorialRejectionLine)) return true;
            ownsDialogue = true;
            if (!dialogue.ShowChoices(order.tutorialRejectionLine,
                new[] { "나무판이요?", "닫기" }, new Action[]
                {
                    () =>
                    {
                        if (!dialogue.Show("손님이 모호한 요구를 하네요. 지금처럼 되물으면 자세한 요청사항을 알려줄 겁니다.",
                            "확인", () =>
                            {
                                if (!dialogue.ShowChoices("관통 모듈 1개요.", new[] { "닫기" }, new Action[]
                                    {
                                        () =>
                                        {
                                            var profile = customer.Profile;
                                            profile.RequestIds = (string[])order.tutorialFollowupRequestIds.Clone();
                                            if (profile.OrderState != null) profile.OrderState.rewards = null;
                                            CustomerRewardInfo.InitializeOrder(profile, dayZeroOwner.DayNumber);
                                            ReleaseDialogue();
                                            NotifySignal("first-sale-rejected", customer);
                                        }
                                    }, "손님")) ReleaseDialogue();
                            }, "튜토리얼")) ReleaseDialogue();
                    },
                    ReleaseDialogue
                }, "손님")) ownsDialogue = false;
            return true;
        }

        /// <summary>
        /// 삭제 모드 전환 단계와 실제 삭제 대상 칸을 구분해 허용합니다. 실습에서는 방금 설치한 기본 블록만 제거할 수 있습니다.
        /// </summary>
        public bool TryAuthorizeRemoval(PlacedBlock block)
        {
            if (!TryAuthorizeAction("delete")) return false;
            var step = CurrentStep;
            if (step != null && step.completion == TutorialCompletion.Signal &&
                (step.requiredSignal == "delete-mode-enabled" || step.requiredSignal == "delete-mode-disabled"))
            {
                FeedbackUI.For(this)?.ShowNotification("E를 눌러 삭제 모드를 전환하세요.");
                return false;
            }
            if (step == null || step.completion != TutorialCompletion.CellEmpty) return true;
            bool allowed = block != null && block.Cell == step.requiredCell && block.GetComponentInParent<CubeAssembly>() == TutorialAssembly;
            if (!allowed) FeedbackUI.For(this)?.ShowNotification("방금 설치한 기본 블록을 지워 보세요.");
            return allowed;
        }
        public bool AllowsRaidRegion(int index) => !IsGuidedDayZero || index == 0;
        public bool PrepareTrainingRaid()
        {
            if (!IsGuidedDayZero) return false;
            if (CurrentStep == null || CurrentStep.requiredSignal != "raid-entered") return false;
            // 입장 단계는 Advance 대신 씬 이동으로 넘어가므로 완료 기록을 여기서 남긴다.
            LogStepCompleted();
            trainingSequence = CurrentSequence; trainingStep = StepIndex + 1; trainingReturning = false;
            return true;
        }
        private IEnumerator ResumeTrainingRaid()
        {
            // RaidController first restores the carried weapon and practice unlocks.
            yield return null;
            ResolvePresentation();
            current = new Request { sequence = trainingSequence, owner = this };
            StepIndex = trainingStep; ShowStep();
        }
        /// <summary>
        /// 훈련 습격 종료 상태를 설정하고 사망 여부에 따라 다음 튜토리얼 단계를 준비합니다.
        /// </summary>
        /// <param name="died">플레이어가 습격 중 사망했는지 여부입니다.</param>
        public static void PrepareTrainingReturn(bool died)
        {
            if (trainingSequence == null) return;
            trainingReturning = true;
            if (died)
            {
                trainingStep = Array.FindIndex(trainingSequence.steps, s => s != null && s.id == "raid-retry");
                if (Instance != null) Instance._raidExitSaveRequested = false;
            }
            else if (Instance != null) { Instance.LogStepCompleted(); trainingStep = Instance.StepIndex + 1; }
        }
        public bool ResumeTrainingHome(_DayManager day)
        {
            if (!TrainingReturnPending || day == null) return false;
            dayZeroOwner = day;
            day.DeclineNextDay();
            ResolvePresentation();
            current = new Request { sequence = trainingSequence, owner = day, done = EndDayZero };
            StepIndex = Mathf.Max(0, trainingStep); ShowStep();
            return true;
        }
        public bool CanReturnFromTraining => !IsGuidedDayZero ||
            (CurrentStep != null && CurrentStep.requiredSignal == "home-returned");
        public void NotifyTrainingLoot(int moduleId)
        {
            if (CurrentStep != null && CurrentStep.requiredModuleId == moduleId)
                NotifySignal("training-module-collected", CurrentInteractionTarget);
        }
        private Component RaidLootTarget()
        {
            var spawner = FindFirstObjectByType<RaidCrateSpawner>();
            if (spawner == null) return null;
            foreach (var objective in spawner.Objectives)
                if (objective.Target != null && (CurrentStep.requiredModuleId == 0 || objective.ModuleId == CurrentStep.requiredModuleId))
                    return objective.Target;
            return null;
        }
        private Component RaidMaterialCrateTarget()
        {
            var player = TutorialPlayer;
            ModuleCreditCrate nearest = null;
            float distance = float.PositiveInfinity;
            foreach (var crate in ModuleCreditCrate.ActiveCrates)
            {
                if (crate == null || !crate.isActiveAndEnabled || crate.IsClaimed ||
                    crate.gameObject.scene != gameObject.scene) continue;
                float candidate = player != null ? (crate.transform.position - player.transform.position).sqrMagnitude : 0f;
                if (candidate < distance) { distance = candidate; nearest = crate; }
            }
            return nearest;
        }
        private Component RaidTankTarget()
        {
            var player = TutorialPlayer;
            RaidEnemyCore nearest = null;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < RaidEnemyCore.ActiveCount; i++)
            {
                var core = RaidEnemyCore.ActiveAt(i);
                if (core == null || core.IsDestroyed || core.EnemyType != RaidEnemyType.Tank ||
                    core.gameObject.scene != gameObject.scene) continue;
                float candidate = player != null ? (core.MarkerPosition - player.transform.position).sqrMagnitude : 0f;
                if (candidate < distance) { distance = candidate; nearest = core; }
            }
            return nearest;
        }
        private Component RaidExitTarget()
        {
            var player = TutorialPlayer;
            NightRaidBoundary nearest = null; float distance = float.PositiveInfinity;
            foreach (var exit in FindObjectsByType<NightRaidBoundary>(FindObjectsSortMode.None))
            {
                float candidate = player != null ? (exit.transform.position - player.transform.position).sqrMagnitude : 0f;
                if (candidate < distance) { distance = candidate; nearest = exit; }
            }
            return nearest;
        }
    }
}
