using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeaponWorkshop
{
    /// <summary>
    /// StartMenuPresenter가 쓰는 구체 도메인 어댑터입니다. GameSaveFiles·PlayerIdentity·SceneManager·LogKit·
    /// FeedbackUI·SaveSlotsUI 같은 기존 도메인을 그대로 감싸고, 알림·오류 로그의 문맥 오브젝트는
    /// 브리지 Start_UI 컴포넌트를 그대로 사용합니다. 대체 구현이 없으므로 인터페이스 없이 구체 클래스로 제공합니다.
    /// </summary>
    public sealed class StartMenuDomainAdapter
    {
        private readonly SaveSlotsUI m_loadSlotsUI;
        private readonly Component m_owner;

        public StartMenuDomainAdapter(SaveSlotsUI _loadSlotsUI, Component _owner)
        {
            m_loadSlotsUI = _loadSlotsUI;
            m_owner = _owner;
        }

        public int LatestSlot()
        {
            return GameSaveFiles.LatestSlot();
        }

        public bool TryReadSlot(int _slot, out GameSaveData _data)
        {
            return GameSaveFiles.TryReadSlot(_slot, out _data);
        }

        public GameSaveData PendingLoad
        {
            get
            {
                return GameSaveFiles.PendingLoad;
            }
            set
            {
                GameSaveFiles.PendingLoad = value;
            }
        }

        public void ClearPendingLoad()
        {
            GameSaveFiles.PendingLoad = null;
        }

        public void ClearRaidRecovery()
        {
            GameSaveFiles.ClearRaidRecovery();
        }

        public void AdoptSaveId(string _saveId)
        {
            PlayerIdentity.Adopt(_saveId);
        }

        public void BeginNewSave()
        {
            PlayerIdentity.BeginNewSave();
        }

        public string InstallId
        {
            get
            {
                return PlayerIdentity.InstallId;
            }
        }

        public string SaveId
        {
            get
            {
                return PlayerIdentity.SaveId;
            }
        }

        public bool IsPendingLoadRetired
        {
            get
            {
                return GameSaveFiles.PendingLoad?.retired == true;
            }
        }

        public bool IsPendingLoadEndChestOpened
        {
            get
            {
                return GameSaveFiles.PendingLoad?.endChestOpened == true;
            }
        }

        public bool CanLoadScene(string _sceneName)
        {
            return Application.CanStreamedLevelBeLoaded(_sceneName);
        }

        public void LoadScene(string _sceneName)
        {
            SceneManager.LoadSceneAsync(_sceneName);
        }

        public void LogError(string _message)
        {
            Debug.LogError(_message, m_owner);
        }

        public void LogGameStart(string _message, GameStartLog _log)
        {
            LogKit.GameLogger.Data("Info", "System", _message, _log);
        }

        public void NotifyMissingSave(string _message)
        {
            FeedbackUI.For(m_owner)?.ShowNotification(_message);
        }

        public void OpenLoadPanel()
        {
            m_loadSlotsUI?.Open();
        }
    }
}
