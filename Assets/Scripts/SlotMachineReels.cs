using System;
using System.Collections;
using LogKit;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WeaponWorkshop
{
    [DefaultExecutionOrder(70)]
    public class SlotMachineReels : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform[] reels;
        [SerializeField] private Transform lever;
        [SerializeField] private Renderer marquee;
        [SerializeField] private ParticleSystem payoutBills;
        [SerializeField] private Interaction interaction;
        [SerializeField] private _DayManager dayManager;
        [SerializeField] private GameObject costPanel;

        public const int SpinPrice = 500;
        private static readonly int[] Prizes = { 1000, 1000, 2000, 2000, 50000, 5000 };
        private static readonly string[] Symbols = { "기본", "연장", "연사", "관통", "방사", "폭발" };
        private Material originalMarquee;
        private Material glowingMarquee;
        private Quaternion restingLever;
        private bool spinning;
        private int completedSpins;

        public Transform OutlineRoot => transform;
        public int CompletedSpins => completedSpins;
        public bool IsSpinning => spinning;

        public void RestoreSpins(int count) => completedSpins = Mathf.Clamp(count, 0, 3);

        private void Awake()
        {
            if (costPanel != null) costPanel.SetActive(false);
            if (payoutBills != null) payoutBills.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (lever != null) restingLever = lever.localRotation;
            if (marquee == null) return;
            originalMarquee = marquee.sharedMaterial;
            if (originalMarquee == null) return;
            glowingMarquee = new Material(originalMarquee);
            glowingMarquee.name = "Slot Machine Marquee Glow";
            glowingMarquee.SetColor("_BaseColor", Color.yellow);
            glowingMarquee.SetColor("_EmissionColor", Color.yellow * 3f);
            glowingMarquee.EnableKeyword("_EMISSION");
        }

        private void OnDisable()
        {
            spinning = false;
            SlotMachineSound.StopRotation();
            if (costPanel != null) costPanel.SetActive(false);
            if (payoutBills != null) payoutBills.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (marquee != null) marquee.sharedMaterial = originalMarquee;
            if (lever != null) lever.localRotation = restingLever;
        }

        private void OnDestroy()
        {
            if (glowingMarquee != null) Destroy(glowingMarquee);
        }

        public bool CanInteract(Interaction interactor)
        {
            PlayerWallet wallet = interactor != null ? PlayerWallet.ForPlayer(interactor.Player) : null;
            return isActiveAndEnabled && !spinning && !GameManager.GameplayBlocked &&
                wallet != null && wallet.CanAfford(SpinPrice);
        }

        public bool Interact(Interaction interactor)
        {
            if (!CanInteract(interactor) || reels == null || reels.Length != 3) return false;
            PlayerWallet wallet = PlayerWallet.ForPlayer(interactor.Player);
            bool lastSpin = wallet.Balance < SpinPrice * 2;
            bool introductorySpin = completedSpins < 3;
            int balanceBefore = wallet.Balance;
            int[] result = ChooseResult(lastSpin, introductorySpin);
            if (!wallet.TryPurchase(SpinPrice, () => true)) return false;
            completedSpins = Mathf.Min(3, completedSpins + 1);
            spinning = true;
            StartCoroutine(Spin(result, wallet));
            LogSpin(result, balanceBefore, wallet.Balance, lastSpin ? "last_funds" : introductorySpin ? "first_three" : "ordinary");
            return true;
        }

        private void LogSpin(int[] result, int balanceBefore, int balanceAfter, string oddsPool)
        {
            int symbol = result[0] == result[1] && result[1] == result[2] ? result[0] : -1;
            var entry = new ShopSlotMachineLogEvent
            {
                eventType = "slot_machine_spin_started",
                eventId = Guid.NewGuid().ToString("N"),
                day = dayManager != null ? dayManager.DayNumber : 0,
                playSeconds = Time.time,
                secondsSinceDayNightTransition = dayManager != null ? dayManager.SecondsSinceDayNightTransition : -1f,
                period = dayManager != null ? dayManager.CurrentPeriod.ToString() : "unknown",
                isNight = dayManager != null && dayManager.IsNight,
                oddsPool = oddsPool,
                introductorySpinsUsed = completedSpins,
                balanceBefore = balanceBefore,
                balanceAfter = balanceAfter,
                price = SpinPrice,
                plannedPrize = symbol >= 0 ? Prizes[symbol] : 0,
                plannedSymbol = symbol >= 0 ? Symbols[symbol] : "none",
                reelSymbols = result
            };
            try { GameLogger.Data("Info", "Shop", entry.eventType, entry); }
            catch (Exception exception) { Debug.LogWarning("슬롯머신 로그 기록 실패: " + exception.Message, this); }
        }

        private static int[] ChooseResult(bool lastSpin, bool introductorySpin)
        {
            // Ordinary weights / 1000: 709 misses, 200 low, 70 middle, 20 explosion, 1 radiation.
            // Expected payout = (200*1000 + 70*2000 + 20*5000 + 1*50000) / 1000 = $490.
            int multiplier = introductorySpin || lastSpin ? 5 : 1;
            int low = 200 * multiplier;
            int middle = 70 * multiplier;
            int explosion = 20 * (lastSpin ? 5 : 1);
            int roll = Random.Range(0, 709 + low + middle + explosion + 1);
            if (roll < 709) return ChooseMiss();
            roll -= 709;
            int symbol = roll < low ? Random.Range(0, 2) :
                roll < low + middle ? Random.Range(2, 4) :
                roll < low + middle + explosion ? 5 : 4;
            return new[] { symbol, symbol, symbol };
        }

        private static int[] ChooseMiss()
        {
            int first = Random.Range(0, 6);
            bool nearMiss = Random.value < 0.8f;
            int second = nearMiss ? first : (first + Random.Range(1, 6)) % 6;
            int third = nearMiss ? (first + Random.Range(1, 6)) % 6 : Random.Range(0, 6);
            return new[] { first, second, third };
        }

        /// <summary>
        /// 결과 심볼에 맞춰 릴을 회전시키고 정해진 시간표대로 순차 정지한다.
        /// result와 wallet을 사용하며 정지음·당첨 지급을 처리한 뒤 회전을 종료 상태로 둔다.
        /// </summary>
        private IEnumerator Spin(int[] result, PlayerWallet wallet)
        {
            if (marquee != null && glowingMarquee != null) marquee.sharedMaterial = glowingMarquee;
            if (lever != null) SlotMachineSound.PlayLever(transform.position);
            const float DEFAULT_SPIN_DURATION = 7.2f;
            float spinDuration = SlotMachineSound.StartRotation(transform, DEFAULT_SPIN_DURATION);
            float[] start = new float[3];
            float[] finish = new float[3];
            float[] stopTimes = { spinDuration * 4.4f / 7.2f, spinDuration * 5.2f / 7.2f, spinDuration };
            float fastDuration = spinDuration * 2.8f / 7.2f;
            const float fastSpeed = 1080f;
            for (int i = 0; i < 3; i++)
            {
                start[i] = Mathf.Repeat(reels[i].localEulerAngles.x, 360f);
                finish[i] = start[i] + 360f * (12 + i * 3) + Mathf.Repeat(result[i] * 60f - start[i], 360f);
            }

            float elapsed = 0f;
            bool[] reelStopSoundsPlayed = new bool[3];
            while (elapsed < stopTimes[2])
            {
                elapsed += Time.deltaTime;
                for (int i = 0; i < 3; i++)
                {
                    float angle;
                    if (elapsed < fastDuration) angle = start[i] + fastSpeed * elapsed;
                    else
                    {
                        float duration = stopTimes[i] - fastDuration;
                        float t = Mathf.Clamp01((elapsed - fastDuration) / duration);
                        float from = start[i] + fastSpeed * fastDuration;
                        // Match the fast-phase velocity, then ease continuously to a full stop.
                        angle = (2f * t * t * t - 3f * t * t + 1f) * from +
                            (t * t * t - 2f * t * t + t) * duration * fastSpeed +
                            (-2f * t * t * t + 3f * t * t) * finish[i];
                    }
                    reels[i].localRotation = Quaternion.Euler(angle, 0f, 0f);
                    if (elapsed >= stopTimes[i] && !reelStopSoundsPlayed[i])
                    {
                        reelStopSoundsPlayed[i] = true;
                        SlotMachineSound.PlayReelStop(transform.position);
                        if (i == reels.Length - 1) SlotMachineSound.StopRotation();
                    }
                }
                if (lever != null)
                {
                    float pull = elapsed < 0.35f ? elapsed / 0.35f : Mathf.Clamp01((0.95f - elapsed) / 0.4f);
                    lever.localRotation = restingLever * Quaternion.Euler(-150f * pull, 0f, 0f);
                }
                yield return null;
            }

            for (int i = 0; i < 3; i++) reels[i].localRotation = Quaternion.Euler(result[i] * 60f, 0f, 0f);
            if (lever != null) lever.localRotation = restingLever;
            if (marquee != null) marquee.sharedMaterial = originalMarquee;
            if (result[0] == result[1] && result[1] == result[2])
            {
                wallet.Credit(Prizes[result[0]]);
                SlotMachineSound.PlayPayout(transform.position);
                if (payoutBills != null) payoutBills.Emit(48);
                FeedbackUI.For(this)?.ShowNotification($"{Symbols[result[0]]} 당첨! ${Prizes[result[0]]:N0}");
            }
            else FeedbackUI.For(this)?.ShowNotification("다음 기회에!");
            spinning = false;
        }
    }
}
