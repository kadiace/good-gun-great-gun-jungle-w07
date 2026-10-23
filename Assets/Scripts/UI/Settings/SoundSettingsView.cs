using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    /// <summary>
    /// 사운드 설정 화면(PausePanel 하위 SoundPanel)의 직렬화 참조 보관, 슬라이더·음소거 토글·열기·닫기·곡 버튼 입력의
    /// C# 이벤트 전달, 음량·곡 상태 표시와 시작 숨김 초기화만 담당하는 View입니다.
    /// SoundManager 같은 도메인 객체와 다른 화면을 참조하지 않습니다.
    /// </summary>
    public sealed class SoundSettingsView : MonoBehaviour
    {
        private const string SCREEN_NAME = "SoundSettingsView";

        [Serializable]
        public sealed class SongRow
        {
            public BgmId id;
            public Button button;
            public TMP_Text label;
        }

        [SerializeField] private GameObject m_panel;
        [SerializeField] private GameObject m_mainPanel;
        [SerializeField] private Button m_openButton;
        [SerializeField] private Button m_closeButton;
        [SerializeField] private Slider m_masterSlider;
        [SerializeField] private Slider m_bgmSlider;
        [SerializeField] private Slider m_sfxSlider;
        [SerializeField] private TMP_Text m_masterValue;
        [SerializeField] private TMP_Text m_bgmValue;
        [SerializeField] private TMP_Text m_sfxValue;
        [SerializeField] private TMP_Text m_songStatus;
        [SerializeField] private Toggle m_muteToggle;
        [SerializeField] private SongRow[] m_songs;

        private readonly List<SongRow> m_boundSongRows = new List<SongRow>();
        private readonly List<UnityAction> m_boundSongHandlers = new List<UnityAction>();

        private GameObject m_savedFocus;
        private bool m_isInitialized;
        private bool m_hasAppliedInitialHiddenState;
        private bool m_hasLoggedReferenceError;

        public event Action OpenRequested;
        public event Action CloseRequested;
        public event Action<float> MasterVolumeChanged;
        public event Action<float> BgmVolumeChanged;
        public event Action<float> SfxVolumeChanged;
        public event Action<bool> MuteChanged;
        public event Action<BgmId> SongSelected;

        public bool IsVisible
        {
            get
            {
                return m_panel != null && m_panel.activeInHierarchy;
            }
        }

        public bool IsPanelSelfActive
        {
            get
            {
                return m_panel != null && m_panel.activeSelf;
            }
        }

        public Selectable OpenButton
        {
            get
            {
                return m_openButton;
            }
        }

        public IReadOnlyList<SongRow> SongRows
        {
            get
            {
                return m_songs;
            }
        }

        public float MasterVolume
        {
            get
            {
                return m_masterSlider != null ? m_masterSlider.value : 0f;
            }
        }

        public float BgmVolume
        {
            get
            {
                return m_bgmSlider != null ? m_bgmSlider.value : 0f;
            }
        }

        public float SfxVolume
        {
            get
            {
                return m_sfxSlider != null ? m_sfxSlider.value : 0f;
            }
        }

        public bool IsMuted
        {
            get
            {
                return m_muteToggle != null && m_muteToggle.isOn;
            }
        }

        private void Awake()
        {
            ApplyInitialHiddenState();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        /// <summary>
        /// 브리지가 보유한 실효 참조(프리팹 값과 씬 오버라이드 반영)를 View 직렬화 참조로 전달합니다.
        /// Initialize 전에 한 번 호출해야 하며, 이 값이 ValidateReferences와 바인딩 대상이 됩니다.
        /// </summary>
        public void BindReferences(GameObject _panel, GameObject _mainPanel, Button _openButton, Button _closeButton,
            Slider _masterSlider, Slider _bgmSlider, Slider _sfxSlider, TMP_Text _masterValue, TMP_Text _bgmValue,
            TMP_Text _sfxValue, TMP_Text _songStatus, Toggle _muteToggle, SongRow[] _songs)
        {
            m_panel = _panel;
            m_mainPanel = _mainPanel;
            m_openButton = _openButton;
            m_closeButton = _closeButton;
            m_masterSlider = _masterSlider;
            m_bgmSlider = _bgmSlider;
            m_sfxSlider = _sfxSlider;
            m_masterValue = _masterValue;
            m_bgmValue = _bgmValue;
            m_sfxValue = _sfxValue;
            m_songStatus = _songStatus;
            m_muteToggle = _muteToggle;
            m_songs = _songs;
        }

        /// <summary>
        /// 시작 숨김 상태를 적용하고 필수 직렬화 참조를 검증한 뒤 슬라이더 범위와 버튼·토글·곡 리스너를 연결합니다.
        /// 초기 숨김은 Awake/Initialize 중 먼저 실행된 한 번만 적용하며, 중복 호출은 초기화된 상태를 유지합니다.
        /// </summary>
        public bool Initialize()
        {
            ApplyInitialHiddenState();

            if (m_isInitialized)
            {
                return true;
            }

            if (!ValidateReferences())
            {
                return false;
            }

            ConfigureSliders();
            BindButtons();
            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// 등록한 Unity UI 리스너를 해제합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            UnbindButtons();
            m_isInitialized = false;
        }

        /// <summary>
        /// 메인 패널을 숨기고 사운드 설정 패널을 표시합니다.
        /// </summary>
        public void Show()
        {
            if (!m_isInitialized && !Initialize())
            {
                return;
            }

            UIRoot.SaveFocus(ref m_savedFocus, transform);

            if (m_mainPanel != null)
            {
                m_mainPanel.SetActive(false);
            }

            if (m_panel != null)
            {
                m_panel.SetActive(true);
            }

            Selectable defaultSelectable = m_masterSlider != null ? (Selectable)m_masterSlider : m_closeButton;
            UIRoot.SetFocus(defaultSelectable, m_closeButton != null ? new Selectable[] { m_closeButton } : null);
        }

        /// <summary>
        /// 사운드 설정 패널을 숨기고 메인 패널을 다시 표시합니다.
        /// </summary>
        public void Hide()
        {
            if (m_panel != null)
            {
                m_panel.SetActive(false);
            }

            if (m_mainPanel != null)
            {
                m_mainPanel.SetActive(true);
            }

            UIRoot.RestoreFocus(ref m_savedFocus, m_openButton);
        }

        /// <summary>
        /// 슬라이더 값과 음량 백분율 텍스트를 알림 이벤트 없이 갱신합니다.
        /// </summary>
        public void SetVolumeDisplays(float _master, float _bgm, float _sfx)
        {
            if (m_masterSlider != null)
            {
                m_masterSlider.SetValueWithoutNotify(_master);
            }

            if (m_bgmSlider != null)
            {
                m_bgmSlider.SetValueWithoutNotify(_bgm);
            }

            if (m_sfxSlider != null)
            {
                m_sfxSlider.SetValueWithoutNotify(_sfx);
            }

            if (m_masterValue != null)
            {
                m_masterValue.text = _master.ToString("P0");
            }

            if (m_bgmValue != null)
            {
                m_bgmValue.text = _bgm.ToString("P0");
            }

            if (m_sfxValue != null)
            {
                m_sfxValue.text = _sfx.ToString("P0");
            }
        }

        /// <summary>
        /// 음소거 토글 표시를 알림 이벤트 없이 갱신합니다.
        /// </summary>
        public void SetMuteDisplay(bool _muted)
        {
            if (m_muteToggle != null)
            {
                m_muteToggle.SetIsOnWithoutNotify(_muted);
            }
        }

        /// <summary>
        /// 곡 선택 안내 문장을 갱신합니다.
        /// </summary>
        public void SetSongStatus(string _text)
        {
            if (m_songStatus != null)
            {
                m_songStatus.text = _text;
            }
        }

        /// <summary>
        /// 곡 행의 문장과 선택 가능 상태를 갱신합니다.
        /// </summary>
        public void SetSongRowState(SongRow _row, string _label, bool _interactable)
        {
            if (_row == null)
            {
                return;
            }

            if (_row.button != null)
            {
                _row.button.interactable = _interactable;
            }

            if (_row.label != null)
            {
                _row.label.text = _label;
            }
        }

        private void ApplyInitialHiddenState()
        {
            if (m_hasAppliedInitialHiddenState)
            {
                return;
            }

            m_hasAppliedInitialHiddenState = true;
            Hide();
        }

        private bool ValidateReferences()
        {
            bool hasSongs = m_songs != null;
            int invalidSongIndex = -1;
            string invalidSongMember = null;
            if (hasSongs)
            {
                for (int i = 0; i < m_songs.Length; i++)
                {
                    SongRow row = m_songs[i];
                    if (row == null)
                    {
                        invalidSongIndex = i;
                        invalidSongMember = "row";
                        break;
                    }

                    if (row.button == null)
                    {
                        invalidSongIndex = i;
                        invalidSongMember = "button";
                        break;
                    }

                    if (row.label == null)
                    {
                        invalidSongIndex = i;
                        invalidSongMember = "label";
                        break;
                    }
                }
            }

            bool isValid = m_panel != null &&
                           m_mainPanel != null &&
                           m_openButton != null &&
                           m_closeButton != null &&
                           m_masterSlider != null &&
                           m_bgmSlider != null &&
                           m_sfxSlider != null &&
                           m_masterValue != null &&
                           m_bgmValue != null &&
                           m_sfxValue != null &&
                           m_songStatus != null &&
                           m_muteToggle != null &&
                           hasSongs &&
                           invalidSongIndex < 0;

            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;

                string songState = "null";
                if (hasSongs)
                {
                    songState = invalidSongIndex >= 0
                        ? $"{m_songs.Length} rows, {invalidSongMember} null at index {invalidSongIndex}"
                        : m_songs.Length.ToString();
                }

                Debug.LogError(
                    $"[{SCREEN_NAME}] Initialize 실패: 필수 참조 누락 " +
                    $"(m_panel={m_panel != null}, m_mainPanel={m_mainPanel != null}, " +
                    $"m_openButton={m_openButton != null}, m_closeButton={m_closeButton != null}, " +
                    $"m_masterSlider={m_masterSlider != null}, m_bgmSlider={m_bgmSlider != null}, " +
                    $"m_sfxSlider={m_sfxSlider != null}, m_masterValue={m_masterValue != null}, " +
                    $"m_bgmValue={m_bgmValue != null}, m_sfxValue={m_sfxValue != null}, " +
                    $"m_songStatus={m_songStatus != null}, m_muteToggle={m_muteToggle != null}, " +
                    $"m_songs={songState})",
                    this);
            }

            return isValid;
        }

        private void ConfigureSliders()
        {
            Slider[] sliders = { m_masterSlider, m_bgmSlider, m_sfxSlider };
            foreach (Slider slider in sliders)
            {
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.wholeNumbers = false;
            }
        }

        private void BindButtons()
        {
            m_masterSlider.onValueChanged.RemoveListener(OnMasterSliderChanged);
            m_masterSlider.onValueChanged.AddListener(OnMasterSliderChanged);
            m_bgmSlider.onValueChanged.RemoveListener(OnBgmSliderChanged);
            m_bgmSlider.onValueChanged.AddListener(OnBgmSliderChanged);
            m_sfxSlider.onValueChanged.RemoveListener(OnSfxSliderChanged);
            m_sfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);
            m_muteToggle.onValueChanged.RemoveListener(OnMuteToggleChanged);
            m_muteToggle.onValueChanged.AddListener(OnMuteToggleChanged);
            m_openButton.onClick.RemoveListener(OnOpenClicked);
            m_openButton.onClick.AddListener(OnOpenClicked);
            m_closeButton.onClick.RemoveListener(OnCloseClicked);
            m_closeButton.onClick.AddListener(OnCloseClicked);

            for (int i = 0; i < m_songs.Length; i++)
            {
                SongRow row = m_songs[i];
                if (row == null || row.button == null)
                {
                    continue;
                }

                BgmId id = row.id;
                UnityAction handler = () => SongSelected?.Invoke(id);
                m_boundSongRows.Add(row);
                m_boundSongHandlers.Add(handler);
                row.button.onClick.AddListener(handler);
            }
        }

        private void UnbindButtons()
        {
            if (m_masterSlider != null)
            {
                m_masterSlider.onValueChanged.RemoveListener(OnMasterSliderChanged);
            }

            if (m_bgmSlider != null)
            {
                m_bgmSlider.onValueChanged.RemoveListener(OnBgmSliderChanged);
            }

            if (m_sfxSlider != null)
            {
                m_sfxSlider.onValueChanged.RemoveListener(OnSfxSliderChanged);
            }

            if (m_muteToggle != null)
            {
                m_muteToggle.onValueChanged.RemoveListener(OnMuteToggleChanged);
            }

            if (m_openButton != null)
            {
                m_openButton.onClick.RemoveListener(OnOpenClicked);
            }

            if (m_closeButton != null)
            {
                m_closeButton.onClick.RemoveListener(OnCloseClicked);
            }

            for (int i = 0; i < m_boundSongRows.Count && i < m_boundSongHandlers.Count; i++)
            {
                SongRow row = m_boundSongRows[i];
                if (row != null && row.button != null)
                {
                    row.button.onClick.RemoveListener(m_boundSongHandlers[i]);
                }
            }

            m_boundSongRows.Clear();
            m_boundSongHandlers.Clear();
        }

        private void OnOpenClicked()
        {
            OpenRequested?.Invoke();
        }

        private void OnCloseClicked()
        {
            CloseRequested?.Invoke();
        }

        private void OnMasterSliderChanged(float _value)
        {
            MasterVolumeChanged?.Invoke(_value);
        }

        private void OnBgmSliderChanged(float _value)
        {
            BgmVolumeChanged?.Invoke(_value);
        }

        private void OnSfxSliderChanged(float _value)
        {
            SfxVolumeChanged?.Invoke(_value);
        }

        private void OnMuteToggleChanged(bool _value)
        {
            MuteChanged?.Invoke(_value);
        }
    }
}
