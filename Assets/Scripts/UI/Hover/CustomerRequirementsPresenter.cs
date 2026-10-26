using System.Collections.Generic;

using UnityEngine;

namespace WeaponWorkshop
{
    // 주문 요구사항의 도메인 조회, 표시 데이터 가공, 갱신 요청을 담당한다.
    // 표시는 CustomerRequirementsView의 명시적 함수만 호출한다.
    public sealed class CustomerRequirementsPresenter
    {
        private const string SCREEN_NAME = "CustomerRequirementsPresenter";
        private const float UPDATE_INTERVAL = 0.12f;

        private readonly CustomerRequirementsView m_view;
        private readonly UIManager m_uiManager;
        private readonly List<CustomerRequirementsView.RowData> m_rows =
            new List<CustomerRequirementsView.RowData>();

        private Bell m_bell;
        private GameSaveController m_save;
        private float m_nextUpdate;
        private Customer m_previousCustomer;
        private EquipableWeapon m_previousWeapon;
        private TutorialStep m_previousStep;
        private bool m_isInitialized;
        private bool m_hasLoggedDependencyError;

        public CustomerRequirementsPresenter(CustomerRequirementsView _view, UIManager _uiManager)
        {
            m_view = _view;
            m_uiManager = _uiManager;
        }

        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (m_view == null)
            {
                if (!m_hasLoggedDependencyError)
                {
                    m_hasLoggedDependencyError = true;
                    Debug.LogError(
                        $"[{SCREEN_NAME}] Initialize 실패: CustomerRequirementsView 참조가 없습니다. " +
                        "UIManager.prefab의 HoverOrder 저작을 실행하세요.");
                }

                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            m_isInitialized = true;
            return true;
        }

        public void Dispose()
        {
            if (m_view != null)
            {
                m_view.Dispose();
            }

            ResetCache();
            m_isInitialized = false;
        }

        public void Bind(Bell _bell)
        {
            m_bell = _bell;
        }

        public void HandleDisabled()
        {
            if (m_view != null)
            {
                m_view.HideRows();
            }

            ResetCache();
        }

        public void Tick()
        {
            if (!m_isInitialized || m_view == null || !m_view.HasLayout)
            {
                return;
            }

            TutorialManager tutorial = TutorialManager.Instance;
            TutorialStep currentStep = tutorial != null && tutorial.IsTutorialActive ? tutorial.CurrentStep : null;
            CustomerRequestData requestTable = TableManager.Instance.CustomerRequest;
            CustomerRequestData.Tag tutorialTag = currentStep != null && currentStep.showTagRequirement &&
                !string.IsNullOrEmpty(currentStep.requiredTagId) && requestTable != null
                ? requestTable.FindTag(currentStep.requiredTagId)
                : null;
            bool isTutorialTagMode = tutorialTag != null;

            Customer customer = m_bell != null ? m_bell.Current : null;
            if (!isTutorialTagMode && (customer == null || !customer.HasActiveOrder))
            {
                m_view.HideRows();
                ResetCache();
                return;
            }

            FirstPersonPlayer player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            WorkshopSession session = player != null ? player.GetComponent<WorkshopSession>() : null;
            WeaponHolder holder = player != null ? player.GetComponent<WeaponHolder>() : null;
            EquipableWeapon weapon = session != null && session.IsEditing && session.ActiveWorkbench != null
                ? session.ActiveWorkbench.CurrentWeapon
                : holder != null ? holder.EquippedWeapon : null;
            if (!isTutorialTagMode) customer.RevealCompletedRequests(weapon);
            if (Time.unscaledTime < m_nextUpdate && m_previousCustomer == customer && m_previousWeapon == weapon &&
                m_previousStep == currentStep)
            {
                return;
            }

            m_nextUpdate = Time.unscaledTime + UPDATE_INTERVAL;
            m_previousCustomer = customer;
            m_previousWeapon = weapon;
            m_previousStep = currentStep;

            CustomerRequestData table = requestTable;
            var snapshot = new CustomerRequestInfo.WeaponSnapshot(weapon);
            if (isTutorialTagMode)
            {
                // 튜토리얼 태그 모드는 손님 주문보다 우선하며 보상 행 없이 태그 조건만 보여 준다.
                m_rows.Clear();
                AddRow("손님 요청사항", true, 0, true);
                AddRow(tutorialTag.name + " 태그", table.MeetsTag(currentStep.requiredTagId, snapshot), 0);
                foreach (CustomerRequestData.Condition condition in tutorialTag.conditions ?? System.Array.Empty<CustomerRequestData.Condition>())
                {
                    if (condition == null)
                    {
                        AddRow("요청 조건을 확인할 수 없습니다.", false, 1);
                    }
                    else
                    {
                        AddRow(Describe(condition, snapshot.Value(condition)), snapshot.Meets(condition), 1);
                    }
                }

                m_view.ShowRows(m_rows);
                return;
            }

            if (m_save == null) m_save = Object.FindFirstObjectByType<GameSaveController>();
            var quote = CustomerRewardInfo.Calculate(customer.Profile, weapon, CustomerRewardInfo.SaleMultiplier(m_save), snapshot);
            int weaponValue = CustomerRewardInfo.CurrentWeaponValue(weapon);
            m_rows.Clear();
            AddRow($"예상 보수 금액: ${quote.price:N0}", true, 0, true);
            AddRow($"현재 총기 가치: ${weaponValue:N0}", true, 0, true);
            string bonus = quote.matchedRequests > 0 && quote.multiplier > 1f
                ? $" (야간 습격 보너스 x {quote.multiplier:0.##} 적용)" : string.Empty;
            long profit = (long)quote.price - weaponValue;
            AddRow($"예상 수익: {(profit < 0 ? "-" : "")}${System.Math.Abs(profit):N0}{bonus}", true, 0, true);
            if (customer.Profile != null && customer.Profile.IsDifficult)
            {
                float percent = TableManager.Instance.CustomerOrder.sale.failedSaleRatio * 100f;
                AddRow($"진상 손님: 총기 가치의 {percent:0.#}% 지급", true, 0, true);
            }
            string[] ids = customer.Profile != null ? customer.Profile.RequestIds : null;
            if (ids == null)
            {
                AddRow("요청 데이터를 확인할 수 없습니다.", false, 0);
            }
            else if (ids.Length == 0)
            {
                AddRow("추가 요청 없음", true, 0, true);
            }
            else
            {
                foreach (string id in ids)
                {
                    if (!customer.IsRequestVisible(id)) continue;
                    AddRequestRows(table, id, snapshot, CustomerRewardInfo.ContractReward(customer.Profile, id));
                }
            }

            m_view.ShowRows(m_rows);
        }

