using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // PausePanel의 하위 화면. 입력은 기존 EventSystem/Input System UI 모듈을 사용한다.
    public sealed class SoundSettingsUI : MonoBehaviour
    {
        [Serializable]
        public sealed class SongRow
        {
            public BgmId id;
            public Button button;
            public TMP_Text label;
        }

        [SerializeField] private GameObject panel;
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private Button openButton, closeButton;
        [SerializeField] private Slider masterSlider, bgmSlider, sfxSlider;
        [SerializeField] private TMP_Text masterValue, bgmValue, sfxValue, songStatus;
        [SerializeField] private Toggle muteToggle;
        [SerializeField] private SongRow[] songs;
        private bool initialized;

        private void Awake() => Initialize();

        // 비활성 패널도 UIManager에서 초기화해 씬에 저장된 활성 상태에 의존하지 않는다.
        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            foreach (var slider in new[] { masterSlider, bgmSlider, sfxSlider })
            {
                slider.minValue = 0f; slider.maxValue = 1f; slider.wholeNumbers = false;
                slider.onValueChanged.AddListener(OnVolumeChanged);
            }
            muteToggle.onValueChanged.AddListener(OnMuteChanged);
            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            foreach (var row in songs)
            {
                BgmId id = row.id;
                row.button.onClick.AddListener(() => { SoundManager.TryChooseMainTrack(id); Sync(); });
            }
            Close();
        }

        /// <summary>
        /// 사운드 설정 화면을 열고 현재 재생 상태와 음량 표시를 동기화한다.
        /// 패널 활성 상태와 BGM 변경 이벤트 구독을 갱신한다.
        /// </summary>
        public void Open()
        {
            mainPanel.SetActive(false);
            panel.SetActive(true);
            SoundManager.MusicPlaybackChanged -= OnMusicPlaybackChanged;
            SoundManager.MusicPlaybackChanged += OnMusicPlaybackChanged;
            Sync();
        }

        /// <summary>
        /// 사운드 설정 화면을 닫고 BGM 변경 이벤트 구독을 해제한다.
        /// 사운드 패널을 숨기고 MainPanel을 다시 표시한다.
        /// </summary>
        public void Close()
        {
            SoundManager.MusicPlaybackChanged -= OnMusicPlaybackChanged;
            panel.SetActive(false);
            mainPanel.SetActive(true);
        }

        private void OnDisable() { if (initialized) Close(); }
        private void OnVolumeChanged(float value) => ApplySettings();
        private void OnMuteChanged(bool value) => ApplySettings();

        /// <summary>
        /// 열린 사운드 설정 화면의 곡 표시를 실제 재생 BGM에 맞춰 갱신한다.
        /// 패널이 표시 중일 때만 UI 표시 상태를 동기화한다.
        /// </summary>
        private void OnMusicPlaybackChanged()
        {
            if (panel.activeSelf) Sync();
        }

        // 볼륨/음소거만 전역 설정으로 저장한다. 곡 선택은 해당 Main 방문 동안의 임시 선택이다.
        private void ApplySettings()
        {
            SoundManager.SetVolumes(masterSlider.value, bgmSlider.value, sfxSlider.value, muteToggle.isOn);
            Sync();
        }

        /// <summary>
        /// 볼륨·음소거·곡 잠금과 실제 재생 곡 표시를 UI에 반영한다.
        /// SoundManager와 테이블 상태를 읽어 현재 행 표시를 갱신한다.
        /// </summary>
        private void Sync()
        {
            masterSlider.SetValueWithoutNotify(SoundManager.MasterVolume);
            bgmSlider.SetValueWithoutNotify(SoundManager.BgmVolume);
            sfxSlider.SetValueWithoutNotify(SoundManager.SfxVolume);
            muteToggle.SetIsOnWithoutNotify(SoundManager.Muted);
            masterValue.text = SoundManager.MasterVolume.ToString("P0");
            bgmValue.text = SoundManager.BgmVolume.ToString("P0");
            sfxValue.text = SoundManager.SfxVolume.ToString("P0");
            songStatus.text = SoundManager.CanChooseMainTrack ? "곡 선택 · Main에서만 적용" : "곡 선택 · Main에서 선택할 수 있습니다";
            foreach (var row in songs)
            {
                var track = SoundInfo.Track(row.id);
                // 목록 표시와 실제 선택 모두 테이블의 Main 선택 허용 여부를 따른다.
                bool unlocked = track != null && SoundManager.IsMainTrackUnlocked(row.id);
                row.button.interactable = unlocked && SoundManager.CanChooseMainTrack;
                row.label.text = (unlocked ? "" : "잠김 · ") + (track?.label ?? row.id.ToString()) +
                    (SoundManager.IsPlayingTrack(row.id) ? "  ♪" : "");
            }
        }
    }
}
