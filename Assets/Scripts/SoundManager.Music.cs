using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeaponWorkshop
{
    public sealed partial class SoundManager
    {
        /// <summary>
        /// 실제 재생 중인 BGM 식별자가 바뀌었을 때 구독자에게 알린다.
        /// </summary>
        internal static event Action MusicPlaybackChanged;

        [Header("BGM 전환")]
        [SerializeField, Range(.5f, 1f)] private float musicFadeDuration = .75f;

        // 사용자 지정 음원은 인스펙터에만 보관한다. 테이블에는 씬 식별자와 곡 해금 정보만 남긴다.
        [Header("Main 기본곡 / 곡 선택 음원 (필수)")]
        [SerializeField, InspectorName("day-first (기본/폴백)"), Tooltip("기본곡과 모든 오류 폴백에 공통으로 사용하는 day-first 음원")]
        private AudioClip defaultMusicClip;
        [SerializeField, InspectorName("day-second")] private AudioClip daySecondMusicClip;
        // 02 클리어 뒤 선택할 수 있는 day-third 음원도 인스펙터에서 직접 연결한다.
        [SerializeField, InspectorName("day-third")] private AudioClip dayThirdMusicClip;
        [SerializeField, InspectorName("day-fourth")] private AudioClip dayFourthMusicClip;
        [SerializeField, InspectorName("day-last")] private AudioClip dayLastMusicClip;

        [Header("Main 재생 금지 음원 식별 (필수)")]
        [SerializeField, InspectorName("day-attack0 (습격 전용)")] private AudioClip dayAttack0MusicClip;
        [SerializeField, InspectorName("day-attack1 (습격 전용)")] private AudioClip dayAttack1MusicClip;
        [SerializeField, InspectorName("day-attack2 (습격 전용)")] private AudioClip dayAttack2MusicClip;

        // 기존 재생 시점은 유지하고 공방/귀환과 훈련 습격의 음원을 별도로 조절한다.
        [Header("튜토리얼 BGM (필수)")]
        [SerializeField, InspectorName("공방 / 훈련 귀환 BGM")] private AudioClip tutorialRainMusicClip;
        [SerializeField, Range(0f, 1f), InspectorName("튜토리얼 공방 / 귀환 볼륨")] private float tutorialWorkshopMusicVolume = .35f;
        [SerializeField, InspectorName("훈련 습격 입장 BGM")] private AudioClip nightMusicClip;
        [SerializeField, Range(0f, 1f), InspectorName("튜토리얼 훈련 습격 볼륨")] private float tutorialRaidMusicVolume = .35f;

        [SerializeField] private RaidBgmSet[] raidMusicSets = System.Array.Empty<RaidBgmSet>();
        private readonly HashSet<string> _unlockedMainTrackIds = new HashSet<string>(StringComparer.Ordinal);
        private BgmId _pendingRaidRewardMusic;
        private bool _hasPendingRaidRewardMusic;
        // 전환 요청 곡과 실제 재생 곡을 나눠 페이드 도중 Main으로 복귀해도 공격곡을 차단한다.
        private BgmId currentBgm, playingBgm, beforeRaidBgm;
        private bool hasBeforeRaidBgm, mainMusicReady, mainMusicInitialized, previousTutorialContext;
        private string activeRaidScene;
        private int clearProgress;
        private Coroutine musicTransition;
        private float musicBaseVolume, musicFade = 1f;
        private readonly Dictionary<AudioSource, float> sfxBaseVolumes = new Dictionary<AudioSource, float>();
        private readonly List<AudioSource> expiredSfx = new List<AudioSource>();

        // 새 볼륨 계수의 기본값은 모두 1이다. 기존 개별 효과음·BGM의 원래 크기를 그대로 유지한다.
        public static float MasterVolume { get; private set; } = 1f;
        public static float BgmVolume { get; private set; } = 1f;
        public static float SfxVolume { get; private set; } = 1f;
        public static bool Muted { get; private set; }
        public static int ClearProgress => instance != null ? instance.clearProgress : 0;
        public static BgmId CurrentBgm => instance != null ? instance.currentBgm : BgmId.None;
        public static bool CanChooseMainTrack => instance != null && instance.mainMusicReady &&
            SceneManager.GetActiveScene().name == "Main" && !IsTutorialMusicContext &&
            !instance._jungleEasterEggAudioBlocked &&
            !(NightRaidController.Instance != null && NightRaidController.Instance.IsInRaid);

        // 기존 판정과 훈련 이동 중의 정적 상태를 함께 확인해 씬 생성 순서의 틈도 막는다.
        public static bool IsTutorialMusicContext => TutorialManager.TrainingRaidPending || TutorialManager.TrainingReturnPending ||
            SceneManager.GetActiveScene().name == TutorialManager.TrainingScene ||
            (TutorialManager.Instance != null && (TutorialManager.Instance.IsTutorialActive || TutorialManager.Instance.IsGuidedDayZero));

        private void LoadSoundSettings()
        {
            MasterVolume = ReadVolume("Sound.Master");
            BgmVolume = ReadVolume("Sound.BGM");
            SfxVolume = ReadVolume("Sound.SFX");
            Muted = PlayerPrefs.GetInt("Sound.Muted", 0) != 0;
        }

        // 손상된 설정값은 기본 배율 1로 되돌린다.
        private static float ReadVolume(string key)
        {
            float value = PlayerPrefs.GetFloat(key, 1f);
            return float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp01(value);
        }

        public static void SetVolumes(float master, float bgm, float sfx, bool muted)
        {
            MasterVolume = Mathf.Clamp01(master); BgmVolume = Mathf.Clamp01(bgm); SfxVolume = Mathf.Clamp01(sfx); Muted = muted;
            PlayerPrefs.SetFloat("Sound.Master", MasterVolume);
            PlayerPrefs.SetFloat("Sound.BGM", BgmVolume);
            PlayerPrefs.SetFloat("Sound.SFX", SfxVolume);
            PlayerPrefs.SetInt("Sound.Muted", Muted ? 1 : 0);
            PlayerPrefs.Save();
            instance?.ApplyChannelVolumes();
        }

        // AudioSource.volume이 원래 적용하던 0~1 제한을 먼저 적용한 뒤 채널 계수를 곱한다.
        private void TrackSfx(AudioSource source, float originalVolume)
        {
            sfxBaseVolumes[source] = Mathf.Clamp01(originalVolume);
            source.volume = Mathf.Clamp01(originalVolume) * (Muted ? 0f : MasterVolume * SfxVolume);
        }

        private void ApplyChannelVolumes()
        {
            if (backgroundMusicSource != null)
                backgroundMusicSource.volume = Mathf.Clamp01(musicBaseVolume) * musicFade * (Muted ? 0f : MasterVolume * BgmVolume);
            expiredSfx.Clear();
            foreach (var pair in sfxBaseVolumes)
                if (pair.Key == null) expiredSfx.Add(pair.Key);
                else pair.Key.volume = pair.Value * (Muted ? 0f : MasterVolume * SfxVolume);
            foreach (var source in expiredSfx) sfxBaseVolumes.Remove(source);
        }

        /// <summary>
        /// 저장 복원 후 Main 음악을 시작하고 저장된 진행도를 반영한다.
        /// progress와 실패 복귀 여부를 사용하며, 실패 복귀는 진입 전 곡을 복구한다.
        /// </summary>
        public static void InitializeMainMusic(int progress, bool unsuccessfulReturn = false)
        {
            if (instance == null) return;
            instance.clearProgress = SoundInfo.ClampProgress(progress);
            instance.mainMusicReady = true;
            instance.mainMusicInitialized = false;
            if (IsTutorialMusicContext) return;
            if (unsuccessfulReturn) RestoreBeforeRaidMusic();
            else instance.RefreshMusicContext();
        }

        /// <summary>
        /// 튜토리얼·Main 상태에 맞춰 BGM을 한 번 초기화한다.
        /// 발견 음향 차단 중에는 복귀 보상 곡을 예약 상태로 보존하며 재생하지 않는다.
        /// </summary>
        private void RefreshMusicContext()
        {
            bool tutorial = IsTutorialMusicContext;
            if (tutorial != previousTutorialContext)
            {
                previousTutorialContext = tutorial;
                mainMusicInitialized = false;
                if (tutorial)
                {
                    if (subscribedDayManager != null) PlayLegacyMusic(BgmId.Raining, tutorialWorkshopMusicVolume);
                    else StopBackgroundMusic();
                }
            }
            if (_jungleEasterEggAudioBlocked || tutorial || !mainMusicReady || mainMusicInitialized ||
                SceneManager.GetActiveScene().name != "Main") return;
            mainMusicInitialized = true;
            BgmId music = _hasPendingRaidRewardMusic ? _pendingRaidRewardMusic : BgmId.DayFirst;
            _hasPendingRaidRewardMusic = false;
            RequestMusic(music);
        }

        /// <summary>
        /// 공방 출발 시점의 실제 곡을 습격 복귀용으로 기억한다.
        /// 현재 Main 곡을 보관하며, 기록이 없으면 day-first를 복구 기준으로 둔다.
        /// </summary>
        public static void CaptureRaidDeparture()
        {
            if (instance == null || IsTutorialMusicContext) return;
            instance.beforeRaidBgm = CurrentBgm != BgmId.None && CurrentBgm != BgmId.Raining && !instance.IsAttackMusic(CurrentBgm)
                ? CurrentBgm : BgmId.DayFirst;
            instance.hasBeforeRaidBgm = true;
        }

        private void BeginRaidMusic(string scene)
        {
            if (IsTutorialMusicContext || activeRaidScene == scene) return;
            activeRaidScene = scene;
            if (!hasBeforeRaidBgm) CaptureRaidDeparture();
            RequestMusic(RaidMusicId(SoundInfo.Raid(scene)?.number ?? 0, RaidBgmPhase.Travel));
        }

        // 첫 벽 파괴 여부는 씬의 NightRaidController가 소유한다.
        public static void OnRaidFirstWallDestroyed(string scene)
        {
            if (instance == null || IsTutorialMusicContext) return;
            instance.RequestMusic(RaidMusicId(SoundInfo.Raid(scene)?.number ?? 0, RaidBgmPhase.FirstWall));
        }

        /// <summary>
        /// 저장 측에서 확정한 해금 상태를 런타임에 적용하고 보상 곡을 공방 복귀 BGM으로 예약한다.
        /// progress, rewardTrackId, unlockedTrackIds를 반영하며, 발견 음향 차단 중에는 곡 예약을 유지한다.
        /// </summary>
        public static void CompleteRaidReturn(int progress, string rewardTrackId, string[] unlockedTrackIds)
        {
            if (instance == null || IsTutorialMusicContext) return;
            instance.clearProgress = SoundInfo.ClampProgress(progress);
            instance.hasBeforeRaidBgm = false;
            SetMainTrackUnlockState(unlockedTrackIds);
            instance._hasPendingRaidRewardMusic = !string.IsNullOrEmpty(rewardTrackId) &&
                Enum.TryParse(rewardTrackId, out instance._pendingRaidRewardMusic);
            if (instance._jungleEasterEggAudioBlocked) return;
            if (instance.mainMusicReady && SceneManager.GetActiveScene().name == "Main")
            {
                if (!instance.mainMusicInitialized) instance.RefreshMusicContext();
                else
                {
                    BgmId music = instance._hasPendingRaidRewardMusic ? instance._pendingRaidRewardMusic : BgmId.DayFirst;
                    instance._hasPendingRaidRewardMusic = false;
                    instance.RequestMusic(music);
                }
            }
        }

        /// <summary>
        /// 저장 파일의 곡별 해금 ID를 런타임 판정에 적용한다.
        /// unlockedTrackIds의 값으로 런타임 해금 목록 전체를 교체한다.
        /// </summary>
        public static void SetMainTrackUnlockState(string[] unlockedTrackIds)
        {
            if (instance == null) return;
            instance._unlockedMainTrackIds.Clear();
            foreach (string trackId in unlockedTrackIds ?? Array.Empty<string>())
                if (!string.IsNullOrEmpty(trackId)) instance._unlockedMainTrackIds.Add(trackId);
        }

        /// <summary>
        /// Main 곡 선택 해금 여부를 UI와 선택 요청이 함께 사용하도록 판정한다.
        /// id와 저장된 개별 곡 해금 ID를 확인하며 DayFirst는 항상 해금한다.
        /// </summary>
        public static bool IsMainTrackUnlocked(BgmId id)
        {
            SoundData.Track track = SoundInfo.Track(id);
            if (track == null || !track.selectableInMain) return false;
            return id == BgmId.DayFirst || (instance != null && instance._unlockedMainTrackIds.Contains(id.ToString()));
        }

        /// <summary>
        /// 습격 실패 복귀 때 진입 전 곡을 복구하거나 타이틀 이동 때 음악을 정지한다.
        /// toTitle이 true이면 이스터에그 런타임 상태도 초기화하고, 그 외에는 저장한 곡이나 DayFirst를 재생한다.
        /// </summary>
        public static void RestoreBeforeRaidMusic(bool toTitle = false)
        {
            if (instance == null) return;
            if (toTitle)
            {
                instance.ResetJungleEasterEggAudio();
                instance.hasBeforeRaidBgm = false;
                instance.mainMusicReady = false;
                instance.StopBackgroundMusic();
                return;
            }
            if (IsTutorialMusicContext) return;
            BgmId track = instance.hasBeforeRaidBgm ? instance.beforeRaidBgm : BgmId.DayFirst;
            // 저장된 진입 전 곡이 공격곡이면 기본곡으로 복구한다.
            if (instance.IsAttackMusic(track)) track = BgmId.DayFirst;
            instance.hasBeforeRaidBgm = false;
            instance.mainMusicInitialized = true;
            instance.RequestMusic(track);
        }

        public static void Bgm_Default() => instance?.RequestMusic(BgmId.DayFirst);

        /// <summary>
        /// 해금되고 선택 가능한 Main 곡을 요청한다.
        /// id를 검사하며 허용된 곡을 재생 요청했으면 true를 반환한다.
        /// </summary>
        public static bool TryChooseMainTrack(BgmId id)
        {
            var track = SoundInfo.Track(id);
            if (!CanChooseMainTrack || track == null || !IsMainTrackUnlocked(id) || instance.IsAttackMusic(id)) return false;
            instance.RequestMusic(id);
            return true;
        }

        private void EnsureMusicSource()
        {
            if (backgroundMusicSource != null) return;
            backgroundMusicSource = gameObject.AddComponent<AudioSource>();
            backgroundMusicSource.playOnAwake = false;
            backgroundMusicSource.loop = true;
            backgroundMusicSource.spatialBlend = 0f;
            backgroundMusicSource.dopplerLevel = 0f;
        }

        /// <summary>
        /// BGM 식별자에 연결된 인스펙터 AudioClip을 조회한다.
        /// id에 맞는 클립을 반환하고 연결이 없는 조건 곡은 null을 반환한다.
        /// </summary>
        private AudioClip GetMusicClip(BgmId id)
        {
            int cue = (int)id - (int)BgmId.Raid01Travel;
            if (cue >= 0 && cue <= (int)BgmId.Raid04Clear - (int)BgmId.Raid01Travel)
            {
                var set = FindRaidMusic(cue / 3 + 1);
                if (set == null) return null;
                return (RaidBgmPhase)(cue % 3) switch
                {
                    RaidBgmPhase.Travel => set.travel,
                    RaidBgmPhase.FirstWall => set.firstWall,
                    _ => null
                };
            }
            return id switch
            {
                BgmId.DayFirst => defaultMusicClip,
                BgmId.Night => nightMusicClip,
                BgmId.DaySecond => daySecondMusicClip,
                BgmId.DayThird => dayThirdMusicClip,
                BgmId.DayFourth => dayFourthMusicClip,
                BgmId.DayLast => dayLastMusicClip,
                BgmId.DayAttack0 => dayAttack0MusicClip,
                BgmId.DayAttack1 => dayAttack1MusicClip,
                BgmId.DayAttack2 => dayAttack2MusicClip,
                BgmId.Raining => tutorialRainMusicClip,
                _ => null
            };
        }

        /// <summary>
        /// 지정한 곡을 폴백 규칙과 페이드에 따라 재생 요청한다.
        /// id의 클립을 확인하고, 곡이 없으면 day-first를 사용하며, 발견 음향 차단 중에는 요청을 보류한다.
        /// </summary>
        private void RequestMusic(BgmId id)
        {
            if (_jungleEasterEggAudioBlocked) return;
            if (IsTutorialMusicContext || SceneManager.GetActiveScene().name == "Intro") return;
            // 곡 선택·자동 전환·복구 등 어느 호출자든 Main의 공격곡 요청은 재생 전에 거부한다.
            if (SceneManager.GetActiveScene().name == "Main" && IsAttackMusic(id)) return;
            if (id == BgmId.None || id == BgmId.Raining) id = BgmId.DayFirst;
            AudioClip clip = GetMusicClip(id);
            if (clip == null)
            {
                id = BgmId.DayFirst;
                clip = defaultMusicClip;
            }
            if (clip == null) { StopBackgroundMusic(); return; }
            if (currentBgm == id && (musicTransition != null || (backgroundMusicSource != null && backgroundMusicSource.isPlaying))) return;
            // 다른 곡으로 사라지는 중에 원래 곡을 다시 골라도 재생 위치는 유지한다.
            if (backgroundMusicSource != null && backgroundMusicSource.clip == clip && backgroundMusicSource.isPlaying)
            {
                CancelMusicFade();
                currentBgm = id;
                SetPlayingBgm(id);
                musicBaseVolume = UsesRaidVolume(id) ? raidMusicVolume : shopMusicVolume;
                musicFade = 1f;
                ApplyChannelVolumes();
                return;
            }
            CancelMusicFade();
            EnsureMusicSource();
            currentBgm = id;
            musicTransition = StartCoroutine(FadeMusic(clip, id, UsesRaidVolume(id) ? raidMusicVolume : shopMusicVolume));
        }

        // 실제 시간으로 페이드하므로 일시정지 중의 설정 변경에도 반응한다.
        private IEnumerator FadeMusic(AudioClip clip, BgmId id, float volume)
        {
            float halfDuration = Mathf.Clamp(musicFadeDuration, .5f, 1f) * .5f;
            float from = musicFade;
            if (backgroundMusicSource.isPlaying)
                for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.unscaledDeltaTime)
                {
                    musicFade = Mathf.Lerp(from, 0f, elapsed / halfDuration);
                    ApplyChannelVolumes();
                    yield return null;
                }
            // 페이드 중 씬이나 튜토리얼 상태가 바뀌었으면 새 곡을 실제로 시작하지 않는다.
            if (IsTutorialMusicContext || SceneManager.GetActiveScene().name == "Intro" ||
                (SceneManager.GetActiveScene().name == "Main" && (IsAttackMusic(id) || IsAttackClip(clip))))
            {
                StopBackgroundMusic();
                yield break;
            }
            backgroundMusicSource.Stop();
            backgroundMusicSource.clip = clip;
            musicBaseVolume = volume;
            musicFade = 0f;
            ApplyChannelVolumes();
            backgroundMusicSource.Play();
            SetPlayingBgm(id);
            for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.unscaledDeltaTime)
            {
                musicFade = Mathf.Clamp01(elapsed / halfDuration);
                ApplyChannelVolumes();
                yield return null;
            }
            musicFade = 1f;
            ApplyChannelVolumes();
            musicTransition = null;
        }

        /// <summary>
        /// 실제 재생 BGM을 갱신하고 식별자가 바뀐 경우 변경 이벤트를 알린다.
        /// id를 사용해 playingBgm을 바꾸며, 값이 같으면 이벤트를 발생시키지 않는다.
        /// </summary>
        private void SetPlayingBgm(BgmId id)
        {
            if (playingBgm == id) return;
            playingBgm = id;
            MusicPlaybackChanged?.Invoke();
        }

        private void CancelMusicFade()
        {
            if (musicTransition != null) StopCoroutine(musicTransition);
            musicTransition = null;
        }

        // 종료 중에는 복구 재생을 시작하지 않고 공통 종료 경로로 정리한다.
        private void OnApplicationQuit() => RestoreBeforeRaidMusic(true);
    }
}
