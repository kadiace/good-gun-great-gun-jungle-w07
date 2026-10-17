using System;
using System.Collections;
using System.Linq;
using UnityEngine;
namespace WeaponWorkshop
{
    [DefaultExecutionOrder(100)]
    public sealed partial class GameSaveController : MonoBehaviour
    {
        [SerializeField, HideInInspector] private _DayManager dayManager;
        [SerializeField, HideInInspector] private FirstPersonPlayer player;
        [SerializeField, HideInInspector] private EquipableWeapon corePrefab;
        [SerializeField, HideInInspector] private Workbench[] workbenches;
        private Coroutine autoSaveRoutine;
        // 진행도는 현재 세이브에 속한다. 다른 슬롯을 로드할 때는 해당 슬롯 값으로 복원한다.
        private int raidClearProgress;
        private int pendingClearedRaid;
        private bool unsuccessfulRaidReturn;
        private const int AutoSaveRetryCount = 10;
        private const float AutoSaveRetryDelay = 0.5f;
        private void OnEnable()
        {
            if (dayManager != null) dayManager.DayAdvanced += SaveAutoAfterDayAdvanced;
        }

        private void OnDisable()
        {
            if (dayManager != null) dayManager.DayAdvanced -= SaveAutoAfterDayAdvanced;
            if (autoSaveRoutine != null)
            {
                StopCoroutine(autoSaveRoutine);
                autoSaveRoutine = null;
            }
        }

        private void SaveAutoAfterDayAdvanced(int day) => QueueAutoSave(day,
            TutorialManager.Instance != null && TutorialManager.Instance.IsGuidedDayZero);

        private void QueueAutoSave(int day, bool waitForFirstBusinessDay)
        {
            if (autoSaveRoutine != null) StopCoroutine(autoSaveRoutine);
            autoSaveRoutine = StartCoroutine(SaveAutoWithRetry(day, waitForFirstBusinessDay));
        }

        private IEnumerator SaveAutoWithRetry(int day, bool waitForFirstBusinessDay)
        {
            if (waitForFirstBusinessDay)
            {
                while (dayManager != null && (dayManager.DayNumber <= 0 ||
                       (TutorialManager.Instance != null && TutorialManager.Instance.IsGuidedDayZero)))
                    yield return new WaitForSecondsRealtime(AutoSaveRetryDelay);
                if (dayManager == null)
                {
                    autoSaveRoutine = null;
                    yield break;
                }
                day = dayManager.DayNumber;
            }

            yield return new WaitForSecondsRealtime(0.25f);
            string error = null;
            for (int attempt = 0; attempt < AutoSaveRetryCount; attempt++)
            {
                if (SaveAuto(out error))
                {
                    autoSaveRoutine = null;
                    yield break;
                }
                if (attempt + 1 < AutoSaveRetryCount)
                    yield return new WaitForSecondsRealtime(AutoSaveRetryDelay);
            }

            Debug.LogError($"Day {day} 자동 저장에 실패했습니다: {error}", this);
            autoSaveRoutine = null;
        }

