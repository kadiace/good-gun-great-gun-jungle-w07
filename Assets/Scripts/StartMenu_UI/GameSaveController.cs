using System;
using System.Collections;
using System.Collections.Generic;
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
        private string _pendingClearedRaidScene;
        private readonly HashSet<string> _unlockedMainTrackIds = new HashSet<string>(StringComparer.Ordinal);
        private bool _trackUnlocksMigrated;
        private readonly HashSet<string> clearedRaidScenes = new HashSet<string>();
        private bool retired;
        private int retirementSavedDay = -1;
        public bool IsRetired => retired;
        // v8 이하에서 온 세이브면 true. 세이브 왕복에서 유지되고 은퇴 조건을 잔액만으로 바꾼다.
        private bool retirementLegacy;
        // 은퇴 누적 실적. 만족 판매 수와 누적 매출이다.
        private int lifetimeSatisfiedSales, lifetimeSaleRevenue;
        public bool RetirementLegacy => retirementLegacy;
        public int LifetimeSatisfiedSales => lifetimeSatisfiedSales;
        public int LifetimeSaleRevenue => lifetimeSaleRevenue;

        /// <summary>판매 실적을 누적한다. 만족 수·매출 모두 int 범위에서 포화된다.</summary>
        public void RecordSale(bool satisfied, int revenue)
        {
            if (satisfied)
                lifetimeSatisfiedSales = (int)Math.Min(int.MaxValue, (long)lifetimeSatisfiedSales + 1);
            if (revenue > 0)
                lifetimeSaleRevenue = (int)Math.Min(int.MaxValue, (long)lifetimeSaleRevenue + revenue);
        }

        private bool endChestOpened;
        public bool EndChestOpened => endChestOpened;

        /// <summary>
        /// 은퇴 상태를 확정하고 은퇴 시점 저장을 강제로 기록한다.
        /// SaveAuto는 은퇴 당일 저장을 건너뛰므로 여기서 직접 기록해야 잔액 차감과 retired가 남는다.
        /// 저장에 실패하면 은퇴 상태를 되돌리고 false를 반환한다. 잔액 복구는 호출부가 담당한다.
        /// </summary>
        public bool CompleteRetirement(out string error)
        {
            error = null;
            // 은퇴 시점 저장에 retired가 들어가도록 상태를 먼저 세팅한다.
            retired = true;
            retirementSavedDay = dayManager != null ? dayManager.DayNumber : -1;
            RefreshRetireButton();
            bool saved = false;
            if (TryCaptureWorkshop(out var data, out error))
            {
                try
                {
                    GameSaveFiles.WriteAutoSave(data, prettyPrint);
                    saved = true;
                }
                catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
                { error = "자동 저장 파일을 기록하지 못했습니다. " + e.Message; }
            }
            if (saved)
            {
                // 이후 같은 날짜의 SaveAuto는 early return으로 은퇴 시점 저장을 유지한다.
                if (autoSaveRoutine != null)
                {
                    StopCoroutine(autoSaveRoutine);
                    autoSaveRoutine = null;
                }
                return true;
            }
            retired = false;
            retirementSavedDay = -1;
            RefreshRetireButton();
            return false;
        }

        /// <summary>은퇴 상태와 누적 실적을 저장 데이터에서 복원한다.</summary>
        private void RestoreRetirementState(GameSaveData data)
        {
            lifetimeSatisfiedSales = Mathf.Max(0, data.lifetimeSatisfiedSales);
            lifetimeSaleRevenue = Mathf.Max(0, data.lifetimeSaleRevenue);
            retirementLegacy = data.retirementLegacy;
            retired = data.retired;
            retirementSavedDay = retired ? data.day : -1;
            endChestOpened = data.endChestOpened;
        }
        private void RefreshRetireButton()
        {
            var button = FindUniqueSceneComponent<RetireButton>(gameObject.scene);
            if (button != null) button.gameObject.SetActive(!retired);
        }
        public bool HasClearedRaid(string scene) => !string.IsNullOrEmpty(scene) && clearedRaidScenes.Contains(scene);
        public float SaleBonusRatio
        {
            get
            {
                float bonus = TableManager.Instance.CustomerOrder.sale.tagBonusRatio;
                var map = TableManager.Instance.RaidMap;
                if (map?.regions == null) return bonus;
                foreach (var region in map.regions)
                    if (region != null && HasClearedRaid(region.scene)) bonus += region.FirstClearSaleBonus();
                return bonus;
            }
        }
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
            if (retired && dayManager != null && dayManager.DayNumber == retirementSavedDay)
                return true;
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsGuidedDayZero)
            { error = "튜토리얼을 마친 뒤 저장할 수 있습니다."; return false; }
            if (!TryCaptureWorkshop(out var data, out error)) return false;
            try
            {
                GameSaveFiles.WriteAutoSave(data, prettyPrint);
                return true;
            }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
            { error = "자동 저장 파일을 기록하지 못했습니다. " + e.Message; return false; }
        }

        /// <summary>
        /// 현재 공방과 곡 해금 상태를 저장 가능한 데이터로 캡처한다.
        /// data에 저장 내용을 반환하고 실패 시 error에 사유를 담는다.
        /// </summary>
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
            data = new GameSaveData { version = 9, day = dayManager.DayNumber, balance = wallet.Balance, moduleCredits = wallet.ModuleCredits,
                slotTickets = wallet.SlotTickets,
                lifetimeSatisfiedSales = lifetimeSatisfiedSales, lifetimeSaleRevenue = lifetimeSaleRevenue,
                retirementLegacy = retirementLegacy,
                retired = retired, endChestOpened = endChestOpened,
                slotMachineSpins = FindUniqueSceneComponent<SlotMachineReels>(gameObject.scene)?.CompletedSpins ?? 0,
                raidClearProgress = raidClearProgress,
                unlockedMainTrackIds = _unlockedMainTrackIds.ToArray(), trackUnlocksMigrated = _trackUnlocksMigrated,
                clearedRaidScenes = clearedRaidScenes.ToArray(),
                outstandingLoans = wallet.OutstandingLoans,
                lastLoanDay = wallet.LastLoanDay,
                unlockedModules = wallet.GetComponent<PlayerModuleUnlocks>()?.CaptureUnlockedModules() ?? Array.Empty<int>(),
                savedUtcTicks = DateTime.UtcNow.Ticks, hasWeapon = weapon != null, saveId = PlayerIdentity.SaveId };
            var slotMachine = FindUniqueSceneComponent<SlotMachineReels>(gameObject.scene);
            if (slotMachine != null)
            {
                data.slotMachineDestructions = slotMachine.DestructionCount;
                data.slotMachineDestroyedDay = slotMachine.DestroyedDay;
                data.slotMachineDestroyedAtNight = slotMachine.DestroyedAtNight;
            }
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
            RefreshRetireButton();
        }
        /// <summary>
        /// 새 게임·저장 불러오기·습격 복귀의 상태를 초기화한다.
        /// 확정된 습격 결과를 복원·진행·BGM 처리 흐름에 전달한다.
        /// </summary>
        private void InitializeSaveState()
        {
            // TryConsumeReturn이 정적 데이터를 비우기 전에 결과를 지역 변수로 받는다.
            int resultRaid = NightRaidTransfer.ResultRaidNumber;
            string resultRaidScene = NightRaidTransfer.ResultRaidScene;
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
                    _pendingClearedRaidScene = cleared ? resultRaidScene : null;
                    unsuccessfulRaidReturn = !cleared;
                    if (showReturnFade) StartCoroutine(AdvanceAfterRaidNextFrame());
                    player.GetComponent<PlayerWallet>()?.ShowModuleCreditLoss(creditsBeforeLoss);
                }
                else NightRaidTransfer.KeepRecovery(workshop);
                return;
            }
            if (NightRaidTransfer.TryConsumeStandaloneReturn(out int slotTickets))
            {
                if (cleared) player.GetComponent<PlayerWallet>().CreditSlotTickets(slotTickets);
                pendingClearedRaid = cleared ? resultRaid : 0;
                _pendingClearedRaidScene = cleared ? resultRaidScene : null;
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
                InitializeNewTrackUnlocks();
                QueueAutoSave(dayManager != null ? dayManager.DayNumber : 0, true);
                return;
            }
            GameSaveFiles.PendingLoad = null;
            if (!restoreOnStart) return;
            Restore(data);
        }
        /// <summary>
        /// 클리어 결과를 진행도와 곡 해금에 반영한 뒤 다음 날로 이동한다.
        /// showFade가 true이면 귀환 페이드 전환을 요청한다.
        /// </summary>
        private void AdvanceAfterRaid(bool showFade)
        {
            // 날짜 증가/습격 건너뛰기가 아닌 확정된 귀환 결과로만 진행도를 전진시킨다.
            if (_pendingClearedRaidScene != null && !SoundManager.IsTutorialMusicContext)
            {
                var clearedRegion = TableManager.Instance.RaidMap.Find(_pendingClearedRaidScene);
                if (clearedRegion != null && clearedRaidScenes.Add(_pendingClearedRaidScene))
                    LogKit.GameLogger.Data("Info", "Game", "raid_first_clear", new RaidFirstClearLog
                    {
                        scene = _pendingClearedRaidScene, day = dayManager.DayNumber,
                        saleBonusAdded = clearedRegion.FirstClearSaleBonus(), saleBonusTotal = SaleBonusRatio
                    });
                raidClearProgress = Math.Max(raidClearProgress, SoundInfo.ClampProgress(pendingClearedRaid));
                string clearedScene = _pendingClearedRaidScene;
                string rewardTrackId = UnlockRaidRewardTrack(clearedScene);
                pendingClearedRaid = 0;
                _pendingClearedRaidScene = null;
                SoundManager.CompleteRaidReturn(raidClearProgress, rewardTrackId, _unlockedMainTrackIds.ToArray());
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
        /// <summary>
        /// 구형 저장의 플레이어·공방·곡 해금 상태를 복원한다.
        /// data의 저장 정보를 씬에 반영하고 복원 불가 항목은 오류로 중단한다.
        /// </summary>
        private void RestoreCore(GameSaveData data)
        {
            GameManager.Instance?.UI?.GetComponent<ShootingTestHUD>()?.CancelTest();
            // v2 압축 세이브(legacyCompactSave)는 월드 스냅샷 대신 블록 나열 복원을 쓴다.
            // 읽기에서 version을 9로 통일하므로 경로 선택은 version이 아니라 표식으로 한다.
            if (!data.legacyCompactSave) { RestoreWorldSave(data); return; }
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
            FindUniqueSceneComponent<SlotMachineReels>(gameObject.scene)?.RestoreSpins(data.slotMachineSpins);
            player.GetComponent<PlayerWallet>().RestoreLoans(data.outstandingLoans);
            player.GetComponent<PlayerWallet>().RestoreLastLoanDay(data.lastLoanDay);
            player.GetComponent<PlayerWallet>().RestoreModuleCredits(data.moduleCredits);
            player.GetComponent<PlayerWallet>().RestoreSlotTickets(data.slotTickets);
            player.GetComponent<PlayerModuleUnlocks>()?.RestoreUnlockedModules(data.unlockedModules);
            player.GetComponent<WeaponPresets>()?.Restore(data.weaponPresets);
            dayManager.RestoreSavedState(data);
            FindUniqueSceneComponent<SlotMachineReels>(gameObject.scene)?.RestoreDamage(data.slotMachineDestructions,
                data.slotMachineDestroyedDay, data.slotMachineDestroyedAtNight);
            RestoreRaidProgress(data);
            RestoreTrackUnlocks(data);
            RestoreRetirementState(data);
            RefreshRetireButton();
        }
        // 구버전 세이브의 누락 필드는 기본값 0이다. 음수는 0으로 보정하고 진행도를 보존한다.
        private void RestoreRaidProgress(GameSaveData data)
        {
            raidClearProgress = SoundInfo.ClampProgress(data.raidClearProgress);
            clearedRaidScenes.Clear();
            if (data.clearedRaidScenes == null) return;
            foreach (string scene in data.clearedRaidScenes)
                if (TableManager.Instance.RaidMap.Find(scene) != null) clearedRaidScenes.Add(scene);
        }

        /// <summary>
        /// 신규 게임의 곡 해금을 DayFirst 하나로 초기화한다.
        /// 런타임 해금 목록과 SoundManager 판정을 새 세션 상태로 맞춘다.
        /// </summary>
        private void InitializeNewTrackUnlocks()
        {
            _unlockedMainTrackIds.Clear();
            _trackUnlocksMigrated = true;
            SoundManager.SetMainTrackUnlockState(_unlockedMainTrackIds.ToArray());
        }

        /// <summary>
        /// 저장의 곡 해금 목록을 복원하거나 구형 진행도 기준으로 한 번 이관한다.
        /// data의 해금 ID와 이관 표식을 갱신하며 실제 맵 클리어 이력은 만들지 않는다.
        /// </summary>
        private void RestoreTrackUnlocks(GameSaveData data)
        {
            _unlockedMainTrackIds.Clear();
            if (data.trackUnlocksMigrated)
            {
                foreach (string trackId in data.unlockedMainTrackIds)
                    if (!string.IsNullOrEmpty(trackId)) _unlockedMainTrackIds.Add(trackId);
            }
            else
            {
                foreach (SoundData.Track track in TableManager.Instance.Sound.tracks ?? Array.Empty<SoundData.Track>())
                    if (track != null && track.id != BgmId.DayFirst.ToString() && track.selectableInMain &&
                        track.unlockRaid >= 0 && track.unlockRaid <= raidClearProgress)
                        _unlockedMainTrackIds.Add(track.id);
                data.unlockedMainTrackIds = _unlockedMainTrackIds.ToArray();
                data.trackUnlocksMigrated = true;
            }
            _trackUnlocksMigrated = true;
            SoundManager.SetMainTrackUnlockState(_unlockedMainTrackIds.ToArray());
        }

        /// <summary>
        /// 클리어 씬에 연결된 곡 ID를 저장 대상 해금 목록에 추가한다.
        /// scene으로 곡 ID를 조회해 반환하며, 연결이 없으면 null을 반환한다.
        /// </summary>
        private string UnlockRaidRewardTrack(string scene)
        {
            SoundData.Track reward = SoundInfo.RaidRewardTrack(scene);
            if (reward == null) return null;
            _unlockedMainTrackIds.Add(reward.id);
            _trackUnlocksMigrated = true;
            return reward.id;
        }
    }
}
