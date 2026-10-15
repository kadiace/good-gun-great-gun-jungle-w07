using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    public sealed class SoundManager : MonoBehaviour
    {
        private const string ResourcePath = "Sound/SFX/";
        private static SoundManager instance;

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

        public static void RegisterButtonSound(Button button, string clipName)
        {
            if (instance != null) instance.RegisterButtonSoundInternal(button, clipName);
        }

        public static void PlayPaperSound(Vector3 position)
        {
            if (instance != null) instance.Play("Paper", position, 0.8f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureInstance()
        {
            SoundManager manager = FindFirstObjectByType<SoundManager>();
            if (manager == null)
                manager = new GameObject(nameof(SoundManager)).AddComponent<SoundManager>();

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
            Unsubscribe();
            StopAllLoops();
            StopBackgroundMusic();
            DetachDayManager();
        }

        private void OnDestroy()
        {
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
            if (Time.unscaledTime < nextButtonScanTime) return;
            nextButtonScanTime = Time.unscaledTime + 1f;
            AttachButtonClickSounds();
        }

        private void Initialize()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            Subscribe();
        }

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
            WorkshopSoundEffects.FootstepPlayed += OnFootstepPlayed;
            WorkshopSoundEffects.PlayerJumped += OnPlayerJumped;
            WorkshopSoundEffects.GravityFieldStarted += OnGravityFieldStarted;
            WorkshopSoundEffects.GravityFieldStopped += OnGravityFieldStopped;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

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
            WorkshopSoundEffects.FootstepPlayed -= OnFootstepPlayed;
            WorkshopSoundEffects.PlayerJumped -= OnPlayerJumped;
            WorkshopSoundEffects.GravityFieldStarted -= OnGravityFieldStarted;
            WorkshopSoundEffects.GravityFieldStopped -= OnGravityFieldStopped;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnBlockInstalled(Vector3 position) => Play("Module Set", position, 0.65f);
        private void OnBlockRemoved(Vector3 position) => Play("Module eliminate", position, 0.65f);
        private void OnAssemblyCompleted(Vector3 position) => Play("AssemblyComplete", position, 0.8f);
        private void OnWeaponEquipped(Vector3 position) => Play("Gun Installation", position, 0.75f);
        private void OnWeaponDropped(Vector3 position) => Play("Gun Drop", position, 0.85f);
        private void OnWeaponDisplayed(Vector3 position) => Play("GunRack", position, 0.8f);
        private void OnWeaponPlacedOnTable(Vector3 position) => Play("PlaceGunOnTable", position, 0.8f);
        private void OnShopShutterMoved(Vector3 position) => Play("IronDoor", position, 0.4f);
        private void OnBusinessStarted(Vector3 position)
        {
            Play("Opening for business", position, 0.85f);
            PlayBackgroundMusic("Raining", shopMusicVolume);
        }
        private void OnBusinessEnded() => RefreshShopMusic();
        private void OnNightRaidStarted() => PlayBackgroundMusic("Night", raidMusicVolume);
        private void OnWeaponSold(Vector3 position) => Play("Gun Sell", position, 0.85f);
        private void OnWeaponDisposed(Vector3 position) => Play("TrashCan", position, 0.85f);
        private void OnCustomLimitReached(Vector3 position) => Play("Error", position, 0.85f);
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
            => Play("HelicopterAttack", position, helicopterAttackVolume * enemySoundVolumeMultiplier, helicopterAttackMinimumInterval);
        private void OnFootstepPlayed(Vector3 position) => Play("Walking", position, 0.35f * 0.6f);
        private void OnPlayerJumped(Vector3 position) => Play("Jumping", position, 0.85f);

        private void OnHelicopterAmbientStarted(Transform helicopter)
        {
            if (helicopter == null || helicopterLoops.ContainsKey(helicopter)) return;
            AudioSource loop = CreateLoop("Helicopter", helicopter, helicopterAmbientVolume * enemySoundVolumeMultiplier,
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

        private AudioSource CreateLoop(string clipName, Transform followTarget, float volume,
            float minDistance = 0f, float maxDistance = 0f)
        {
            AudioClip clip = LoadClip(clipName);
            if (clip == null || followTarget == null) return null;

            GameObject loopObject = new GameObject("SFX_" + clipName);
            loopObject.transform.SetParent(followTarget, false);
            AudioSource source = loopObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.volume = volume;
            if (maxDistance > 0f)
            {
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = Mathf.Max(0f, minDistance);
                source.maxDistance = Mathf.Max(source.minDistance + 0.1f, maxDistance);
            }
            source.Play();
            return source;
        }

        private void PlayBackgroundMusic(string clipName, float volume)
        {
            AudioClip clip = LoadBackgroundMusic(clipName);
            if (clip == null) return;

            if (backgroundMusicSource == null)
            {
                backgroundMusicSource = gameObject.AddComponent<AudioSource>();
                backgroundMusicSource.playOnAwake = false;
                backgroundMusicSource.spatialBlend = 0f;
                backgroundMusicSource.dopplerLevel = 0f;
            }

            if (backgroundMusicSource.clip == clip && backgroundMusicSource.isPlaying)
            {
                backgroundMusicSource.volume = Mathf.Clamp01(volume);
                return;
            }

            backgroundMusicSource.Stop();
            backgroundMusicSource.clip = clip;
            backgroundMusicSource.loop = true;
            backgroundMusicSource.volume = Mathf.Clamp01(volume);
            backgroundMusicSource.Play();
        }

        private void StopBackgroundMusic()
        {
            if (backgroundMusicSource == null) return;
            backgroundMusicSource.Stop();
            backgroundMusicSource.clip = null;
        }

        // 레거시 테이블: Resources 경로 문자열로 클립을 찾는다. 새 테이블 값은 TableManager에서 받아온다.
        private AudioClip LoadBackgroundMusic(string clipName)
            => Resources.Load<AudioClip>("Sound/BGM/" + clipName);

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StopBackgroundMusic();
            AttachDayManager();
            AttachButtonClickSounds();
        }

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

        private void RegisterButtonSoundInternal(Button button, string clipName)
        {
            if (button == null || string.IsNullOrWhiteSpace(clipName)) return;
            if (buttonClickListeners.TryGetValue(button, out UnityAction previous))
                button.onClick.RemoveListener(previous);

            UnityAction listener = () => PlayUIButtonSound(clipName);
            buttonClickListeners[button] = listener;
            button.onClick.AddListener(listener);
        }

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
            }
            float volume = clipName == "SpawnButtonClick" ? spawnButtonClickVolume : uiClickVolume;
            uiSoundSource.PlayOneShot(clip, volume);
        }

        private void AttachDayManager()
        {
            DetachDayManager();
            subscribedDayManager = FindFirstObjectByType<_DayManager>();
            if (subscribedDayManager == null) return;
            subscribedDayManager.Changed += RefreshShopMusic;
            RefreshShopMusic();
        }

        private void DetachDayManager()
        {
            if (subscribedDayManager != null) subscribedDayManager.Changed -= RefreshShopMusic;
            subscribedDayManager = null;
        }

        private void RefreshShopMusic()
        {
            if (subscribedDayManager != null)
                PlayBackgroundMusic("Raining", shopMusicVolume);
            else
                StopBackgroundMusic();
        }

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

        private void StopLoop(AudioSource source)
        {
            if (source == null) return;
            source.Stop();
            Destroy(source.gameObject);
        }

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
            AudioSource.PlayClipAtPoint(clip, position, volume);
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
        }

        // 레거시 테이블: Resources 경로 문자열 + 파일명 키. 새 테이블 값은 TableManager에서 받아온다.
        private AudioClip LoadClip(string clipName)
        {
            if (clips.TryGetValue(clipName, out AudioClip clip)) return clip;
            clip = Resources.Load<AudioClip>(ResourcePath + clipName);
            if (clip != null) clips.Add(clipName, clip);
            return clip;
        }
    }
}
