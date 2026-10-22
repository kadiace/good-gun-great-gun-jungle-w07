using System.Linq;
using UnityEngine;
namespace WeaponWorkshop
{
    [System.Serializable]
    public sealed class RaidMissionResult
    {
        public string raidId;
        public string orderId;
        public string raidScene;
        public bool escaped;
        public bool training;
        public float elapsedSeconds;
        public int helicopterKills;
        public int tankKills;
        public int[] collectedModuleIds = System.Array.Empty<int>();

        public bool MeetsRequirements(float maxEscapeSeconds = 0f, bool requireModulePickup = false,
            int requiredHelicopterKills = 0, int requiredTankKills = 0, int requiredModuleId = 0)
        {
            if (!escaped || training) return false;
            if (maxEscapeSeconds > 0f && elapsedSeconds > maxEscapeSeconds) return false;
            if (helicopterKills < requiredHelicopterKills || tankKills < requiredTankKills) return false;
            if (requireModulePickup && (collectedModuleIds == null || collectedModuleIds.Length == 0)) return false;
            return requiredModuleId <= 0 || (collectedModuleIds != null && System.Array.IndexOf(collectedModuleIds, requiredModuleId) >= 0);
        }
    }

    // Only data survives scene changes. Every scene owns its player and services.
    public static class NightRaidTransfer
    {
        public static string MissionOrderId { get; private set; }
        public static RaidMissionResult MissionResult { get; private set; }
        public static void SetMissionOrder(string orderId) => MissionOrderId = orderId;
        internal static void RecordMissionResult(RaidMissionResult result) => MissionResult = result;
        public static bool TryGetMissionResult(string orderId, out RaidMissionResult result)
        {
            result = MissionResult;
            if (string.IsNullOrEmpty(orderId) || result == null || result.orderId != orderId)
            { result = null; return false; }
            return true;
        }
        public static GameSaveData Workshop { get; private set; }
        public static bool Returning { get; private set; }
        public static bool ReturnDayFadePending;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetReturnFade() => ReturnDayFadePending = false;
        private static bool enteringRaid;
        private static bool standaloneReturn;
        private static int standaloneSlotTickets;
        // 결과 확정 시에만 채운다. 훈련 습격과 결과 없는 복귀는 번호 0·클리어 false다.
        public static int ResultRaidNumber { get; private set; }
        public static string ResultRaidScene { get; private set; }
        public static bool ResultCleared { get; private set; }
        /// <summary>
        /// 확정 습격 결과에 번호와 안정적인 씬 키를 기록한다.
        /// </summary>
        public static void RecordResult(int raidNumber, string raidScene, bool cleared)
        {
            ResultRaidNumber = raidNumber;
            ResultRaidScene = raidScene;
            ResultCleared = cleared;
        }
        public static bool HasPendingReturn => Returning || standaloneReturn;
        private static int moduleCreditsBeforeLoss;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { ResetTransfer(); MissionOrderId = null; MissionResult = null; }
        private static void ResetTransfer() { Workshop = null; Returning = enteringRaid = standaloneReturn = false; moduleCreditsBeforeLoss = standaloneSlotTickets = 0; RecordResult(0, null, false); }
        // 씬을 직접 연 경우에는 공방 스냅샷 없이 결과만 전달한다.
        public static void MarkStandaloneReturn(int slotTickets)
        {
            standaloneReturn = true;
            standaloneSlotTickets = Mathf.Max(0, slotTickets);
        }
        public static bool TryConsumeStandaloneReturn(out int slotTickets)
        {
            bool pending = standaloneReturn;
            slotTickets = pending ? standaloneSlotTickets : 0;
            if (pending) ResetTransfer();
            return pending;
        }
        /// <summary>
        /// 공방 체크포인트를 보관하고 습격 진입 상태를 시작한다.
        /// checkpoint를 저장하고 이전 습격 결과를 초기화한다.
        /// </summary>
        public static void Begin(GameSaveData checkpoint)
        {
            Workshop = checkpoint; Returning = false; enteringRaid = true; moduleCreditsBeforeLoss = 0;
            MissionResult = null;
            RecordResult(0, null, false);
        }
        public static bool TryConsumeEntry(out int balance, out int moduleCredits, out int[] unlockedModules, out WeaponWorldSave weapon)
        {
            balance = 0; moduleCredits = 0; unlockedModules = System.Array.Empty<int>(); weapon = null;
            if (!enteringRaid || Workshop == null) return false;
            enteringRaid = false; balance = Workshop.balance; moduleCredits = Workshop.moduleCredits; unlockedModules = Workshop.unlockedModules;
            weapon = Workshop.weapons.FirstOrDefault(w => w.held);
            return true;
        }
        public static bool PrepareReturn(FirstPersonPlayer player, bool died, out string error)
        {
            error = null;
            if (Workshop == null) { error = "공방 복귀 상태가 없습니다."; return false; }
            if (TutorialManager.TrainingRaidPending)
            {
                // Training loot, damage, deaths and expenses never alter the departure snapshot.
                TutorialManager.PrepareTrainingReturn(died);
                Returning = true; return true;
            }
            var held = player.GetComponent<WeaponHolder>().EquippedWeapon;
            WeaponWorldSave carried = null;
            if (!died && held != null && !GameSaveController.TryCaptureCarried(held, out carried, out error)) return false;
            if (!died)
            {
                var weapons = Workshop.weapons.Where(w => !w.held).ToList();
                if (carried != null) weapons.Add(carried);
                Workshop.weapons = weapons.ToArray(); Workshop.hasWeapon = carried != null;
            }
            if (!died)
            {
                Workshop.balance = player.GetComponent<PlayerWallet>().Balance;
                Workshop.outstandingLoans = player.GetComponent<PlayerWallet>().OutstandingLoans;
                Workshop.lastLoanDay = player.GetComponent<PlayerWallet>().LastLoanDay;
            }
            moduleCreditsBeforeLoss = died ? player.GetComponent<PlayerWallet>().ModuleCredits : 0;
            // Death forfeits raid module credits and unlocks; keep the departure snapshot.
            if (!died)
            {
                Workshop.moduleCredits = player.GetComponent<PlayerWallet>().ModuleCredits;
                Workshop.slotTickets = player.GetComponent<PlayerWallet>().SlotTickets;
                Workshop.unlockedModules = player.GetComponent<PlayerModuleUnlocks>()?.CaptureUnlockedModules() ?? System.Array.Empty<int>();
            }
            Workshop.inWorkshop = false; Workshop.confirmation = false;
            Workshop.settlement = Workshop.night && !died;
            Returning = true; return true;
        }
        public static bool TryConsumeReturn(out GameSaveData checkpoint, out int creditsBeforeLoss)
        {
            checkpoint = null; creditsBeforeLoss = 0;
            if (!Returning || Workshop == null) return false;
            creditsBeforeLoss = moduleCreditsBeforeLoss;
            checkpoint = Workshop; ResetTransfer(); return true;
        }
        public static void Abandon() => Reset();
        // 사망 귀환도 출발 스냅샷을 쓰므로 스냅샷에 직접 기록한다.
        public static void MarkEndChestOpened()
        {
            if (Workshop != null) Workshop.endChestOpened = true;
            GameSaveFiles.TryMarkEndChestOpened(PlayerIdentity.SaveId);
        }
        public static void MarkReturning() { if (Workshop != null) Returning = true; }
        public static void KeepRecovery(GameSaveData checkpoint)
        {
            Workshop = checkpoint;
            Debug.LogError("공방 복원에 실패했습니다. 복귀 스냅샷은 메모리에 보관했습니다.");
        }
    }
}
