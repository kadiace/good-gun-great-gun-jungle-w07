using System;

using UnityEngine;

namespace WeaponWorkshop
{
    // 지도 표시 데이터와 지역 이동 요청을 기존 컨트롤러·지갑·표에서 읽어 View에 전달한다.
    public sealed class RaidMapPresenter : IDisposable
    {
        private const string PRESENTER_NAME = "RaidMapPresenter";

        private readonly RaidMapView m_view;
        private readonly NightRaidController m_controller;
        private readonly ModuleCatalog m_moduleCatalog;

        private int[] m_slotControllerIndex;
        private PlayerWallet m_watchedWallet;
        private GameSaveController m_save;
        private int m_hoveredSlot = -1;
        private bool m_isInitialized;
        private bool m_hasLoggedViewError;
        private bool m_hasLoggedMappingMismatch;

        public RaidMapPresenter(RaidMapView _view, NightRaidController _controller, ModuleCatalog _moduleCatalog)
        {
            m_view = _view;
            m_controller = _controller;
            m_moduleCatalog = _moduleCatalog;
        }

        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (m_view == null)
            {
                if (!m_hasLoggedViewError)
                {
                    m_hasLoggedViewError = true;
                    Debug.LogError($"[{PRESENTER_NAME}] 초기화 실패: RaidMapView 참조가 없습니다.");
                }

                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            BuildSlotMapping();
            m_view.RegionClicked += OnRegionClicked;
            m_view.BackClicked += OnBackClicked;
            m_view.RegionHovered += OnRegionHovered;
            m_view.RegionHoverCleared += OnRegionHoverCleared;
            m_view.ShowDefaultRegionInfo();
            m_isInitialized = true;
            return true;
        }

        public void Dispose()
        {
            if (m_view != null)
            {
                m_view.RegionClicked -= OnRegionClicked;
                m_view.BackClicked -= OnBackClicked;
                m_view.RegionHovered -= OnRegionHovered;
                m_view.RegionHoverCleared -= OnRegionHoverCleared;
            }

            WatchWallet(false);
            if (m_view != null)
            {
                m_view.Dispose();
            }

            m_isInitialized = false;
        }

        public void Show(bool _show)
        {
            if (!m_isInitialized)
            {
                return;
            }

            m_view.SetVisible(_show);
            ClearRegionInfo(m_hoveredSlot);
            if (_show)
            {
                m_view.BringToFront();
            }

            WatchWallet(_show);
            Refresh();
        }

        // 화면이 꺼질 때 지갑 구독만 정리하고 표시 상태는 보존한다.
        public void OnHidden()
        {
            WatchWallet(false);
        }

        public void Refresh()
        {
            if (!m_isInitialized)
            {
                return;
            }

            bool show = m_view.IsShown;
            m_save = show ? UnityEngine.Object.FindFirstObjectByType<GameSaveController>() : null;
            int regionCount = m_controller != null ? m_controller.RegionCount : 0;
            for (int slot = 0; slot < m_slotControllerIndex.Length; slot++)
            {
                int index = m_slotControllerIndex[slot];
                bool listed = index >= 0 && index < regionCount;
                m_view.SetRegionListed(slot, listed);
                if (!listed)
                {
                    m_view.SetRegionFirstClearMark(slot, false);
                    m_view.SetRegionInteractable(slot, false);
                    continue;
                }

                RaidMapData.Region region = TableManager.Instance.RaidMap.Find(m_controller.RegionScene(index));
                bool firstClearAvailable = region != null && region.FirstClearSaleBonus() > 0f &&
                    (m_save == null || !m_save.HasClearedRaid(region.scene));
                m_view.SetRegionFirstClearMark(slot, firstClearAvailable);
                m_view.SetRegionInteractable(slot, show && CanAffordEntry(index) &&
                    (TutorialManager.Instance == null || TutorialManager.Instance.AllowsRaidRegion(index)));
            }
        }

        public void SetLoading()
        {
            if (m_isInitialized)
            {
                m_view.SetAllInteractable(false);
            }
        }

        public void ResetBackButton()
        {
            if (m_isInitialized)
            {
                m_view.SetBackInteractable(TutorialManager.ActionAllowed("close-map"));
            }
        }

