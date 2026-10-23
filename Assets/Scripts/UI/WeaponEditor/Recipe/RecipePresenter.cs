using System;

using System.Text;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 레시피 화면의 표 기반 표시 데이터와 공방 편집·무기 정보 표시·튜토리얼 승인에 따른 표시 정책을 담당하는
    /// Presenter입니다. TableManager.CustomerRequest 표가 단일 출처며 조건 문구는 CustomerRequestInfo로 해석합니다.
    /// 프레임 진입은 브리지의 LateUpdate가 Tick으로 전달합니다.
    /// </summary>
    public sealed class RecipePresenter : IDisposable
    {
        private const string TUTORIAL_ACTION = "recipe";
        private const string TUTORIAL_INSPECTED_SIGNAL = "recipe-inspected";
        private const string OPEN_BUTTON_LABEL = "레시피 보기";
        private const string CLOSE_BUTTON_LABEL = "레시피 접기";
        private const string EMPTY_CONDITIONS_TEXT = "조건이 설정되지 않았습니다.";

        private readonly RecipeView m_view;
        private readonly Func<WorkshopSession> m_sessionProvider;
        private readonly StringBuilder m_conditionText = new StringBuilder();

        private bool m_isInitialized;
        private bool m_isLayoutDirty;

        public bool IsOpen => m_view != null && m_view.IsPanelOpen;

        public RecipePresenter(RecipeView _view, Func<WorkshopSession> _sessionProvider)
        {
            m_view = _view;
            m_sessionProvider = _sessionProvider;
        }

        /// <summary>
        /// View 초기화와 버튼 이벤트 구독을 연결하고 패널·라벨을 닫힌 상태로 명시적으로 맞춥니다.
        /// 의존성이 누락되면 오류를 기록하고 해당 화면 초기화만 실패로 처리합니다.
        /// </summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (m_view == null || m_sessionProvider == null)
            {
                Debug.LogError(
                    $"[RecipePresenter] Initialize 실패: 필수 의존성 누락 (_view={m_view != null}, _sessionProvider={m_sessionProvider != null})");
                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            m_view.ToggleRequested -= OnToggleRequested;
            m_view.ToggleRequested += OnToggleRequested;

            // 씬에 저장된 활성 상태와 무관하게 닫힌 라벨로 시작한다.
            Close();
            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// View 이벤트 구독과 버튼 리스너를 해제합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            if (m_view != null)
            {
                m_view.ToggleRequested -= OnToggleRequested;
                m_view.Dispose();
            }

            m_isInitialized = false;
        }

        /// <summary>
        /// 게임 조작 차단·이미 열림·무기 정보 숨김을 검사하고 튜토리얼 승인을 받은 뒤
        /// 표에서 조건 문구를 만들어 표시합니다. 검사 승인 시 recipe-inspected 신호를 보냅니다.
        /// </summary>
        public void Open()
        {
            if (!m_isInitialized || GameManager.GameplayBlocked || IsOpen)
            {
                return;
            }

            if (!m_view.IsWeaponInfoVisible || !TutorialManager.TryAction(TUTORIAL_ACTION))
            {
                return;
            }

            RenderRecipes();
            m_view.SetPanelVisible(true);
            m_view.SetButtonLabel(CLOSE_BUTTON_LABEL);
            m_isLayoutDirty = true;
            m_view.ResetScroll();
            // Inline inspection can finish while the recipe stays visible during construction.
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.NotifySignal(TUTORIAL_INSPECTED_SIGNAL);
            }
        }

        /// <summary>
        /// 패널을 닫고 버튼 라벨을 닫힘 문구로 되돌립니다. 숨김 자체로 도메인 처리를 하지 않습니다.
        /// </summary>
        public void Close()
        {
            if (m_view == null)
            {
                return;
            }

            m_view.SetPanelVisible(false);
            m_view.SetButtonLabel(OPEN_BUTTON_LABEL);
        }

        /// <summary>
        /// 브리지 LateUpdate에서 호출됩니다. 편집 세션에 따라 버튼을 표시하고
        /// 비편집·무기 정보 숨김에 닫으며, 열려 있는 동안 인라인 높이를 맞춥니다.
        /// </summary>
        public void Tick()
        {
            if (!m_isInitialized)
            {
                return;
            }

            WorkshopSession session = m_sessionProvider != null ? m_sessionProvider() : null;
            bool canBrowse = session != null && session.IsEditing;
            m_view.SetButtonActive(canBrowse);
            if (!canBrowse)
            {
                Close();
                return;
            }

            if (!IsOpen)
            {
                return;
            }

            // Inline inspection can hide the weapon info while the recipe stays open.
            if (!m_view.IsWeaponInfoVisible)
            {
                Close();
                return;
            }

            m_view.ApplyInlineLayout(m_isLayoutDirty);
            m_isLayoutDirty = false;
        }

        private void OnToggleRequested()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        private void RenderRecipes()
        {
            CustomerRequestData table = TableManager.Instance != null ? TableManager.Instance.CustomerRequest : null;
            if (table == null)
            {
                return;
            }

            int count = m_view.RecipeCount;
            for (int i = 0; i < count; i++)
            {
                var tag = table.FindTag(m_view.GetRecipeTagId(i));
                if (tag == null)
                {
                    continue;
                }

                m_view.SetRecipeEntry(i, tag.name, tag.color, DescribeConditions(tag.conditions));
            }
        }

        private string DescribeConditions(CustomerRequestData.Condition[] _conditions)
        {
            m_conditionText.Length = 0;
            foreach (CustomerRequestData.Condition condition in _conditions ?? Array.Empty<CustomerRequestData.Condition>())
            {
                if (condition == null)
                {
                    continue;
                }

                m_conditionText.Append("• ").Append(CustomerRequestInfo.Describe(condition)).AppendLine();
            }

            return m_conditionText.Length > 0 ? m_conditionText.ToString().TrimEnd() : EMPTY_CONDITIONS_TEXT;
        }
    }
}