        public bool Save(int slot, out string error)
        {
            error = null;
            if (slot < 0 || slot >= GameSaveFiles.SlotCount)
            { error = "자동 저장 슬롯은 직접 저장할 수 없습니다."; return false; }
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsGuidedDayZero)
            { error = "튜토리얼을 마친 뒤 저장할 수 있습니다."; return false; }
            if (NightRaidController.Instance != null && NightRaidController.Instance.IsBusy)
            { error = "야간 습격에서는 공방에 귀환한 뒤 저장하세요."; return false; }
            if (!TryCaptureWorkshop(out var data,out error)) return false;
            try { GameSaveFiles.Write(slot,data,prettyPrint);return true; }
            catch(Exception e) when(e is System.IO.IOException || e is UnauthorizedAccessException)
            {error="저장 파일을 기록하지 못했습니다: "+e.Message;return false;}
        }
        public bool SaveAuto(out string error)
        {
            error = null;
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsGuidedDayZero)
            { error = "튜토리얼을 마친 뒤 저장할 수 있습니다."; return false; }
            if (!TryCaptureWorkshop(out var data, out error)) return false;
            try { GameSaveFiles.WriteAutoSave(data, prettyPrint); return true; }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
            { error = "자동 저장 파일을 기록하지 못했습니다. " + e.Message; return false; }
        }

        public bool TryCaptureWorkshop(out GameSaveData data,out string error)
        {
            data=null;error=null;
            if (dayManager == null || player == null || dayManager.CurrentCustomerDay == null)
            { error = "일차와 플레이어 연결을 확인하세요."; return false; }
            var holder = player.GetComponent<WeaponHolder>();
            var session = player.GetComponent<WorkshopSession>();
            EquipableWeapon weapon = holder != null ? holder.EquippedWeapon : null;
            if (weapon == null && session != null && session.ActiveWorkbench != null && session.ActiveWorkbench.CurrentCore != null)
                weapon = session.ActiveWorkbench.CurrentCore.GetComponentInParent<EquipableWeapon>();
            if ((weapon != null && weapon.IsMoving) || (session != null && session.IsTransitioning))
            { error = "무기와 카메라 이동이 끝난 후 저장하세요."; return false; }
            var wallet = player.GetComponent<PlayerWallet>();
            if (wallet == null) { error = "지갑 연결을 확인하세요."; return false; }
            data = new GameSaveData { day = dayManager.DayNumber, balance = wallet.Balance, moduleCredits = wallet.ModuleCredits,
                raidClearProgress = raidClearProgress,
                outstandingLoans = wallet.OutstandingLoans,
                lastLoanDay = wallet.LastLoanDay,
                unlockedModules = wallet.GetComponent<PlayerModuleUnlocks>()?.CaptureUnlockedModules() ?? Array.Empty<int>(),
                savedUtcTicks = DateTime.UtcNow.Ticks, hasWeapon = weapon != null, saveId = PlayerIdentity.SaveId };
            data.playerPosition = player.transform.position; data.playerEuler = player.transform.eulerAngles;
            data.weaponPresets = player.GetComponent<WeaponPresets>()?.Capture();
            data.viewEuler = player.ViewPoint.localEulerAngles;
            data.inWorkshop = session != null && session.IsInWorkshop;
            if (data.inWorkshop)
            {
                data.workbench = Array.IndexOf(workbenches, session.ActiveWorkbench);
                if (data.workbench < 0) { error = "저장할 작업대 연결이 없습니다."; return false; }
                data.benchEuler = session.ActiveWorkbench.RotationTarget.localEulerAngles;
            }
            data.confirmation = SaleConfirmationUI.Instance != null && SaleConfirmationUI.Instance.IsOpen;
            dayManager.CaptureSave(data);
            if (weapon != null)
            {
                var assembly = weapon.GetComponentInChildren<CubeAssembly>();
                if (assembly == null || !assembly.TryCaptureLayout(out var layout))
                { error = "현재 무기 구성을 저장할 수 없습니다."; return false; }
                data.paidCost = layout.paidConstructionCost;
                data.weaponName = weapon.WeaponName;
                data.passedTestIds = weapon.CapturePassedTests(out data.testAssemblyKey);
                data.blocks = layout.blocks.Select(b => new SaveBlock { slot = b.slot, cell = b.cell,
                    output = b.outputDirection, normal = b.orientationNormal, paidCost = b.paidPurchaseCost, paidModuleCreditCost = b.paidModuleCreditCost, hasColor = b.hasColor, color = b.color }).ToArray();
            }
            data.sceneName = gameObject.scene.name;
            if (!CaptureWorld(data, out error)) return false;
            data.travelCustomerDay=dayManager.CurrentCustomerDay;
            return true;
        }
        public bool TryCaptureTravel(out GameSaveData data,out string error)
        {
            bool map=saveMapStructure,weapons=saveAllWeapons,targets=saveTargetDamage,physics=saveWeaponPhysics;
            try {saveMapStructure=saveAllWeapons=saveTargetDamage=saveWeaponPhysics=true;return TryCaptureWorkshop(out data,out error);}
            finally {saveMapStructure=map;saveAllWeapons=weapons;saveTargetDamage=targets;saveWeaponPhysics=physics;}
        }
        private void Start()
        {
            using (dayManager != null ? dayManager.SuppressShopLogs() : null) InitializeSaveState();
            // 저장/복귀 복원이 끝난 뒤에만 새 Main BGM을 허용한다.
            SoundManager.InitializeMainMusic(raidClearProgress, unsuccessfulRaidReturn);
        }
        private void InitializeSaveState()
        {
            // TryConsumeReturn이 정적 데이터를 비우기 전에 결과를 지역 변수로 받는다.
            int resultRaid = NightRaidTransfer.ResultRaidNumber;
            bool cleared = NightRaidTransfer.ResultCleared;
            bool showReturnFade = NightRaidTransfer.ReturnDayFadePending;
            NightRaidTransfer.ReturnDayFadePending = false;
            if(NightRaidTransfer.TryConsumeReturn(out var workshop, out int creditsBeforeLoss))
            {
                // Travel starts at this scene's player placement; disk loads still restore saved poses.
                if (player != null)
                {
                    workshop.playerPosition = player.transform.position;
                    workshop.playerEuler = player.transform.eulerAngles;
                    workshop.viewEuler = player.ViewPoint.localEulerAngles;
                }
                if(RestoreWorldSave(workshop))
                {
                    if (TutorialManager.TrainingReturnPending && TutorialManager.Instance != null &&
                        TutorialManager.Instance.ResumeTrainingHome(dayManager)) return;
                    pendingClearedRaid = cleared ? resultRaid : 0;
                    unsuccessfulRaidReturn = !cleared;
                    if (showReturnFade) StartCoroutine(AdvanceAfterRaidNextFrame());
                    player.GetComponent<PlayerWallet>()?.ShowModuleCreditLoss(creditsBeforeLoss);
                }
                else NightRaidTransfer.KeepRecovery(workshop);
                return;
            }
            if (NightRaidTransfer.TryConsumeStandaloneReturn())
            {
                pendingClearedRaid = cleared ? resultRaid : 0;
                unsuccessfulRaidReturn = !cleared;
                if (cleared) AdvanceAfterRaid(true);
                if (dayManager != null)
                {
                    if (!cleared) dayManager.CloseShop();
                }
                return;
            }
            var data = GameSaveFiles.PendingLoad;
            if (data == null)
            {
                QueueAutoSave(dayManager != null ? dayManager.DayNumber : 0, true);
                return;
            }
            GameSaveFiles.PendingLoad = null;
            if (!restoreOnStart) return;
            Restore(data);
        }
        private void AdvanceAfterRaid(bool showFade)
        {
            // 날짜 증가/습격 건너뛰기가 아닌 확정된 귀환 결과로만 진행도를 전진시킨다.
            if (pendingClearedRaid > 0 && !SoundManager.IsTutorialMusicContext)
            {
                raidClearProgress = Math.Max(raidClearProgress, SoundInfo.ClampProgress(pendingClearedRaid));
                pendingClearedRaid = 0;
                SoundManager.CompleteRaidReturn(raidClearProgress);
                QueueAutoSave(dayManager.DayNumber, false);
            }
            if (!dayManager.CanStartNextDay) { dayManager.ShowSettlement(); return; }
            var ui = GameManager.Instance != null ? GameManager.Instance.UI : null;
            var transition = ui != null ? ui.GetComponent<DayTransitionUI>() : null;
            if (showFade && transition != null && transition.BeginReturn(dayManager)) return;
            dayManager.StartNextDay();
        }
        private IEnumerator AdvanceAfterRaidNextFrame()
        {
            yield return null;
            AdvanceAfterRaid(true);
        }
        private void Restore(GameSaveData data)
        {
            using (dayManager != null ? dayManager.SuppressShopLogs() : null) RestoreCore(data);
        }
        private void RestoreCore(GameSaveData data)
        {
            GameManager.Instance?.UI?.GetComponent<ShootingTestHUD>()?.CancelTest();
            if (data.version >= 3) { RestoreWorldSave(data); return; }
            if (dayManager == null || player == null || !dayManager.CanRestoreSave(data) || player.GetComponent<PlayerWallet>() == null ||
                (data.inWorkshop && (workbenches == null || data.workbench < 0 || data.workbench >= workbenches.Length || workbenches[data.workbench] == null)))
            { Debug.LogError("저장된 일차 또는 플레이어 연결을 확인하세요.", this); return; }
            player.RestorePose(data.playerPosition, data.playerEuler, data.viewEuler);
            EquipableWeapon restored = null;
            if (data.hasWeapon)
            {
                if (corePrefab == null) { Debug.LogError("저장 복원용 코어 프리팹이 없습니다.", this); return; }
                restored = Instantiate(corePrefab, player.transform.position + Vector3.up, Quaternion.identity);
                restored.SetWeaponName(data.weaponName);
                var assembly = restored.GetComponentInChildren<CubeAssembly>();
                bool missingPrefab = assembly == null;
                if (!missingPrefab)
                    foreach (var savedBlock in data.blocks)
                        if (assembly.GetPrefab(savedBlock.slot) == null) { missingPrefab = true; break; }
                if (missingPrefab)
                { Destroy(restored.gameObject); Debug.LogError("저장된 모듈 프리팹을 찾을 수 없습니다.", this); return; }
                var blocks = data.blocks.Select(b => new CubeAssembly.BlockSnapshot { slot = b.slot,
                    prefab = assembly.GetPrefab(b.slot), cell = b.cell, outputDirection = b.output,
                    orientationNormal = b.normal, paidPurchaseCost = b.paidCost, paidModuleCreditCost = b.paidModuleCreditCost, hasColor = b.hasColor, color = b.color }).ToArray();
                bool restoredLayout;
                using (restored.DeferPlayerCollisionIgnore())
                    restoredLayout = assembly.RestoreLayout(new CubeAssembly.LayoutSnapshot(blocks, data.paidCost));
                if (!restoredLayout || player.GetComponent<WeaponHolder>() == null)
                { Destroy(restored.gameObject); Debug.LogError("저장 무기를 복원하지 못했습니다.", this); return; }
                restored.RestorePassedTests(data.passedTestIds, data.testAssemblyKey);
            }
            if (data.inWorkshop)
            {
                var bench = workbenches[data.workbench];
                if (restored != null) bench.PlaceWeapon(restored, false);
                bench.RotationTarget.localRotation = Quaternion.Euler(data.benchEuler);
                player.GetComponent<WorkshopSession>()?.Enter(bench, false);
            }
            else if (restored != null && !player.GetComponent<WeaponHolder>().Equip(restored))
            { Destroy(restored.gameObject); Debug.LogError("저장 무기를 장착하지 못했습니다.", this); return; }
            player.GetComponent<PlayerWallet>().RestoreBalance(data.balance);
            player.GetComponent<PlayerWallet>().RestoreLoans(data.outstandingLoans);
            player.GetComponent<PlayerWallet>().RestoreLastLoanDay(data.lastLoanDay);
            player.GetComponent<PlayerWallet>().RestoreModuleCredits(data.moduleCredits);
            player.GetComponent<PlayerModuleUnlocks>()?.RestoreUnlockedModules(data.unlockedModules);
            player.GetComponent<WeaponPresets>()?.Restore(data.weaponPresets);
            dayManager.RestoreSavedState(data);
            RestoreRaidProgress(data);
        }
        // 구버전 세이브의 누락 필드는 기본값 0이다. 음수는 0으로 보정하고 진행도를 보존한다.
        private void RestoreRaidProgress(GameSaveData data) => raidClearProgress = SoundInfo.ClampProgress(data.raidClearProgress);
    }
}
