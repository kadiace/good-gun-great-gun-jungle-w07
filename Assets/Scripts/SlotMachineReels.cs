using System.Collections;
using TMPro;
using UnityEngine;

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
        [SerializeField] private GameObject costPanel;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text statusText;

        private const int SpinPrice = 1000;
        private static readonly int[] Prizes = { 1500, 1500, 3000, 3000, 5000, 5000 };
        private static readonly string[] Symbols = { "기본", "연장", "연사", "관통", "방사", "폭발" };
        private Material originalMarquee;
        private Material glowingMarquee;
        private Quaternion restingLever;
        private bool spinning;
        private int completedSpins;

        public Transform OutlineRoot => transform;
        public int CompletedSpins => completedSpins;

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
            if (costPanel != null) costPanel.SetActive(false);
            if (payoutBills != null) payoutBills.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (marquee != null) marquee.sharedMaterial = originalMarquee;
            if (lever != null) lever.localRotation = restingLever;
        }

        private void OnDestroy()
        {
            if (glowingMarquee != null) Destroy(glowingMarquee);
        }

        private void LateUpdate()
        {
            if (costPanel == null) return;
            bool hovered = interaction != null && interaction.isActiveAndEnabled &&
                ReferenceEquals(interaction.AimedTarget, this) && !GameManager.GameplayBlocked;
            if (costPanel.activeSelf != hovered) costPanel.SetActive(hovered);
            if (!hovered) return;
            PlayerWallet wallet = PlayerWallet.ForPlayer(interaction.Player);
            bool affordable = wallet != null && wallet.CanAfford(SpinPrice);
            if (costText != null) costText.color = affordable ? Color.white : Color.red;
            if (statusText != null) statusText.text = spinning ? "회전 중" : affordable ? "F · 돌리기" : "잔액 부족";
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
            int[] result = ChooseResult(lastSpin, completedSpins < 3);
            if (!wallet.TryPurchase(SpinPrice, () => true)) return false;
            completedSpins = Mathf.Min(3, completedSpins + 1);
            spinning = true;
            StartCoroutine(Spin(result, wallet));
            return true;
        }

        private static int[] ChooseResult(bool lastSpin, bool introductorySpin)
        {
            if (lastSpin)
            {
                if (Random.value < 0.7f)
                {
                    int jackpot = Random.Range(4, 6);
                    return new[] { jackpot, jackpot, jackpot };
                }
                return ChooseMiss();
            }

            int roll = introductorySpin ? Random.Range(0, 20) : Random.Range(0, 34);
            int miss = introductorySpin ? 8 : 27;
            int low = introductorySpin ? 9 : 4;
            int middle = 2;
            if (roll < miss) return ChooseMiss();
            int symbol = roll < miss + low ? Random.Range(0, 2) :
                roll < miss + low + middle ? Random.Range(2, 4) : Random.Range(4, 6);
            return new[] { symbol, symbol, symbol };
        }

        private static int[] ChooseMiss()
        {
            int first = Random.Range(0, 6);
            int second = Random.Range(0, 6);
            int third = Random.Range(0, 6);
            if (first == second && second == third) third = (third + Random.Range(1, 6)) % 6;
            return new[] { first, second, third };
        }

        private IEnumerator Spin(int[] result, PlayerWallet wallet)
        {
            if (marquee != null && glowingMarquee != null) marquee.sharedMaterial = glowingMarquee;
            float[] start = new float[3];
            float[] finish = new float[3];
            for (int i = 0; i < 3; i++)
            {
                start[i] = Mathf.Repeat(reels[i].localEulerAngles.x, 360f);
                finish[i] = start[i] + 360f * (10 + i) + Mathf.Repeat(result[i] * 60f - start[i], 360f);
            }

            float elapsed = 0f;
            while (elapsed < 5.8f)
            {
                elapsed += Time.deltaTime;
                for (int i = 0; i < 3; i++)
                {
                    float t = Mathf.Clamp01(elapsed / (5f + i * 0.4f));
                    float eased = 1f - (1f - t) * (1f - t) * (1f - t);
                    reels[i].localRotation = Quaternion.Euler(Mathf.Lerp(start[i], finish[i], eased), 0f, 0f);
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
                if (payoutBills != null) payoutBills.Emit(48);
                FeedbackUI.For(this)?.ShowNotification($"{Symbols[result[0]]} 당첨! ${Prizes[result[0]]:N0}");
            }
            else FeedbackUI.For(this)?.ShowNotification("다음 기회에!");
            spinning = false;
        }
    }
}