        public RectTransform TutorialHouseButton =>
            m_isInitialized && m_view.IsVisibleInHierarchy && m_view.RegionSlotCount > 0
                ? m_view.GetRegionRect(0) : null;

        public void ShowRegionInfo(int _slot)
        {
            if (!m_isInitialized)
            {
                return;
            }

            int index = ResolveControllerIndex(_slot);
            if (index < 0)
            {
                return;
            }

            m_hoveredSlot = _slot;
            UIManager ui = GameManager.Instance != null ? GameManager.Instance.UI : null;
            FirstPersonPlayer player = ui != null ? ui.Player : null;
            RaidMapData.Region region = TableManager.Instance.RaidMap.Find(m_controller.RegionScene(index));
            GameSaveController save = UnityEngine.Object.FindFirstObjectByType<GameSaveController>();
            PlayerModuleUnlocks unlocks = player != null ? player.GetComponent<PlayerModuleUnlocks>() : null;
            m_view.SetRegionInfoText(region.Describe(m_moduleCatalog, unlocks, save != null && save.HasClearedRaid(region?.scene)));
        }

        public void ClearRegionInfo(int _slot)
        {
            if (!m_isInitialized || m_hoveredSlot != _slot)
            {
                return;
            }

            m_hoveredSlot = -1;
            m_view.ShowDefaultRegionInfo();
        }

        private void OnRegionClicked(int _slot)
        {
            int index = ResolveControllerIndex(_slot);
            if (index >= 0 && m_controller != null)
            {
                m_controller.EnterRegion(index);
            }
        }

        private void OnBackClicked()
        {
            if (m_controller != null)
            {
                m_controller.CloseMap();
            }
        }

        private void OnRegionHovered(int _slot)
        {
            ShowRegionInfo(_slot);
        }

        private void OnRegionHoverCleared(int _slot)
        {
            ClearRegionInfo(_slot);
        }

        private int ResolveControllerIndex(int _slot)
        {
            if (m_slotControllerIndex == null || _slot < 0 || _slot >= m_slotControllerIndex.Length)
            {
                return -1;
            }

            return m_slotControllerIndex[_slot];
        }

        private void BuildSlotMapping()
        {
            int slots = m_view.RegionSlotCount;
            m_slotControllerIndex = new int[slots];
            int regionCount = m_controller != null ? m_controller.RegionCount : 0;
            for (int slot = 0; slot < slots; slot++)
            {
                string key = m_view.RegionSceneKey(slot);
                int resolved = -1;
                for (int index = 0; index < regionCount; index++)
                {
                    if (!string.IsNullOrEmpty(key) &&
                        string.Equals(m_controller.RegionScene(index), key, StringComparison.Ordinal))
                    {
                        resolved = index;
                        break;
                    }
                }

                if (resolved < 0 && slot < regionCount)
                {
                    resolved = slot;
                    if (!m_hasLoggedMappingMismatch)
                    {
                        m_hasLoggedMappingMismatch = true;
                        Debug.LogWarning(
                            $"[{PRESENTER_NAME}] 지역 슬롯 '{key}'을(를) 표의 씬 이름에서 찾지 못해 슬롯 순서로 대체합니다. " +
                            "RaidMapView의 m_regionSceneKeys를 확인하세요.");
                    }
                }

                m_slotControllerIndex[slot] = resolved;
            }
        }

        private bool CanAffordEntry(int _index)
        {
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsGuidedDayZero)
            {
                return true;
            }

            if (m_watchedWallet == null || m_controller == null)
            {
                return false;
            }

            RaidMapData.Region region = TableManager.Instance.RaidMap.Find(m_controller.RegionScene(_index));
            return region.CanAffordEntry(m_watchedWallet);
        }

        // 지도가 열려 있는 동안에만 잔액 변화를 구독해 버튼 상태를 다시 계산한다.
        private void WatchWallet(bool _watch)
        {
            PlayerWallet wallet = _watch ? PlayerWallet.ForScene(m_view) : null;
            if (m_watchedWallet == wallet)
            {
                return;
            }

            if (m_watchedWallet != null)
            {
                m_watchedWallet.Changed -= Refresh;
            }

            m_watchedWallet = wallet;
            if (m_watchedWallet != null)
            {
                m_watchedWallet.Changed += Refresh;
            }
        }
    }
}
