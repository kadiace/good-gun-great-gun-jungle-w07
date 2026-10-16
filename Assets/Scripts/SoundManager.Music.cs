using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeaponWorkshop
{
    public sealed partial class SoundManager
    {
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
        private bool musicConfigurationValid;
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

        // Main의 저장 복원이 끝난 뒤 한 번만 시작한다. 사망/입장 실패는 진입 전 곡을 복구한다.
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
            if (tutorial || !mainMusicReady || mainMusicInitialized || SceneManager.GetActiveScene().name != "Main") return;
            mainMusicInitialized = true;
            RequestMusic(MainMusicId(clearProgress));
        }

        // 공방을 떠나기 직전 호출한다. 직접 습격 씬을 연 경우에는 진행도 곡을 기본값으로 삼는다.
        public static void CaptureRaidDeparture()
        {
            if (instance == null || IsTutorialMusicContext) return;
            instance.beforeRaidBgm = CurrentBgm != BgmId.None && CurrentBgm != BgmId.Raining && !instance.IsAttackMusic(CurrentBgm)
                ? CurrentBgm : MainMusicId(instance.clearProgress);
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

        // 결과 확정 데이터가 있는 다음날 처리에서만 호출한다. 날짜 증가 자체는 클리어 신호가 아니다.
        public static void CompleteRaidReturn(int progress)
        {
            if (instance == null || IsTutorialMusicContext) return;
            instance.clearProgress = SoundInfo.ClampProgress(progress);
            instance.hasBeforeRaidBgm = false;
            instance.RequestMusic(MainMusicId(instance.clearProgress));
        }

        // 사망·입장 복원 실패·중도 복귀·타이틀 이동은 이 함수로 모은다.
        public static void RestoreBeforeRaidMusic(bool toTitle = false)
        {
            if (instance == null) return;
            if (toTitle)
            {
                instance.hasBeforeRaidBgm = false;
                instance.mainMusicReady = false;
                instance.StopBackgroundMusic();
                return;
            }
            if (IsTutorialMusicContext) return;
            BgmId track = instance.hasBeforeRaidBgm ? instance.beforeRaidBgm : MainMusicId(instance.clearProgress);
            // 저장된 진입 전 곡에 공격곡이 섞여 있어도 Main에서는 진행도로 복구한다.
            if (instance.IsAttackMusic(track)) track = MainMusicId(instance.clearProgress);
            instance.hasBeforeRaidBgm = false;
            instance.mainMusicInitialized = true;
            instance.RequestMusic(track);
        }

        public static void Bgm_Default() => instance?.RequestMusic(BgmId.DayFirst);

        // 선택은 현재 Main에만 적용한다. 저장 진행도와 다음 자동 전환의 규칙은 바꾸지 않는다.
        public static bool TryChooseMainTrack(BgmId id)
        {
            var track = SoundInfo.Track(id);
            if (!CanChooseMainTrack || track == null || !track.selectableInMain || instance.IsAttackMusic(id) ||
                track.unlockRaid < 0 || track.unlockRaid > ClearProgress) return false;
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

        // 일반 곡 슬롯과 습격 조건 슬롯을 하나의 재생 경로로 연결한다.
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
                    RaidBgmPhase.Clear => set.clear,
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

        // 새 흐름에서는 Raining을 받지 않는다. 테이블/곡 오류의 유일한 폴백은 day-first다.
        private void RequestMusic(BgmId id)
        {
            if (!musicConfigurationValid || IsTutorialMusicContext || SceneManager.GetActiveScene().name == "Intro") return;
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
                playingBgm = id;
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
            playingBgm = id;
            musicBaseVolume = volume;
            musicFade = 0f;
            ApplyChannelVolumes();
            backgroundMusicSource.Play();
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

        private void CancelMusicFade()
        {
            if (musicTransition != null) StopCoroutine(musicTransition);
            musicTransition = null;
        }

        // 종료 중에는 복구 재생을 시작하지 않고 공통 종료 경로로 정리한다.
        private void OnApplicationQuit() => RestoreBeforeRaidMusic(true);
    }
}
