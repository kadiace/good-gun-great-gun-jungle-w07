using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WeaponWorkshop
{
    /// <summary>
    /// 모듈 팔레트의 선택·즐겨찾기·잠금·가격 표시와 드래그 상태·드롭 판정을 담당하는 Presenter입니다.
    /// 즐겨찾기와 선택의 실제 상태는 CubeBuilder가 계속 소유하며, 여기서는 표시와 입력 해석만 한다.
    /// </summary>
    public sealed class ModulePalettePresenter : IDisposable
    {
        private const string DRAG_GUIDE_IDLE = ModulePaletteView.HotbarGuideText;
        private const string DRAG_GUIDE_NO_TARGET = "교체할 퀵슬롯에 놓으세요 · 바깥에 놓으면 취소됩니다";

        private readonly ModulePaletteView m_view;

        private CubeBuilder m_builder;
        private ModuleSlotUI m_hoveredSlot;
        private ModuleSlotUI m_dragSource;
        private ModuleSlotUI m_dropTarget;
        private int m_draggingModule;
        private int m_dragPointerId;
        private bool m_dragStartedWithPalette;
        private int m_lastState = int.MinValue;
        private int m_moduleCount = -1;
        private bool m_isInitialized;

        public ModulePalettePresenter(ModulePaletteView _view)
        {
            m_view = _view;
        }

        public bool IsInitialized => m_isInitialized;

        public int HoveredModuleId
        {
            get
            {
                return m_hoveredSlot != null && m_hoveredSlot.isActiveAndEnabled ? m_hoveredSlot.ModuleId : 0;
            }
        }

        public bool IsDragging => m_draggingModule != 0;

        /// <summary>
        /// View와 CubeBuilder를 연결하고 카탈로그가 준비되어 있으면 목록 슬롯을 배치합니다.
        /// 중복 호출에 안전하며, 이후 빌더가 바뀌면 SetBuilder로 갱신합니다.
        /// </summary>
        public bool Initialize(CubeBuilder _builder)
        {
            if (m_isInitialized)
            {
                SetBuilder(_builder);
                return true;
            }

            if (m_view == null)
            {
                Debug.LogError("[ModulePalettePresenter] 초기화 실패: ModulePaletteView 참조가 없습니다.");
                return false;
            }

            m_builder = _builder;
            if (m_builder != null)
            {
                m_builder.FavoriteSelected += OnFavoriteSelected;
            }

            m_isInitialized = true;
            EnsureModuleList();
            return true;
        }

        public void Dispose()
        {
            if (m_builder != null)
            {
                m_builder.FavoriteSelected -= OnFavoriteSelected;
            }

            ClearHover();
            CancelDrag();
            m_view?.HideDragGhost();
            m_isInitialized = false;
        }

        public void SetBuilder(CubeBuilder _builder)
        {
            if (m_builder == _builder)
            {
                return;
            }

            if (m_builder != null)
            {
                m_builder.FavoriteSelected -= OnFavoriteSelected;
            }

            m_builder = _builder;
            if (m_builder != null)
            {
                m_builder.FavoriteSelected += OnFavoriteSelected;
            }

            m_lastState = int.MinValue;
            EnsureModuleList();
        }

        public void SetHoveredSlot(ModuleSlotUI _slot)
        {
            if (m_builder == null || _slot == null)
            {
                return;
            }

            m_hoveredSlot = _slot;
            if (m_builder.PaletteOpen && _slot.FavoriteIndex < 0)
            {
                m_builder.SetHoveredPaletteModule(_slot.ModuleId);
            }

            UpdateDropTarget();
        }

        public void ClearHoveredSlot(ModuleSlotUI _slot)
        {
            if (m_hoveredSlot != _slot)
            {
                return;
            }

            ClearHover();
        }

        public void ClearHover()
        {
            m_hoveredSlot = null;
            m_builder?.SetHoveredPaletteModule(0);
            SetDropTarget(null);
        }

        public void SlotDisabled(ModuleSlotUI _slot)
        {
            ClearHoveredSlot(_slot);
            if (m_dragSource == _slot)
            {
                CancelDrag();
            }
            else if (m_dropTarget == _slot)
            {
                SetDropTarget(null);
            }
        }

        public void HandleClick(ModuleSlotUI _slot)
        {
            if (m_builder == null || _slot == null || m_draggingModule != 0)
            {
                return;
            }

            if (!TutorialManager.TryAction("select-module"))
            {
                return;
            }

            if ((!m_builder.PaletteOpen && _slot.FavoriteIndex < 0) || m_builder.IsModuleLocked(_slot.ModuleId))
            {
                return;
            }

            if (m_builder.SelectModule(_slot.ModuleId))
            {
                if (_slot.FavoriteIndex >= 0)
                {
                    m_view?.FocusHotbarSlot(_slot.FavoriteIndex);
                }
            }
        }

        private void OnFavoriteSelected(int _index)
        {
            if (m_view == null)
            {
                return;
            }

            m_view.FocusHotbarSlot(_index);
        }

        public void BeginDrag(ModuleSlotUI _slot, PointerEventData _eventData)
        {
            if (_eventData == null || _eventData.button != PointerEventData.InputButton.Left ||
                m_draggingModule != 0)
            {
                return;
            }

            if (!CanDragFrom(_slot))
            {
                TutorialManager.Instance?.NotifyBlockedAction();
                return;
            }

            m_dragSource = _slot;
            m_draggingModule = _slot.ModuleId;
            m_dragPointerId = _eventData.pointerId;
            m_dragStartedWithPalette = m_builder.PaletteOpen;
            _eventData.eligibleForClick = false;
            m_builder.SetFavoriteDrag(true);
            m_builder.Catalog.TryGet(m_draggingModule, out ModuleCatalog.Entry entry);
            m_view.ShowDragGhost(entry.color, entry.displayName, GetLocalPoint(_eventData));
            UpdateDropTarget();
            UpdateDragGuide();
        }

        public void Drag(PointerEventData _eventData)
        {
            if (m_draggingModule == 0 || _eventData == null || _eventData.pointerId != m_dragPointerId)
            {
                return;
            }

            m_view.MoveDragGhost(GetLocalPoint(_eventData));
        }

        public void Drop(ModuleSlotUI _target, PointerEventData _eventData)
        {
            if (m_draggingModule == 0 || m_dragSource == null || _eventData == null ||
                _eventData.pointerId != m_dragPointerId ||
                _eventData.pointerDrag != m_dragSource.gameObject)
            {
                return;
            }

            if (!CanDropOn(_target))
            {
                if (_target != null && _target != m_dragSource)
                {
                    TutorialManager.Instance?.NotifyBlockedAction();
                }

                return;
            }

            if (m_dragSource.FavoriteIndex >= 0)
            {
                if (!m_builder.SwapFavorites(m_dragSource.FavoriteIndex, _target.FavoriteIndex))
                {
                    return;
                }
            }
            else
            {
                m_builder.SetFavorite(_target.FavoriteIndex, m_draggingModule);
            }

            m_lastState = ComputeState();
            Refresh();
            CancelDrag();
        }

        public void EndDrag(PointerEventData _eventData)
        {
            if (m_draggingModule != 0 && _eventData != null && _eventData.pointerId == m_dragPointerId)
            {
                CancelDrag();
            }
        }

        public bool TryCancelSlotDrag()
        {
            if (m_draggingModule == 0)
            {
                return false;
            }

            CancelDrag();
            return true;
        }

        public void CancelDrag()
        {
            m_draggingModule = 0;
            m_dragSource = null;
            m_dragStartedWithPalette = false;
            m_builder?.SetFavoriteDrag(false);
            SetDropTarget(null);
            UpdateDragGuide();
            m_view?.HideDragGhost();
        }

        /// <summary>
        /// 매 프레임 드래그 지속 조건을 확인합니다. 창이 닫히거나 잠금·모드가 바뀌면 여기서 취소됩니다.
        /// </summary>
        public void UpdateDrag(bool _paletteOpen)
        {
            if (m_draggingModule == 0)
            {
                return;
            }

            if (!CanContinueDrag())
            {
                CancelDrag();
                return;
            }

            if (_paletteOpen)
            {
                m_dragStartedWithPalette = true;
            }

            UpdateDropTarget();
        }

        public void ClearHoverIfNeeded(bool _active, bool _customMode, bool _paletteOpen)
        {
            if (!_active || _customMode ||
                (!_paletteOpen && m_hoveredSlot != null && m_hoveredSlot.FavoriteIndex < 0))
            {
                ClearHover();
            }
        }

        /// <summary>
        /// 선택·즐겨찾기·잠금·재료 비용이 바뀐 경우에만 표시를 다시 그립니다.
        /// </summary>
        public void RefreshIfStateChanged()
        {
            if (m_builder == null || m_builder.Catalog == null)
            {
                return;
            }

            EnsureModuleList();
            int state = ComputeState();
            if (state == m_lastState)
            {
                return;
            }

            m_lastState = state;
            Refresh();
        }

        private void EnsureModuleList()
        {
            if (m_builder == null || m_builder.Catalog == null || m_view == null)
            {
                return;
            }

            int count = m_builder.Catalog.Count;
            if (count == m_moduleCount)
            {
                return;
            }

            List<int>[] buckets = { new List<int>(), new List<int>(), new List<int>() };
            for (int id = 1; id <= count; id++)
            {
                m_builder.Catalog.TryGet(id, out ModuleCatalog.Entry entry);
                bool decorative = entry.data != null && (entry.data.ModuleType == StatType.Scope ||
                    entry.data.ModuleType == StatType.Color ||
                    (entry.prefab != null && entry.prefab.GetComponent<TriggerModule>() != null));
                int category = id <= 4 ? 0 : decorative ? 2 : 1;
                buckets[category].Add(id);
            }

            m_view.BuildPaletteSlots(new[] { buckets[0].ToArray(), buckets[1].ToArray(), buckets[2].ToArray() });
            m_moduleCount = count;
            m_lastState = int.MinValue;
        }

        private int ComputeState()
        {
            int state = m_builder.DeleteMode ? -1 : m_builder.SelectedSlot;
            for (int i = 0; i < m_builder.FavoriteCount; i++)
            {
                state = state * 31 + m_builder.GetFavorite(i);
            }

            for (int id = 1; id <= m_builder.Catalog.Count; id++)
            {
                state = state * 31 + (m_builder.IsModuleLocked(id) ? 1 : 0);
                state = state * 31 + m_builder.GetNextModuleCreditCost(id);
            }

            return state;
        }

        private void Refresh()
        {
            ModuleCatalog catalog = m_builder.Catalog;
            int selected = m_builder.DeleteMode ? 0 : m_builder.SelectedSlot;
            IReadOnlyList<ModuleSlotUI> hotbarSlots = m_view.HotbarSlots;
            if (hotbarSlots != null)
            {
                for (int i = 0; i < hotbarSlots.Count; i++)
                {
                    ModuleSlotUI slot = hotbarSlots[i];
                    if (slot == null)
                    {
                        continue;
                    }

                    int id = m_builder.GetFavorite(i);
                    catalog.TryGet(id, out ModuleCatalog.Entry entry);
                    slot.Show(id, entry.displayName, entry.color, id == selected, m_builder.IsModuleLocked(id),
                        GetCostLabel(entry, id));
                }
            }

            for (int id = 1; id <= catalog.Count; id++)
            {
                ModuleSlotUI slot = m_view.PaletteSlot(id);
                if (slot == null)
                {
                    continue;
                }

                catalog.TryGet(id, out ModuleCatalog.Entry entry);
                slot.Show(id, entry.displayName, entry.color, id == selected, m_builder.IsModuleLocked(id),
                    GetCostLabel(entry, id));
            }

            UpdateDragGuide();
        }

        private string GetCostLabel(ModuleCatalog.Entry _entry, int _moduleId)
        {
            return $"${_entry.data.BuyPrice} · 재료{m_builder.GetNextModuleCreditCost(_moduleId)}";
        }

        private bool CanDragFrom(ModuleSlotUI _slot)
        {
            return m_builder != null && m_builder.Catalog != null && m_builder.CanEditFavorites &&
                _slot != null && _slot.isActiveAndEnabled && _slot.ModuleId > 0 &&
                !m_builder.IsModuleLocked(_slot.ModuleId) &&
                (_slot.FavoriteIndex >= 0 || m_builder.PaletteOpen) &&
                (TutorialManager.Instance == null || TutorialManager.Instance.AllowsQuickslotSource(_slot.ModuleId));
        }

        private bool CanContinueDrag()
        {
            return CanDragFrom(m_dragSource) &&
                (!m_dragStartedWithPalette || m_builder.PaletteOpen) &&
                (m_dragSource.FavoriteIndex < 0 ||
                 m_builder.GetFavorite(m_dragSource.FavoriteIndex) == m_draggingModule);
        }

        private bool CanDropOn(ModuleSlotUI _target)
        {
            return m_draggingModule != 0 && CanContinueDrag() && _target != null && _target.isActiveAndEnabled &&
                _target.FavoriteIndex >= 0 && _target != m_dragSource &&
                (m_dragSource.FavoriteIndex >= 0 || _target.ModuleId != m_draggingModule) &&
                (TutorialManager.Instance == null ||
                 TutorialManager.Instance.AllowsQuickslotChange(m_draggingModule, _target.FavoriteIndex));
        }

        private void UpdateDropTarget()
        {
            SetDropTarget(CanDropOn(m_hoveredSlot) ? m_hoveredSlot : null);
        }

        private void SetDropTarget(ModuleSlotUI _target)
        {
            if (m_dropTarget == _target)
            {
                return;
            }

            m_dropTarget?.SetDropHighlight(false);
            m_dropTarget = _target;
            m_dropTarget?.SetDropHighlight(true);
            UpdateDragGuide();
        }

        private void UpdateDragGuide()
        {
            if (m_view == null)
            {
                return;
            }

            TutorialStep step = TutorialManager.Instance?.CurrentStep;
            if (step != null && step.restrictQuickslotChange && m_dropTarget == null)
            {
                m_view.SetGuideText(step.objective);
                return;
            }

            m_view.SetGuideText(m_draggingModule == 0 ? DRAG_GUIDE_IDLE : m_dropTarget == null
                ? DRAG_GUIDE_NO_TARGET
                : $"{(m_dropTarget.FavoriteIndex + 1) % 10}번 퀵슬롯에 놓아 " +
                  (m_dragSource.FavoriteIndex >= 0 ? "서로 교체합니다" : "등록합니다"));
        }

        private Vector2 GetLocalPoint(PointerEventData _eventData)
        {
            RectTransform root = m_view.Root;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, _eventData.position,
                _eventData.pressEventCamera, out Vector2 local))
            {
                return local;
            }

            return Vector2.zero;
        }
    }
}
