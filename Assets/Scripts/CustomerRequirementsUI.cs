using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    [RequireComponent(typeof(UIManager))]
    public sealed class CustomerRequirementsUI : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private RectTransform rowTemplate;
        [SerializeField] private QuestInfoTextSizing textSizing;
        private UIManager ui;
        private Bell bell;
        private readonly List<(RectTransform rect, TMP_Text status, TMP_Text description)> rows =
            new List<(RectTransform, TMP_Text, TMP_Text)>();
        private int used;
        private float nextUpdate;
        private Customer previousCustomer;
        private EquipableWeapon previousWeapon;
        private TutorialStep previousStep;
        public bool HasActiveOrder => bell != null && bell.Current != null && bell.Current.HasActiveOrder;

        private void Awake()
        {
            ui = GetComponent<UIManager>();
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            HideRows();
        }
        public void Bind(Bell source) => bell = source;
        /// <summary>
        /// 손님 주문 또는 튜토리얼 단계의 태그 요구사항에 맞춰 요청사항 목록 UI를 갱신합니다.
        /// 튜토리얼 태그 모드가 활성화되어 있으면 손님 주문보다 우선하여 해당 태그 조건을 표시합니다.
        /// </summary>
        private void LateUpdate()
        {
            if (content == null || rowTemplate == null) return;

            var tutorial = TutorialManager.Instance;
            bool tutorialActive = tutorial != null && tutorial.IsTutorialActive;
            var currentStep = tutorialActive ? tutorial.CurrentStep : null;
            var table = TableManager.Instance.CustomerRequest;
            var tutorialTag = (currentStep != null && currentStep.showTagRequirement &&
                !string.IsNullOrEmpty(currentStep.requiredTagId) && table != null)
                ? table.FindTag(currentStep.requiredTagId)
                : null;
            bool isTutorialTagMode = tutorialTag != null;

            var customer = bell != null ? bell.Current : null;
            bool hasCustomerOrder = customer != null && customer.HasActiveOrder;

            if (!isTutorialTagMode && !hasCustomerOrder)
            {
                HideRows();
                previousCustomer = null;
                previousWeapon = null;
                previousStep = null;
                nextUpdate = 0f;
                return;
            }

            var player = ui.Player;
            var session = player != null ? player.GetComponent<WorkshopSession>() : null;
            var holder = ui.PlayerWeaponHolder;
            var weapon = session != null && session.IsEditing && session.ActiveWorkbench != null
                ? session.ActiveWorkbench.CurrentWeapon : holder != null ? holder.EquippedWeapon : null;

            if (Time.unscaledTime < nextUpdate && previousCustomer == customer && previousWeapon == weapon && previousStep == currentStep)
                return;

            nextUpdate = Time.unscaledTime + .12f;
            previousCustomer = customer;
            previousWeapon = weapon;
            previousStep = currentStep;

            var snapshot = new CustomerRequestInfo.WeaponSnapshot(weapon);
            used = 0;
            Row("손님 요청사항", true, 0, true);

            if (isTutorialTagMode)
            {
                string tagId = currentStep.requiredTagId;
                Row(tutorialTag.name + " 태그", table.MeetsTag(tagId, snapshot), 0);
                foreach (var condition in tutorialTag.conditions ?? System.Array.Empty<CustomerRequestData.Condition>())
                {
                    if (condition == null) Row("요청 조건을 확인할 수 없습니다.", false, 1);
                    else Row(Describe(condition, snapshot.Value(condition)), snapshot.Meets(condition), 1);
                }
            }
            else
            {
                var ids = customer.Profile != null ? customer.Profile.RequestIds : null;
                if (ids == null) Row("요청 데이터를 확인할 수 없습니다.", false, 0);
                else if (ids.Length == 0) Row("추가 요청 없음", true, 0, true);
                else foreach (string id in ids)
                {
                    var request = table.FindRequest(id);
                    if (request == null) { Row("등록되지 않은 요청", false, 0); continue; }
                    var tag = table.FindTag(request.tagId);
                    bool grouped = tag != null || request.conditions == null || request.conditions.Length != 1 ||
                        (request.requiredTestIds != null && request.requiredTestIds.Length > 0);
                    if (grouped) Row(tag != null ? tag.name + " 태그" : request.name, table.MeetsRequest(id, snapshot), 0);
                    foreach (var condition in table.Conditions(request))
                        if (condition == null) Row("요청 조건을 확인할 수 없습니다.", false, grouped ? 1 : 0);
                        else Row(Describe(condition, snapshot.Value(condition)), snapshot.Meets(condition), grouped ? 1 : 0);
                    foreach (string testId in request.requiredTestIds ?? System.Array.Empty<string>())
                    {
                        var test = TableManager.Instance.ShootingTestTable.Find(testId);
                        bool passed = snapshot.HasPassedTest(testId);
                        Row((test?.name ?? "미등록 사격 테스트") + ": " + (passed ? "통과" : "미통과"), passed, grouped ? 1 : 0);
                    }
                }
            }

            for (int i = used; i < rows.Count; i++)
                if (rows[i].rect.gameObject.activeSelf)
                {
                    rows[i].rect.gameObject.SetActive(false);
                    textSizing?.MarkDirty();
                }
        }
        private static string Describe(CustomerRequestData.Condition condition, float current)
        {
            if (condition.Measure == GunMeasure.Shape) return CustomerRequestInfo.Describe(condition, current);
            // Keep the current value together below the requirement in this narrow HUD column.
            return CustomerRequestInfo.Describe(condition, includePathScope: false) + "\n<nobr>현재 " + current.ToString("0.##") + "</nobr>";
        }
        private void Row(string text, bool met, int depth, bool heading = false)
        {
            if (used == rows.Count)
            {
                var rect = Instantiate(rowTemplate, content);
                // The prefab defines status first and wrapped description second.
                var labels = rect.GetComponentsInChildren<TMP_Text>(true);
                rows.Add((rect, labels[0], labels[1]));
            }
            var row = rows[used++];
            var layout = row.rect.GetComponent<HorizontalLayoutGroup>();
            string status = heading ? string.Empty : met ? "[O]" : "[ ]";
            bool changed = !row.rect.gameObject.activeSelf || row.description.text != text ||
                row.status.text != status ||
                row.status.gameObject.activeSelf == heading ||
                row.description.fontStyle != (heading ? FontStyles.Bold : FontStyles.Normal) ||
                layout.padding.left != depth * 14;
            row.rect.gameObject.SetActive(true);
            layout.padding.left = depth * 14;
            Color color = heading ? Color.white : met ? new Color(.5f, .88f, .65f) : new Color(1f, .63f, .56f);
            row.status.text = status;
            row.status.gameObject.SetActive(!heading);
            row.status.color = row.description.color = color;
            row.description.text = text;
            row.description.fontStyle = heading ? FontStyles.Bold : FontStyles.Normal;
            if (changed) textSizing?.MarkDirty();
        }
        private void HideRows()
        {
            foreach (var row in rows)
                if (row.rect.gameObject.activeSelf)
                {
                    row.rect.gameObject.SetActive(false);
                    textSizing?.MarkDirty();
                }
        }
        private void OnDisable() { HideRows(); previousCustomer = null; previousWeapon = null; previousStep = null; nextUpdate = 0f; }
    }
}
