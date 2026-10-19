using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    public sealed partial class SoundManager : MonoBehaviour
    {
        private const string ResourcePath = "Sound/SFX/";
        private static SoundManager instance;
        internal bool IsActiveInstance => instance == this && isActiveAndEnabled;
        internal static SoundManager ActiveInstance => instance;

        [Header("Sound Playback Limits")]
        [SerializeField, Min(0f), Tooltip("Minimum time between basic shot sound plays. 0.05 seconds allows at most 20 plays per second.")]
        private float basicShotMinimumInterval = 0.05f;
        [SerializeField, Min(0f), Tooltip("Minimum time between charged shot sound plays.")]
        private float chargedShotMinimumInterval = 0.05f;
        [SerializeField, Min(0f), Tooltip("Minimum time between shotgun sound plays.")]
        private float shotgunMinimumInterval = 0.05f;
        [SerializeField, Min(0f), Tooltip("Minimum time between water shot sound plays.")]
        private float waterShotMinimumInterval = 0.05f;
        [SerializeField, Min(0f), Tooltip("Minimum time between explosion sound plays. 0.05 seconds allows at most 20 plays per second.")]
        private float explosionMinimumInterval = 0.05f;
        [SerializeField, Min(0f), Tooltip("Minimum time between building collapse sound plays.")]
        private float collapseMinimumInterval = 0.05f;
        [SerializeField, Min(0f), Tooltip("Minimum time between helicopter attack sound plays.")]
        private float helicopterAttackMinimumInterval = 0.05f;
        [Header("Sound Volume Multipliers")]
        [SerializeField, Min(0f), Tooltip("Multiplier for player weapon firing sounds.")]
        private float playerWeaponSoundVolumeMultiplier = 0.7f;
        [SerializeField, Min(0f), Tooltip("Multiplier for enemy sound effects.")]
        private float enemySoundVolumeMultiplier = 1.25f;
        [Header("Raid Sounds")]
        [SerializeField, Range(0f, 1f)] private float tankMoveVolume = 0.03f;
        [SerializeField, Range(0f, 1f)] private float helicopterAmbientVolume = 0.4f;
        [SerializeField, Min(0.1f)] private float helicopterAmbientMinDistance = 5f;
        [SerializeField, Min(0.2f)] private float helicopterAmbientMaxDistance = 35f;
        [SerializeField, Range(0f, 1f)] private float helicopterAttackVolume = 0.5f;
        [Header("Background Music")]
        [SerializeField, Range(0f, 1f)] private float shopMusicVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float raidMusicVolume = 0.35f;
        [Header("UI Sounds")]
        [SerializeField, Range(0f, 1f)] private float uiClickVolume = 0.65f;
        [SerializeField, Range(0f, 1f)] private float spawnButtonClickVolume = 0.7f;

        [Header("작업대 효과음")]
        [SerializeField, InspectorName("작업대 효과음 설정 컴포넌트")] private WorkbenchSoundSettings workbenchSounds;

        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, double> lastSoundPlaybackTimes = new Dictionary<string, double>();
        private readonly Dictionary<Button, UnityAction> buttonClickListeners = new Dictionary<Button, UnityAction>();
        private readonly Dictionary<Transform, AudioSource> chargeLoops = new Dictionary<Transform, AudioSource>();
        private readonly Dictionary<GravityField, AudioSource> gravityLoops = new Dictionary<GravityField, AudioSource>();
        private readonly Dictionary<Transform, AudioSource> helicopterLoops = new Dictionary<Transform, AudioSource>();
        private readonly Dictionary<Transform, AudioSource> tankMoveLoops = new Dictionary<Transform, AudioSource>();
        private AudioSource backgroundMusicSource;
        private AudioSource uiSoundSource;
        private _DayManager subscribedDayManager;
        private float nextButtonScanTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            instance = null;
        }

        /// <summary>
        /// 지정한 버튼에 클릭 효과음을 연결한다.
        /// button과 clipName을 사용하며, 매니저 인스턴스가 있으면 기존 연결을 교체한다.
        /// </summary>
        public static void RegisterButtonSound(Button button, string clipName)
        {
            if (instance != null) instance.RegisterButtonSoundInternal(button, clipName);
        }

        /// <summary>
        /// 지정한 위치에서 종이 넘기는 효과음을 재생한다.
        /// position을 사용하며, 매니저 인스턴스가 있을 때 작업대 설명서 음원 설정을 재생한다.
        /// </summary>
        public static void PlayPaperSound(Vector3 position)
        {
            if (instance != null) instance.PlayWorkbenchSound(WorkbenchSoundCue.Paper, position);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureInstance()
        {
            SoundManager manager = FindFirstObjectByType<SoundManager>();
            if (manager == null)
            {
                // 프리팹에 페이드 시간과 오류 복구용 기본곡을 보관한다.
                var prefab = Resources.Load<GameObject>(nameof(SoundManager));
                if (prefab != null) manager = Instantiate(prefab).GetComponent<SoundManager>();
                if (manager == null) manager = new GameObject(nameof(SoundManager)).AddComponent<SoundManager>();
            }

            manager.Initialize();
        }

        private void Awake() => Initialize();

        private void OnEnable()
        {
            if (instance == this)
            {
                Subscribe();
                AttachDayManager();
            }
        }

        private void OnDisable()
        {
            if (instance != this) return;
            Unsubscribe();
            StopAllLoops();
            StopBackgroundMusic();
            DetachDayManager();
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            Unsubscribe();
            StopAllLoops();
            StopBackgroundMusic();
            DetachDayManager();
            foreach (KeyValuePair<Button, UnityAction> entry in buttonClickListeners)
                if (entry.Key != null) entry.Key.onClick.RemoveListener(entry.Value);
            buttonClickListeners.Clear();
            if (instance == this) instance = null;
        }

        private void Update()
        {
            // 튜토리얼 종료 신호와 씬 복원 순서가 달라도 새 곡을 너무 일찍 재생하지 않는다.
            RefreshMusicContext();
            if (Time.unscaledTime < nextButtonScanTime) return;
            nextButtonScanTime = Time.unscaledTime + 1f;
            AttachButtonClickSounds();
            ApplyChannelVolumes();
        }

        /// <summary>
        /// 이 오브젝트를 유일한 사운드 매니저로 등록하고 사운드 이벤트를 구독한다.
        /// 이미 다른 인스턴스가 있으면 이 오브젝트를 제거하고, 선택된 인스턴스는 씬 이동 뒤에도 유지한다.
        /// </summary>
        private void Initialize()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            bool firstInitialization = instance != this;
            instance = this;
            if (firstInitialization)
            {
                if (workbenchSounds == null) workbenchSounds = GetComponent<WorkbenchSoundSettings>();
                LoadSoundSettings();
                // 필수 음원 누락은 명확히 알리고 새 자동 재생을 차단한다.
                if (!ValidateBgmConfiguration(out string error)) Debug.LogError(error, this);
            }
            DontDestroyOnLoad(gameObject);
            Subscribe();
        }

        /// <summary>
        /// 게임 내 사운드 이벤트와 씬 로드 이벤트를 처리 메서드에 연결한다.
        /// 중복 연결을 막기 위해 같은 이벤트의 기존 연결을 먼저 제거한다.
        /// </summary>
        private void Subscribe()
        {
            WorkshopSoundEffects.BlockInstalled -= OnBlockInstalled;
            WorkshopSoundEffects.BlockRemoved -= OnBlockRemoved;
            WorkshopSoundEffects.AssemblyCompleted -= OnAssemblyCompleted;
            WorkshopSoundEffects.WeaponEquipped -= OnWeaponEquipped;
            WorkshopSoundEffects.WeaponDropped -= OnWeaponDropped;
            WorkshopSoundEffects.WeaponDisplayed -= OnWeaponDisplayed;
            WorkshopSoundEffects.WeaponPlacedOnTable -= OnWeaponPlacedOnTable;
            WorkshopSoundEffects.ShopShutterMoved -= OnShopShutterMoved;
            WorkshopSoundEffects.BusinessStarted -= OnBusinessStarted;
            WorkshopSoundEffects.BusinessEnded -= OnBusinessEnded;
            WorkshopSoundEffects.NightRaidStarted -= OnNightRaidStarted;
            WorkshopSoundEffects.BlockFragmented -= OnBlockFragmented;
            WorkshopSoundEffects.TankAppeared -= OnTankAppeared;
            WorkshopSoundEffects.TankLanded -= OnTankLanded;
            WorkshopSoundEffects.TankAttacked -= OnTankAttacked;
            WorkshopSoundEffects.TankMovementStarted -= OnTankMovementStarted;
            WorkshopSoundEffects.TankMovementStopped -= OnTankMovementStopped;
            WorkshopSoundEffects.HelicopterAmbientStarted -= OnHelicopterAmbientStarted;
            WorkshopSoundEffects.HelicopterAmbientStopped -= OnHelicopterAmbientStopped;
            WorkshopSoundEffects.HelicopterAttacked -= OnHelicopterAttacked;
            WorkshopSoundEffects.WeaponSold -= OnWeaponSold;
            WorkshopSoundEffects.WeaponDisposed -= OnWeaponDisposed;
            WorkshopSoundEffects.CustomLimitReached -= OnCustomLimitReached;
            WorkshopSoundEffects.BlockInstalled += OnBlockInstalled;
            WorkshopSoundEffects.BlockRemoved += OnBlockRemoved;
            WorkshopSoundEffects.AssemblyCompleted += OnAssemblyCompleted;
            WorkshopSoundEffects.WeaponEquipped += OnWeaponEquipped;
            WorkshopSoundEffects.WeaponDropped += OnWeaponDropped;
            WorkshopSoundEffects.WeaponDisplayed += OnWeaponDisplayed;
            WorkshopSoundEffects.WeaponPlacedOnTable += OnWeaponPlacedOnTable;
            WorkshopSoundEffects.ShopShutterMoved += OnShopShutterMoved;
            WorkshopSoundEffects.BusinessStarted += OnBusinessStarted;
            WorkshopSoundEffects.BusinessEnded += OnBusinessEnded;
            WorkshopSoundEffects.NightRaidStarted += OnNightRaidStarted;
            WorkshopSoundEffects.BlockFragmented += OnBlockFragmented;
            WorkshopSoundEffects.TankAppeared += OnTankAppeared;
            WorkshopSoundEffects.TankLanded += OnTankLanded;
            WorkshopSoundEffects.TankAttacked += OnTankAttacked;
            WorkshopSoundEffects.TankMovementStarted += OnTankMovementStarted;
            WorkshopSoundEffects.TankMovementStopped += OnTankMovementStopped;
            WorkshopSoundEffects.HelicopterAmbientStarted += OnHelicopterAmbientStarted;
            WorkshopSoundEffects.HelicopterAmbientStopped += OnHelicopterAmbientStopped;
            WorkshopSoundEffects.HelicopterAttacked += OnHelicopterAttacked;
            WorkshopSoundEffects.WeaponSold += OnWeaponSold;
            WorkshopSoundEffects.WeaponDisposed += OnWeaponDisposed;
            WorkshopSoundEffects.CustomLimitReached += OnCustomLimitReached;
            WorkshopSoundEffects.BasicShotFired += OnBasicShotFired;
            WorkshopSoundEffects.ChargeLoopStarted += OnChargeLoopStarted;
            WorkshopSoundEffects.ChargeLoopStopped += OnChargeLoopStopped;
            WorkshopSoundEffects.ChargeFullyLoaded += OnChargeFullyLoaded;
            WorkshopSoundEffects.ChargedShotFired += OnChargedShotFired;
            WorkshopSoundEffects.ShotgunFired += OnShotgunFired;
            WorkshopSoundEffects.WaterFired += OnWaterFired;
            WorkshopSoundEffects.ExplosionTriggered += OnExplosionTriggered;
            WorkshopSoundEffects.GravityFieldStarted += OnGravityFieldStarted;
            WorkshopSoundEffects.GravityFieldStopped += OnGravityFieldStopped;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>
        /// 게임 내 사운드 이벤트와 씬 로드 이벤트에서 처리 메서드 연결을 해제한다.
        /// 이 매니저가 더 이상 이벤트를 받지 않도록 구독 상태를 정리한다.
        /// </summary>
        private void Unsubscribe()
        {
            WorkshopSoundEffects.BlockInstalled -= OnBlockInstalled;
            WorkshopSoundEffects.BlockRemoved -= OnBlockRemoved;
            WorkshopSoundEffects.AssemblyCompleted -= OnAssemblyCompleted;
            WorkshopSoundEffects.WeaponEquipped -= OnWeaponEquipped;
            WorkshopSoundEffects.WeaponDropped -= OnWeaponDropped;
            WorkshopSoundEffects.WeaponDisplayed -= OnWeaponDisplayed;
            WorkshopSoundEffects.WeaponPlacedOnTable -= OnWeaponPlacedOnTable;
            WorkshopSoundEffects.ShopShutterMoved -= OnShopShutterMoved;
            WorkshopSoundEffects.BusinessStarted -= OnBusinessStarted;
            WorkshopSoundEffects.BusinessEnded -= OnBusinessEnded;
            WorkshopSoundEffects.NightRaidStarted -= OnNightRaidStarted;
            WorkshopSoundEffects.BlockFragmented -= OnBlockFragmented;
            WorkshopSoundEffects.TankAppeared -= OnTankAppeared;
            WorkshopSoundEffects.TankLanded -= OnTankLanded;
            WorkshopSoundEffects.TankAttacked -= OnTankAttacked;
            WorkshopSoundEffects.TankMovementStarted -= OnTankMovementStarted;
            WorkshopSoundEffects.TankMovementStopped -= OnTankMovementStopped;
            WorkshopSoundEffects.HelicopterAmbientStarted -= OnHelicopterAmbientStarted;
            WorkshopSoundEffects.HelicopterAmbientStopped -= OnHelicopterAmbientStopped;
            WorkshopSoundEffects.HelicopterAttacked -= OnHelicopterAttacked;
            WorkshopSoundEffects.WeaponSold -= OnWeaponSold;
            WorkshopSoundEffects.WeaponDisposed -= OnWeaponDisposed;
            WorkshopSoundEffects.CustomLimitReached -= OnCustomLimitReached;
            WorkshopSoundEffects.BasicShotFired -= OnBasicShotFired;
            WorkshopSoundEffects.ChargeLoopStarted -= OnChargeLoopStarted;
            WorkshopSoundEffects.ChargeLoopStopped -= OnChargeLoopStopped;
            WorkshopSoundEffects.ChargeFullyLoaded -= OnChargeFullyLoaded;
            WorkshopSoundEffects.ChargedShotFired -= OnChargedShotFired;
            WorkshopSoundEffects.ShotgunFired -= OnShotgunFired;
            WorkshopSoundEffects.WaterFired -= OnWaterFired;
            WorkshopSoundEffects.ExplosionTriggered -= OnExplosionTriggered;
            WorkshopSoundEffects.GravityFieldStarted -= OnGravityFieldStarted;
            WorkshopSoundEffects.GravityFieldStopped -= OnGravityFieldStopped;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnBlockInstalled(Vector3 position) => PlayWorkbenchSound(WorkbenchSoundCue.Install, position);
        private void OnBlockRemoved(Vector3 position) => PlayWorkbenchSound(WorkbenchSoundCue.Remove, position);
        private void OnAssemblyCompleted(Vector3 position) => PlayWorkbenchSound(WorkbenchSoundCue.AssemblyComplete, position);
        private void OnWeaponEquipped(Vector3 position) => Play("Gun Installation", position, 0.75f);
        private void OnWeaponDropped(Vector3 position) => Play("Gun Drop", position, 0.85f);
        private void OnWeaponDisplayed(Vector3 position) => Play("GunRack", position, 0.8f);
        private void OnWeaponPlacedOnTable(Vector3 position) => PlayWorkbenchSound(WorkbenchSoundCue.PlaceWeapon, position);
        private void OnShopShutterMoved(Vector3 position) => Play("IronDoor", position, 0.4f);
        private void OnBusinessStarted(Vector3 position)
        {
            Play("Opening for business", position, 0.85f);
            if (IsTutorialMusicContext) PlayLegacyMusic(BgmId.Raining, tutorialWorkshopMusicVolume);
        }
        private void OnBusinessEnded() => RefreshShopMusic();
        private void OnNightRaidStarted()
        {
            // 훈련 습격의 기존 Night 호출은 보존하고 새 자동 전환 규칙에는 진입시키지 않는다.
            if (IsTutorialMusicContext) PlayLegacyMusic(BgmId.Night, tutorialRaidMusicVolume);
            else BeginRaidMusic(SceneManager.GetActiveScene().name);
        }
        private void OnWeaponSold(Vector3 position) => Play("Gun Sell", position, 0.85f);
        private void OnWeaponDisposed(Vector3 position) => Play("TrashCan", position, 0.85f);
        private void OnCustomLimitReached(Vector3 position) => PlayWorkbenchSound(WorkbenchSoundCue.LimitReached, position);

        // 일반 작업대는 별도 컴포넌트의 설정을 읽는다. 기존 튜토리얼 음원/음량은 변경하지 않는다.
        private void PlayWorkbenchSound(WorkbenchSoundCue cue, Vector3 position)
        {
            if (!IsTutorialMusicContext && workbenchSounds != null)
            {
                workbenchSounds.Play(this, cue, position);
                return;
            }
            // 레거시 테이블: 튜토리얼 호환 및 설정 컴포넌트가 없는 기존 매니저의 재생 경로다.
            switch (cue)
            {
                case WorkbenchSoundCue.Install: Play("Module Set", position, .65f); break;
                case WorkbenchSoundCue.Remove: Play("Module eliminate", position, .65f); break;
                case WorkbenchSoundCue.PlaceWeapon: Play("PlaceGunOnTable", position, .8f); break;
                case WorkbenchSoundCue.AssemblyComplete: Play("AssemblyComplete", position, .8f); break;
                case WorkbenchSoundCue.Paper: Play("Paper", position, .8f); break;
                case WorkbenchSoundCue.LimitReached: Play("Error", position, .85f); break;
            }
        }
        private void OnBasicShotFired(Vector3 position) => Play("Gun shot", position, 0.8f * playerWeaponSoundVolumeMultiplier, basicShotMinimumInterval);
        private void OnChargeFullyLoaded(Vector3 position) => Play("ChargeFull", position, 0.85f);
        private void OnChargedShotFired(Vector3 position) => Play("Charge Shot", position, 0.95f * playerWeaponSoundVolumeMultiplier, chargedShotMinimumInterval);
        private void OnShotgunFired(Vector3 position) => Play("ShotGun Shot", position, 0.85f * playerWeaponSoundVolumeMultiplier, shotgunMinimumInterval);
        private void OnWaterFired(Vector3 position) => Play("WaterGun Shot", position, 0.8f * playerWeaponSoundVolumeMultiplier, waterShotMinimumInterval);
        private void OnExplosionTriggered(Vector3 position) => Play("Explosion", position, 0.4f, explosionMinimumInterval);
        private void OnBlockFragmented(Vector3 position) => Play("Collapse", position, 0.5f, collapseMinimumInterval);
        private void OnTankAppeared(Vector3 position) => Play("TankAppear", position, 0.75f * 2f * enemySoundVolumeMultiplier);
        private void OnTankLanded(Vector3 position) => Play("TankLanding", position, 0.8f * 1.3f * enemySoundVolumeMultiplier);
        private void OnTankAttacked(Vector3 position) => Play("TankAttack", position, 0.75f * 1.3f * 1.5f * 1.8f * enemySoundVolumeMultiplier);

        private void OnTankMovementStarted(Transform tank)
        {
            if (tank == null || tankMoveLoops.ContainsKey(tank)) return;
            AudioSource loop = CreateLoop("TankMove", tank, tankMoveVolume * enemySoundVolumeMultiplier, 5f, 50f);
            if (loop != null) tankMoveLoops.Add(tank, loop);
        }

        private void OnTankMovementStopped(Transform tank)
        {
            if (tank == null || !tankMoveLoops.TryGetValue(tank, out AudioSource loop)) return;
            tankMoveLoops.Remove(tank);
            StopLoop(loop);
        }

        private void OnHelicopterAttacked(Vector3 position)
            => Play("HelicopterAttack", position, HelicopterVolume(helicopterAttackVolume), helicopterAttackMinimumInterval);

        // 헬기 전용 음원에 기존 최종 음량의 0.7배를 적용한다. 마스터/SFX 슬라이더는 이후 별도로 곱한다.
        private float HelicopterVolume(float volume) => Mathf.Clamp01(volume * enemySoundVolumeMultiplier) * .7f;

        private void OnHelicopterAmbientStarted(Transform helicopter)
        {
            if (helicopter == null || helicopterLoops.ContainsKey(helicopter)) return;
            AudioSource loop = CreateLoop("Helicopter", helicopter, HelicopterVolume(helicopterAmbientVolume),
                helicopterAmbientMinDistance, helicopterAmbientMaxDistance);
            if (loop != null) helicopterLoops.Add(helicopter, loop);
        }

        private void OnHelicopterAmbientStopped(Transform helicopter)
        {
            if (helicopter == null || !helicopterLoops.TryGetValue(helicopter, out AudioSource loop)) return;
            helicopterLoops.Remove(helicopter);
            StopLoop(loop);
        }

        private void OnChargeLoopStarted(Transform source)
        {
            if (source == null || chargeLoops.ContainsKey(source)) return;
            AudioSource loop = CreateLoop("Charging", source, 0.32f);
            if (loop != null) chargeLoops.Add(source, loop);
        }

        private void OnChargeLoopStopped(Transform source)
        {
            if (source == null || !chargeLoops.TryGetValue(source, out AudioSource loop)) return;
            chargeLoops.Remove(source);
            StopLoop(loop);
        }

        private void OnGravityFieldStarted(GravityField field)
        {
            if (field == null || gravityLoops.ContainsKey(field)) return;
            AudioSource loop = CreateLoop("GravityGun", field.transform, 0.55f);
            if (loop != null) gravityLoops.Add(field, loop);
        }

        private void OnGravityFieldStopped(GravityField field)
        {
            if (field == null || !gravityLoops.TryGetValue(field, out AudioSource loop)) return;
            gravityLoops.Remove(field);
            StopLoop(loop);
        }

        /// <summary>
        /// 지정한 클립을 대상 오브젝트에 붙인 반복 재생 AudioSource로 만든다.
        /// clipName, followTarget, volume과 거리값을 사용하며, AudioSource를 반환하고 대상이나 클립이 없으면 null을 반환한다.
        /// </summary>
        private AudioSource CreateLoop(string clipName, Transform followTarget, float volume,
            float minDistance = 0f, float maxDistance = 0f)
        {
            AudioClip clip = LoadClip(clipName);
            return CreateLoop(clip, followTarget, volume, minDistance, maxDistance);
        }

        /// <summary>
        /// 지정한 클립을 대상 오브젝트에 붙인 반복 재생 AudioSource로 만든다.
        /// clip, followTarget, volume과 거리값을 사용하며 SFX 채널에 등록된 AudioSource를 반환한다.
        /// </summary>
        internal AudioSource CreateLoop(AudioClip clip, Transform followTarget, float volume,
            float minDistance = 0f, float maxDistance = 0f)
        {
            if (clip == null || followTarget == null) return null;

            GameObject loopObject = new GameObject("SFX_" + clip.name);
            loopObject.transform.SetParent(followTarget, false);
            AudioSource source = loopObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            TrackSfx(source, volume);
            if (maxDistance > 0f)
            {
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = Mathf.Max(0f, minDistance);
                source.maxDistance = Mathf.Max(source.minDistance + 0.1f, maxDistance);
            }
            source.Play();
            return source;
        }

        /// <summary>
        /// 배경음악 클립을 반복 재생하거나 현재 재생 중인 클립의 음량을 갱신한다.
        /// 튜토리얼 곡과 음량을 사용하며 실제 재생 곡 변경을 구독자에게 알린다.
        /// </summary>
        private void PlayLegacyMusic(BgmId id, float volume)
        {
            // 기존 튜토리얼 재생 경로에서도 Main의 공격곡 금지 조건을 우회하지 못하게 한다.
            if (SceneManager.GetActiveScene().name == "Main" && IsAttackMusic(id)) return;
            AudioClip clip = GetMusicClip(id);
            if (clip == null)
            {
                id = BgmId.DayFirst;
                clip = defaultMusicClip;
            }
            if (clip == null)
            {
                StopBackgroundMusic();
                return;
            }
            CancelMusicFade();
            EnsureMusicSource();
            currentBgm = id;
            musicBaseVolume = volume;
            musicFade = 1f;
            if (backgroundMusicSource.clip == clip && backgroundMusicSource.isPlaying)
            {
                SetPlayingBgm(id);
                ApplyChannelVolumes();
                return;
            }

            backgroundMusicSource.Stop();
            backgroundMusicSource.clip = clip;
            backgroundMusicSource.loop = true;
            ApplyChannelVolumes();
            backgroundMusicSource.Play();
            SetPlayingBgm(id);
        }

        /// <summary>
        /// 배경음악 재생을 멈추고 AudioSource에서 현재 클립 참조를 지운다.
        /// 재생 식별자를 None으로 갱신해 변경 이벤트를 알린다.
        /// </summary>
        private void StopBackgroundMusic()
        {
            CancelMusicFade();
            currentBgm = BgmId.None;
            if (backgroundMusicSource == null)
            {
                SetPlayingBgm(BgmId.None);
                return;
            }
            backgroundMusicSource.Stop();
            backgroundMusicSource.clip = null;
            SetPlayingBgm(BgmId.None);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive) return;
            // Main은 저장 복원 뒤 초기화한다. 습격으로 넘어갈 때는 직전 Main 곡을 유지해 기록한다.
            mainMusicReady = false;
            activeRaidScene = null;
            previousTutorialContext = IsTutorialMusicContext;
            if (scene.name == "Intro") RestoreBeforeRaidMusic(true);
            // 습격 공격곡은 Main 로드 직후부터 멈춘다. 저장 복원/다음날 처리 전에도 흘러나오지 않는다.
            else if (scene.name == "Main" && (IsAttackMusic(currentBgm) || IsAttackMusic(playingBgm) ||
                     (backgroundMusicSource != null && IsAttackClip(backgroundMusicSource.clip)))) StopBackgroundMusic();
            else if (IsTutorialMusicContext) StopBackgroundMusic();
            AttachDayManager();
            AttachButtonClickSounds();
        }

        /// <summary>
        /// 현재 씬의 버튼에 기본 클릭음을 연결하고 이미 파괴된 버튼의 기록을 정리한다.
        /// 발견한 버튼과 buttonClickListeners를 사용하며 버튼 이벤트 연결과 추적 목록을 갱신한다.
        /// </summary>
        private void AttachButtonClickSounds()
        {
            Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Button button in buttons)
                if (button != null && !buttonClickListeners.ContainsKey(button))
                    RegisterButtonSoundInternal(button, "UIClick");

            List<Button> destroyedButtons = null;
            foreach (KeyValuePair<Button, UnityAction> entry in buttonClickListeners)
            {
                if (entry.Key != null) continue;
                destroyedButtons ??= new List<Button>();
                destroyedButtons.Add(entry.Key);
            }
            if (destroyedButtons == null) return;
            foreach (Button button in destroyedButtons) buttonClickListeners.Remove(button);
        }

        /// <summary>
        /// 버튼의 클릭 효과음 연결을 지정한 클립으로 등록하거나 교체한다.
        /// button과 clipName을 사용하며 buttonClickListeners와 버튼의 onClick 리스너를 갱신한다.
        /// </summary>
        private void RegisterButtonSoundInternal(Button button, string clipName)
        {
            if (button == null || string.IsNullOrWhiteSpace(clipName)) return;
            if (buttonClickListeners.TryGetValue(button, out UnityAction previous))
                button.onClick.RemoveListener(previous);

            UnityAction listener = () => PlayUIButtonSound(clipName);
            buttonClickListeners[button] = listener;
            button.onClick.AddListener(listener);
        }

        /// <summary>
        /// 지정한 UI 효과음을 전용 AudioSource로 재생한다.
        /// clipName으로 클립과 음량 설정을 선택하며, uiSoundSource가 없으면 만들어 재사용한다.
        /// </summary>
        private void PlayUIButtonSound(string clipName)
        {
            AudioClip clip = LoadClip(clipName);
            if (clip == null) return;
            if (uiSoundSource == null)
            {
                uiSoundSource = gameObject.AddComponent<AudioSource>();
                uiSoundSource.playOnAwake = false;
                uiSoundSource.loop = false;
                uiSoundSource.spatialBlend = 0f;
                uiSoundSource.dopplerLevel = 0f;
                TrackSfx(uiSoundSource, 1f);
            }
            float volume = clipName == "SpawnButtonClick" ? spawnButtonClickVolume : uiClickVolume;
            uiSoundSource.PlayOneShot(clip, volume);
        }

        /// <summary>
        /// 현재 씬의 날짜 매니저를 찾아 변경 이벤트를 구독한다.
        /// subscribedDayManager를 갱신하고, 구독 뒤 현재 영업 상태에 맞춰 배경음악을 새로 고친다.
        /// </summary>
        private void AttachDayManager()
        {
            DetachDayManager();
            subscribedDayManager = FindFirstObjectByType<_DayManager>();
            if (subscribedDayManager == null) return;
            subscribedDayManager.Changed += RefreshShopMusic;
            RefreshShopMusic();
        }

        /// <summary>
        /// 구독 중인 날짜 매니저의 변경 이벤트에서 연결을 해제한다.
        /// subscribedDayManager 참조를 비워 날짜 매니저 추적 상태를 초기화한다.
        /// </summary>
        private void DetachDayManager()
        {
            if (subscribedDayManager != null) subscribedDayManager.Changed -= RefreshShopMusic;
            subscribedDayManager = null;
        }

        /// <summary>
        /// 날짜 매니저 구독 여부에 따라 공방 배경음악을 재생하거나 멈춘다.
        /// subscribedDayManager 상태를 확인해 배경음악 AudioSource를 갱신한다.
        /// </summary>
        private void RefreshShopMusic()
        {
            // 기존 튜토리얼 Raining의 즉시 재생·이어 재생 동작을 보존한다.
            if (subscribedDayManager != null && IsTutorialMusicContext)
                PlayLegacyMusic(BgmId.Raining, tutorialWorkshopMusicVolume);
            else if (mainMusicReady && subscribedDayManager != null && !IsTutorialMusicContext)
                RefreshMusicContext();
        }

        /// <summary>
        /// 충전·중력장·헬기·전차의 반복 사운드를 모두 멈추고 추적 목록을 비운다.
        /// 각 루프 AudioSource를 제거하고 관련 사전 상태를 초기화한다.
        /// </summary>
        private void StopAllLoops()
        {
            foreach (AudioSource source in chargeLoops.Values) StopLoop(source);
            foreach (AudioSource source in gravityLoops.Values) StopLoop(source);
            foreach (AudioSource source in helicopterLoops.Values) StopLoop(source);
            foreach (AudioSource source in tankMoveLoops.Values) StopLoop(source);
            chargeLoops.Clear();
            gravityLoops.Clear();
            helicopterLoops.Clear();
            tankMoveLoops.Clear();
        }

        /// <summary>
        /// 지정한 반복 사운드를 멈추고 AudioSource가 붙은 오브젝트를 제거한다.
        /// source가 null이면 아무 작업도 하지 않는다.
        /// </summary>
        internal void StopLoop(AudioSource source)
        {
            if (source == null) return;
            sfxBaseVolumes.Remove(source);
            source.Stop();
            Destroy(source.gameObject);
        }

        /// <summary>
        /// 지정한 효과음을 위치와 음량에 맞춰 재생하고 최소 재생 간격을 적용한다.
        /// clipName, position, volume, minimumInterval을 사용하며 재생 시각 기록을 갱신한다.
        /// </summary>
        private void Play(string clipName, Vector3 position, float volume, float minimumInterval = 0f)
        {
            AudioClip clip = LoadClip(clipName);
            if (clip == null) return;

            minimumInterval = Mathf.Max(0f, minimumInterval);
            double now = Time.timeAsDouble;
            if (minimumInterval > 0f && lastSoundPlaybackTimes.TryGetValue(clipName, out double lastPlayedAt)
                && now - lastPlayedAt < minimumInterval)
                return;

            if (minimumInterval > 0f) lastSoundPlaybackTimes[clipName] = now;
            PlayClip(clip, position, volume);
        }

        /// <summary>
        /// clip을 position에서 volume과 공통 마스터/SFX 설정으로 한 번 재생한다.
        /// 개별 정지에 사용할 AudioSource를 반환하며 클립이 없으면 null을 반환한다.
        /// </summary>
        internal AudioSource PlayClip(AudioClip clip, Vector3 position, float volume)
        {
            if (clip == null) return null;
            // PlayClipAtPoint와 동일한 위치/기본 감쇠·수명을 사용하되 채널에서 추적한다.
            var sound = new GameObject("One shot audio");
            sound.transform.position = position;
            var source = sound.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 1f;
            TrackSfx(source, volume);
            source.Play();
            Destroy(sound, clip.length * (Time.timeScale < .01f ? .01f : Time.timeScale));
            return source;
        }

        private void OnValidate()
        {
            basicShotMinimumInterval = Mathf.Max(0f, basicShotMinimumInterval);
            chargedShotMinimumInterval = Mathf.Max(0f, chargedShotMinimumInterval);
            shotgunMinimumInterval = Mathf.Max(0f, shotgunMinimumInterval);
            waterShotMinimumInterval = Mathf.Max(0f, waterShotMinimumInterval);
            explosionMinimumInterval = Mathf.Max(0f, explosionMinimumInterval);
            collapseMinimumInterval = Mathf.Max(0f, collapseMinimumInterval);
            helicopterAttackMinimumInterval = Mathf.Max(0f, helicopterAttackMinimumInterval);
            playerWeaponSoundVolumeMultiplier = Mathf.Max(0f, playerWeaponSoundVolumeMultiplier);
            enemySoundVolumeMultiplier = Mathf.Max(0f, enemySoundVolumeMultiplier);
            tankMoveVolume = Mathf.Clamp01(tankMoveVolume);
            helicopterAmbientVolume = Mathf.Clamp01(helicopterAmbientVolume);
            helicopterAmbientMinDistance = Mathf.Max(0f, helicopterAmbientMinDistance);
            helicopterAmbientMaxDistance = Mathf.Max(helicopterAmbientMinDistance + 0.1f, helicopterAmbientMaxDistance);
            helicopterAttackVolume = Mathf.Clamp01(helicopterAttackVolume);
            shopMusicVolume = Mathf.Clamp01(shopMusicVolume);
            raidMusicVolume = Mathf.Clamp01(raidMusicVolume);
            tutorialWorkshopMusicVolume = Mathf.Clamp01(tutorialWorkshopMusicVolume);
            tutorialRaidMusicVolume = Mathf.Clamp01(tutorialRaidMusicVolume);
        }

        // 레거시 테이블: Resources 경로 문자열 + 파일명 키. 새 테이블 값은 TableManager에서 받아온다.
        /// <summary>
        /// 효과음 캐시에서 클립을 찾고, 없으면 Resources에서 불러와 캐시에 저장한다.
        /// clipName을 사용하며 찾은 AudioClip을 반환하고 리소스가 없으면 null을 반환한다.
        /// </summary>
        private AudioClip LoadClip(string clipName)
        {
            if (clips.TryGetValue(clipName, out AudioClip clip)) return clip;
            clip = Resources.Load<AudioClip>(ResourcePath + clipName);
            if (clip != null) clips.Add(clipName, clip);
            return clip;
        }
    }
}
