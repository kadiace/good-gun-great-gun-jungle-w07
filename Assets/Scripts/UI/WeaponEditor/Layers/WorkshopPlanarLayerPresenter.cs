using UnityEngine;

namespace WeaponWorkshop
{
    // 층 이동 UI의 표시 값 계산과 층 변경 요청을 담당하는 일반 C# Presenter다.
    // 도메인 상태는 기존 WorkshopPlanarView·WorkshopPlanarLayerPresentation·CubeBuilder에서만 읽고
    // 카운트·층 판정 로직을 복제하지 않는다. 층 변경은 기존 WorkshopPlanarView.SetSelectedDepth로만 요청한다.
    // 브리지(WorkshopPlanarLayerUI)가 생성·초기화·해제를 소유한다.
    public sealed class WorkshopPlanarLayerPresenter
    {
        private const string DEFAULT_HINT =
            "Ctrl+휠: 층 이동 · 좌클릭 드래그: 설치/삭제\n청록: +방향 층 / 주황: −방향 층 · 빨강: 삭제 범위";
        private static readonly string[] AXIS_LABELS = { "+FRONT", "+UP", "+RIGHT", "−FRONT", "−UP", "−RIGHT" };
        private static readonly Color SELECTED_COLOR = new(0.15f, 0.48f, 0.55f, 1f);
        private static readonly Color UNSELECTED_COLOR = new(0.1f, 0.13f, 0.15f, 0.95f);

        private readonly WorkshopPlanarLayerView m_view;
        private WorkshopPlanarLayerState m_state;
        private WorkshopPlanarLayerSlotState[] m_slots;
        private WorkshopPlanarView m_model;
        private CubeBuilder m_builder;
        private WorkshopPlanarLayerPresentation m_presentation;
        private bool m_isInitialized;

        public WorkshopPlanarLayerPresenter(WorkshopPlanarLayerView _view)
        {
            m_view = _view;
        }

        /// <summary>View 이벤트를 구독한다. 중복 호출에 안전하다.</summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (m_view == null)
            {
                return false;
            }

            m_view.PreviousRequested += OnPreviousRequested;
            m_view.NextRequested += OnNextRequested;
            m_view.CoreRequested += OnCoreRequested;
            m_view.LayerRequested += OnLayerRequested;
            m_isInitialized = true;
            return true;
        }

        /// <summary>View 이벤트 구독을 해제한다. 중복 호출에 안전하다.</summary>
        public void Dispose()
        {
            if (m_view != null && m_isInitialized)
            {
                m_view.PreviousRequested -= OnPreviousRequested;
                m_view.NextRequested -= OnNextRequested;
                m_view.CoreRequested -= OnCoreRequested;
                m_view.LayerRequested -= OnLayerRequested;
            }

            m_state = null;
            m_slots = null;
            m_isInitialized = false;
        }

        /// <summary>브리지 Bind를 그대로 받아 도메인 참조와 표시 여부를 맞춘다.</summary>
        public void Bind(WorkshopPlanarView _value, CubeBuilder _owner, WorkshopPlanarLayerPresentation _display, bool _show)
        {
            bool wasVisible = m_view.IsVisible;
            m_model = _value;
            m_builder = _owner;
            m_presentation = _display;
            if (!m_isInitialized)
            {
                return;
            }

            m_view.SetVisible(_show);
            if (_show)
            {
                Refresh();
                // 패널을 새로 연 때만, 조작 가능(CanChangeView)일 때만 기본 포커스를 묻는다.
                // 이미 열린 동안의 재Bind와 드래그·차단 상태에서는 기존 포커스를 건드리지 않는다.
                if (!wasVisible && m_model != null && m_model.CanChangeView)
                {
                    m_view.FocusDefaultIfNeeded();
                }
            }
        }

        /// <summary>패널이 보이는 동안 브리지 LateUpdate(108)에서 호출한다. 숨김·미바인딩·3D 모드면 계산하지 않는다.</summary>
        public void Refresh()
        {
            if (!m_isInitialized || m_model == null || !m_view.IsVisible || !m_model.Is2D)
            {
                return;
            }

            m_view.Render(BuildState());
        }

        // 주변 5층 슬롯 값·개수·선택·색·안내 문구를 계산한다. 상태·슬롯 버퍼는 슬롯 수가 바뀔 때만 다시 만든다.
        private WorkshopPlanarLayerState BuildState()
        {
            int slotCount = m_view.SlotCount;
            if (m_state == null || m_slots == null || m_slots.Length != slotCount)
            {
                m_slots = new WorkshopPlanarLayerSlotState[slotCount];
                for (int i = 0; i < slotCount; i++)
                {
                    m_slots[i] = new WorkshopPlanarLayerSlotState();
                }

                m_state = new WorkshopPlanarLayerState
                {
                    Slots = m_slots,
                };
            }

            int center = slotCount / 2;
            bool canEdit = m_model.CanChangeView;
            m_state.Title = $"{AXIS_LABELS[(int)m_model.Axis]} · {m_model.SelectedDepth:+0;-0;0}층 편집";
            m_state.Hint = m_builder != null && m_builder.HasPlanarStroke ? m_builder.PlanarStrokeCaption : DEFAULT_HINT;
            m_state.CanEdit = canEdit;
            m_state.SelectedColor = SELECTED_COLOR;
            m_state.UnselectedColor = UNSELECTED_COLOR;
            for (int i = 0; i < m_slots.Length; i++)
            {
                // ±2 이동도 int 경계에서 넘치지 않게 long으로 계산해 포화 처리한다.
                long raw = (long)m_model.SelectedDepth + i - center;
                bool inRange = raw <= int.MaxValue && raw >= int.MinValue;
                int depth = inRange ? (int)raw : raw > 0 ? int.MaxValue : int.MinValue;
                int count = m_presentation != null ? m_presentation.CountAt(depth) : 0;
                WorkshopPlanarLayerSlotState slot = m_slots[i];
                slot.Depth = depth;
                slot.IsInRange = inRange;
                slot.IsSelected = inRange && depth == m_model.SelectedDepth;
                slot.Interactable = canEdit && inRange;
                slot.Caption = $"{depth:+0;-0;0}층\n{(count == 0 ? "빈 층" : count + "셀")}";
            }

            return m_state;
        }

        private void OnPreviousRequested()
        {
            if (m_model != null && m_model.SelectedDepth > int.MinValue)
            {
                m_model.SetSelectedDepth(m_model.SelectedDepth - 1);
            }
        }

        private void OnNextRequested()
        {
            if (m_model != null && m_model.SelectedDepth < int.MaxValue)
            {
                m_model.SetSelectedDepth(m_model.SelectedDepth + 1);
            }
        }

        private void OnCoreRequested()
        {
            m_model?.SetSelectedDepth(0);
        }

        private void OnLayerRequested(int _slot)
        {
            if (m_model == null)
            {
                return;
            }

            long raw = (long)m_model.SelectedDepth + _slot - m_view.SlotCount / 2;
            if (raw > int.MaxValue || raw < int.MinValue)
            {
                return;
            }

            m_model.SetSelectedDepth((int)raw);
        }
    }
}
