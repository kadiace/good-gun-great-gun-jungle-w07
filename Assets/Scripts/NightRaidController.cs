using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using Unity.Profiling;
using LogKit;
using Stopwatch = System.Diagnostics.Stopwatch;
namespace WeaponWorkshop
{
    public sealed class NightRaidController : MonoBehaviour
    {
        private struct CellDestroyedLog
        {
            public string stageId;
            public float time;
            public string blockId;
        }

        private static readonly ProfilerMarker cellLogFlushMarker = new ProfilerMarker("NightRaidController.CellLogFlush");
        private const double CellLogFrameBudgetMilliseconds = 1.0;
        private readonly System.Collections.Generic.Queue<CellDestroyedLog> pendingCellLogs =
            new System.Collections.Generic.Queue<CellDestroyedLog>();
        // 셀 로그가 다 쓰인 뒤에 기록할 습격 결과 로그(셀 로그보다 먼저 쓰이지 않게 대기).
        private string pendingResultMessage;
        private object pendingResultLog;
        [SerializeField] private _DayManager dayManager;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private UIManager uiManager;
        private NightRaidUI ui => uiManager != null ? uiManager.RaidUI : null;
        [SerializeField] private GameSaveController workshopSave;
        // 목적지 목록은 스테이지 테이블에서 읽는다. 씬마다 목적지 배열을 따로 두지 않는다.
        public int RegionCount => TableManager.Instance.RaidMap.Count();
        public string RegionScene(int index) => TableManager.Instance.RaidMap.SceneAt(index);
        [SerializeField] private EquipableWeapon carriedWeaponPrefab;
        public EquipableWeapon CarriedWeaponPrefab => carriedWeaponPrefab;
        [SerializeField] private string standaloneWorkshopScene = "Main";
        public static NightRaidController Instance { get; private set; }
        public bool IsBusy => mapOpen || transitioning || inRaid;
        public bool IsMapOpen => mapOpen;
        public bool IsInRaid => inRaid;
        private bool mapOpen, transitioning, inRaid, returnOpen, openedFromPreparation, entryRestoreFailed;
        private bool returningAfterDeath;
        private bool firstWallDestroyed;
        private FirstPersonPlayer player;
        private WeaponHolder playerWeaponHolder;
        private Camera raidCamera;
        private RaidPlayerHealth playerHealth;
        private PaintMaterialLibrary travelPaints;
        private float returnCooldown;
        private NightRaidBoundary extractionExit;
        public bool IsExtracting => extractionExit != null || resultOpen;
        public bool IsResultFinalizedFor(RaidPlayerHealth _health) =>
            _health != null && inRaid && resultOpen && ReferenceEquals(_health, playerHealth);
        private float extractionElapsed;
        private const float ExtractionDuration = 5f;
        private int[] raidStartingModules;
        private float raidStartedAt;
        private int enemyKills, materialCrateCredits;
        private int raidEarnedDollars;
        private int earnedSlotTickets, collectedSlotTickets;
        private int raidEarnedParts, raidEarnedTickets;
        private readonly System.Collections.Generic.List<int> raidEarnedModules = new System.Collections.Generic.List<int>();
        private readonly System.Collections.Generic.List<int> moduleCrateRewards = new System.Collections.Generic.List<int>();
        private readonly System.Collections.Generic.List<int> pendingMaterialPickups = new System.Collections.Generic.List<int>();
        // 스테이지 중 획득한 모듈도 완료 정산 때 반영.
        private readonly System.Collections.Generic.List<int> pendingModuleCollects = new System.Collections.Generic.List<int>();
        private readonly System.Collections.Generic.HashSet<string> discoveredEnemies = new System.Collections.Generic.HashSet<string>();
        private readonly System.Collections.Generic.HashSet<string> loggedRaidWeaponAssemblies = new System.Collections.Generic.HashSet<string>();
        private bool resultOpen;
        private bool rewardsCommitted;
        private readonly System.Collections.Generic.SortedSet<int> trainingModules = new System.Collections.Generic.SortedSet<int>();
        public int RaidEarnedDollars => raidEarnedDollars;
        public int RaidEarnedParts => raidEarnedParts;
        public int RaidEarnedTickets => raidEarnedTickets;
        public System.Collections.Generic.IReadOnlyList<int> RaidEarnedModules => raidEarnedModules;
        public bool HasEarnedModule(int moduleId) => raidEarnedModules.Contains(moduleId) || trainingModules.Contains(moduleId);
        public bool HasTrainingModule(int moduleId) => trainingModules.Contains(moduleId);
        public bool TryCollectTrainingModule(int moduleId)
        {
            if (!inRaid || resultOpen || !TutorialManager.TrainingRaidPending)
            {
                return false;
            }
            if (trainingModules.Add(moduleId))
                FeedbackUI.For(this)?.ShowNotification("튜토리얼 모듈 획득: " + player.GetComponent<PlayerModuleUnlocks>().GetModuleName(moduleId));
            return true;
        }
        private string RaidRewards(System.Collections.Generic.List<int> modules)
        {
            var unlocks = player.GetComponent<PlayerModuleUnlocks>();
            // 튜토리얼 습격은 복귀 때 출발 상태로 되돌린다(NightRaidTransfer.PrepareReturn). 획득량을 실제 보상처럼 보이지 않게 표시한다.
            bool training = TutorialManager.TrainingRaidPending;
            var text = new System.Text.StringBuilder(training ? "획득 정산 (튜토리얼 연습)\n\n" : "획득 정산\n\n");
            if (training) text.AppendLine("연습 습격의 획득물은 공방에 반영되지 않습니다.").AppendLine();
            text.Append("슬롯머신 티켓  +").Append(earnedSlotTickets).AppendLine();
            text.Append("돈  +").Append(raidEarnedDollars).AppendLine();
            foreach (int id in unlocks.CaptureUnlockedModules())
                if (System.Array.IndexOf(raidStartingModules, id) < 0)
                    if (!modules.Contains(id)) modules.Add(id);
            foreach (int id in raidEarnedModules)
                if (!modules.Contains(id)) modules.Add(id);
            foreach (int id in trainingModules)
                if (!raidEarnedModules.Contains(id))
                    if (!modules.Contains(id)) modules.Add(id);
            if (modules.Count == 0) text.AppendLine("획득한 신규 모듈 없음");
            return text.ToString();
        }
        /// <summary>
        /// 습격 결과를 표시하고 씬 키와 클리어 여부를 복귀 데이터에 기록한다.
        /// died 상태에 따라 사망·탈출 결과와 UI 동작을 결정한다.
        /// </summary>
        private void ShowRaidResult(bool died)
        {
            if (!inRaid || transitioning || resultOpen)
            {
                return;
            }
            died |= playerHealth != null && playerHealth.IsDead;
            if (returningAfterDeath)
            {
                return;
            }
            resultOpen = true;
            // 쌓인 셀 로그를 이 프레임에 전부 쓰지 않는다. 결과 로그는 셀 로그 뒤에 쓰이도록 대기열 끝에 둔다.
            // 결과 화면 동안 Update의 예산 기록이 이어서 비우고, 남은 것은 복귀·비활성 시 FlushCellLogs가 쓴다.
            // 결과 화면은 실제 시간으로 재생되므로 기존 야간 정지 정책으로 게임 진행을 멈춘다.
            gameManager.SetNight(true);
            if (died)
            {
                CancelExtraction();
                returnOpen = false;
                ui.ShowReturn(false);
            }
            else { extractionExit = null; ui.SetExtraction(true, 1f, 0f); }
            bool isClear = IsRaidClear(died);
            returningAfterDeath = died;
            if (isClear) raidEarnedTickets = TutorialManager.TrainingRaidPending ? collectedSlotTickets : earnedSlotTickets;
            int materialPickups = pendingMaterialPickups.Count;
            int dollarReward = raidEarnedDollars;
            materialCrateCredits = 0;
            foreach (int v in pendingMaterialPickups) materialCrateCredits += v;
            moduleCrateRewards.AddRange(pendingModuleCollects);
            pendingModuleCollects.Clear();
            // 확정된 추출/사망 결과를 Main까지 전달한다. 훈련은 기존 복귀만 수행한다.
            if (!SoundManager.IsTutorialMusicContext)
                NightRaidTransfer.RecordResult(SoundInfo.Raid(gameObject.scene.name)?.number ?? 0, gameObject.scene.name, isClear);
            pendingResultMessage = died ? "습격 사망" : "습격 탈출 성공";
            pendingResultLog = new RaidResultLog
            {
                scene = gameObject.scene.name, survived = !died,
                time = Time.timeSinceLevelLoad, duration = Time.timeSinceLevelLoad - raidStartedAt,
                moduleCrateRewards = moduleCrateRewards.ToArray(),
                materialCrateCredits = materialCrateCredits, enemyKills = enemyKills,
                health = playerHealth != null ? playerHealth.CurrentHealth : 0f,
                exitReason = died ? "died" : "extracted", endChestOpened = endChestOpened,
                day = RaidDay, training = TutorialManager.TrainingRaidPending,
                materialPickups = materialPickups, dollarReward = dollarReward,
                rewardsApplied = isClear && !TutorialManager.TrainingRaidPending
            };
            if (pendingCellLogs.Count == 0) RecordPendingResultLog();
            var resultModules = new System.Collections.Generic.List<int>();
            ui.ShowRaidResult(died, died ? string.Empty : RaidRewards(resultModules), resultModules, () =>
            { returnOpen = true; ReturnHome(); });
        }
        public void ShowDeathResult() => ShowRaidResult(true);
        // 향후 목표 조건을 추가할 단일 확장 지점. 지금은 생존한 추출 성공만 클리어다.
        private bool IsRaidClear(bool died) => !died;
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
            FlushCellLogsWithinBudget();
            // Raid death must not depend on the legacy death UI being active.
            if (inRaid && !resultOpen && !transitioning && !returningAfterDeath && !entryRestoreFailed && playerHealth != null && playerHealth.IsDead)
            {
                ShowDeathResult();
                return;
            }
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
            loggedRaidWeaponAssemblies.Clear();
            firstWallDestroyed = false;
            endChestScanned = endChestOpened = false;
            endChestHitAt = -1f;
            CancelExtraction();
            ui.Bind(this); ui.ShowMap(false); ui.ShowReturn(false);
        }
        private void OnEnable()
        {
            DestructibleTarget.CellDestroyed += OnCellDestroyed;
            RaidEnemyCore.Spawned += OnEnemySpawned;
            RaidEnemyCore.Killed += OnEnemyKilled;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            Application.quitting += OnApplicationQuitting;
            if (inRaid) SubscribeWeaponEvents();
        }
        private void OnDisable()
        {
            FlushCellLogs();
            DestructibleTarget.CellDestroyed -= OnCellDestroyed;
            RaidEnemyCore.Spawned -= OnEnemySpawned;
            RaidEnemyCore.Killed -= OnEnemyKilled;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            Application.quitting -= OnApplicationQuitting;
            UnsubscribeWeaponEvents();
        }
        private void OnCellDestroyed(DestructibleTarget target, string blockId)
        {
            if (!inRaid || target.gameObject.scene != gameObject.scene) return;
            // 씬에 진입한 뒤 최초 1회만 전환하며 훈련 판정은 사운드 매니저에서도 방어한다.
            if (!firstWallDestroyed && !resultOpen && !transitioning && !entryRestoreFailed)
            {
                firstWallDestroyed = true;
                SoundManager.OnRaidFirstWallDestroyed(gameObject.scene.name);
            }
            pendingCellLogs.Enqueue(new CellDestroyedLog
            { stageId = gameObject.scene.name, time = Time.timeSinceLevelLoad, blockId = blockId });
        }
        private void FlushCellLogsWithinBudget()
        {
            if (pendingCellLogs.Count == 0)
            {
                RecordPendingResultLog();
                return;
            }
            using (cellLogFlushMarker.Auto())
            {
                long started = Stopwatch.GetTimestamp();
                double sharedBudgetMilliseconds = System.Math.Min(CellLogFrameBudgetMilliseconds,
                    DestructionFrameBudget.RemainingMilliseconds);
                long budgetTicks = (long)(Stopwatch.Frequency * sharedBudgetMilliseconds / 1000.0);
                // One entry always drains so a spent combat budget cannot stall the queue forever.
                bool wrote = false;
                while (pendingCellLogs.Count > 0 &&
                    (!wrote || Stopwatch.GetTimestamp() - started < budgetTicks))
                { RecordNextCellLog(); wrote = true; }
            }
        }
        private void FlushCellLogs()
        {
            if (pendingCellLogs.Count > 0)
                using (cellLogFlushMarker.Auto())
                {
                    while (pendingCellLogs.Count > 0) RecordNextCellLog();
                }
            RecordPendingResultLog();
        }
        private void RecordPendingResultLog()
        {
            if (pendingResultLog == null) return;
            GameLogger.Data("Info", "NightRaid", pendingResultMessage, pendingResultLog);
            pendingResultLog = null;
            pendingResultMessage = null;
        }
        private void RecordNextCellLog()
        {
            CellDestroyedLog entry = pendingCellLogs.Dequeue();
            GameLogger.Data("Info", "NightRaid", "블록 파괴", new RaidBlockDestroyedLog
            { stageId = entry.stageId, time = entry.time, blockId = entry.blockId });
        }
        private void OnSceneUnloaded(Scene scene)
        {
            if (scene == gameObject.scene) FlushCellLogs();
        }
        private void OnApplicationQuitting() => FlushCellLogs();
        private void OnEnemySpawned(RaidEnemyCore core)
        {
            if (!inRaid || core.gameObject.scene != gameObject.scene || string.IsNullOrEmpty(core.EnemyId)) return;
            GameLogger.Data("Info", "NightRaid", "적 생성", new RaidEnemyLog { time = Time.timeSinceLevelLoad, enemyId = core.EnemyId });
        }
        private void OnEnemyKilled(RaidEnemyCore core)
        {
            if (!inRaid || resultOpen || transitioning || entryRestoreFailed || core.gameObject.scene != gameObject.scene ||
                (playerHealth != null && playerHealth.IsDead)) return;
            enemyKills++;
            earnedSlotTickets += core.SlotTicketReward;
            core.ReleaseTicketVisuals(player);
            GameLogger.Data("Info", "NightRaid", "적 처치", new RaidEnemyLog { time = Time.timeSinceLevelLoad, enemyId = core.EnemyId });
        }
        public bool TryCollectTicket(FirstPersonPlayer collector)
        {
            if (!inRaid || resultOpen || transitioning || entryRestoreFailed || collector != player ||
                (playerHealth != null && playerHealth.IsDead) || collectedSlotTickets >= earnedSlotTickets) return false;
            var wallet = player.GetComponent<PlayerWallet>();
            if (wallet == null) return false;
            collectedSlotTickets++;
            raidEarnedTickets = collectedSlotTickets;
            return true;
        }