        private void AddRequestRows(CustomerRequestData _table, string _id,
            CustomerRequestInfo.WeaponSnapshot _snapshot, int _reward)
        {
            CustomerRequestData.Request request = _table.FindRequest(_id);
            if (request == null)
            {
                AddRow("등록되지 않은 요청", false, 0);
                return;
            }

            AddRow($"{_table.Phrase(_id)} (달성 시 보수: ${_reward:N0})", _table.MeetsRequest(_id, _snapshot), 0);

            foreach (CustomerRequestData.Condition condition in _table.Conditions(request))
            {
                if (condition == null)
                {
                    AddRow("- 요청 조건을 확인할 수 없습니다.", false, 1);
                }
                else
                {
                    AddRow("- " + Describe(condition, _snapshot.Value(condition)), _snapshot.Meets(condition), 1);
                }
            }

            foreach (string testId in request.requiredTestIds ?? System.Array.Empty<string>())
            {
                ShootingTestTableData.Entry test = TableManager.Instance.ShootingTestTable.Find(testId);
                bool passed = _snapshot.HasPassedTest(testId);
                AddRow("- " + (test?.name ?? "미등록 사격 테스트") + ": " + (passed ? "통과" : "미통과"), passed, 1);
            }
        }

        private static string Describe(CustomerRequestData.Condition _condition, float _current)
        {
            if (_condition.Measure == GunMeasure.Shape)
            {
                return CustomerRequestInfo.Describe(_condition, _current);
            }

            // Keep the current value together below the requirement in this narrow HUD column.
            return CustomerRequestInfo.Describe(_condition, includePathScope: false) +
                "\n<nobr>현재 " + _current.ToString("0.##") + "</nobr>";
        }

        private void AddRow(string _text, bool _met, int _depth, bool _heading = false)
        {
            m_rows.Add(new CustomerRequirementsView.RowData(_text, _met, _depth, _heading));
        }

        private void ResetCache()
        {
            m_previousCustomer = null;
            m_previousWeapon = null;
            m_previousStep = null;
            m_nextUpdate = 0f;
        }
    }
}
