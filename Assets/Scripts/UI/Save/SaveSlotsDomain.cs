using System;
using System.IO;
using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// SaveSlotsPresenter가 쓰는 구체 도메인 어댑터입니다. GameSaveFiles·GameSaveController·Start_UI·FeedbackUI·
    /// NightRaidController 같은 기존 도메인을 그대로 감싸고, 알림·선택창의 문맥 오브젝트는 브리지 SaveSlotsUI
    /// 컴포넌트를 그대로 사용합니다. 저장 데이터의 원본은 이 클래스가 복제하지 않습니다.
    /// 대체 구현이 없으므로 인터페이스 없이 구체 클래스로 제공합니다.
    /// </summary>
    public sealed class SaveSlotsDomain
    {
        private readonly GameSaveController m_saveController;
        private readonly Start_UI m_startMenu;
        private readonly Component m_owner;
        private readonly bool m_confirmDelete;

        public SaveSlotsDomain(GameSaveController _saveController, Start_UI _startMenu, Component _owner, bool _confirmDelete)
        {
            m_saveController = _saveController;
            m_startMenu = _startMenu;
            m_owner = _owner;
            m_confirmDelete = _confirmDelete;
        }

        /// <summary>saveController가 연결되면 저장 모드, 아니면 불러오기 모드다(기존 Open·Refresh 판정과 같다).</summary>
        public bool IsSaveMode
        {
            get
            {
                return m_saveController != null;
            }
        }

        /// <summary>불러오기 모드의 필수 문맥(Start_UI)이 있는지 확인합니다.</summary>
        public bool HasLoadContext
        {
            get
            {
                return m_startMenu != null;
            }
        }

        /// <summary>기존 Open 가드와 같이 저장·불러오기 양쪽 문맥이 모두 없으면 열지 않습니다.</summary>
        public bool CanOpen
        {
            get
            {
                return m_saveController != null || m_startMenu != null;
            }
        }

        public bool IsNightRaidBusy
        {
            get
            {
                return NightRaidController.Instance != null && NightRaidController.Instance.IsBusy;
            }
        }

        /// <summary>기존 confirmDelete 직렬화 값. 프리팹별 실효값(UIManager=1, StartUI_Manager=0)을 그대로 따릅니다.</summary>
        public bool ConfirmDelete
        {
            get
            {
                return m_confirmDelete;
            }
        }

        public bool ConfirmOverwrite
        {
            get
            {
                return m_saveController != null && m_saveController.ConfirmOverwrite;
            }
        }

        public int SlotCount
        {
            get
            {
                return GameSaveFiles.SlotCount;
            }
        }

        public int AutoSaveSlotIndex
        {
            get
            {
                return GameSaveFiles.AutoSaveSlotIndex;
            }
        }

        public bool AutoSaveExists
        {
            get
            {
                return GameSaveFiles.AutoSaveExists;
            }
        }

        public bool SlotExists(int _slot)
        {
            return GameSaveFiles.Exists(_slot);
        }

        public bool TryReadAutoSave(out GameSaveData _data)
        {
            return GameSaveFiles.TryReadAutoSave(out _data);
        }

        public bool TryReadSlot(int _slot, out GameSaveData _data)
        {
            return GameSaveFiles.TryRead(_slot, out _data);
        }

        public string DescribeSlot(int _slot)
        {
            return GameSaveFiles.Description(_slot);
        }

        public string DescribeAutoSave()
        {
            return GameSaveFiles.AutoSaveDescription();
        }

        public bool Save(int _slot, out string _error)
        {
            if (m_saveController == null)
            {
                _error = "저장 관리자 연결이 없습니다.";
                return false;
            }

            return m_saveController.Save(_slot, out _error);
        }

        public void DeleteSlotFile(int _slot)
        {
            GameSaveFiles.Delete(_slot);
        }

        public void DeleteAutoSaveFile()
        {
            GameSaveFiles.DeleteAutoSave();
        }

        public void LoadSlot(int _slot)
        {
            if (m_startMenu != null)
            {
                m_startMenu.LoadSlot(_slot);
            }
        }

        public void RefreshSaveAvailability()
        {
            if (m_startMenu != null)
            {
                m_startMenu.RefreshSaveAvailability();
            }
        }

        public void ShowNotification(string _message)
        {
            FeedbackUI.For(m_owner)?.ShowNotification(_message);
        }

        /// <summary>기존 ShowChoice 호출과 같이 예/아니오 선택창을 띄우고 예 콜백을 등록합니다.</summary>
        public void ShowConfirmChoice(string _message, Action _onYes)
        {
            FeedbackUI.For(m_owner)?.ShowChoice(m_owner, _message, _onYes);
        }

        public bool IsChoiceOpen
        {
            get
            {
                FeedbackUI feedback = FeedbackUI.For(m_owner);
                return feedback != null && feedback.IsChoiceOpen;
            }
        }

        /// <summary>기존 Close 경로와 같이 이 화면이 연 선택창만 취소합니다.</summary>
        public void CancelOwnChoice()
        {
            FeedbackUI.For(m_owner)?.CancelChoice(m_owner);
        }

        /// <summary>기존 DeleteConfirmed·DeleteAutoSaveConfirmed가 함께 잡던 I/O 예외 범위입니다.</summary>
        public static bool IsSaveIoException(Exception _exception)
        {
            return _exception is IOException || _exception is UnauthorizedAccessException;
        }
    }
}