        public bool TryCollectDollar(FirstPersonPlayer collector, int amount)
        {
            if (!CanCollectRaidReward(collector, amount)) return false;
            raidEarnedDollars = (int)System.Math.Min(int.MaxValue, (long)raidEarnedDollars + amount);
            return true;
        }

        public bool TryCollectMaterial(FirstPersonPlayer collector, int amount)
        {
            if (!CanCollectRaidReward(collector, amount)) return false;
            raidEarnedParts = (int)System.Math.Min(int.MaxValue, (long)raidEarnedParts + amount);
            pendingMaterialPickups.Add(amount);
            return true;
        }

        public bool TryCollectModule(FirstPersonPlayer collector, int moduleId)
        {
            if (!inRaid || resultOpen || transitioning || collector != player || moduleId <= 0 ||
                (playerHealth != null && playerHealth.IsDead)) return false;
            if (TutorialManager.TrainingRaidPending && !TryCollectTrainingModule(moduleId)) return false;
            if (!raidEarnedModules.Contains(moduleId)) raidEarnedModules.Add(moduleId);
            if (!pendingModuleCollects.Contains(moduleId)) pendingModuleCollects.Add(moduleId);
            return true;
        }

        private bool CanCollectRaidReward(FirstPersonPlayer collector, int amount) =>
            inRaid && !resultOpen && !transitioning && collector == player && amount > 0 &&
            (playerHealth == null || !playerHealth.IsDead);
        public void RecordPlayerHit(RaidPlayerHealth health, string enemyId, RaidEnemyType enemyType, float damage)
        {
            if (!inRaid || resultOpen || health == null || health != playerHealth || health.gameObject.scene != gameObject.scene || damage <= 0f)
            {
                return;
            }
            try
            {
                GameLogger.Data("Info", "NightRaid", "player_hit", new RaidPlayerHitLog
                {
                    stageId = gameObject.scene.name, enemyId = enemyId ?? "", enemyType = enemyType.ToString(),
                    time = Time.timeSinceLevelLoad, duration = Time.timeSinceLevelLoad - raidStartedAt,
                    damage = damage, health = health.CurrentHealth, position = health.transform.position
                });
            }
            catch (System.Exception exception) { Debug.LogWarning("습격 피격 로그 기록 실패: " + exception.Message); }
        }
        // 엔드 상자 지표. 상자는 습격 씬에 하나라 습격당 한 번씩만 남긴다.
        private bool endChestScanned, endChestOpened;
        private float endChestHitAt = -1f;
        public bool EndChestFirstEnding { get; private set; }
        private static int RaidDay => NightRaidTransfer.Workshop != null ? NightRaidTransfer.Workshop.day : -1;
        private T EndChestEntry<T>(T entry) where T : EndChestLog
        {
            entry.scene = gameObject.scene.name;
            entry.day = RaidDay;
            entry.time = Time.timeSinceLevelLoad;
            entry.duration = inRaid ? Time.timeSinceLevelLoad - raidStartedAt : 0f;
            return entry;
        }
        public void NotifyEndChestScanned(EndChestCore core)
        {
            if (endChestScanned || core == null) return;
            endChestScanned = true;
            GameLogger.Data("Info", "NightRaid", "end_chest_scanned", EndChestEntry(new EndChestScannedLog
            { requiredGravity = core.RequiredGravity, requiredHoming = core.RequiredHoming }));
        }
        public void NotifyEndChestHit(in ProjectileStats stats)
        {
            if (endChestHitAt >= 0f) return;
            endChestHitAt = Time.timeSinceLevelLoad;
            var entry = EndChestEntry(new EndChestHitLog { gravity = stats.gravity, homing = stats.homing, scanned = endChestScanned });
            entry.weaponAssemblyId = CaptureAssemblyId(playerWeaponHolder != null ? playerWeaponHolder.EquippedWeapon : null,
                out entry.weaponAssemblyIdStatus);
            GameLogger.Data("Info", "NightRaid", "end_chest_hit", entry);
        }
        public void NotifyEndChestOpened()
        {
            if (endChestOpened) return;
            endChestOpened = true;
            var workshop = NightRaidTransfer.Workshop;
            EndChestFirstEnding = workshop == null || (!workshop.retired && !workshop.endChestOpened);
            NightRaidTransfer.MarkEndChestOpened();
            GameLogger.Data("Info", "NightRaid", "end_chest_opened", EndChestEntry(new EndChestOpenedLog
            { absorbSeconds = endChestHitAt >= 0f ? Time.timeSinceLevelLoad - endChestHitAt : 0f }));
        }
        // END 화면 그만하기: 결과 화면 없이 로비로 가므로 결과 로그를 여기서 닫는다. 보상은 확정하지 않는다.
        public void NotifyEndingQuit()
        {
            if (!inRaid || resultOpen) return;
            resultOpen = true;
            FlushCellLogs();
            int credits = 0;
            foreach (int v in pendingMaterialPickups) credits += v;
            var modules = new System.Collections.Generic.List<int>(moduleCrateRewards);
            modules.AddRange(pendingModuleCollects);
            GameLogger.Data("Info", "NightRaid", "습격 엔딩 종료", new RaidResultLog
            {
                scene = gameObject.scene.name, survived = playerHealth == null || !playerHealth.IsDead,
                time = Time.timeSinceLevelLoad, duration = Time.timeSinceLevelLoad - raidStartedAt,
                moduleCrateRewards = modules.ToArray(), materialCrateCredits = credits, enemyKills = enemyKills,
                health = playerHealth != null ? playerHealth.CurrentHealth : 0f,
                exitReason = "ending_quit", endChestOpened = endChestOpened,
                day = RaidDay, training = TutorialManager.TrainingRaidPending,
                materialPickups = pendingMaterialPickups.Count, dollarReward = raidEarnedDollars, rewardsApplied = false
            });
        }
        private static string CaptureAssemblyId(EquipableWeapon weapon, out string status)
        {
            if (weapon == null) { status = "no_weapon"; return ""; }
            try
            {
                string key = weapon.TestAssemblyKey;
                status = string.IsNullOrEmpty(key) ? "assembly_id_failed" : "ok";
                return key ?? "";
            }
            catch (System.Exception) { status = "assembly_id_failed"; return ""; }
        }
        private static string CaptureWeaponCode(EquipableWeapon weapon, out string status)
        {
            if (weapon == null) { status = "no_weapon"; return ""; }
            try { status = "ok"; return WeaponShareCode.Export(weapon); }
            catch (System.Exception) { status = "export_failed"; return ""; }
        }
        // 주문 판정(CustomerRequestInfo의 총구 스탯)과 같은 총구별 계산의 최댓값. 산탄 절반은 GetMuzzleStats에 포함, 차지 배율은 발사 때만 붙어 제외.
        private static void CaptureMuzzleMax(EquipableWeapon weapon, out float penetration, out float explosion)
        {
            penetration = explosion = 0f;
            var core = weapon.GetComponentInChildren<PowerCore>(true);
            var assembly = core != null ? core.GetComponent<CubeAssembly>() : null;
            if (assembly == null) return;
            try
            {
                foreach (var path in assembly.GetProjectilePaths())
                {
                    var stats = core.GetMuzzleStats(path.path);
                    penetration = Mathf.Max(penetration, stats.penetration);
                    explosion = Mathf.Max(explosion, stats.explosion);
                }
            }
            catch (System.Exception) { penetration = explosion = 0f; }
        }
        private void LogRaidStart(RaidCrateSpawner crates)
        {
            EquipableWeapon weapon = playerWeaponHolder != null ? playerWeaponHolder.EquippedWeapon : null;
            var entry = new RaidStartLog { scene = gameObject.scene.name, day = RaidDay };
            entry.entryCost = NightRaidTransfer.EntryCost;
            entry.training = TutorialManager.TrainingRaidPending;
            if (crates != null)
            {
                entry.spawnedModuleCrates = crates.SpawnedModuleCrates;
                entry.spawnedMaterialCrates = crates.SpawnedMaterialCrates;
                entry.replacedModuleSpots = crates.ReplacedModuleSpots;
                entry.materialPerCrate = crates.MaterialPerCrate;
            }
            var raidWallet = player != null ? player.GetComponent<PlayerWallet>() : null;
            entry.balance = raidWallet != null ? raidWallet.Balance : 0;
            entry.moduleCredits = raidWallet != null ? raidWallet.ModuleCredits : 0;
            entry.unlockedModules = raidStartingModules ?? System.Array.Empty<int>();
            if (weapon != null)
            {
                CaptureMuzzleMax(weapon, out entry.penetration, out entry.explosion);
                entry.weaponCode = CaptureWeaponCode(weapon, out string codeStatus);
                entry.weaponCodeStatus = codeStatus;
                entry.weaponAssemblyId = CaptureAssemblyId(weapon, out string assemblyIdStatus);
                entry.weaponAssemblyIdStatus = assemblyIdStatus;
                if (entry.weaponCodeStatus == "ok" && !string.IsNullOrEmpty(entry.weaponAssemblyId))
                    loggedRaidWeaponAssemblies.Add(entry.weaponAssemblyId);
            }
            GameLogger.Data("Info", "NightRaid", "습격 시작", entry);
        }
        private void LogRaidWeaponConfiguration(EquipableWeapon weapon, string assemblyId, string assemblyIdStatus)
        {
            if (weapon == null || string.IsNullOrEmpty(assemblyId) || !loggedRaidWeaponAssemblies.Add(assemblyId)) return;
            string weaponCode = CaptureWeaponCode(weapon, out string codeStatus);
            var entry = new RaidWeaponConfigLog
            {
                time = Time.timeSinceLevelLoad,
                weaponAssemblyId = assemblyId,
                weaponAssemblyIdStatus = assemblyIdStatus,
                weaponCode = weaponCode,
                weaponCodeStatus = codeStatus
            };
            GameLogger.Data("Info", "NightRaid", "raid_weapon_config", entry);
        }
        private void OnRaidWeaponChanged(EquipableWeapon previous, EquipableWeapon next)
        {
            if (!inRaid || next == null || next.gameObject.scene != gameObject.scene) return;
            string assemblyId = CaptureAssemblyId(next, out string status);
            LogRaidWeaponConfiguration(next, assemblyId, status);
        }
        private void OnRaidWeaponFired(EquipableWeapon weapon)
        {
            if (!inRaid || player == null || weapon == null || weapon.gameObject.scene != gameObject.scene) return;
            string assemblyId = CaptureAssemblyId(weapon, out string status);
            LogRaidWeaponConfiguration(weapon, assemblyId, status);
            GameLogger.Data("Info", "NightRaid", "weapon_fired", new RaidWeaponFiredLog
            {
                time = Time.timeSinceLevelLoad,
                position = player.transform.position,
                weaponAssemblyId = assemblyId,
                weaponAssemblyIdStatus = status
            });
        }
        private void SubscribeWeaponEvents()
        {
            if (playerWeaponHolder == null) return;
            playerWeaponHolder.EquippedWeaponChanged += OnRaidWeaponChanged;
            playerWeaponHolder.ShotFired += OnRaidWeaponFired;
        }
        private void UnsubscribeWeaponEvents()
        {
            if (playerWeaponHolder == null) return;
            playerWeaponHolder.EquippedWeaponChanged -= OnRaidWeaponChanged;
            playerWeaponHolder.ShotFired -= OnRaidWeaponFired;
        }
        private void FixedUpdate()
        {
            if (!inRaid || resultOpen || entryRestoreFailed || player == null) return;
            int moduleCrates = 0, materialCrates = 0, tanks = 0, helicopters = 0;
            foreach (var crate in ModuleUnlockCrate.ActiveCrates)
                if (crate != null && crate.gameObject.scene == gameObject.scene && !crate.IsClaimed) moduleCrates++;
            foreach (var crate in ModuleCreditCrate.ActiveCrates)
                if (crate != null && crate.gameObject.scene == gameObject.scene && !crate.IsClaimed) materialCrates++;
            for (int i = 0; i < RaidEnemyCore.ActiveCount; i++)
            {
                var core = RaidEnemyCore.ActiveAt(i);
                if (core == null || core.IsDestroyed || core.gameObject.scene != gameObject.scene || string.IsNullOrEmpty(core.EnemyId)) continue;
                if (core.EnemyType == RaidEnemyType.Tank) tanks++;
                else if (core.EnemyType == RaidEnemyType.Helicopter) helicopters++;
                if (GameManager.GameplayBlocked || raidCamera == null || discoveredEnemies.Contains(core.EnemyId)) continue;
                Vector3 aimPoint = core.GetComponent<TargetHealth>().AimPoint;
                Vector3 viewport = raidCamera.WorldToViewportPoint(aimPoint);
                if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) continue;
                Vector3 direction = aimPoint - raidCamera.transform.position;
                DestructionCollisionBatch.Flush();
                if (Physics.Raycast(raidCamera.transform.position, direction, out var hit, direction.magnitude,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                    RaidEnemyCore.FindForPart(hit.collider.transform) == core && discoveredEnemies.Add(core.EnemyId))
                    GameLogger.Data("Info", "NightRaid", "적 발견", new RaidEnemyLog { time = Time.timeSinceLevelLoad, enemyId = core.EnemyId });
            }
            GameLogger.Data("Info", "NightRaid", "플레이어 상태", new RaidPlayerStateLog
            {
                time = Time.timeSinceLevelLoad, position = player.transform.position,
                cameraRotation = raidCamera != null ? raidCamera.transform.rotation.eulerAngles : player.ViewPoint.rotation.eulerAngles,
                health = playerHealth != null ? playerHealth.CurrentHealth : 0f,
                remainingModuleCrates = moduleCrates, remainingMaterialCrates = materialCrates,
                tanks = tanks, helicopters = helicopters
            });
        }
        private void OnDestroy()
        {
            FlushCellLogs();
            if (playerHealth != null) playerHealth.Died -= ShowDeathResult;
            travelPaints?.Dispose();
            if (Instance == this) Instance = null;
        }
        private void Start()
        {
            var area = Object.FindFirstObjectByType<NightRaidArea>();
            if (area == null) return;
            player = uiManager.Player;
            playerWeaponHolder = player != null ? player.GetComponent<WeaponHolder>() : null;
            raidCamera = playerWeaponHolder != null ? playerWeaponHolder.AimCamera : null;
            // Core light rays sample opaque depth to stay hidden behind intact armour, including on mobile URP.
            if (raidCamera != null) raidCamera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
            playerHealth = player != null ? player.GetComponent<RaidPlayerHealth>() : null;
            if (player == null || area.SpawnPoint == null)
            { ui.ShowError("습격 플레이어 또는 시작 위치 연결이 없습니다."); return; }
            WorkshopSoundEffects.RaiseNightRaidStarted();
            Teleport(area.SpawnPoint.position, area.SpawnPoint.rotation);
            player.SetViewRotation(Quaternion.identity);
            if (NightRaidTransfer.TryConsumeEntry(out int balance, out int moduleCredits, out var unlockedModules, out var weapon))
            {
                player.GetComponent<PlayerWallet>().RestoreBalance(balance);
                player.GetComponent<PlayerWallet>().RestoreLoans(NightRaidTransfer.Workshop.outstandingLoans);
                player.GetComponent<PlayerWallet>().RestoreLastLoanDay(NightRaidTransfer.Workshop.lastLoanDay);
                player.GetComponent<PlayerWallet>().RestoreModuleCredits(moduleCredits);
                player.GetComponent<PlayerWallet>().RestoreSlotTickets(NightRaidTransfer.Workshop.slotTickets);
                player.GetComponent<PlayerModuleUnlocks>()?.RestoreUnlockedModules(unlockedModules);
                travelPaints = PaintMaterialLibrary.CreateDefaults();
                if (!GameSaveController.TryRestoreCarried(weapon, carriedWeaponPrefab, player, travelPaints, out var error))
                {
                    // 복귀 버튼(ReturnHome)은 inRaid일 때만 동작한다.
                    inRaid = true; entryRestoreFailed = true; returnOpen = true;
                    ui.ShowReturn(true); ui.ShowError(error); gameManager.SetDialogue(true);
                    return;
                }
            }
            inRaid = true;
            raidStartingModules = player.GetComponent<PlayerModuleUnlocks>()?.CaptureUnlockedModules() ?? System.Array.Empty<int>();
            raidStartedAt = Time.timeSinceLevelLoad;
            enemyKills = materialCrateCredits = raidEarnedDollars = 0;
            raidEarnedParts = raidEarnedTickets = 0;
            pendingMaterialPickups.Clear();
            raidEarnedModules.Clear();
            earnedSlotTickets = collectedSlotTickets = 0;
            pendingModuleCollects.Clear(); discoveredEnemies.Clear();
            rewardsCommitted = false;
            loggedRaidWeaponAssemblies.Clear();
            // 보상 상자는 입장 상태(해금 모듈)를 복원한 뒤 한 번에 만든다. 이미 해금한 모듈 자리는 재료 상자가 된다.
            var crateSpawner = Object.FindFirstObjectByType<RaidCrateSpawner>();
            crateSpawner?.Spawn(player.GetComponent<PlayerModuleUnlocks>());
            LogRaidStart(crateSpawner);
            SubscribeWeaponEvents();
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
            if (!mapOpen || transitioning || index < 0 || index >= RegionCount) return;
            var tutorial = TutorialManager.Instance;
            if (tutorial != null && !tutorial.AllowsRaidRegion(index)) return;
            if (!IsRegionUnlocked(index)) return;
            player = uiManager.Player;
            var holder = player != null ? player.GetComponent<WeaponHolder>() : null;
            var weapon = holder != null ? holder.EquippedWeapon : null;
            if (weapon == null || !weapon.IsEquipped || weapon.IsMoving)
            { ui.ShowUnarmedNotice(); return; }
            bool training = tutorial != null && tutorial.IsGuidedDayZero;
            string regionScene = training ? TutorialManager.TrainingScene : RegionScene(index);
            if (string.IsNullOrEmpty(regionScene)) return;
            if (!Application.CanStreamedLevelBeLoaded(regionScene))
            { ui.ShowError("습격 씬이 빌드 목록에 없습니다."); return; }
            if (player == null) return;
            // 0일차 안내 습격은 무료다. 그 외에는 표 행의 입장 비용을 확인한다.
            var region = training ? null : TableManager.Instance.RaidMap.Find(regionScene);
            if (!training && region == null)
            { ui.ShowError("습격 지역 데이터가 없습니다."); return; }
            int cost = training ? 0 : region.EntryCost();
            // 잔액 검사는 버튼이 이미 꺼져 있어야 하므로 방어용이다.
            if (cost > 0 && !region.CanAffordEntry(player.GetComponent<PlayerWallet>()))
            { ui.ShowError("입장 비용이 부족합니다."); return; }
            // 확인 시점에는 지도·잔액 상태가 바뀌었을 수 있으므로 다시 검사한 뒤 진입한다.
            string name = training ? "연습 목조주택" : region.name;
            string message = cost > 0 ? $"$ {cost:N0}를 내고 {name}에 입장하시겠습니까?" : $"{name}에 입장하시겠습니까?";
            ui.ShowEntryConfirm(message, () =>
            {
                if (!mapOpen || transitioning || player == null || !IsRegionUnlocked(index) ||
                    (!training && !region.CanAffordEntry(player.GetComponent<PlayerWallet>()))) return;
                BeginRaid(regionScene, cost, training, tutorial);
            });
        }
        public bool IsRegionUnlocked(int index)
        {
            if (index < 0 || index >= RegionCount) return false;
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsGuidedDayZero) return index == 0;
            var region = TableManager.Instance.RaidMap.Find(RegionScene(index));
            if (region == null) return false;
            if (region.selectionOrder <= 1) return true;
            string previousScene = TableManager.Instance.RaidMap.SceneAt(region.selectionOrder - 2);
            var save = FindFirstObjectByType<GameSaveController>();
            return save != null && save.HasClearedRaid(previousScene);
        }
        // 검사(EnterRegion)·확인창 뒤의 확정 단계(차감+이동).
        private void BeginRaid(string regionScene, int cost, bool training, TutorialManager tutorial)
        {
            var wallet = player.GetComponent<PlayerWallet>();
            player.GetComponent<WorkshopSession>()?.LeaveImmediately();
            // 차감은 이동 체크포인트 캡처보다 반드시 앞에 둔다. 캡처가 현재 잔액을 저장하므로 뒤에 빼면 비용이 무효가 된다.
            wallet?.Debit(cost);
            GameSaveData checkpoint = null; string error = null;
            if (workshopSave == null || !workshopSave.TryCaptureTravel(out checkpoint, out error))
            { wallet?.Credit(cost); ui.ShowError(workshopSave == null ? "공방 저장 관리자가 연결되지 않았습니다." : error); return; }
            checkpoint.settlement = true;
            // PrepareTrainingRaid는 0.9 도달 뒤 onReady에서 호출한다. 로드 실패 시 튜토리얼 상태를 남기지 않는다.
            // 여기서부터는 실제 진입 확정. 사망·귀환을 포함해 환불하지 않는다. 로드 실패만 예외로 환불한다.
            transitioning = true; ui.SetLoading();
            NightRaidTransfer.Begin(checkpoint, cost);
            SoundManager.CaptureRaidDeparture();
            NightRaidEntryOverlay.BeginEntry(regionScene,
                () => !training || tutorial.PrepareTrainingRaid(),
                () => AbortRaidEntry(cost));
        }

        // 커버가 새 씬을 활성화하기 전에 실패했을 때만 호출된다.
        private void AbortRaidEntry(int cost)
        {
            if (!transitioning || inRaid) return;
            NightRaidTransfer.Abandon();
            player?.GetComponent<PlayerWallet>()?.Credit(cost);
            transitioning = false;
            ui.ResetBackButton();
            ui.ShowMap(true);
            ui.ShowError("습격 진입에 실패해 입장 비용을 환불했습니다.");
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
        /// <summary>
        /// 복귀 상태를 보존하고 공방 씬으로 돌아간다.
        /// 확정된 습격 씬 키와 클리어 결과를 Main에 전달한다.
        /// </summary>
        public void ReturnHome()
        {
            if (!inRaid || transitioning || !returnOpen) return;
            FlushCellLogs();
            if (!resultOpen && !returningAfterDeath && playerHealth != null && playerHealth.IsDead)
            {
                ShowDeathResult();
                return;
            }
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
                else if (CanCommitRaidRewards()) CommitRewardsToWorkshop();
            }
            else
            {
                // 에디터에서 습격 씬을 직접 연 경우에도 확정 결과를 넘긴다.
                int raidNumber = NightRaidTransfer.ResultRaidNumber;
                string raidScene = NightRaidTransfer.ResultRaidScene;
                bool cleared = NightRaidTransfer.ResultCleared;
                NightRaidTransfer.Abandon();
                NightRaidTransfer.RecordResult(raidNumber, raidScene, cleared);
                NightRaidTransfer.MarkStandaloneReturn(
                    CanCommitRaidRewards() ? raidEarnedDollars : 0,
                    CanCommitRaidRewards() ? raidEarnedParts : 0,
                    CanCommitRaidRewards() ? raidEarnedTickets : 0,
                    CanCommitRaidRewards() ? raidEarnedModules.ToArray() : System.Array.Empty<int>());
                if (CanCommitRaidRewards()) rewardsCommitted = true;
            }
            transitioning = true; gameManager.SetNight(true); ui.ShowReturn(false);
            // 무기 복원 실패로 즉시 돌아온 경우에는 클리어/다음날 처리를 하지 않는다.
            NightRaidTransfer.ReturnDayFadePending = !returningAfterDeath && (!entryRestoreFailed || TutorialManager.TrainingRaidPending);
            NightRaidReturnOverlay.BeginReturn(homeScene);
        }

        private bool CanCommitRaidRewards() => !rewardsCommitted && !returningAfterDeath && !entryRestoreFailed &&
            resultOpen && !TutorialManager.TrainingRaidPending && NightRaidTransfer.ResultCleared;

        private bool CommitRewardsToWorkshop()
        {
            if (!CanCommitRaidRewards()) return false;
            NightRaidTransfer.AddRaidRewardsToWorkshop(raidEarnedDollars, raidEarnedParts, raidEarnedTickets,
                raidEarnedModules.ToArray());
            rewardsCommitted = true;
            return true;
        }
        /// <summary>
        /// 사망 복귀를 클리어 실패로 기록하고 공방으로 이동한다.
        /// 현재 씬 키를 결과 데이터에 담아 보상 곡이 지급되지 않게 한다.
        /// </summary>
        public void ReturnAfterDeath()
        {
            if (!inRaid || transitioning) return;
            if (resultOpen && !returningAfterDeath)
            {
                return;
            }
            CancelExtraction();
            returningAfterDeath = true;
            // 기존 사망 UI가 직접 호출하는 경로에서도 성공 결과가 남지 않게 한다.
            if (!SoundManager.IsTutorialMusicContext)
                NightRaidTransfer.RecordResult(SoundInfo.Raid(gameObject.scene.name)?.number ?? 0, gameObject.scene.name, false);
            returnOpen = true;
            ReturnHome();
        }
    }
}
