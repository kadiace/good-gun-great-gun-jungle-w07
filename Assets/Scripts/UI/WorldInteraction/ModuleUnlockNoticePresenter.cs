using System;
using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// PlayerModuleUnlocks의 해금 이벤트를 수신하여 View에 알림 표시를 요청하는 Presenter입니다.
    /// </summary>
    public sealed class ModuleUnlockNoticePresenter : IDisposable
    {
        private const string PRESENTER_NAME = "ModuleUnlockNoticePresenter";

        private readonly ModuleUnlockNoticeView m_view;
        private PlayerModuleUnlocks m_unlocks;
        private bool m_isInitialized;
        private bool m_isSubscribed;

        public ModuleUnlockNoticePresenter(ModuleUnlockNoticeView _view, PlayerModuleUnlocks _unlocks)
        {
            m_view = _view;
            m_unlocks = _unlocks;
        }

        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (m_view == null)
            {
                Debug.LogError($"[{PRESENTER_NAME}] Initialize 실패: ModuleUnlockNoticeView 참조가 없습니다.");
                return false;
            }

            Subscribe();
            m_isInitialized = true;
            return true;
        }

        public void BindUnlocks(PlayerModuleUnlocks _unlocks)
        {
            if (m_unlocks == _unlocks)
            {
                return;
            }

            if (m_isSubscribed)
            {
                Unsubscribe();
            }

            m_unlocks = _unlocks;

            if (m_isInitialized)
            {
                Subscribe();
            }
        }

        public void Subscribe()
        {
            if (m_isSubscribed || m_unlocks == null)
            {
                return;
            }

            m_unlocks.ModuleUnlocked -= OnModuleUnlocked;
            m_unlocks.ModuleUnlocked += OnModuleUnlocked;
            m_isSubscribed = true;
        }

        public void Unsubscribe()
        {
            if (!m_isSubscribed || m_unlocks == null)
            {
                return;
            }

            m_unlocks.ModuleUnlocked -= OnModuleUnlocked;
            m_isSubscribed = false;
        }

        public void Dispose()
        {
            Unsubscribe();
            m_unlocks = null;
            m_isInitialized = false;
        }

        private void OnModuleUnlocked(int _id)
        {
            if (m_view == null || m_unlocks == null)
            {
                return;
            }

            string moduleName = m_unlocks.GetModuleName(_id);
            m_view.ShowNotification($"{moduleName} 모듈 해금!");
        }
    }
}
