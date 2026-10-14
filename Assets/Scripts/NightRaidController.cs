using UnityEngine;
using UnityEngine.SceneManagement;
namespace WeaponWorkshop
{
    public sealed class NightRaidController : MonoBehaviour
    {
        [SerializeField] private _DayManager dayManager;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private UIManager uiManager;
        private NightRaidUI ui => uiManager != null ? uiManager.RaidUI : null;
        [SerializeField] private GameSaveController workshopSave;
        [SerializeField] private string[] regionScenes;
        public string RegionScene(int index) => regionScenes != null && index >= 0 && index < regionScenes.Length ? regionScenes[index] : null;
        [SerializeField] private EquipableWeapon carriedWeaponPrefab;
        [SerializeField] private string standaloneWorkshopScene = "SampleScene";
        public static NightRaidController Instance { get; private set; }
        public bool IsBusy => mapOpen || transitioning || inRaid;
        public bool IsInRaid => inRaid;
        private bool mapOpen, transitioning, inRaid, returnOpen, openedFromPreparation, entryRestoreFailed;
        private bool returningAfterDeath;
        private FirstPersonPlayer player;
        private RaidPlayerHealth playerHealth;
        private PaintMaterialLibrary travelPaints;
        private float returnCooldown;
        private NightRaidBoundary extractionExit;
        public bool IsExtracting => extractionExit != null || resultOpen;
        private float extractionElapsed;
        private const float ExtractionDuration = 5f;
        private int raidStartingCredits, raidStartingBalance;
        private int[] raidStartingModules;
        private bool resultOpen;
        private readonly System.Collections.Generic.SortedSet<int> trainingModules = new System.Collections.Generic.SortedSet<int>();
        public bool HasTrainingModule(int moduleId) => trainingModules.Contains(moduleId);
        public bool TryCollectTrainingModule(int moduleId)
        {
            if (!inRaid || !TutorialManager.TrainingRaidPending) return false;
            if (trainingModules.Add(moduleId))
                FeedbackUI.For(this)?.ShowNotification("튜토리얼 모듈 획득: " + player.GetComponent<PlayerModuleUnlocks>().GetModuleName(moduleId));
            return true;
        }
        private string RaidRewards()
        {
            var wallet = player.GetComponent<PlayerWallet>();
            var unlocks = player.GetComponent<PlayerModuleUnlocks>();
            var text = new System.Text.StringBuilder("획득 정산\n\n");
            text.Append("모듈 재료  +").Append(Mathf.Max(0, wallet.ModuleCredits - raidStartingCredits)).AppendLine();
            int money = Mathf.Max(0, wallet.Balance - raidStartingBalance);
            if (money > 0) text.Append("돈  +").Append(money).AppendLine();
            int count = 0;
            foreach (int id in unlocks.CaptureUnlockedModules())
                if (System.Array.IndexOf(raidStartingModules, id) < 0)
                { text.Append("• ").Append(unlocks.GetModuleName(id)).AppendLine(); count++; }
            foreach (int id in trainingModules)
            { text.Append("• 튜토리얼 모듈: ").Append(unlocks.GetModuleName(id)).AppendLine(); count++; }
            if (count == 0) text.AppendLine("획득한 신규 모듈 없음");
            return text.ToString();
        }
        private void ShowRaidResult(bool died)
        {
            if (!inRaid || transitioning) return;
            died |= playerHealth != null && playerHealth.IsDead;
            if (returningAfterDeath || (resultOpen && !died)) return;
            resultOpen = true;
            if (died)
            {
                CancelExtraction();
                returnOpen = false;
                ui.ShowReturn(false);
            }
            else { extractionExit = null; ui.SetExtraction(true, 1f, 0f); }
            returningAfterDeath = died;
            ui.ShowRaidResult(died, died ? string.Empty : RaidRewards(), () =>
            { returnOpen = true; ReturnHome(); });
        }
        public void ShowDeathResult() => ShowRaidResult(true);
        public void BeginExtraction(FirstPersonPlayer entrant, NightRaidBoundary exit)
        {
            if (!inRaid || transitioning || resultOpen || returnOpen || entrant != player || exit == null || !exit.Contains(player) ||
                extractionExit != null || Time.unscaledTime < returnCooldown || (playerHealth != null && playerHealth.IsDead)) return;
            if (TutorialManager.Instance != null && !TutorialManager.Instance.CanReturnFromTraining)
            {
                FeedbackUI.For(this)?.ShowNotification("먼저 안내된 상자에서 연습용 모듈을 획득하세요.");
                returnCooldown = Time.unscaledTime + 2f;
                return;
            }
            extractionExit = exit; extractionElapsed = 0f;
            ui.SetExtraction(true, 0f, ExtractionDuration);
        }
        public void CancelExtraction(NightRaidBoundary exit = null)
        {
            if (exit != null && extractionExit != exit) return;
            extractionExit = null; extractionElapsed = 0f;
            if (ui != null) ui.SetExtraction(false, 0f, ExtractionDuration);
        }
        private void Update()
        {
            // Raid death must not depend on the legacy death UI being active.
            if (inRaid && !transitioning && !returningAfterDeath && !entryRestoreFailed && playerHealth != null && playerHealth.IsDead)
            { ShowDeathResult(); return; }
            if (extractionExit == null || transitioning) return;
            if (!extractionExit.isActiveAndEnabled || !extractionExit.Contains(player) || (playerHealth != null && playerHealth.IsDead))
            { CancelExtraction(); return; }
            if (GameManager.GameplayBlocked) return;
            extractionElapsed = Mathf.Min(ExtractionDuration, extractionElapsed + Time.deltaTime);
            ui.SetExtraction(true, extractionElapsed / ExtractionDuration, ExtractionDuration - extractionElapsed);
            if (extractionElapsed < ExtractionDuration) return;
            ShowRaidResult(false);
        }
        private void Awake()
        {
            Instance = this;
            trainingModules.Clear();
            mapOpen = transitioning = inRaid = returnOpen = entryRestoreFailed = false;
            resultOpen = returningAfterDeath = false;
            CancelExtraction();
            ui.ShowMap(false); ui.ShowReturn(false); ui.Bind(this);
        }
        private void OnDestroy()
        {
            if (playerHealth != null) playerHealth.Died -= ShowDeathResult;
            travelPaints?.Dispose();
            if (Instance == this) Instance = null;
        }
        private void Start()
        {
            var area = Object.FindFirstObjectByType<NightRaidArea>();
            if (area == null) return;
            player = uiManager.Player;
            playerHealth = player != null ? player.GetComponent<RaidPlayerHealth>() : null;
            inRaid = true;
            WorkshopSoundEffects.RaiseNightRaidStarted();
            if (player == null || area.SpawnPoint == null)
            { ui.ShowError("습격 플레이어 또는 시작 위치 연결이 없습니다."); return; }
            Teleport(area.SpawnPoint.position, area.SpawnPoint.rotation);
            player.SetViewRotation(Quaternion.identity);
            if (NightRaidTransfer.TryConsumeEntry(out int balance, out int moduleCredits, out var unlockedModules, out var weapon))
            {
                player.GetComponent<PlayerWallet>().RestoreBalance(balance);
                player.GetComponent<PlayerWallet>().RestoreModuleCredits(moduleCredits);
                player.GetComponent<PlayerModuleUnlocks>()?.RestoreUnlockedModules(unlockedModules);
                travelPaints = PaintMaterialLibrary.CreateDefaults();
                if (!GameSaveController.TryRestoreCarried(weapon, carriedWeaponPrefab, player, travelPaints, out var error))
                {
                    entryRestoreFailed = true; returnOpen = true;
                    ui.ShowReturn(true); ui.ShowError(error); gameManager.SetDialogue(true);
                    return;
                }
            }
            player.GetComponent<PlayerRaidDeathLoot>()?.BeginRaid();
            raidStartingCredits = player.GetComponent<PlayerWallet>().ModuleCredits;
            raidStartingBalance = player.GetComponent<PlayerWallet>().Balance;
            raidStartingModules = player.GetComponent<PlayerModuleUnlocks>()?.CaptureUnlockedModules() ?? System.Array.Empty<int>();
            if (playerHealth != null) playerHealth.Died += ShowDeathResult;
            gameManager.SetDialogue(false); gameManager.SetNight(false);
        }
        public void OpenMap()
        {
            if (IsBusy || dayManager == null || !dayManager.IsNight || dayManager.IsBankrupt) return;
            openedFromPreparation = !dayManager.IsSettlementOpen;
            mapOpen = true; dayManager.HideSettlementForRaid();
            gameManager.SetNight(true); ui.ResetBackButton(); ui.ShowMap(true);
            if (TutorialManager.Instance != null) TutorialManager.Instance.NotifySignal("raid-map-opened", TutorialManager.Instance.CurrentInteractionTarget);
        }
        public void CloseMap()
        {
            if (!TutorialManager.ActionAllowed("close-map")) return;
            if (transitioning || !mapOpen) return;
            mapOpen = false; ui.ShowMap(false);
            if (openedFromPreparation) gameManager.SetNight(false);
            else dayManager.ShowSettlement();
        }
        public void EnterRegion(int index)
        {
            if (!mapOpen || transitioning || index < 0 || index >= regionScenes.Length) return;
            var tutorial = TutorialManager.Instance;
            if (tutorial != null && !tutorial.AllowsRaidRegion(index)) return;
            bool training = tutorial != null && tutorial.IsGuidedDayZero;
            string regionScene = training ? TutorialManager.TrainingScene : regionScenes[index];
            if (!Application.CanStreamedLevelBeLoaded(regionScene))
            { ui.ShowError("습격 씬이 빌드 목록에 없습니다."); return; }
            player = uiManager.Player; if (player == null) return;
            player.GetComponent<WorkshopSession>()?.LeaveImmediately();
            GameSaveData checkpoint = null; string error = null;
            if (workshopSave == null || !workshopSave.TryCaptureTravel(out checkpoint, out error))
            { ui.ShowError(workshopSave == null ? "공방 저장 관리자가 연결되지 않았습니다." : error); return; }
            checkpoint.settlement = true;
            if (training && !tutorial.PrepareTrainingRaid()) return;
            transitioning = true; ui.SetLoading(); PoolManager.ClearActive();
            NightRaidTransfer.Begin(checkpoint);
            SceneManager.LoadSceneAsync(regionScene, LoadSceneMode.Single);
        }
        private void Teleport(Vector3 position, Quaternion rotation)
        {
            var body = player.GetComponent<CharacterController>(); body.enabled = false;
            player.transform.SetPositionAndRotation(position, rotation); body.enabled = true;
            player.SetViewRotation(player.ViewPoint.localRotation);
        }
        public void CancelReturn()
        {
            if (!returnOpen || transitioning || entryRestoreFailed) return;
            returnOpen = false; returnCooldown = Time.unscaledTime + .75f;
            ui.ShowReturn(false); gameManager.SetDialogue(false);
        }
        public void ReturnHome()
        {
            if (!inRaid || transitioning || !returnOpen) return;
            if (!returningAfterDeath && playerHealth != null && playerHealth.IsDead)
            { ShowDeathResult(); return; }
            var checkpoint = NightRaidTransfer.Workshop;
            string homeScene = checkpoint != null ? checkpoint.sceneName : standaloneWorkshopScene;
            if (!Application.CanStreamedLevelBeLoaded(homeScene))
            { ui.ShowError("복귀할 공방 씬이 없습니다."); return; }
            if (checkpoint != null)
            {
                if (entryRestoreFailed)
                {
                    if (TutorialManager.TrainingRaidPending) TutorialManager.PrepareTrainingReturn(true);
                    NightRaidTransfer.MarkReturning();
                }
                else if (!NightRaidTransfer.PrepareReturn(player, returningAfterDeath, out var error)) { ui.ShowError(error); return; }
            }
            else
            {
                NightRaidTransfer.Abandon();
                if (returningAfterDeath) NightRaidTransfer.MarkStandaloneDeathReturn();
            }
            transitioning = true; gameManager.SetNight(true); ui.ShowReturn(false); PoolManager.ClearActive();
            NightRaidTransfer.ReturnDayFadePending = true;
            SceneManager.LoadSceneAsync(homeScene, LoadSceneMode.Single);
        }
        public void ReturnAfterDeath()
        {
            if (!inRaid || transitioning) return;
            CancelExtraction();
            returningAfterDeath = true;
            returnOpen = true;
            ReturnHome();
        }
        public void PersistDeathRecovery()
        {
            if (!inRaid || NightRaidTransfer.Workshop == null) return;
            if (!NightRaidTransfer.PrepareReturn(player, true, out var error))
            { Debug.LogError(error, this); return; }
            NightRaidTransfer.Workshop.raidDeathReturnPending = true;
            GameSaveFiles.TryWriteRaidRecovery(NightRaidTransfer.Workshop);
        }
    }
}
