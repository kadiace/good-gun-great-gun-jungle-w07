using System.Collections;
using UnityEngine;
using LogKit;

namespace WeaponWorkshop
{
    public sealed class Customer : MonoBehaviour, IInteractable
    {
        private const string Okay = "알겠습니다!";
        private const string Huh = "예?";
        [SerializeField, Min(0.1f)] private float walkSpeed = 4f;
        [SerializeField] private CustomerProfile profile;
        [SerializeField, Tooltip("손님 2D 그림. 프로필에 그림이 있으면 교체")] private SpriteRenderer portrait;
        [SerializeField, Min(0f), Tooltip("걸을 때 통통 튀는 높이(m)")] private float bobHeight = 0.08f;
        [SerializeField, Min(0f), Tooltip("초당 튀는 횟수")] private float bobRate = 3f;
        [SerializeField, Min(0f), Tooltip("그림 크기(PPU)와 상관없이 이 높이(m)로 맞춤. 0이면 원본 크기")] private float portraitHeight = 2f;
        [SerializeField, Min(0f), Tooltip("가로로 넓은 그림은 이 너비(m)를 넘지 않게 줄임 (매대 구멍 2m). 0이면 제한 없음")] private float portraitMaxWidth = 1.8f;
        private Vector3 exitPoint;
        private Vector3 portraitRest;
        private int saveStage, dialogueIndex, soldPrice;

        public CustomerSave CaptureSave() => new CustomerSave { stage = saveStage, line = dialogueIndex,
            price = soldPrice, talked = HasTalked, satisfied = SaleSatisfied, position = transform.position };

        public void RestoreSave(CustomerSave state, CustomerProfile savedProfile, Vector3 counter, Vector3 exit)
        {
            Arrive(savedProfile, counter, exit);
            StopAllCoroutines();
            transform.position = state.position;
            HasTalked = state.talked; SaleSatisfied = state.satisfied; soldPrice = state.price;
            saveStage = state.stage; dialogueIndex = state.line; atCounter = state.stage == 1 || state.stage == 2;
            if (state.stage == 0) StartCoroutine(WalkTo(counter, () => { atCounter = true; saveStage = 1; }));
            else if (state.stage == 2) { talking = true; ShowLine(state.line); }
            else if (state.stage == 3) ShowSaleReaction(state.price);
            else if (state.stage == 4) LeaveAfterSale(state.price);
        }
        private bool atCounter;
        private bool talking;
        private bool outcomeReported;
        private bool deathStarted;
        [SerializeField] private GameObject deathSmokePrefab;
        private Coroutine hitReaction;
        private Color portraitColor;
        private void Awake()
        {
            if (portrait == null) return;
            portraitColor = portrait.color;
        }
        public event System.Action<int> SaleCompleted;
        public event System.Action<int, int> SaleAccepted;
        public event System.Action CustomerKilled;
        /// <summary>보호 피격 또는 실제 사망에서 손님 비명 재생을 요청한다.</summary>
        public static event System.Action<string, Vector3> ScreamRequested;
        /// <summary>실제 사망이 확정된 경우에만 손님 ID와 위치를 전달한다.</summary>
        public static event System.Action<string, Vector3> DeathConfirmed;
        public event System.Action<CustomerProfile> Interacted;
        public event System.Action<CustomerProfile, bool, bool> OrderFinished;
        public event System.Action<CustomerProfile, EquipableWeapon, int, bool, int> WeaponSold;
        public CustomerProfile Profile => profile;
        public bool HasTalked { get; private set; }
        public bool HasActiveOrder => HasTalked && atCounter && !deathStarted && saveStage < 3;
        public bool SaleSatisfied { get; private set; }
        public Transform OutlineRoot => transform;

        public void Arrive(CustomerProfile customerProfile, Vector3 counterPoint, Vector3 exit)
        {
            if (portrait != null) portraitRest = portrait.transform.localPosition;
            if (customerProfile != null) profile = customerProfile;
            if (portrait != null && profile != null && profile.portrait != null) portrait.sprite = profile.portrait;
            FitPortrait();
            saveStage = 0;
            exitPoint = exit;
            StartCoroutine(WalkTo(counterPoint, () => { atCounter = true; saveStage = 1; }));
        }

        public bool CanInteract(Interaction interactor) => atCounter;
        public bool Interact(Interaction interactor)
        {
            if (interactor == null || interactor.Player == null) return false;
            WeaponHolder holder = interactor.Player.GetComponent<WeaponHolder>();
            bool accepted = SaleConfirmationUI.Instance != null
                ? SaleConfirmationUI.Instance.UseCustomer(this, holder) : Use(holder);
            if (accepted) Interacted?.Invoke(profile);
            return accepted;
        }

        // 처음이면 대화(총 들고 있어도), 대화 후 총을 들고 오면 판매. 빈손이면 다시 대화.
        public bool Use(WeaponHolder holder)
        {
            if (!atCounter || talking || profile == null) return false;
            if (HasTalked && holder != null && holder.EquippedWeapon != null) return Sell(holder);
            Talk();
            return true;
        }

