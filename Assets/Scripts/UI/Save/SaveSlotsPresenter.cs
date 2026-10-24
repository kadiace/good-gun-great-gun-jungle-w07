using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    /// <summary>
    /// 저장 슬롯 패널의 동작을 담당하는 Presenter입니다. 저장·불러오기 모드 판정, 슬롯 요약 가공,
    /// 저장/불러오기/삭제 요청, 덮어쓰기·삭제 확인, 실패 안내와 닫기 포커스 복원을 맡고
    /// 표시는 SaveSlotsView에만 맡깁니다. 저장 파일 원본은 GameSaveFiles·GameSaveController가 소유하며
    /// 기존 SaveSlotsUI의 판정 순서·문구·취소 경로를 그대로 유지합니다.
    /// </summary>
    public sealed class SaveSlotsPresenter : IDisposable
    {
        private const string PRESENTER_NAME = "SaveSlotsPresenter";
        private const string SAVE_TITLE = "게임 저장";
        private const string LOAD_TITLE = "게임 불러오기";
        private const string MISSING_CONTEXT_MESSAGE = "저장 관리자 연결이 없습니다.";
        private const string NIGHT_RAID_BUSY_MESSAGE = "공방에 귀환한 뒤 저장하세요.";

        private readonly SaveSlotsView m_view;
        private readonly SaveSlotsDomain m_domain;

        private GameObject m_savedFocus;
        private bool m_isInitialized;
        private bool m_hasLoggedDependencyError;

        public bool IsInitialized
        {
            get
            {
                return m_isInitialized;
            }
        }

        public bool IsOpen
        {
            get
            {
                return m_view != null && m_view.IsOpen;
            }
        }

        public SaveSlotsPresenter(SaveSlotsView _view, SaveSlotsDomain _domain)
        {
            m_view = _view;
            m_domain = _domain;
        }

        /// <summary>View를 초기화(패널 닫힘 포함)하고 카드·삭제 클릭 이벤트를 구독한 뒤 슬롯 표시를 갱신합니다.</summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (m_view == null || m_domain == null)
            {
                if (!m_hasLoggedDependencyError)
                {
                    m_hasLoggedDependencyError = true;
                    Debug.LogError(
                        $"[{PRESENTER_NAME}] Initialize 실패: 필수 의존성 누락 " +
                        $"(_view={m_view != null}, _domain={m_domain != null})");
                }

                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            m_view.AutoSaveClicked -= LoadAutoSave;
            m_view.AutoSaveClicked += LoadAutoSave;
            m_view.SlotDeleteRequested -= DeleteSlot;
            m_view.SlotDeleteRequested += DeleteSlot;
            m_view.AutoSaveDeleteRequested -= DeleteAutoSave;
            m_view.AutoSaveDeleteRequested += DeleteAutoSave;

            m_isInitialized = true;
            // 기존 Awake의 Refresh와 같이 씬 진입 시점의 저장 파일 상태를 표시에 반영한다.
            Refresh();
            return true;
        }

        /// <summary>구독한 View 이벤트를 해제합니다. 중복 호출에 안전합니다.</summary>
        public void Dispose()
        {
            if (m_view != null)
            {
                m_view.AutoSaveClicked -= LoadAutoSave;
                m_view.SlotDeleteRequested -= DeleteSlot;
                m_view.AutoSaveDeleteRequested -= DeleteAutoSave;
                m_view.Dispose();
            }

            m_isInitialized = false;
        }

        /// <summary>
        /// 패널을 엽니다. 저장 관리자 연결 부재와 야간 습격 중 저장은 기존과 같이 알림으로 막고,
        /// 제목을 모드에 맞게 바꾼 뒤 메인 패널·여는 버튼을 숨깁니다.
        /// </summary>
        public void Open()
        {
            if (!EnsureInitialized())
            {
                return;
            }

            if (!m_domain.CanOpen)
            {
                m_domain.ShowNotification(MISSING_CONTEXT_MESSAGE);
                return;
            }

            if (m_domain.IsNightRaidBusy)
            {
                m_domain.ShowNotification(NIGHT_RAID_BUSY_MESSAGE);
                return;
            }

            m_view.SetTitle(m_domain.IsSaveMode ? SAVE_TITLE : LOAD_TITLE);
            UIRoot.SaveFocus(ref m_savedFocus, m_view.transform);
            m_view.SetPanelVisible(true);
            Refresh();
            m_view.SetMainPanelVisible(false);
            m_view.SetOpenerVisible(false);
            UIRoot.SetFocus(m_view.DefaultSelectable);
        }

        /// <summary>
        /// 패널을 닫고 이 화면이 연 선택창을 취소하며 메인 패널·여는 버튼을 복원합니다.
        /// 기존 Close의 순서와 부수효과(숨김 자체가 취소 콜백을 실행)를 유지합니다.
        /// </summary>
        public void Close()
        {
            m_domain.CancelOwnChoice();
            m_view.SetPanelVisible(false);
            m_view.SetMainPanelVisible(true);
            m_view.SetOpenerVisible(true);
            Selectable openerSelectable = m_view.OpenButton != null ? m_view.OpenButton.GetComponent<Selectable>() : null;
            UIRoot.RestoreFocus(ref m_savedFocus, openerSelectable);
            FocusOpenerOrPause();
        }

        /// <summary>UICancelRouter의 Pause 하위 저장 닫기 경로. 열려 있을 때만 닫고 포커스를 복원합니다.</summary>
        public bool TryCloseForCancel()
        {
            if (!m_view.IsOpen)
            {
                return false;
            }

            Close();
            FocusOpenerOrPause();
            return true;
        }

        /// <summary>저장 파일 상태를 다시 읽어 카드 요약·가능 상태와 삭제 버튼 표시를 갱신합니다.</summary>
        public void Refresh()
        {
            bool loadMode = !m_domain.IsSaveMode && m_domain.HasLoadContext;
            bool hasAutoSave = m_domain.TryReadAutoSave(out _);
            m_view.SetAutoSaveState(
                "자동 저장\n" + m_domain.DescribeAutoSave(),
                hasAutoSave,
                loadMode && hasAutoSave,
                loadMode && hasAutoSave,
                m_domain.AutoSaveExists);

            for (int i = 0; i < m_domain.SlotCount; i++)
            {
                m_view.SetSlotState(
                    i,
                    $"슬롯 {i + 1}\n{m_domain.DescribeSlot(i)}",
                    m_domain.IsSaveMode || m_domain.TryReadSlot(i, out _),
                    loadMode,
                    loadMode && m_domain.SlotExists(i));
            }
        }

        /// <summary>슬롯 카드 클릭(persistent 호출 SelectSlot 경로). 모드에 따라 불러오거나 저장 확인을 거칩니다.</summary>
        public void SelectSlot(int _slot)
        {
            if (!EnsureInitialized() || _slot < 0 || _slot >= m_domain.SlotCount)
            {
                return;
            }

            if (!m_domain.IsSaveMode)
            {
                m_domain.LoadSlot(_slot);
                return;
            }

            if (m_domain.ConfirmOverwrite && m_domain.SlotExists(_slot))
            {
                int slot = _slot;
                m_domain.ShowConfirmChoice($"슬롯 {slot + 1}을 덮어쓰시겠습니까?", () => SaveConfirmed(slot));
                return;
            }

            SaveConfirmed(_slot);
        }

        /// <summary>자동저장 카드 클릭. 저장 모드·문맥 부재·파일 부재는 기존과 같이 무시합니다.</summary>
        private void LoadAutoSave()
        {
            if (m_domain.IsSaveMode || !m_domain.HasLoadContext || !m_domain.TryReadAutoSave(out _))
            {
                return;
            }

            m_domain.LoadSlot(m_domain.AutoSaveSlotIndex);
        }

        /// <summary>수동 슬롯 삭제 요청(View 이벤트와 persistent 호출 DeleteSlot N의 공용 진입점).</summary>
        public void DeleteSlot(int _slot)
        {
            if (m_domain.IsSaveMode || !m_domain.HasLoadContext || _slot < 0 || _slot >= m_domain.SlotCount ||
                !m_domain.SlotExists(_slot))
            {
                return;
            }

            if (m_domain.ConfirmDelete)
            {
                if (m_domain.IsChoiceOpen)
                {
                    return;
                }

                int slot = _slot;
                m_domain.ShowConfirmChoice($"슬롯 {slot + 1}을 삭제하시겠습니까?", () => DeleteConfirmed(slot));
                return;
            }

            DeleteConfirmed(_slot);
        }

        /// <summary>자동저장 삭제 요청(View 이벤트와 persistent 호출 DeleteAutoSave의 공용 진입점).</summary>
        public void DeleteAutoSave()
        {
            if (m_domain.IsSaveMode || !m_domain.HasLoadContext || !m_domain.AutoSaveExists)
            {
                return;
            }

            if (m_domain.ConfirmDelete)
            {
                if (m_domain.IsChoiceOpen)
                {
                    return;
                }

                m_domain.ShowConfirmChoice("자동 저장 파일을 삭제하시겠습니까?", DeleteAutoSaveConfirmed);
                return;
            }

            DeleteAutoSaveConfirmed();
        }

        private void DeleteConfirmed(int _slot)
        {
            try
            {
                m_domain.DeleteSlotFile(_slot);
                Refresh();
                m_domain.RefreshSaveAvailability();
                m_domain.ShowNotification($"슬롯 {_slot + 1}을 삭제했습니다.");
            }
            catch (Exception e) when (SaveSlotsDomain.IsSaveIoException(e))
            {
                m_domain.ShowNotification("저장 파일을 삭제하지 못했습니다. " + e.Message);
            }
        }

        private void DeleteAutoSaveConfirmed()
        {
            try
            {
                m_domain.DeleteAutoSaveFile();
                Refresh();
                m_domain.RefreshSaveAvailability();
                m_domain.ShowNotification("자동 저장 파일을 삭제했습니다.");
            }
            catch (Exception e) when (SaveSlotsDomain.IsSaveIoException(e))
            {
                m_domain.ShowNotification("자동 저장 파일을 삭제하지 못했습니다. " + e.Message);
            }
        }

        private void SaveConfirmed(int _slot)
        {
            if (m_domain.Save(_slot, out string error))
            {
                Refresh();
                m_domain.ShowNotification($"슬롯 {_slot + 1}에 저장했습니다.");
            }
            else
            {
                m_domain.ShowNotification(error);
            }
        }

        /// <summary>
        /// 닫은 뒤 여는 버튼으로 포커스를 돌리고, 없으면 일시정지 기본 선택으로 복원합니다.
        /// 기존 FocusOpenerOrPause와 같은 대상·조건을 유지합니다.
        /// </summary>
        private void FocusOpenerOrPause()
        {
            if (TryFocus(m_view.OpenButton))
            {
                return;
            }

            PauseView pause = GameManager.Instance != null && GameManager.Instance.UI != null
                ? GameManager.Instance.UI.Screen<PauseUI>()?.View
                : null;
            if (pause != null && pause.DefaultSelectable != null)
            {
                TryFocus(pause.DefaultSelectable.gameObject);
            }
        }

        private bool TryFocus(GameObject _target)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || _target == null || !_target.activeInHierarchy)
            {
                return false;
            }

            Selectable selectable = _target.GetComponent<Selectable>();
            if (selectable == null || !selectable.isActiveAndEnabled || !selectable.IsInteractable())
            {
                return false;
            }

            eventSystem.SetSelectedGameObject(null);
            eventSystem.SetSelectedGameObject(_target);
            return true;
        }

        private bool EnsureInitialized()
        {
            if (!m_isInitialized && !Initialize())
            {
                return false;
            }

            return true;
        }
    }
}
