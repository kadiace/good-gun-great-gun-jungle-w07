using System;
using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 구체적인 SoundSettingsView와 SoundManager 도메인을 연결해 음량·음소거 즉시 적용,
    /// Main 방문 중 곡 선택(잠금·재생 표시·임시 선택), 열기·닫기·취소 닫기 포커스 복원을 담당하는 Presenter입니다.
    /// </summary>
    public sealed class SoundSettingsPresenter : IDisposable
    {
        private const string PRESENTER_NAME = "SoundSettingsPresenter";

        private readonly SoundSettingsView m_view;

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
                return m_view != null && m_view.IsVisible;
            }
        }

        public SoundSettingsPresenter(SoundSettingsView _view)
        {
            m_view = _view;
        }

        /// <summary>
        /// View를 초기화하고 표시 입력 이벤트를 구독합니다. 중복 호출에 안전합니다.
        /// </summary>
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
                        $"[{PRESENTER_NAME}] Initialize 실패: SoundSettingsView 참조가 없습니다.");
                }

                return false;
            }

            if (!m_view.Initialize())
            {
                return false;
            }

            m_view.OpenRequested -= OnOpenRequested;
            m_view.OpenRequested += OnOpenRequested;
            m_view.CloseRequested -= OnCloseRequested;
            m_view.CloseRequested += OnCloseRequested;
            m_view.MasterVolumeChanged -= OnMasterVolumeChanged;
            m_view.MasterVolumeChanged += OnMasterVolumeChanged;
            m_view.BgmVolumeChanged -= OnBgmVolumeChanged;
            m_view.BgmVolumeChanged += OnBgmVolumeChanged;
            m_view.SfxVolumeChanged -= OnSfxVolumeChanged;
            m_view.SfxVolumeChanged += OnSfxVolumeChanged;
            m_view.MuteChanged -= OnMuteChanged;
            m_view.MuteChanged += OnMuteChanged;
            m_view.SongSelected -= OnSongSelected;
            m_view.SongSelected += OnSongSelected;

            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// View 이벤트 구독과 BGM 재생 변경 구독, View 리스너를 해제합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            if (m_view != null)
            {
                m_view.OpenRequested -= OnOpenRequested;
                m_view.CloseRequested -= OnCloseRequested;
                m_view.MasterVolumeChanged -= OnMasterVolumeChanged;
                m_view.BgmVolumeChanged -= OnBgmVolumeChanged;
                m_view.SfxVolumeChanged -= OnSfxVolumeChanged;
                m_view.MuteChanged -= OnMuteChanged;
                m_view.SongSelected -= OnSongSelected;
                m_view.Dispose();
            }

            UnsubscribeMusicPlayback();
            m_isInitialized = false;
        }

        /// <summary>
        /// 사운드 설정 화면을 열고 BGM 재생 변경 구독을 갱신한 뒤 현재 상태 표시를 동기화합니다.
        /// </summary>
        public void Open()
        {
            if (!m_isInitialized && !Initialize())
            {
                return;
            }

            SoundManager.MusicPlaybackChanged -= OnMusicPlaybackChanged;
            SoundManager.MusicPlaybackChanged += OnMusicPlaybackChanged;
            m_view.Show();
            Sync();
        }

        /// <summary>
        /// 사운드 설정 화면을 닫고 BGM 재생 변경 구독을 해제하며 메인 패널을 복원합니다.
        /// </summary>
        public void Close()
        {
            UnsubscribeMusicPlayback();

            if (m_view != null)
            {
                m_view.Hide();
            }
        }

        /// <summary>
        /// 취소(ESC) 경로 닫기. 열려 있을 때만 닫고 열기 버튼 또는 일시정지 기본 버튼으로 포커스를 복원합니다.
        /// </summary>
        public bool TryCloseForCancel()
        {
            if (!IsOpen)
            {
                return false;
            }

            Close();
            FocusOpenerOrPause();
            return true;
        }

        private void OnOpenRequested()
        {
            Open();
        }

        private void OnCloseRequested()
        {
            Close();
        }

        private void OnMasterVolumeChanged(float _value)
        {
            ApplySettings();
        }

        private void OnBgmVolumeChanged(float _value)
        {
            ApplySettings();
        }

        private void OnSfxVolumeChanged(float _value)
        {
            ApplySettings();
        }

        private void OnMuteChanged(bool _value)
        {
            ApplySettings();
        }

        private void OnSongSelected(BgmId _id)
        {
            // 선택 요청은 도메인이 잠금·상태를 판정하고, 결과와 무관하게 표시를 다시 동기화한다.
            SoundManager.TryChooseMainTrack(_id);
            Sync();
        }

        private void OnMusicPlaybackChanged()
        {
            if (m_view != null && m_view.IsPanelSelfActive)
            {
                Sync();
            }
        }

        private void UnsubscribeMusicPlayback()
        {
            SoundManager.MusicPlaybackChanged -= OnMusicPlaybackChanged;
        }

        // 볼륨/음소거만 전역 설정으로 저장한다. 곡 선택은 해당 Main 방문 동안의 임시 선택이다.
        private void ApplySettings()
        {
            SoundManager.SetVolumes(m_view.MasterVolume, m_view.BgmVolume, m_view.SfxVolume, m_view.IsMuted);
            Sync();
        }

        /// <summary>
        /// 볼륨·음소거·곡 잠금과 실제 재생 곡 표시를 View에 반영한다.
        /// SoundManager와 테이블 상태를 읽어 현재 행 표시를 갱신한다.
        /// </summary>
        private void Sync()
        {
            m_view.SetVolumeDisplays(SoundManager.MasterVolume, SoundManager.BgmVolume, SoundManager.SfxVolume);
            m_view.SetMuteDisplay(SoundManager.Muted);
            m_view.SetSongStatus(SoundManager.CanChooseMainTrack
                ? "곡 선택 · Main에서만 적용"
                : "곡 선택 · Main에서 선택할 수 있습니다");

            IReadOnlyList<SoundSettingsView.SongRow> rows = m_view.SongRows;
            if (rows == null)
            {
                return;
            }

            foreach (SoundSettingsView.SongRow row in rows)
            {
                if (row == null)
                {
                    continue;
                }

                var track = SoundInfo.Track(row.id);
                // 목록 표시와 실제 선택 모두 테이블의 Main 선택 허용 여부를 따른다.
                bool unlocked = track != null && SoundManager.IsMainTrackUnlocked(row.id);
                string label = (unlocked ? "" : "잠김 · ") + (track?.label ?? row.id.ToString()) +
                    (SoundManager.IsPlayingTrack(row.id) ? "  ♪" : "");
                m_view.SetSongRowState(row, label, unlocked && SoundManager.CanChooseMainTrack);
            }
        }

        private void FocusOpenerOrPause()
        {
            if (TryFocus(m_view.OpenButton != null ? m_view.OpenButton.gameObject : null))
            {
                return;
            }

            PauseView pause = GameManager.Instance != null && GameManager.Instance.UI != null
                ? GameManager.Instance.UI.PauseView
                : null;
            if (pause != null && pause.DefaultSelectable != null)
            {
                TryFocus(pause.DefaultSelectable.gameObject);
            }
        }

        private static bool TryFocus(GameObject _target)
        {
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem == null || _target == null || !_target.activeInHierarchy)
            {
                return false;
            }

            var selectable = _target.GetComponent<UnityEngine.UI.Selectable>();
            if (selectable == null || !selectable.isActiveAndEnabled || !selectable.IsInteractable())
            {
                return false;
            }

            eventSystem.SetSelectedGameObject(null);
            eventSystem.SetSelectedGameObject(_target);
            return true;
        }
    }
}
