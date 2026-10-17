using System.Linq;
using UnityEngine;
namespace WeaponWorkshop
{
    // Only data survives scene changes. Every scene owns its player and services.
    public static class NightRaidTransfer
    {
        public static GameSaveData Workshop { get; private set; }
        public static bool Returning { get; private set; }
        public static bool ReturnDayFadePending;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetReturnFade() => ReturnDayFadePending = false;
        private static bool enteringRaid;
        private static bool standaloneReturn;
        // 결과 확정 시에만 채운다. 훈련 습격과 결과 없는 복귀는 번호 0·클리어 false다.
        public static int ResultRaidNumber { get; private set; }
        public static string ResultRaidScene { get; private set; }
        public static bool ResultCleared { get; private set; }
        /// <summary>
        /// 확정 습격 결과에 번호와 안정적인 씬 키를 기록한다.
        /// raidNumber, scene, cleared를 받아 Main 복귀 처리에 넘길 결과를 갱신한다.
        /// </summary>
        public static void RecordResult(int raidNumber, string scene, bool cleared)
        {
            ResultRaidNumber = raidNumber;
            ResultRaidScene = scene;
            ResultCleared = cleared;
        }
        public static bool HasPendingReturn => Returning || standaloneReturn;
        private static int moduleCreditsBeforeLoss;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { Workshop = null; Returning = enteringRaid = standaloneReturn = false; moduleCreditsBeforeLoss = 0; RecordResult(0, null, false); }
        // 씬을 직접 연 경우에는 공방 스냅샷 없이 결과만 전달한다.
        public static void MarkStandaloneReturn() => standaloneReturn = true;
        public static bool TryConsumeStandaloneReturn()
        {
            bool pending = standaloneReturn;
            if (pending) Reset();
            return pending;
        }
        /// <summary>
        /// 공방 체크포인트를 보관하고 습격 진입 상태를 시작한다.
        /// checkpoint를 저장하고 이전 습격 결과를 초기화한다.
        /// </summary>
        public static void Begin(GameSaveData checkpoint)
        {
            Workshop = checkpoint; Returning = false; enteringRaid = true; moduleCreditsBeforeLoss = 0;
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
                Workshop.unlockedModules = player.GetComponent<PlayerModuleUnlocks>()?.CaptureUnlockedModules() ?? System.Array.Empty<int>();
            }
            Workshop.inWorkshop = false; Workshop.confirmation = false; Workshop.settlement = !died;
            Returning = true; return true;
        }
        public static bool TryConsumeReturn(out GameSaveData checkpoint, out int creditsBeforeLoss)
        {
            checkpoint = null; creditsBeforeLoss = 0;
            if (!Returning || Workshop == null) return false;
            creditsBeforeLoss = moduleCreditsBeforeLoss;
            checkpoint = Workshop; Reset(); return true;
        }
        public static void Abandon() => Reset();
        public static void MarkReturning() { if (Workshop != null) Returning = true; }
        public static void KeepRecovery(GameSaveData checkpoint)
        {
            Workshop = checkpoint;
            Debug.LogError("공방 복원에 실패했습니다. 복귀 스냅샷은 메모리에 보관했습니다.");
        }
    }
}
