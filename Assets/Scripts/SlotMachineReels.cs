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
        [SerializeField] private ParticleSystem damageSmoke;
        [SerializeField] private GameObject intactVisual;
        [SerializeField] private GameObject brokenVisual;
        private ParticleSystem smokeInstance;

        public const int SpinPrice = 1000;
        private static readonly int[] Prizes = { 500, 1000, 2000, 4000, 50000, 5000 };
        private static readonly string[] Symbols = { "기본", "연장", "연사", "관통", "방사", "폭발" };
        private Material originalMarquee;
        private Material glowingMarquee;
        private Quaternion restingLever;
        private bool spinning;
        private bool skipRequested;
        private int completedSpins;
        private int destructionCount;
        private int destroyedDay = -1;
        private bool destroyedAtNight;
        private bool destroyed;
        private float penetrationResistance = 1f;
        private float explosionResistance = 1f;

        public Transform OutlineRoot => transform;
        public int CompletedSpins => completedSpins;
        public bool IsSpinning => spinning;
        public int DestructionCount => destructionCount;
        public int DestroyedDay => destroyedDay;
        public bool DestroyedAtNight => destroyedAtNight;

        public void RestoreSpins(int count) => completedSpins = Mathf.Clamp(count, 0, 3);

        public void RestoreDamage(int count, int day, bool atNight)
        {
            destructionCount = Mathf.Max(0, count);
            destroyedDay = day;
            destroyedAtNight = atNight;
            destroyed = day >= 0;
            penetrationResistance = explosionResistance = 1f + 2f * Mathf.Max(0, destructionCount - (destroyed ? 1 : 0));
            RefreshDamageState();
        }

        private void Awake()
        {
            if (dayManager == null) dayManager = FindFirstObjectByType<_DayManager>();
            destroyed = false;
            if (costPanel != null) costPanel.SetActive(false);
            if (payoutBills != null) payoutBills.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (intactVisual != null) intactVisual.SetActive(true);
            if (brokenVisual != null) brokenVisual.SetActive(false);
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

        private void OnEnable()
        {
            if (dayManager == null) dayManager = FindFirstObjectByType<_DayManager>();
            if (dayManager != null) dayManager.Changed += RefreshDamageState;
            RefreshDamageState();
        }

        private void OnDisable()
        {
            if (dayManager != null) dayManager.Changed -= RefreshDamageState;
            StopAllCoroutines();
            spinning = false;
            skipRequested = false;
            SlotMachineSound.StopRotation();
            if (costPanel != null) costPanel.SetActive(false);
            if (payoutBills != null) payoutBills.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (marquee != null) marquee.sharedMaterial = originalMarquee;
            if (lever != null) lever.localRotation = restingLever;
            if (smokeInstance != null) smokeInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void RefreshDamageState()
        {
            if (destroyed && dayManager != null && dayManager.DayNumber > destroyedDay &&
                dayManager.IsNight == destroyedAtNight)
            {
                destroyed = false;
                destroyedDay = -1;
                penetrationResistance = explosionResistance = 1f + 2f * destructionCount;
            }
            if (intactVisual != null) intactVisual.SetActive(!destroyed);
            if (brokenVisual != null) brokenVisual.SetActive(destroyed);
            if (destroyed && smokeInstance == null && damageSmoke != null)
            {
                smokeInstance = Instantiate(damageSmoke, transform);
                smokeInstance.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            }
            if (smokeInstance == null) return;
            if (destroyed && !smokeInstance.isPlaying) smokeInstance.Play(true);
            else if (!destroyed && smokeInstance.isPlaying)
                smokeInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        /// <summary>
        /// explosionHit에 따라 penetration 또는 explosion을 해당 저항과 비교해 슬롯머신을 파괴한다.
        /// 이미 파괴됐거나 위력이 부족하면 무시하고, 새 파괴 시 외형·파괴음·기록을 갱신한다.
        /// </summary>
        public void ReceiveAttack(float penetration, float explosion, bool explosionHit)
        {
            if (destroyed || (explosionHit ? explosion < explosionResistance : penetration < penetrationResistance)) return;
            float requiredPenetration = penetrationResistance;
            float requiredExplosion = explosionResistance;
            destroyed = true;
            destroyedDay = dayManager != null ? dayManager.DayNumber : 0;
            destroyedAtNight = dayManager != null && dayManager.IsNight;
            destructionCount++;
            if (spinning)
            {
                StopAllCoroutines();
                spinning = false;
                skipRequested = false;
                SlotMachineSound.StopRotation();
                if (marquee != null) marquee.sharedMaterial = originalMarquee;
                if (lever != null) lever.localRotation = restingLever;
            }
            RefreshDamageState();
            SlotMachineSound.PlayDestroyed(transform.position);
            var entry = new ShopSlotMachineDestroyedLogEvent
            {
                eventType = "slot_machine_destroyed", eventId = Guid.NewGuid().ToString("N"),
                day = destroyedDay, period = dayManager != null ? dayManager.CurrentPeriod.ToString() : "unknown",
                isNight = destroyedAtNight, penetration = penetration, explosion = explosion,
                penetrationResistance = requiredPenetration, explosionResistance = requiredExplosion,
                destructionCount = destructionCount
            };
            try { GameLogger.Data("Info", "Shop", entry.eventType, entry); }
            catch (Exception exception) { Debug.LogWarning("슬롯머신 파괴 로그 기록 실패: " + exception.Message, this); }
        }

        private void OnDestroy()
        {
            if (glowingMarquee != null) Destroy(glowingMarquee);
        }

        public bool CanInteract(Interaction interactor)
        {
            PlayerWallet wallet = interactor != null ? PlayerWallet.ForPlayer(interactor.Player) : null;
            return isActiveAndEnabled && !destroyed && !GameManager.GameplayBlocked &&
                wallet != null && (spinning || wallet.SlotTickets > 0 || wallet.CanAfford(SpinPrice));
        }

        public bool Interact(Interaction interactor)
        {
            if (!CanInteract(interactor) || reels == null || reels.Length != 3) return false;
            if (spinning)
            {
                skipRequested = true;
                return true;
            }
            PlayerWallet wallet = PlayerWallet.ForPlayer(interactor.Player);
            int balanceBefore = wallet.Balance;
            bool paidWithTicket = wallet.SlotTickets > 0;
            if (paidWithTicket)
            {
                if (!wallet.TrySpendSlotTicket()) return false;
            }
            else if (!wallet.TryPurchase(SpinPrice, () => true)) return false;
            int[] result = ChooseResult();
            completedSpins = Mathf.Min(3, completedSpins + 1);
            spinning = true;
            skipRequested = false;
            LogSpin(result, balanceBefore, wallet.Balance, paidWithTicket);
            StartCoroutine(Spin(result, wallet));
            return true;
        }

        public string GetHoverDescription(PlayerWallet wallet)
        {
            string resistance = $"관통 내성 {penetrationResistance:0.##} / 폭발 내성 {explosionResistance:0.##}";
            if (destroyed)
            {
                string when = dayManager != null && dayManager.DayNumber > destroyedDay &&
                    destroyedAtNight && !dayManager.IsNight ? "오늘 밤" :
                    $"다음 날 {(destroyedAtNight ? "밤" : "낮")}";
                return resistance + $"\n파괴되어 수리 중입니다. {when}에 사용할 수 있습니다.";
            }
            if (spinning) return resistance + "\n회전 중... (F로 건너뛰기)";
            string ticketAmount = wallet == null || wallet.SlotTickets == 0
                ? "<color=#FF0000>1개</color>" : "1개";
            string amount = wallet == null || (wallet.SlotTickets == 0 && !wallet.CanAfford(SpinPrice))
                ? "<color=#FF0000>$1000</color>" : "$1000";
            return resistance + $"\n슬롯머신 티켓 {ticketAmount} 또는 {amount}을 소모해 당길 수 있습니다.\n" +
                "슬롯머신 티켓이 있으면 먼저 사용합니다.\n당첨금:\n" +
                "기본 $500\n연장 $1,000\n연사 $2,000\n관통 $4,000\n폭발 $5,000\n방사 $50,000";
        }

        private void LogSpin(int[] result, int balanceBefore, int balanceAfter, bool paidWithTicket)
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
                oddsPool = "fixed",
                paidWithTicket = paidWithTicket,
                paidWithMoney = !paidWithTicket,
                introductorySpinsUsed = completedSpins,
                balanceBefore = balanceBefore,
                balanceAfter = balanceAfter,
                price = paidWithTicket ? 0 : SpinPrice,
                plannedPrize = symbol >= 0 ? Prizes[symbol] : 0,
                plannedSymbol = symbol >= 0 ? Symbols[symbol] : "none",
                reelSymbols = result
            };
            try { GameLogger.Data("Info", "Shop", entry.eventType, entry); }
            catch (Exception exception) { Debug.LogWarning("슬롯머신 로그 기록 실패: " + exception.Message, this); }
        }

        private static int[] ChooseResult()
        {
            int roll = Random.Range(0, 1000);
            if (roll < 614) return ChooseMiss();
            int symbol = roll < 814 ? 0 :
                roll < 914 ? 1 :
                roll < 964 ? 2 :
                roll < 989 ? 3 :
                roll < 999 ? 5 : 4;
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
        /// result와 wallet을 사용하며 정지음·당첨 지급 또는 실패음을 처리한 뒤 회전을 종료 상태로 둔다.
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
            while (elapsed < stopTimes[2] && !skipRequested)
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

            for (int i = 0; i < 3; i++)
            {
                reels[i].localRotation = Quaternion.Euler(result[i] * 60f, 0f, 0f);
                if (!reelStopSoundsPlayed[i]) SlotMachineSound.PlayReelStop(transform.position);
            }
            SlotMachineSound.StopRotation();
            if (lever != null) lever.localRotation = restingLever;
            if (marquee != null) marquee.sharedMaterial = originalMarquee;
            if (!spinning) yield break;
            spinning = false;
            skipRequested = false;
            if (result[0] == result[1] && result[1] == result[2])
            {
                wallet.Credit(Prizes[result[0]]);
                SlotMachineSound.PlayPayout(transform.position);
                if (payoutBills != null) payoutBills.Emit(48);
                FeedbackUI.For(this)?.ShowNotification($"{Symbols[result[0]]} 당첨! ${Prizes[result[0]]:N0}");
            }
            else
            {
                SlotMachineSound.PlayFail(transform.position);
                FeedbackUI.For(this)?.ShowNotification("다음 기회에!");
            }
        }
    }
}
