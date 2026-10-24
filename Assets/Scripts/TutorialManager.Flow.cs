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
        private static int originalMoney, originalCredits;
        private static int[] originalUnlocks;
        private static string[] originalPresets;
        private static int originalSlotMachineDestructions, originalSlotMachineDestroyedDay;
        private static bool originalSlotMachineDestroyedAtNight;
        public const string TrainingScene = "Tutorial_WoodenHouse";
        public bool IsGuidedDayZero => dayZeroOwner != null || trainingSequence != null;
        public static bool TrainingRaidPending => trainingSequence != null && !trainingReturning;
        public static bool TrainingReturnPending => trainingSequence != null && trainingReturning;
        public static void AbandonTraining() { LogAbandoned(); ResetTraining(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTraining()
        {
            trainingSequence = null; trainingStep = 0; trainingReturning = practiceEconomy = false;
            originalUnlocks = null; originalPresets = null;
            originalSlotMachineDestructions = 0; originalSlotMachineDestroyedDay = -1; originalSlotMachineDestroyedAtNight = false;
        }
        private FirstPersonPlayer TutorialPlayer => GameManager.Instance != null && GameManager.Instance.UI != null
            ? GameManager.Instance.UI.Player : null;
        private CubeAssembly TutorialAssembly => workbench != null && workbench.CurrentWeapon != null
            ? workbench.CurrentWeapon.GetComponentInChildren<CubeAssembly>() : null;
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
            var slotMachine = FindFirstObjectByType<SlotMachineReels>();
            originalSlotMachineDestructions = slotMachine != null ? slotMachine.DestructionCount : 0;
            originalSlotMachineDestroyedDay = slotMachine != null ? slotMachine.DestroyedDay : -1;
            originalSlotMachineDestroyedAtNight = slotMachine != null && slotMachine.DestroyedAtNight;
            var emptyFirst = (string[])originalPresets.Clone();
            emptyFirst[0] = null;
            player.GetComponent<WeaponPresets>().Restore(emptyFirst);
            practiceEconomy = true;
            wallet.RestoreBalance(Mathf.Max(originalMoney, practiceMoney));
            wallet.RestoreModuleCredits(Mathf.Max(originalCredits, practiceModuleCredits));
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
                var savedPresets = player.GetComponent<WeaponPresets>().Capture();
                originalPresets[0] = savedPresets[0];
                player.GetComponent<WeaponPresets>().Restore(originalPresets);
                var slotMachine = FindFirstObjectByType<SlotMachineReels>();
                if (slotMachine != null) slotMachine.RestoreDamage(originalSlotMachineDestructions,
                    originalSlotMachineDestroyedDay, originalSlotMachineDestroyedAtNight);
            }
            ResetTraining();
        }
        private TutorialStep scannedStep;
        private ModuleUnlockCrate scannedCrate;
        private int withheldBalance;
        private void PrepareStep(TutorialStep step)
        {
            scannedStep = null; scannedCrate = null;
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
                case TutorialCompletion.TargetScanned:
                    var scanner = player != null ? player.GetComponent<PlayerScanMode>() : null;
                    if (step.completion == TutorialCompletion.TargetScanned && scannedStep == step)
                    {
                        if (scanner == null || scanner.IsScanning) return false;
                        if (scannedCrate != null) scannedCrate.GetComponent<DestructibleTarget>().DamageProtected = false;
                        return true;
                    }
                    var target = CurrentInteractionTarget;
                    if (player == null || target == null || target.GetComponentInParent<ModuleUnlockCrate>() == null) return false;
                    if ((player.transform.position - target.transform.position).sqrMagnitude >
                        step.requiredTargetDistance * step.requiredTargetDistance) return false;
                    if (step.completion == TutorialCompletion.TargetNearby) return true;
                    bool scanned = scanner != null && scanner.IsScanning && scanner.ScanComplete && scanner.Target != null &&
                        scanner.Target.GetComponentInParent<ModuleUnlockCrate>() == target.GetComponentInParent<ModuleUnlockCrate>();
                    if (scanned) { scannedStep = step; scannedCrate = target.GetComponentInParent<ModuleUnlockCrate>(); }
                    return false;
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
                    return NightRaidController.Instance != null && NightRaidController.Instance.IsInRaid &&
                        NightRaidController.Instance.RaidEarnedParts > 0;
                default: return false;
            }
        }
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
            if (!allowed) FeedbackUI.For(this)?.ShowNotification("앞에 설치한 연장 모듈을 지워 보세요.");
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
        public static void PrepareTrainingReturn(bool died)
        {
            if (trainingSequence == null) return;
            trainingReturning = true;
            if (died)
            {
                trainingStep = Array.FindIndex(trainingSequence.steps, s => s != null && s.id == "raid-retry");
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
