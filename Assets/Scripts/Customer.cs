using System.Collections;
using System.Collections.Generic;
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
        private readonly HashSet<string> revealedRequests = new HashSet<string>();
        private int dialoguePhase;
        private string dialogueRequestId;
        private WeaponHolder dialogueHolder;
        private bool UsesRequestDialogue => profile != null && profile.OrderState != null &&
            profile.OrderState.generatedDay > 0 && !profile.IsDifficult && profile.RequestIds.Length > 0;

        public CustomerSave CaptureSave() => new CustomerSave { stage = saveStage, line = dialogueIndex,
            price = soldPrice, talked = HasTalked, satisfied = SaleSatisfied, position = transform.position,
            revealedRequests = new List<string>(revealedRequests).ToArray(),
            dialoguePhase = dialoguePhase, dialogueRequestId = dialogueRequestId };

        public void RestoreSave(CustomerSave state, CustomerProfile savedProfile, Vector3 counter, Vector3 exit)
        {
            Arrive(savedProfile, counter, exit);
            StopAllCoroutines();
            transform.position = state.position;
            HasTalked = state.talked; SaleSatisfied = state.satisfied; soldPrice = state.price;
            revealedRequests.Clear();
            foreach (string id in state.revealedRequests ?? System.Array.Empty<string>()) revealedRequests.Add(id);
            if (UsesRequestDialogue)
            {
                PrepareRequestDialogue();
                profile.OrderState.mandatoryRevealed = true;
                foreach (var optional in profile.OrderState.optionalDialogues)
                    optional.revealed |= revealedRequests.Contains(optional.requirementId);
            }
            dialoguePhase = state.dialoguePhase > 0 ? state.dialoguePhase : (HasTalked ? 2 : 0);
            dialogueRequestId = state.dialogueRequestId;
            if (HasTalked && state.stage == 1) NotifyIfCustomOrder();
            saveStage = state.stage; dialogueIndex = state.line; atCounter = state.stage == 1 || state.stage == 2;
            if (state.stage == 0) StartCoroutine(WalkTo(counter, () => { atCounter = true; saveStage = 1; }));
            else if (state.stage == 2)
            {
                talking = true;
                if (UsesRequestDialogue)
                {
                    dialogueHolder = FindFirstObjectByType<FirstPersonPlayer>()?.GetComponent<WeaponHolder>();
                    ShowRequestDialogue();
                }
                else ShowLine(state.line);
            }
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
        public bool HasActiveOrder => (HasTalked || UsesRequestDialogue) && atCounter && !deathStarted && saveStage < 3;
        public bool SaleSatisfied { get; private set; }
        public Transform OutlineRoot => transform;

        public void Arrive(CustomerProfile customerProfile, Vector3 counterPoint, Vector3 exit)
        {
            if (portrait != null) portraitRest = portrait.transform.localPosition;
            if (customerProfile != null) profile = customerProfile;
            if (portrait != null && profile != null && profile.portrait != null) portrait.sprite = profile.portrait;
            FitPortrait();
            saveStage = 0;
            revealedRequests.Clear();
            HasTalked = false;
            talking = false;
            atCounter = false;
            dialoguePhase = 0;
            dialogueRequestId = null;
            dialogueHolder = null;
            if (UsesRequestDialogue)
            {
                PrepareRequestDialogue();
                profile.OrderState.mandatoryRevealed = true;
            }
            exitPoint = exit;
            StartCoroutine(WalkTo(counterPoint, () => { atCounter = true; saveStage = 1; }));
        }

        private void NotifyIfCustomOrder()
        {
            if (profile == null) return;
            var onboarding = TableManager.Instance.Onboarding;
            var requests = TableManager.Instance.CustomerRequest;
            foreach (string requestId in profile.RequestIds)
            {
                string canonical = requests.FindRequest(requestId)?.id;
                if (System.Array.IndexOf(onboarding.customRequestIds, canonical) < 0) continue;
                OnboardingManager.NotifyCustomOrder();
                break;
            }
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

        public bool Use(WeaponHolder holder)
        {
            if (!atCounter || talking || profile == null) return false;
            if (UsesRequestDialogue)
            {
                dialogueHolder = holder;
                dialoguePhase = HasTalked ? 2 : 0;
                saveStage = 2;
                talking = true;
                ShowRequestDialogue();
            }
            else Talk(HasTalked ? holder : null);
            return true;
        }

        // 요구 조건 체크. 판매 전에 외부(UI 등)에서도 호출 가능.
        public bool CheckWeapon(EquipableWeapon weapon) => profile != null && profile.Check(weapon);

        public bool IsRequestVisible(string id)
        {
            if (profile == null) return false;
            if (!UsesRequestDialogue) return profile.OrderState?.generatedDay == 0 ||
                !string.IsNullOrEmpty(TableManager.Instance.CustomerRequest.FindRequest(id)?.tagId) || revealedRequests.Contains(id);
            string canonical = CanonicalRequestId(id);
            var order = profile.OrderState;
            if (order.mandatoryDialogue.requirementId == canonical) return true;
            foreach (var optional in order.optionalDialogues)
                if (optional.requirementId == canonical) return optional.revealed;
            return false;
        }

        public void RevealCompletedRequests(EquipableWeapon weapon)
        {
            if (!HasActiveOrder || weapon == null || profile == null) return;
            var table = TableManager.Instance.CustomerRequest;
            var snapshot = new CustomerRequestInfo.WeaponSnapshot(weapon);
            foreach (string id in profile.RequestIds)
                if (!IsRequestVisible(id) && table.MeetsRequest(id, snapshot)) RevealRequest(id);
        }

        private static string CanonicalRequestId(string id) => TableManager.Instance.CustomerRequest.FindRequest(id)?.id ?? id;

        private void RevealRequest(string id)
        {
            string canonical = CanonicalRequestId(id);
            revealedRequests.Add(canonical);
            if (!UsesRequestDialogue) return;
            foreach (var optional in profile.OrderState.optionalDialogues)
                if (optional.requirementId == canonical) optional.revealed = true;
        }

        private void PrepareRequestDialogue()
        {
            var order = profile.OrderState;
            if (order.mandatoryDialogue != null && order.optionalDialogues != null) return;
            var data = TableManager.Instance.CustomerOrder;
            var requests = TableManager.Instance.CustomerRequest;
            string mandatoryId = CanonicalRequestId(profile.RequestIds[0]);
            CustomerOrderData.MandatoryDialogue mandatory = null;
            foreach (var row in data.mandatoryDialogues)
                if (row != null && CanonicalRequestId(row.requirementId) == mandatoryId) { mandatory = row; break; }
            order.mandatoryDialogue = new CustomerOrderData.MandatoryDialogue
            {
                requirementId = mandatoryId,
                intro = mandatory?.intro ?? requests.Phrase(mandatoryId) + " 총을 부탁드려요.",
                introResponse = mandatory?.introResponse ?? Huh,
                explicitRequest = mandatory?.explicitRequest ?? requests.Phrase(mandatoryId) + " 부탁드려요."
            };
            var optionalDialogues = new List<CustomerOrderState.OptionalDialogueState>();
            var seen = new HashSet<string> { mandatoryId };
            for (int index = 1; index < profile.RequestIds.Length; index++)
            {
                string id = CanonicalRequestId(profile.RequestIds[index]);
                if (!seen.Add(id)) continue;
                CustomerOrderData.OptionalDialogueVariant variant = null;
                foreach (var row in data.optionalDialogues)
                {
                    if (row == null || CanonicalRequestId(row.requirementId) != id || row.variants == null || row.variants.Length == 0) continue;
                    variant = row.variants[Random.Range(0, row.variants.Length)];
                    break;
                }
                var request = requests.FindRequest(id);
                string hint = variant?.hint ?? (request != null ? VagueRequest(request) : requests.Phrase(id));
                optionalDialogues.Add(new CustomerOrderState.OptionalDialogueState
                {
                    requirementId = id, hint = hint, question = variant?.question ?? "예? " + hint + "?",
                    explicitRequest = variant?.explicitRequest ?? requests.Phrase(id) + " 부탁드려요."
                });
            }
            order.optionalDialogues = optionalDialogues.ToArray();
        }

        private void ShowRequestDialogue()
        {
            dialogueIndex = 0;
            var order = profile.OrderState;
            var mandatory = order.mandatoryDialogue;
            if (dialoguePhase == 0)
            {
                if (string.IsNullOrEmpty(mandatory.intro))
                {
                    dialoguePhase = 2;
                    HasTalked = true;
                    NotifyIfCustomOrder();
                    ShowRequestDialogue();
                    return;
                }
                DialogueUI.Instance.Show(mandatory.intro, mandatory.introResponse, () =>
                {
                    dialoguePhase = 1;
                    order.mandatoryRevealed = true;
                    ShowRequestDialogue();
                });
                return;
            }
            if (dialoguePhase == 1)
            {
                DialogueUI.Instance.Show(mandatory.explicitRequest, Okay, () =>
                {
                    HasTalked = true;
                    NotifyIfCustomOrder();
                    dialoguePhase = 2;
                    ShowRequestDialogue();
                });
                return;
            }
            if (dialoguePhase == 3)
            {
                foreach (var optional in order.optionalDialogues)
                {
                    if (optional.requirementId != dialogueRequestId) continue;
                    DialogueUI.Instance.Show(optional.explicitRequest, Okay, () =>
                    {
                        dialoguePhase = 2;
                        dialogueRequestId = null;
                        ShowRequestDialogue();
                    });
                    return;
                }
                dialoguePhase = 2;
            }
            var lines = new List<string> { $"<b>{mandatory.explicitRequest}</b>" };
            var labels = new List<string>();
            var actions = new List<System.Action>();
            foreach (var optional in order.optionalDialogues)
            {
                lines.Add(optional.revealed ? optional.explicitRequest : $"<b>{optional.hint}</b>");
                if (optional.revealed) continue;
                labels.Add(optional.question);
                actions.Add(() =>
                {
                    RevealRequest(optional.requirementId);
                    dialogueRequestId = optional.requirementId;
                    dialoguePhase = 3;
                    ShowRequestDialogue();
                });
            }
            if (dialogueHolder != null && dialogueHolder.EquippedWeapon != null)
            {
                labels.Add("총기 제출하기");
                actions.Add(() =>
                {
                    CloseRequestDialogue();
                    if (dialogueHolder != null && dialogueHolder.EquippedWeapon != null) Sell(dialogueHolder);
                });
            }
            labels.Add("닫기");
            actions.Add(CloseRequestDialogue);
            DialogueUI.Instance.ShowChoices(string.Join("\n", lines), labels.ToArray(), actions.ToArray());
        }

        private void CloseRequestDialogue()
        {
            talking = false;
            saveStage = 1;
            dialoguePhase = 2;
            dialogueRequestId = null;
            DialogueUI.Instance.Hide();
        }

        /// <summary>
        /// 제출한 무기를 판매하고 대금과 손님 완료를 기존 이벤트로 전달합니다. Day0 첫 제출 거절은 무기를 꺼내기 전에 처리합니다.
        /// </summary>
        public bool Sell(WeaponHolder holder)
        {
            if (!atCounter || talking || profile == null || holder == null || holder.EquippedWeapon == null) return false;
            if (TutorialManager.Instance != null && TutorialManager.Instance.TryRejectFirstSubmission(this)) return true;
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsGuidedDayZero && !CheckWeapon(holder.EquippedWeapon))
            { FeedbackUI.For(this)?.ShowNotification("손님이 요청한 조건에 맞춰 무기를 완성하세요."); return false; }
            EquipableWeapon weapon = holder.TakeWeapon();
            bool satisfied = CheckWeapon(weapon);
            SaleSatisfied = satisfied;
            int cost = weapon != null ? weapon.PaidConstructionCost : 0;
            var save = FindFirstObjectByType<GameSaveController>();
            var reward = CustomerRewardInfo.Calculate(profile, weapon, CustomerRewardInfo.SaleMultiplier(save));
            int price = reward.price;
            Debug.Log($"[Customer] 총 판매: {price} (조건 {(satisfied ? "만족" : "불만족")})");
            WeaponSold?.Invoke(profile, weapon, price, satisfied, reward.matchedRequests);
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

        private void Talk(WeaponHolder submissionHolder)
        {
            saveStage = 2;
            talking = true;
            ShowLine(0, submissionHolder);
        }

        private void ShowLine(int index, WeaponHolder submissionHolder = null)
        {
            dialogueIndex = index;
            bool last = index >= profile.Lines.Length - 1;
            string answer = last && profile.EndsClear ? Okay : Huh;
            string line = profile.Lines[index];
            if (profile.OrderState != null && profile.OrderState.generatedDay > 0 && !profile.IsDifficult)
            {
                var table = TableManager.Instance.CustomerRequest;
                var mandatory = new List<string>();
                var optional = new List<string>();
                foreach (string id in profile.RequestIds)
                {
                    var request = table.FindRequest(id);
                    if (request == null) continue;
                    if (!string.IsNullOrEmpty(request.tagId)) mandatory.Add("<b>" + table.Phrase(id) + "</b>");
                    else optional.Add("<b>" + VagueRequest(request) + "</b>");
                }
                line = string.Join(", ", mandatory) + " 총을 부탁드려요." +
                    (optional.Count > 0 ? " 그리고 " + string.Join(", ", optional) + "." : "");
            }
            DialogueUI.Instance.Show(line, answer, () =>
            {
                if (!last) { ShowLine(index + 1, submissionHolder); return; }
                // 튜토리얼은 처음 주문을 들었을 때만.
                TutorialManager.Run(HasTalked ? null : profile.orderTutorialSequence, this, () =>
                {
                    HasTalked = true;
                    saveStage = 1;
                    NotifyIfCustomOrder();
                    if (submissionHolder != null)
                    {
                        bool hasWeapon = submissionHolder.EquippedWeapon != null;
                        string[] labels = hasWeapon ? new[] { "총기 제출하기", "닫기" } : new[] { "닫기" };
                        System.Action close = () => { talking = false; DialogueUI.Instance.Hide(); };
                        System.Action[] actions = hasWeapon
                            ? new System.Action[] { () => { close(); if (submissionHolder.EquippedWeapon != null) Sell(submissionHolder); }, close }
                            : new System.Action[] { close };
                        foreach (string id in profile.RequestIds)
                        {
                            var request = TableManager.Instance.CustomerRequest.FindRequest(id);
                            if (request == null || !string.IsNullOrEmpty(request.tagId) || revealedRequests.Contains(id)) continue;
                            var oldLabels = labels;
                            var oldActions = actions;
                            labels = new string[oldLabels.Length + 1]; actions = new System.Action[oldActions.Length + 1];
                            labels[0] = "예? " + VagueRequest(request) + "?";
                            actions[0] = () =>
                            {
                                revealedRequests.Add(id);
                                DialogueUI.Instance.Show("<b>" + TableManager.Instance.CustomerRequest.Phrase(id) + "</b> 부탁드려요.",
                                    "알겠습니다", () => ShowLine(index, submissionHolder));
                            };
                            System.Array.Copy(oldLabels, 0, labels, 1, oldLabels.Length);
                            System.Array.Copy(oldActions, 0, actions, 1, oldActions.Length);
                            break;
                        }
                        DialogueUI.Instance.ShowChoices(line, labels, actions);
                    }
                    else { talking = false; DialogueUI.Instance.Hide(); }
                    // 신호가 다음 튜토리얼 대화를 즉시 열 수 있으므로 손님 대화 처리를 먼저 끝낸다.
                    if (TutorialManager.Instance != null) TutorialManager.Instance.NotifySignal("customer-greeted", this);
                });
            });
        }

        private static string VagueRequest(CustomerRequestData.Request request)
        {
            if (request.id.StartsWith("construction/")) return "예산에 맞는 걸로 해주세요";
            if (request.id.StartsWith("custom/") || request.id.StartsWith("color/") || request.id.StartsWith("shape/"))
                return "겉모습에도 신경 써 주세요";
            return "발사 느낌도 좀 특별했으면 해요";
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
