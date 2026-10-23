using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // PausePanel의 하위 화면 호환 브리지. 기존 직렬화 참조(씬 오버라이드 보존)를 같은 GameObject의
    // SoundSettingsView에 전달하고 표시·적용 로직은 SoundSettingsPresenter가 담당한다.
    // 입력은 기존 EventSystem/Input System UI 모듈을 사용한다.
    public sealed class SoundSettingsUI : MonoBehaviour
    {
        [Serializable]
        public sealed class SongRow
        {
            public BgmId id;
            public Button button;
            public TMP_Text label;
        }

        // 아래 직렬화 필드 이름은 기존 UIManager.prefab 직렬화와의 호환을 위해 유지한다.
        [SerializeField] private GameObject panel;
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private Button openButton, closeButton;
        [SerializeField] private Slider masterSlider, bgmSlider, sfxSlider;
        [SerializeField] private TMP_Text masterValue, bgmValue, sfxValue, songStatus;
        [SerializeField] private Toggle muteToggle;
        [SerializeField] private SongRow[] songs;
        [SerializeField] private SoundSettingsView m_view;

        private SoundSettingsPresenter m_presenter;
        private bool m_initialized;

        public bool IsOpen => m_view != null && m_view.IsVisible;

        private void Awake()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            if (m_presenter != null)
            {
                m_presenter.Dispose();
                m_presenter = null;
            }

            m_initialized = false;
        }

        // 비활성 패널도 UIManager에서 초기화해 씬에 저장된 활성 상태에 의존하지 않는다.
        public void Initialize()
        {
            if (m_initialized)
            {
                return;
            }

            if (m_view == null)
            {
                Debug.LogError(
                    "[SoundSettingsUI] Initialize 실패: m_view에 SoundSettingsView 참조가 필요합니다. " +
                    "UIManager.prefab에서 같은 GameObject에 View를 추가하고 연결하세요.", this);
                return;
            }

            m_view.BindReferences(panel, mainPanel, openButton, closeButton, masterSlider, bgmSlider, sfxSlider,
                masterValue, bgmValue, sfxValue, songStatus, muteToggle, ConvertSongRows());

            m_presenter = new SoundSettingsPresenter(m_view);
            if (!m_presenter.Initialize())
            {
                m_presenter = null;
                return;
            }

            m_initialized = true;
            // 기존 초기화와 같이 시작 상태를 명시적으로 숨긴다. View의 지연 Awake가 이후 다시 숨기지 않는다.
            m_presenter.Close();
        }

        /// <summary>
        /// 사운드 설정 화면을 열고 현재 재생 상태와 음량 표시를 동기화한다.
        /// 패널 활성 상태와 BGM 변경 이벤트 구독을 갱신한다.
        /// </summary>
        public void Open()
        {
            if (!EnsureInitialized() || m_presenter == null)
            {
                return;
            }

            m_presenter.Open();
        }

        /// <summary>
        /// 사운드 설정 화면을 닫고 BGM 변경 이벤트 구독을 해제한다.
        /// 사운드 패널을 숨기고 MainPanel을 다시 표시한다.
        /// </summary>
        public void Close()
        {
            if (!EnsureInitialized() || m_presenter == null)
            {
                return;
            }

            m_presenter.Close();
        }

        public bool TryCloseForCancel()
        {
            if (!EnsureInitialized() || m_presenter == null)
            {
                return false;
            }

            return m_presenter.TryCloseForCancel();
        }

        private bool EnsureInitialized()
        {
            if (!m_initialized)
            {
                Initialize();
            }

            return m_initialized;
        }

        private void OnDisable()
        {
            if (m_initialized && m_presenter != null)
            {
                m_presenter.Close();
            }
        }

        private SoundSettingsView.SongRow[] ConvertSongRows()
        {
            if (songs == null)
            {
                return Array.Empty<SoundSettingsView.SongRow>();
            }

            var rows = new SoundSettingsView.SongRow[songs.Length];
            for (int i = 0; i < songs.Length; i++)
            {
                SongRow row = songs[i];
                rows[i] = row == null
                    ? null
                    : new SoundSettingsView.SongRow { id = row.id, button = row.button, label = row.label };
            }

            return rows;
        }
    }
}
