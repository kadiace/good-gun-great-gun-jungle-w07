using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class Bell : MonoBehaviour
    {
        [SerializeField, Tooltip("프로필에 프리팹이 없을 때 쓰는 기본 손님")] private Customer customerPrefab;
        [SerializeField, Tooltip("종을 치면 앞에서부터 차례로 옴. 날짜 시스템은 SetCustomers로 교체")]
        private List<CustomerProfile> customers = new List<CustomerProfile>();
        [SerializeField, Tooltip("손님이 나타나는 위치 (가판대 기준 왼쪽)")] private Transform spawnPoint;
        [SerializeField, Tooltip("총을 받고 떠나는 위치 (가판대 기준 오른쪽)")] private Transform exitPoint;
        [SerializeField, Tooltip("매대 앞 손님이 서는 위치")] private Transform counterPoint;
        [SerializeField, Tooltip("판매금 지급 및 손님 완료를 전달할 날짜 관리자")] private _DayManager dayManager;
        private Customer current;
        private readonly Queue<CustomerProfile> waiting = new Queue<CustomerProfile>();
        public event System.Action<int> SaleCompleted;
        public event System.Action<int> SaleAccepted;
        public event System.Action CustomerKilled;
        public event System.Action<CustomerProfile> CustomerInteracted;
        public event System.Action<CustomerProfile, bool, bool> OrderFinished;
        public event System.Action<CustomerProfile, EquipableWeapon, int, bool, int> WeaponSold;
        public Transform OutlineRoot => transform;
        public Customer Current => current;
        public int RemainingCustomers => waiting.Count;
        public int NextCustomerIndex => customers.Count - waiting.Count;

        // 이미 나온 손님 수는 유지하고 남은 대기열만 새 목록으로 교체 (저장 인덱스 유지).
        public void ReplaceRemaining(IList<CustomerProfile> all)
        {
            int next = NextCustomerIndex;
            customers.Clear();
            customers.AddRange(all);
            waiting.Clear();
            for (int i = next; i < customers.Count; i++) waiting.Enqueue(customers[i]);
        }

        public void CaptureSave(GameSaveData data)
        {
            data.nextCustomer = customers.Count - waiting.Count;
            data.currentCustomer = current != null ? data.nextCustomer - 1 : -1;
            data.customer = current != null ? current.CaptureSave() : null;
        }
        public void RestoreSave(GameSaveData data)
        {
            if (current != null) { var old = current; ReleaseCustomer(); Destroy(old.gameObject); }
            waiting.Clear();
            for (int i = data.nextCustomer; i < customers.Count; i++) waiting.Enqueue(customers[i]);
            if (data.currentCustomer < 0 || data.customer == null) { if (DayStarted) SpawnNext(); return; }
            var profile = customers[data.currentCustomer];
            var prefab = profile.prefab != null ? profile.prefab : customerPrefab;
            current = Instantiate(prefab, data.customer.position, Quaternion.identity);
            SubscribeCustomer();
            current.RestoreSave(data.customer, profile, counterPoint.position, exitPoint.position);
        }

        // 종을 쳐서 하루가 시작됐는지. 첫 손님을 꺼낸 순간부터 참이라 저장/복원도 그대로 따라감.
        private bool emptyTutorialStarted;
        public bool DayStarted => emptyTutorialStarted || waiting.Count < customers.Count;
        private void Awake() => BeginDay();
        private void Start() => GameManager.Instance?.UI?.Screen<CustomerRequirementsUI>()?.Bind(this);

        public void EndDay()
        {
            waiting.Clear();
            if (current == null) return;
            var departing = current;
            ReleaseCustomer();
            // Dismiss unfinished customers without recording a sale or kill.
            Destroy(departing.gameObject);
        }

        // Inspector의 손님 순서를 보존하여 매일 새 대기열을 만듭니다.
        public void BeginDay()
        {
            emptyTutorialStarted = false;
            waiting.Clear();
            foreach (CustomerProfile profile in customers) waiting.Enqueue(profile);
        }

        // 날짜 시작 시 그날 손님 목록 할당.
        public void SetCustomers(IEnumerable<CustomerProfile> profiles)
        {
            customers.Clear();
            if (profiles != null) customers.AddRange(profiles);
            BeginDay();
        }

        // 종은 하루 시작 트리거. 이후 손님이 떠나거나 죽으면 다음 손님이 자동으로 옴.
        public bool CanInteract(Interaction interactor) => false;

        public bool Interact(Interaction interactor)
        {
            return false;
        }

        public bool StartBusiness()
        {
            if (!DayStarted && !GameManager.GameplayBlocked && dayManager != null && dayManager.DayNumber == 0 && customers.Count == 0)
            {
                emptyTutorialStarted = true;
                WorkshopSoundEffects.RaiseBusinessStarted(transform.position);
                return true;
            }
            CustomerProfile first = waiting.Count > 0 ? waiting.Peek() : null;
            if (DayStarted || GameManager.GameplayBlocked || !SpawnNext()) return false;
            WorkshopSoundEffects.RaiseBusinessStarted(transform.position);
            if (first != null) TutorialManager.Run(first.callTutorialSequence, this);
            return true;
        }

        private bool CanSpawn() =>
            isActiveAndEnabled && (dayManager == null || dayManager.CanAcceptCustomers) && current == null && waiting.Count > 0 && spawnPoint != null && counterPoint != null && exitPoint != null;

        private bool SpawnNext()
        {
            if (!CanSpawn()) return false;
            CustomerProfile profile = waiting.Peek();
            Customer prefab = profile != null && profile.prefab != null ? profile.prefab : customerPrefab;
            if (prefab == null) return false;
            waiting.Dequeue();
            current = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
            SubscribeCustomer();
            current.Arrive(profile, counterPoint.position, exitPoint.position);
            return true;
        }
        private void OnSaleAccepted(int price, int constructionCost)
        {
            dayManager?.ReceivePayment(price, constructionCost);
            SaleAccepted?.Invoke(price);
        }

        private void OnSaleCompleted(int price)
        {
            bool dissatisfied = current != null && !current.SaleSatisfied;
            ReleaseCustomer();
            dayManager?.CompletedGuest(0, dissatisfied: dissatisfied);
            SaleCompleted?.Invoke(price);
            dayManager?.CloseShopAfterCustomer();
            dayManager?.RefreshRemainingCustomers();
            dayManager?.RefillCustomers();
            SpawnNext();
            if (TutorialManager.Instance != null) TutorialManager.Instance.NotifySignal("customer-sold");
        }

        private void OnCustomerKilled()
        {
            ReleaseCustomer();
            dayManager?.CompletedGuest(0, killed: true);
            CustomerKilled?.Invoke();
            dayManager?.CloseShopAfterCustomer();
            dayManager?.RefreshRemainingCustomers();
            dayManager?.RefillCustomers();
            SpawnNext();
        }

        private void ReleaseCustomer()
        {
            if (current == null) return;
            current.SaleCompleted -= OnSaleCompleted;
            current.SaleAccepted -= OnSaleAccepted;
            current.CustomerKilled -= OnCustomerKilled;
            current.Interacted -= OnCustomerInteracted;
            current.OrderFinished -= OnOrderFinished;
            current.WeaponSold -= OnWeaponSold;
            current = null;
        }

        private void SubscribeCustomer()
        {
            current.SaleCompleted += OnSaleCompleted;
            current.SaleAccepted += OnSaleAccepted;
            current.CustomerKilled += OnCustomerKilled;
            current.Interacted += OnCustomerInteracted;
            current.OrderFinished += OnOrderFinished;
            current.WeaponSold += OnWeaponSold;
        }
        private void OnCustomerInteracted(CustomerProfile order) => CustomerInteracted?.Invoke(order);
        private void OnOrderFinished(CustomerProfile order, bool success, bool dead) => OrderFinished?.Invoke(order, success, dead);
        private void OnWeaponSold(CustomerProfile order, EquipableWeapon weapon, int price, bool satisfied, int tagCount) =>
            WeaponSold?.Invoke(order, weapon, price, satisfied, tagCount);

        private void OnDestroy() => ReleaseCustomer();
    }
}