        // 요구 조건 체크. 판매 전에 외부(UI 등)에서도 호출 가능.
        public bool CheckWeapon(EquipableWeapon weapon) => profile != null && profile.Check(weapon);

        public bool Sell(WeaponHolder holder)
        {
            if (!atCounter || talking || profile == null || holder == null || holder.EquippedWeapon == null) return false;
            var day = FindFirstObjectByType<_DayManager>();
            if (day != null && day.NightWorkshop && (!CheckWeapon(holder.EquippedWeapon) || !profile.RaidMissionMet))
            { FeedbackUI.For(this)?.ShowNotification("태그와 야간 습격 주문 조건을 모두 달성해야 판매할 수 있습니다."); return false; }
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsGuidedDayZero && !CheckWeapon(holder.EquippedWeapon))
            { FeedbackUI.For(this)?.ShowNotification("손님이 요청한 조건에 맞춰 무기를 완성하세요."); return false; }
            EquipableWeapon weapon = holder.TakeWeapon();
            bool satisfied = CheckWeapon(weapon);
            SaleSatisfied = satisfied;
            // 만족은 실제 낸 제작비 × (1 + 태그비율 × 보너스 요청 수) + 손님 보상(Price), 불만족은 제작비 × 불만족 비율.
            // 비율은 주문 표(CustomerOrderData.sale)에서 읽는다.
            int cost = weapon != null ? weapon.PaidConstructionCost : 0;
            int tagCount = profile.SaleBonusCount();
            var sale = TableManager.Instance.CustomerOrder.sale;
            var save = FindFirstObjectByType<GameSaveController>();
            float bonusRatio = save != null ? save.SaleBonusRatio : sale.tagBonusRatio;
            int price = satisfied ? Mathf.RoundToInt(cost * (1f + bonusRatio * tagCount)) + Mathf.Max(0, profile.Price)
                : Mathf.RoundToInt(cost * sale.dissatisfiedRatio);
            Debug.Log($"[Customer] 총 판매: {price} (조건 {(satisfied ? "만족" : "불만족")})");
            WeaponSold?.Invoke(profile, weapon, price, satisfied, tagCount);
            GameLogger.Count("WeaponSold");
            GameLogger.Measure("SalePrice", price);
            Destroy(weapon.gameObject);
            atCounter = false;
            SaleAccepted?.Invoke(price, cost);
            WorkshopSoundEffects.RaiseWeaponSold(transform.position);
            // 조건 만족 여부에 따라 손님별 반응 대사 → 확인 누르면 퇴장.
            ShowSaleReaction(price);
            return true;
        }

        private void ShowSaleReaction(int price)
        {
            saveStage = 3; soldPrice = price; talking = true;
            bool satisfied = SaleSatisfied;
            DialogueUI.Instance.Show(profile.Reaction(satisfied), satisfied ? "감사합니다!" : "...죄송합니다", () =>
            {
                talking = false;
                DialogueUI.Instance.Hide();
                LeaveAfterSale(price);
            });
        }

        private void LeaveAfterSale(int price)
        {
            saveStage = 4; soldPrice = price;
            if (!outcomeReported)
            {
                outcomeReported = true;
                OrderFinished?.Invoke(profile, SaleSatisfied, false);
                SaleCompleted?.Invoke(price);
            }
            StartCoroutine(WalkTo(exitPoint, () =>
            {
                Destroy(gameObject);
            }));
        }

        /// <summary>보호 피격이면 비명 요청만 전달하고, 실제 사망이면 기존 집계와 사망 처리를 시작한다.</summary>
        public void Kill()
        {
            if (deathStarted) return;
            if (TutorialManager.Instance != null && TutorialManager.Instance.HandleCustomerHit(this))
            {
                ScreamRequested?.Invoke(profile != null ? profile.ProfileId : null, transform.position);
                if (hitReaction != null) StopCoroutine(hitReaction);
                hitReaction = StartCoroutine(ReactToHit(false));
                return;
            }
            // 여러 투사체가 같은 프레임에 맞아도 하루 집계는 한 번만 합니다.
            bool reportKill = !outcomeReported;
            deathStarted = true;
            outcomeReported = true;
            DeathConfirmed?.Invoke(profile != null ? profile.ProfileId : null, transform.position);
            ScreamRequested?.Invoke(profile != null ? profile.ProfileId : null, transform.position);
            if (reportKill) OrderFinished?.Invoke(profile, false, true);
            atCounter = false;
            StopAllCoroutines();
            if (talking && DialogueUI.Instance != null) DialogueUI.Instance.Hide();
            talking = false;
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            StartCoroutine(ReactToHit(true, reportKill));
        }

