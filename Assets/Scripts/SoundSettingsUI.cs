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

        public void Open()
        {
            Sync();
            mainPanel.SetActive(false);
            panel.SetActive(true);
        }

        public void Close()
        {
            panel.SetActive(false);
            mainPanel.SetActive(true);
        }

        private void OnDisable() { if (initialized) Close(); }
        private void OnVolumeChanged(float value) => ApplySettings();
        private void OnMuteChanged(bool value) => ApplySettings();

        // 볼륨/음소거만 전역 설정으로 저장한다. 곡 선택은 해당 Main 방문 동안의 임시 선택이다.
        private void ApplySettings()
        {
            SoundManager.SetVolumes(masterSlider.value, bgmSlider.value, sfxSlider.value, muteToggle.isOn);
            Sync();
        }

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
                bool unlocked = track != null && track.selectableInMain && track.unlockRaid >= 0 && track.unlockRaid <= SoundManager.ClearProgress;
                row.button.interactable = unlocked && SoundManager.CanChooseMainTrack;
                row.label.text = (unlocked ? "" : "잠김 · ") + (track?.label ?? row.id.ToString()) +
                    (SoundManager.IsPlayingTrack(row.id) ? "  ♪" : "");
            }
        }
    }
}