        private IEnumerator ReactToHit(bool dying, bool reportKill = false)
        {
            const float duration = .45f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float shake = Mathf.Sin(t * Mathf.PI * 6f) * (1f - t);
                if (portrait != null)
                {
                    float redness = dying ? 1f : 1f - t;
                    portrait.color = Color.Lerp(portraitColor, new Color(1f, .25f, .25f, portraitColor.a), redness);
                    portrait.transform.localPosition = portraitRest + Vector3.right * shake * .045f +
                        (dying ? Vector3.down * .3f * t + Vector3.back * .15f * t : Vector3.zero);
                }
                yield return null;
            }
            hitReaction = null;
            if (!dying)
            {
                if (portrait != null)
                {
                    portrait.color = portraitColor;
                    portrait.transform.localPosition = portraitRest;
                }
                yield break;
            }
            float smokeDuration = 0f;
            GameObject smoke = null;
            if (deathSmokePrefab != null)
            {
                Vector3 position = portrait != null ? portrait.bounds.center : transform.position + Vector3.up;
                smoke = Instantiate(deathSmokePrefab, position, Quaternion.identity);
                smoke.SetActive(true);
                foreach (var particles in smoke.GetComponentsInChildren<ParticleSystem>(true))
                {
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = particles.main;
                    main.loop = false; main.duration = .35f;
                    main.useUnscaledTime = true;
                    main.startColor = Color.white;
                    var lifetimeColor = particles.colorOverLifetime;
                    lifetimeColor.enabled = true;
                    var whiteSmoke = new Gradient();
                    whiteSmoke.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                        new[] { new GradientAlphaKey(.7f, 0f), new GradientAlphaKey(0f, 1f) });
                    lifetimeColor.color = whiteSmoke;
                    smokeDuration = Mathf.Max(smokeDuration, (main.duration + main.startLifetime.constantMax) / Mathf.Max(.01f, main.simulationSpeed));
                    particles.Play();
                }
            }
            if (portrait != null) portrait.enabled = false;
            if (reportKill) CustomerKilled?.Invoke();
            if (smokeDuration > 0f) yield return new WaitForSecondsRealtime(smokeDuration);
            if (smoke != null) Destroy(smoke);
            Destroy(gameObject);
        }

        private void FitPortrait()
        {
            if (portrait == null || portrait.sprite == null || portraitHeight <= 0f) return;
            Vector2 size = portrait.sprite.bounds.size;
            if (size.y <= 0f) return;
            float scale = portraitHeight / size.y;
            if (portraitMaxWidth > 0f && size.x * scale > portraitMaxWidth) scale = portraitMaxWidth / size.x;
            portrait.transform.localScale = Vector3.one * scale;
        }

        private void Bob(float height)
        {
            if (portrait != null) portrait.transform.localPosition = portraitRest + Vector3.up * height;
        }

        private void Talk()
        {
            saveStage = 2;
            talking = true;
            ShowLine(0);
        }

        private void ShowLine(int index)
        {
            dialogueIndex = index;
            var mission = profile.OrderState;
            bool nightTrial = mission != null && mission.raidEscapeSeconds > 0f &&
                FindFirstObjectByType<_DayManager>()?.NightWorkshop == true;
            bool last = index >= profile.Lines.Length - (nightTrial ? 0 : 1);
            string answer = last && profile.EndsClear ? Okay : Huh;
            string line = nightTrial && index >= profile.Lines.Length
                ? $"총기의 임상 실험도 부탁드립니다. 야간 습격에서 {mission.raidEscapeSeconds / 60f:0.#}분 안에 탈출하고" +
                  (mission.raidModuleCrate ? " 모듈 상자를 회수한 뒤" : "") +
                  $" 헬기 {mission.raidHelicopterKills}대와 탱크 {mission.raidTankKills}대를 처치해 주세요. 모두 완료하면 사겠습니다."
                : profile.Lines[index];
            DialogueUI.Instance.Show(line, answer, () =>
            {
                if (!last) { ShowLine(index + 1); return; }
                // 튜토리얼은 처음 주문을 들었을 때만.
                TutorialManager.Run(HasTalked ? null : profile.orderTutorialSequence, this, () =>
                {
                    talking = false;
                    HasTalked = true;
                    saveStage = 1;
                    DialogueUI.Instance.Hide();
                    if (TutorialManager.Instance != null) TutorialManager.Instance.NotifySignal("customer-greeted", this);
                });
            });
        }

        private void OnDestroy()
        {
            TutorialManager.Instance?.Cancel(this);
            if (talking && DialogueUI.Instance != null) DialogueUI.Instance.Hide();
        }

        private IEnumerator WalkTo(Vector3 target, System.Action arrived)
        {
            while ((transform.position - target).sqrMagnitude > 0.0001f)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, walkSpeed * Time.deltaTime);
                Bob(Mathf.Abs(Mathf.Sin(Time.time * bobRate * Mathf.PI)) * bobHeight);
                yield return null;
            }
            Bob(0f);
            arrived();
        }
    }
}
